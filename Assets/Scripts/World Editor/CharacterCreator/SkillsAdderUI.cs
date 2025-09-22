using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class SkillsAdderUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private StatsService svc;          // scene singleton
    [SerializeField] private Button addSkillButton;     // “Add Skill” button
    [SerializeField] private GameObject adderPanel;     // Adder root (has SkillAddButton children)
    [SerializeField] private GameObject skillsListRoot;

    // === NEW: overlay like Conditions/Senses ===
    [Header("Overlay / Sorting (match Conditions & Senses)")]
    [SerializeField] private int overlaySortingOrder = 6010; // a bit above the panel
    [SerializeField] private Camera uiCamera;
    private RectTransform adderRect;

    void Awake()
    {
#if UNITY_2023_1_OR_NEWER
        if (!svc) svc = FindFirstObjectByType<StatsService>();
#else
        if (!svc) svc = FindObjectOfType<StatsService>();
#endif
        if (addSkillButton) addSkillButton.onClick.AddListener(OpenAdder);

        if (adderPanel)
        {
            adderRect = adderPanel.GetComponent<RectTransform>();
            EnsureTopCanvas(adderPanel, overlaySortingOrder);
            foreach (var add in adderPanel.GetComponentsInChildren<SkillAddButton>(true))
                add.Initialize(this);
        }

        WireAdderButtonsToAddAndClose();

        var root = GetComponentInParent<Canvas>();
        if (root && root.renderMode != RenderMode.ScreenSpaceOverlay) uiCamera = root.worldCamera;
    }

    void OnEnable() { StatsService.StatsRecomputed += RefreshDisabledStates; RefreshDisabledStates(); }
    void OnDisable()
    {
        StatsService.StatsRecomputed -= RefreshDisabledStates;
        if (skillsListRoot) skillsListRoot.SetActive(true);
    }

    public void OpenAdder()
    {
        if (!adderPanel) return;
        RefreshDisabledStates();
        EnsureTopCanvas(adderPanel, TopOrder() + 20);  // ensure above everything
        adderPanel.transform.SetAsLastSibling();
        adderPanel.SetActive(true);
        WireAdderButtonsToAddAndClose();
        if (skillsListRoot) skillsListRoot.SetActive(false); // list OFF while adder is open
    }

    public void CloseAdder()
    {
        if (adderPanel) adderPanel.SetActive(false);
        if (skillsListRoot) skillsListRoot.SetActive(true);  // list ON
    }

    void Update()
    {
        // click OUTSIDE the adder closes it (same as Conditions/Senses adder)
        if (!adderRect || !adderPanel || !adderPanel.activeSelf) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(adderRect, Input.mousePosition, uiCamera))
                CloseAdder();
        }
    }

    void WireAdderButtonsToAddAndClose()
    {
        if (!adderPanel) return;
        foreach (var btn in adderPanel.GetComponentsInChildren<Button>(true))
        {
            btn.onClick.RemoveListener(CloseAdder);
            btn.onClick.AddListener(() =>
            {
                var id = ExtractSkillId(btn.gameObject);
                if (!string.IsNullOrEmpty(id))
                    TryAddSkill(id, id);
                else
                    CloseAdder();
            });
        }
    }

    void RefreshDisabledStates()
    {
        if (!adderPanel || svc?.Model == null) return;

        var buttons = adderPanel.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            string id = ExtractSkillId(btn.gameObject);
            var entry = GetSkillEntry(id);
            bool alreadyOnList = IsShownInSkillsList(entry);
            SetDisabled(btn, alreadyOnList);
        }
    }

    characterStats.SkillEntry GetSkillEntry(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var list = svc?.Model?.skills?.list;
        if (list == null) return null;
        return list.Find(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase));
    }

    static bool IsShownInSkillsList(characterStats.SkillEntry e)
    {
        if (e == null) return false;
        if (e.mode == characterStats.SkillMode.Improvable)
            return e.awareness > 0 || e.freshness > 0 || e.confidence > 0 || e.efficiency > 0 || e.precision > 0;
        return e.progress > 0 || e.learned;
    }

    static string ExtractSkillId(GameObject go)
    {
        var sab = go.GetComponent<SkillAddButton>();
        if (sab != null && !string.IsNullOrWhiteSpace(sab.skillId))
            return sab.skillId.Trim();

        string label = null;
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp) label = tmp.text;
        else { var ui = go.GetComponentInChildren<UnityEngine.UI.Text>(true); if (ui) label = ui.text; }

        if (string.IsNullOrWhiteSpace(label)) return null;

        label = label.Trim();
        if (label.StartsWith("Add ", StringComparison.OrdinalIgnoreCase)) label = label.Substring(4).Trim();
        int paren = label.IndexOf('(');
        if (paren >= 0) label = label.Substring(0, paren).Trim();
        return label;
    }

    static void SetDisabled(Button btn, bool disabled)
    {
        btn.interactable = !disabled;
        var cg = btn.GetComponent<CanvasGroup>();
        if (!cg) cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.interactable   = !disabled;
        cg.blocksRaycasts = !disabled;
        cg.alpha          = disabled ? 0.5f : 1f;
    }

    // ---- Add logic (unchanged) ----
    public void TryAddSkill(string id, string displayName)
    {
        if (svc?.Model == null) { CloseAdder(); return; }

        var existing = svc.Model.skills?.list?.Find(e => e.id.Equals(id, System.StringComparison.OrdinalIgnoreCase));
        if (existing != null && IsShownInSkillsList(existing)) { CloseAdder(); return; }

        var s = svc.Model;
        int ageY = Mathf.Max(0, s.profile.age.years);
        float ageF = ageY + (s.profile.age.months / 12f);

        bool isLearnOnce = IsLearnOnce(id);
        var entry = s.skills.AddOrGet(id, isLearnOnce ? characterStats.SkillMode.LearnOnce
                                                      : characterStats.SkillMode.Improvable);

        if (isLearnOnce)
        {
            var (start, full) = LearnOnceWindow(id);
            int progress;
            if (ageF >= full)
            {
                if (IsOptionalLearnOnce(id))
                {
                    bool learned = UnityEngine.Random.value < LearnOnceAdoptionRate(id);
                    progress = learned ? 100 : UnityEngine.Random.Range(0, 25);
                }
                else progress = 100;
            }
            else if (ageF <= start) progress = 0;
            else
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, full, ageF));
                progress = Mathf.Clamp(Mathf.RoundToInt(t * 100f + UnityEngine.Random.Range(-3f, 3f)), 0, 100);
            }
            entry.SetLearnOnceProgress(progress);
        }
        else
        {
            int awarenessCap = (ageY < 10) ? 0 : (ageY < 18 ? 1 : 2);
            int cap = PerfCapMaxFor(id, ageY);
            float minFactor = ageY < 18 ? 0.08f : 0.18f;
            int minVal = Mathf.RoundToInt(cap * minFactor);
            float skew = ageY < 18 ? 2.3f : 1.7f;

            int fresh = LowBiased(minVal, cap, skew);
            int confidence = LowBiased(minVal, cap, skew);
            int efficiency = LowBiased(minVal, cap, skew);
            int precision = LowBiased(minVal, cap, skew);

            entry.SetImprovable(UnityEngine.Random.Range(0, awarenessCap + 1),
                                fresh, confidence, efficiency, precision);
        }

        CloseAdder();
        svc.CommitAndRecompute();
    }

    // ---- helpers mirrored from generator (unchanged) ----
    static readonly System.Collections.Generic.HashSet<string> LEARN_ONCE =
        new System.Collections.Generic.HashSet<string>(new[]{
            "Sitting","Crawling","Walking","Toilet Independence",
            "Counting","Reading","Writing",
            "Ride Bicycle","Swim Basics","Drive","Kneel","Genuflect","Crouch",
            "Showering/Bathing","Handwashing",
            "Brush Teeth","Floss","Wash Face","Rinse Mouth",
            "Wash Hair","Nail Care","Open/Close","Place","Pour",
            "Wink","Wave","Nod",
        }, System.StringComparer.OrdinalIgnoreCase);

    static bool IsLearnOnce(string id) => LEARN_ONCE.Contains(id);
    static bool IsOptionalLearnOnce(string id)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        return k == "ride bicycle" || k == "swim basics" || k == "drive" || k == "floss";
    }
    static float LearnOnceAdoptionRate(string id)
    {
        switch ((id ?? "").Trim().ToLowerInvariant())
        {
            case "ride bicycle":  return 0.92f;
            case "swim basics":   return 0.78f;
            case "drive":         return 0.94f;
            case "floss":         return 0.92f;
            default:              return 0.98f;
        }
    }
    static (float start, float full) LearnOnceWindow(string id)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        switch (k)
        {
            case "sitting":  return (0.25f, 0.75f);
            case "crawling": return (0.50f, 1.10f);
            case "walking":  return (0.75f, 1.40f);
            case "kneel":    return (0.80f, 2.00f);
            case "crouch":   return (0.80f, 2.00f);
            case "genuflect":return (2.50f, 6.00f);
            case "showering/bathing":
            case "showering":
            case "bathing":      return (3.00f, 7.00f);
            case "handwashing":
            case "wash hands":   return (2.00f, 5.00f);
            case "brush teeth":  return (2.50f, 7.00f);
            case "floss":        return (4.50f, 10.00f);
            case "wash face":    return (2.50f, 5.00f);
            case "rinse mouth":  return (2.50f, 4.50f);
            case "wash hair":    return (3.50f, 8.00f);
            case "nail care":    return (4.00f, 9.00f);
            case "open/close":
            case "open":
            case "close":        return (0.50f, 1.50f);
            case "place":        return (0.50f, 1.50f);
            case "pour":         return (2.00f, 4.00f);
            case "wave":         return (0.60f, 2.00f);
            case "nod":          return (0.80f, 2.00f);
            case "wink":         return (3.50f, 7.00f);
            case "toilet independence": return (2.00f, 3.50f);
            case "counting":            return (3.00f, 6.00f);
            case "reading":             return (4.00f, 8.00f);
            case "writing":             return (4.50f, 8.50f);
            case "ride bicycle":        return (3.00f, 8.00f);
            case "swim basics":         return (3.00f, 10.00f);
            case "drive":               return (16.00f, 19.00f);
            default: return (2.0f, 10.0f);
        }
    }
    static int LowBiased(int min, int max, float skew = 1.8f)
    {
        if (max <= min) return Mathf.Clamp(min, 0, 100);
        float t = Mathf.Pow(UnityEngine.Random.value, skew);
        int v = min + Mathf.RoundToInt(t * (max - min));
        return Mathf.Clamp(v, 0, 100);
    }
    static int PerfCapMaxFor(string id, int ageY)
    {
        string k = (id ?? "").ToLowerInvariant();
        (int min, int max) perfCap;
        if (k.Contains("sprint") || k.Contains("push") || k.Contains("lift") ||
            k.Contains("climb")  || k.Contains("jump") || k.Contains("bench") ||
            k.Contains("pullups")|| k.Contains("situp"))
            perfCap = StatLimits.GetAgeScaledRange(ageY, 20, 35, 95, 20, 1.0f, 30);
        else if (k.Contains("drawing") || k.Contains("painting") || k.Contains("meditation") ||
                k.Contains("decision") || k.Contains("problem")  || k.Contains("imagination"))
            perfCap = StatLimits.GetAgeScaledRange(ageY, 25, 55, 92, 35, 0.9f, 30);
        else if (k.Contains("public speaking") || k.Contains("presentation") || k.Contains("debate") ||
                k.Contains("rhetoric") || k.Contains("persuasion") || k.Contains("negotiation") ||
                k.Contains("conflict") || k.Contains("feedback")   || k.Contains("storytelling"))
            perfCap = StatLimits.GetAgeScaledRange(ageY, 28, 60, 90, 20, 0.8f, 30);
        else if (k.Contains("driv") || k.Contains("navigation"))
            perfCap = StatLimits.GetAgeScaledRange(ageY, 18, 60, 95, 0, 0.9f, 30);
        else
            perfCap = StatLimits.GetAgeScaledRange(ageY, 22, 55, 90, 25, 0.9f, 30);
        return Mathf.Clamp(perfCap.max, 0, 100);
    }

    // === NEW: overlay helpers (same as Conditions/Senses) ===
    static int TopOrder()
    {
        int top = 0;
#if UNITY_2023_1_OR_NEWER
        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
#else
        foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
#endif
            top = Mathf.Max(top, c.sortingOrder);
        return top;
    }

    static void EnsureTopCanvas(GameObject go, int order)
    {
        if (!go) return;
        var c = go.GetComponent<Canvas>(); if (!c) c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = order;

        if (!go.GetComponent<GraphicRaycaster>()) go.AddComponent<GraphicRaycaster>();
        if (!go.GetComponent<CanvasGroup>()) go.AddComponent<CanvasGroup>();
    }
}
