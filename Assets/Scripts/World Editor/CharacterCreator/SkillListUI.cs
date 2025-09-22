using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class SkillsListUI : MonoBehaviour
{
    [SerializeField] private StatsService svc;
    [SerializeField] private RectTransform listParent;       // Skills/Skills List
    [SerializeField] private RectTransform improvablePrefab; // "Improvable Skill"
    [SerializeField] private RectTransform learnedPrefab;    // "Learned Skill"

    // === NEW: same overlay pattern as Conditions/Senses ===
    [Header("Overlay / Sorting (match Conditions & Senses)")]
    [SerializeField] private RectTransform panelRoot;        // <— assign Relationships & Skills Panel
    [SerializeField] private int overlaySortingOrder = 6000;
    [SerializeField] private Camera uiCamera;
    private RectTransform panelRect;

    void Awake()
    {
        panelRect = panelRoot ? panelRoot : transform as RectTransform;
        if (panelRect) EnsureTopCanvas(panelRect.gameObject, overlaySortingOrder);

        var root = GetComponentInParent<Canvas>();
        if (root && root.renderMode != RenderMode.ScreenSpaceOverlay) uiCamera = root.worldCamera;
    }

    void OnEnable()
    {
        StatsService.StatsRecomputed += Rebuild;
        // bring panel above siblings whenever it enables (like Conditions)
        if (panelRect)
        {
            EnsureTopCanvas(panelRect.gameObject, TopOrder() + 10);
            panelRect.SetAsLastSibling();
        }
        Rebuild();
    }

    void OnDisable() { StatsService.StatsRecomputed -= Rebuild; }

    void Update()
    {
        // click OUTSIDE the panel closes it (same as Conditions/Senses)
        if (!panelRect || !panelRect.gameObject.activeInHierarchy) return;
        if (Input.GetMouseButtonDown(0))
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition, uiCamera))
                panelRect.gameObject.SetActive(false);
        }
    }

    void Rebuild()
    {
        if (!svc || svc.Model == null || !listParent) return;

        for (int i = listParent.childCount - 1; i >= 0; i--)
            Destroy(listParent.GetChild(i).gameObject);

        var skills = svc.Model.skills?.list;
        if (skills == null) return;

        foreach (var e in skills)
        {
            if (e.mode == characterStats.SkillMode.Improvable)
            {
                if (e.awareness > 0 || e.freshness > 0 || e.confidence > 0 || e.efficiency > 0 || e.precision > 0)
                {
                    var row = Instantiate(improvablePrefab, listParent, false);
                    SetText(row, "Skill Title", e.id);

                    BindImprovableSlider(row, "Freshness Label",  e.freshness,  v => e.freshness  = Mathf.RoundToInt(v));
                    BindImprovableSlider(row, "Confidence Label", e.confidence, v => e.confidence = Mathf.RoundToInt(v));
                    BindImprovableSlider(row, "Efficiency Label", e.efficiency, v => e.efficiency = Mathf.RoundToInt(v));
                    BindImprovableSlider(row, "Precision Label",  e.precision,  v => e.precision  = Mathf.RoundToInt(v));
                    AttachRemoveOnTitle(row, e);
                }
            }
            else // LearnOnce
            {
                if (e.progress > 0 || e.learned)
                {
                    var row = Instantiate(learnedPrefab, listParent, false);
                    SetText(row, "Skill Title", e.id);

                    var s = FindChildSlider(row);
                    if (s)
                    {
                        Setup100Slider(s);
                        s.SetValueWithoutNotify(e.progress);
                        AttachRemoveOnTitle(row, e);

                        s.onValueChanged.AddListener(v =>
                        {
                            e.SetLearnOnceProgress(Mathf.RoundToInt(v));
                        });

                        AttachCommitOnRelease(s);
                    }

                    var tTf = row.Find("Toggle");
                    if (tTf)
                    {
                        var t = tTf.GetComponent<Toggle>();
                        if (t)
                        {
                            t.SetIsOnWithoutNotify(e.learned);
                            t.onValueChanged.AddListener(v =>
                            {
                                e.learned = v;
                                if (v && s && e.progress < 100)
                                {
                                    e.progress = 100;
                                    s.SetValueWithoutNotify(100);
                                }
                                svc.CommitAndRecompute();
                            });
                        }
                    }
                }
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listParent);
    }

    // ---- helpers (unchanged) ----
    void BindImprovableSlider(Transform row, string containerName, int value, System.Action<float> apply)
    {
        var container = row.Find(containerName);
        var slider = container ? container.GetComponentInChildren<Slider>(true) : FindChildSlider(row);
        if (!slider)
        {
            Debug.LogWarning($"Could not find slider for {containerName} in {row.name}");
            return;
        }

        Setup100Slider(slider);
        slider.SetValueWithoutNotify(value);
        slider.interactable = true;

        var canvasGroup = slider.GetComponentInParent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        slider.onValueChanged.AddListener(v => apply(v));
        AttachCommitOnRelease(slider);
    }

    void AttachCommitOnRelease(Slider s)
    {
        var et = s.gameObject.GetComponent<EventTrigger>();
        if (!et) et = s.gameObject.AddComponent<EventTrigger>();
        if (et.triggers == null) et.triggers = new List<EventTrigger.Entry>();

        void Add(EventTriggerType type)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => { if (svc) svc.CommitAndRecompute(); });
            et.triggers.Add(entry);
        }

        Add(EventTriggerType.PointerUp);
        Add(EventTriggerType.EndDrag);
    }

    static Slider FindChildSlider(Transform root) => root.GetComponentInChildren<Slider>(true);

    static void Setup100Slider(Slider s)
    {
        s.minValue = 0f;
        s.maxValue = 100f;
        s.wholeNumbers = true;
        s.gameObject.SetActive(true);
        s.enabled = true;
        s.interactable = true;
    }

    static void SetText(Transform root, string childName, string value)
    {
        var tf = root.Find(childName);
        if (!tf) return;
        var tmp = tf.GetComponentInChildren<TMP_Text>(true);
        if (tmp) tmp.text = value;
        else { var ui = tf.GetComponentInChildren<UnityEngine.UI.Text>(true); if (ui) ui.text = value; }
    }

    void AttachRemoveOnTitle(Transform row, characterStats.SkillEntry entry)
    {
        var titleTf = row.Find("Skill Title");
        if (!titleTf) return;

        var go = titleTf.gameObject;
        var tmp = go.GetComponentInChildren<TMP_Text>(true);
        if (tmp) tmp.raycastTarget = true;
        var g = go.GetComponent<Graphic>();
        if (g) g.raycastTarget = true;

        var et = go.GetComponent<EventTrigger>();
        if (!et) et = go.AddComponent<EventTrigger>();
        if (et.triggers == null) et.triggers = new List<EventTrigger.Entry>();

        var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        click.callback.AddListener(_ =>
        {
            var list = svc?.Model?.skills?.list;
            if (list == null) return;
            list.Remove(entry);
            svc?.CommitAndRecompute();
        });
        et.triggers.Add(click);
    }

    // === NEW: shared overlay helpers, same as Conditions/Senses ===
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
