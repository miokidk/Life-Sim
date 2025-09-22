using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LanguagesUI : MonoBehaviour
{
    [Header("Services + Root")]
    [SerializeField] private StatsService svc;

    [Tooltip("The main Language Panel (the list of toggles)")]
    [SerializeField] private GameObject panelRoot;

    [Tooltip("Old top bar with Close button (now unused)")]
    [SerializeField] private GameObject closePanelRoot;

    [SerializeField] private Button openLanguagesButton;
    [SerializeField] private TMP_Text openButtonTMP;   // text on Open button
    [SerializeField] private Text openButtonText;  // optional non-TMP
    [Tooltip("Optional. If null, we create a transparent full-screen blocker at runtime.")]
    [SerializeField] private GameObject outsideClickBlocker;

    [Header("Rows (toggle + its Primary toggle)")]
    [SerializeField] private Toggle english; [SerializeField] private Toggle englishPrimary;
    [SerializeField] private Toggle spanish; [SerializeField] private Toggle spanishPrimary;
    [SerializeField] private Toggle french; [SerializeField] private Toggle frenchPrimary;
    [SerializeField] private Toggle chinese; [SerializeField] private Toggle chinesePrimary;
    [SerializeField] private Toggle arabic; [SerializeField] private Toggle arabicPrimary;

    [Header("Overlay / Sorting")]
    [SerializeField] private int overlaySortingOrder = 5000; // ensure on top
    [SerializeField] private bool startHidden = true;

    readonly Dictionary<string, bool> selected = new();
    string primary;   // display value ("English"), or null
    bool suppress;
    bool openedThisFrame;

    /* ---------------- lifecycle ---------------- */
    void Awake()
    {
        if (openLanguagesButton) openLanguagesButton.onClick.AddListener(() => { PullFromModel(); OpenPanel(); });

        // wire toggles
        Wire("English", english, englishPrimary);
        Wire("Spanish", spanish, spanishPrimary);
        Wire("French", french, frenchPrimary);
        Wire("Chinese", chinese, chinesePrimary);
        Wire("Arabic", arabic, arabicPrimary);

        EnsureTopCanvas(panelRoot, overlaySortingOrder);

        // never show the old close bar
        if (closePanelRoot) closePanelRoot.SetActive(false);
        HideAllPrimaryToggles();
        if (startHidden) ShowPanel(false);

        UpdateOpenButtonLabel(GetModelPrimary()); // safe even if no model yet

#if UNITY_2023_1_OR_NEWER
        if (svc == null) svc = UnityEngine.Object.FindFirstObjectByType<StatsService>();
#else
        if (svc == null) svc = UnityEngine.Object.FindObjectOfType<StatsService>();
#endif

        StatsService.StatsRecomputed += OnStatsRecomputed;
        OnStatsRecomputed(); // updates the "Primary: ..." label immediately on spawn
    }

    void Update()
    {
        if (panelRoot && panelRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            CommitAndHide();

        // one-frame guard to avoid the opening click immediately closing
        if (openedThisFrame) openedThisFrame = false;
    }

    /* ---------------- helpers ---------------- */
    void Wire(string name, Toggle mainT, Toggle primaryT)
    {
        mainT.onValueChanged.AddListener(v => OnMainChanged(name, primaryT, v));
        primaryT.onValueChanged.AddListener(v => OnPrimaryChanged(name, primaryT, v));
    }

    void EnsureTopCanvas(GameObject go, int order)
    {
        if (!go) return;
        var c = go.GetComponent<Canvas>(); if (!c) c = go.AddComponent<Canvas>();
        c.overrideSorting = true; c.sortingOrder = order;
        if (!go.GetComponent<GraphicRaycaster>()) go.AddComponent<GraphicRaycaster>();
        if (!go.GetComponent<CanvasGroup>()) go.AddComponent<CanvasGroup>();
    }

    void EnsureBlocker()
    {
        if (outsideClickBlocker) return;

        var canvas = panelRoot.GetComponentInParent<Canvas>();
        var canvasRT = canvas.GetComponent<RectTransform>();

        var go = new GameObject("LanguagesPanelBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(canvasRT, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        // transparent but raycastable
        var img = go.GetComponent<Image>(); img.color = new Color(0, 0, 0, 0);

        // click anywhere on blocker -> close panel (except on the first frame we opened)
        go.GetComponent<Button>().onClick.AddListener(() => { if (!openedThisFrame) CommitAndHide(); });

        outsideClickBlocker = go;
        outsideClickBlocker.SetActive(false);
    }

    void OpenPanel()
    {
        EnsureBlocker();
        ShowPanel(true);

        // FIX: Re-apply canvas sorting properties after activation to ensure it's rendered on top.
        EnsureTopCanvas(panelRoot, overlaySortingOrder);

        openedThisFrame = true;

        // make sure blocker is just behind the panel so it catches outside clicks
        if (outsideClickBlocker)
            outsideClickBlocker.transform.SetSiblingIndex(panelRoot.transform.GetSiblingIndex());
        if (panelRoot)
            panelRoot.transform.SetAsLastSibling();
    }

    void ShowPanel(bool on)
    {
        if (panelRoot) panelRoot.SetActive(on);
        if (outsideClickBlocker) outsideClickBlocker.SetActive(on);
    }

    void HideAllPrimaryToggles()
    {
        englishPrimary.gameObject.SetActive(false);
        spanishPrimary.gameObject.SetActive(false);
        frenchPrimary.gameObject.SetActive(false);
        chinesePrimary.gameObject.SetActive(false);
        arabicPrimary.gameObject.SetActive(false);
    }

    static string Canon(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();
    static string Title(string s)
    {
        var c = Canon(s);
        if (string.IsNullOrEmpty(c)) return "—";
        return char.ToUpperInvariant(c[0]) + c.Substring(1);
    }

    // ordered list so our auto-pick is deterministic
    static readonly string[] LangOrder = { "English", "Spanish", "French", "Chinese", "Arabic" };

    bool IsSelected(string lang) => selected.TryGetValue(lang, out var v) && v;

    int KnownCount()
    {
        int n = 0;
        for (int i = 0; i < LangOrder.Length; i++) if (IsSelected(LangOrder[i])) n++;
        return n;
    }

    string FirstKnown()
    {
        for (int i = 0; i < LangOrder.Length; i++) if (IsSelected(LangOrder[i])) return LangOrder[i];
        return null;
    }

    Toggle PrimaryToggleFor(string lang)
    {
        switch (lang)
        {
            case "English": return englishPrimary;
            case "Spanish": return spanishPrimary;
            case "French": return frenchPrimary;
            case "Chinese": return chinesePrimary;
            case "Arabic": return arabicPrimary;
            default: return null;
        }
    }

    void SetPrimary(string lang)
    {
        if (string.IsNullOrEmpty(lang)) return;

        suppress = true;
        foreach (var t in AllPrimaryToggles()) if (t) t.isOn = false;
        var pt = PrimaryToggleFor(lang);
        if (pt) pt.isOn = true;
        suppress = false;

        primary = lang; // display form
        UpdateOpenButtonLabel(primary);
    }

    void EnsurePrimaryInvariant()
    {
        int count = KnownCount();

        if (count == 0)
        {
            primary = null;
            suppress = true;
            foreach (var t in AllPrimaryToggles()) if (t) t.isOn = false;
            suppress = false;
            UpdateOpenButtonLabel(primary);
            return;
        }

        if (string.IsNullOrEmpty(primary) || !IsSelected(primary))
        {
            var pick = FirstKnown();
            if (!string.IsNullOrEmpty(pick))
                SetPrimary(pick);
        }
        else
        {
            suppress = true;
            foreach (var t in AllPrimaryToggles()) if (t) t.isOn = false;
            var pt = PrimaryToggleFor(primary);
            if (pt) pt.isOn = true;
            suppress = false;
            UpdateOpenButtonLabel(primary);
        }
    }

    /* ---------------- UI logic ---------------- */
    void OnMainChanged(string lang, Toggle primaryT, bool on)
    {
        if (suppress) return;
        selected[lang] = on;
        primaryT.gameObject.SetActive(on);

        if (!on && primary == lang)
        {
            suppress = true;
            primaryT.isOn = false;
            suppress = false;

            var fallback = FirstKnown();
            primary = null; // will be set by EnsurePrimaryInvariant if fallback exists
        }

        EnsurePrimaryInvariant();
    }



    void OnPrimaryChanged(string lang, Toggle primaryT, bool on)
    {
        if (suppress) return;

        if (on)
        {
            suppress = true;
            foreach (var t in AllPrimaryToggles()) if (t != primaryT) t.isOn = false;
            suppress = false;
            primary = lang;
            UpdateOpenButtonLabel(primary);
        }
        else
        {
            if (primary == lang && KnownCount() > 0)
            {
                suppress = true;
                primaryT.isOn = true;
                suppress = false;
                return;
            }
        }
    }

    IEnumerable<Toggle> AllPrimaryToggles()
    {
        yield return englishPrimary; yield return spanishPrimary;
        yield return frenchPrimary; yield return chinesePrimary; yield return arabicPrimary;
    }

    /* ---------------- data sync ---------------- */
    void PullFromModel()
    {
        var model = svc ? svc.Model : null;
        var prof  = model ? model.profile : null;

        if (prof != null && prof.language == null)
            prof.language = new characterStats.LanguageStats();

        var m = prof != null ? prof.language : null;

        selected["English"] = m?.english ?? false;
        selected["Spanish"] = m?.spanish ?? false;
        selected["French"]  = m?.french  ?? false;
        selected["Chinese"] = m?.chinese ?? false;
        selected["Arabic"]  = m?.arabic  ?? false;

        string pLower = Canon(m?.primary_language);
        primary = string.IsNullOrEmpty(pLower) ? null : Title(pLower);

        suppress = true;
        if (english) english.isOn = selected["English"];
        if (spanish) spanish.isOn = selected["Spanish"];
        if (french)  french .isOn = selected["French"];
        if (chinese) chinese.isOn = selected["Chinese"];
        if (arabic)  arabic .isOn = selected["Arabic"];

        if (englishPrimary) englishPrimary.gameObject.SetActive(english && english.isOn);
        if (spanishPrimary) spanishPrimary.gameObject.SetActive(spanish && spanish.isOn);
        if (frenchPrimary)  frenchPrimary .gameObject.SetActive(french && french.isOn);
        if (chinesePrimary) chinesePrimary.gameObject.SetActive(chinese && chinese.isOn);
        if (arabicPrimary)  arabicPrimary .gameObject.SetActive(arabic && arabic.isOn);

        if (englishPrimary) englishPrimary.isOn = pLower == "english";
        if (spanishPrimary) spanishPrimary.isOn = pLower == "spanish";
        if (frenchPrimary)  frenchPrimary .isOn = pLower == "french";
        if (chinesePrimary) chinesePrimary.isOn = pLower == "chinese";
        if (arabicPrimary)  arabicPrimary .isOn = pLower == "arabic";
        suppress = false;

        EnsurePrimaryInvariant();
    }


    // old hook (still safe to keep if something references it)
    void OnCloseClicked() => CommitAndHide();

    void CommitAndHide()
    {
        // label is already kept in sync, but ensure before hide
        EnsurePrimaryInvariant();
        ShowPanel(false);

        if (svc == null || svc.Model == null) return;

        var cur = svc.Model.profile.language;
        var path = "profile.language.";

        ChangeBuffer.BeginEdit(svc, "Languages");
        TryRecordBool(path + "english", cur.english, selected["English"]);
        TryRecordBool(path + "spanish", cur.spanish, selected["Spanish"]);
        TryRecordBool(path + "french", cur.french, selected["French"]);
        TryRecordBool(path + "chinese", cur.chinese, selected["Chinese"]);
        TryRecordBool(path + "arabic", cur.arabic, selected["Arabic"]);

        string beforePrimary = cur.primary_language ?? "";
        string afterPrimary = Canon(primary) ?? ""; // save lowercase to model (may be "")
        if (beforePrimary != afterPrimary)
            ChangeBuffer.Record(path + "primary_language", beforePrimary, afterPrimary);

        ChangeBuffer.EndEdit(); // will trigger recompute + PullFromModel
    }

    void TryRecordBool(string path, bool before, bool after)
    { if (before != after) ChangeBuffer.Record(path, before, after); }

    void UpdateOpenButtonLabel(string anyPrimary)
    {
        string label = "Primary: " + Title(anyPrimary);
        if (openButtonTMP) openButtonTMP.text = label;
        if (openButtonText) openButtonText.text = label;
    }

    string GetModelPrimary()
    {
        var p = svc?.Model?.profile?.language?.primary_language;
        return p; // Title() applied at display time
    }

    void OnStatsRecomputed()
    {
        PullFromModel(); // also updates label via EnsurePrimaryInvariant
    }
    
    void OnEnable()
    {
        if (svc == null)
        {
        #if UNITY_2023_1_OR_NEWER
            svc = UnityEngine.Object.FindFirstObjectByType<StatsService>();
        #else
            svc = UnityEngine.Object.FindObjectOfType<StatsService>();
        #endif
        }
        StatsService.StatsRecomputed += OnStatsRecomputed;
    }

    void OnDisable()
    {
        StatsService.StatsRecomputed -= OnStatsRecomputed;

    }

    void OnDestroy()
    {
        // double-unsubscribe is fine/safe
        StatsService.StatsRecomputed -= OnStatsRecomputed;
    }
}