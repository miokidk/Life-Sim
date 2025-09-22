using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ConditionsUI : MonoBehaviour
{
    [Header("Services")]
    [SerializeField] private StatsService svc;

    [Header("Open / Panel / Adder")]
    [SerializeField] private Button openConditionsButton;   // main "Conditions" button
    [SerializeField] private GameObject conditionsPanel;    // "Conditions Panel"
    [SerializeField] private Button addButton;              // "Add Button" inside panel
    [SerializeField] private Transform panelButtonsParent;  // container under panel (your layout)
    [SerializeField] private GameObject adderPanel;         // "Conditions Adder"

    [Header("Prefabs")]
    [SerializeField] private Button panelButtonPrefab;      // simple UI Button (TMP child)

    [Header("Optional")]
    [SerializeField] private Camera uiCamera;               // leave null for Screen Space Overlay

    [Header("Overlay / Sorting")]
    [SerializeField] private int overlaySortingOrder = 6000;


    public const string HealthPrefix =
        "thoughtsAndFeelings.feeling_categories.negative_feelings.health_conditions.";

    readonly Dictionary<string, Button> panelButtons = new();

    RectTransform panelRect;
    RectTransform adderRect;

    void Awake()
    {

        #if UNITY_2023_1_OR_NEWER
        if (svc == null) svc = UnityEngine.Object.FindFirstObjectByType<StatsService>(); // or FindAnyObjectByType for speed if any instance is fine
        #else
        if (svc == null) svc = UnityEngine.Object.FindObjectOfType<StatsService>();
        #endif


        panelRect = conditionsPanel.GetComponent<RectTransform>();
        adderRect = adderPanel.GetComponent<RectTransform>();

        openConditionsButton.onClick.AddListener(OpenPanel);
        addButton.onClick.AddListener(OpenAdder);

        // Hook all adder entries
        foreach (var add in adderPanel.GetComponentsInChildren<ConditionAddButton>(true))
            add.Initialize(this);

        StatsService.StatsRecomputed += RebuildFromModel;

        EnsureTopCanvas(conditionsPanel, overlaySortingOrder);
        EnsureTopCanvas(adderPanel, overlaySortingOrder + 1);

        // (optional) start hidden
        conditionsPanel.SetActive(false);
        adderPanel.SetActive(false);

#if UNITY_2023_1_OR_NEWER
        if (svc == null) svc = UnityEngine.Object.FindFirstObjectByType<StatsService>();
#else
        if (svc == null) svc = UnityEngine.Object.FindObjectOfType<StatsService>();
#endif

    }

    void OnDestroy() => StatsService.StatsRecomputed -= RebuildFromModel;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var mouse = Input.mousePosition;

            // If adder is open: close it when you click anywhere outside it (but keep the panel open)
            if (adderPanel.activeSelf)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(adderRect, mouse, uiCamera))
                    adderPanel.SetActive(false);
                return;
            }

            // If adder is closed and panel is open: click outside panel closes the panel
            if (conditionsPanel.activeSelf)
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(panelRect, mouse, uiCamera))
                    ClosePanel();
            }
        }
    }

    /* ——— Visibility ——— */
    void OpenPanel()
    {
        // FIX: Activate the panel *before* applying canvas sorting properties.
        conditionsPanel.SetActive(true);
        EnsureTopCanvas(conditionsPanel, TopOrder() + 10);

        adderPanel.SetActive(false);
        conditionsPanel.transform.SetAsLastSibling();
        RebuildFromModel();
    }

    void ClosePanel()
    {
        conditionsPanel.SetActive(false);
        adderPanel.SetActive(false);
    }

    /* ——— Model <-> UI ——— */

    // Called by ConditionAddButton when a condition in the Adder is clicked
    public void TryAddCondition(string fullPath, string displayName)
    {

#if UNITY_2023_1_OR_NEWER
        if (svc == null) svc = UnityEngine.Object.FindFirstObjectByType<StatsService>();
#else
        if (svc == null) svc = UnityEngine.Object.FindObjectOfType<StatsService>();
#endif
        if (svc == null || svc.Model == null)
        {
            Debug.LogWarning("ConditionsUI: No StatsService/target. Spawn or select a character first.");
            adderPanel.SetActive(false);
            return;
        }

        if (svc == null || svc.Model == null) return;

        bool already = Convert.ToBoolean(svc.GetValue(fullPath) ?? false);
        if (already) { adderPanel.SetActive(false); return; }

        ChangeBuffer.BeginEdit(svc, $"Add {displayName}");
        ChangeBuffer.Record(fullPath, false, true);
        ChangeBuffer.EndEdit();              // recompute + event fires
        adderPanel.SetActive(false);         // close adder after click
        // RebuildFromModel() will run via StatsRecomputed
    }

    void RebuildFromModel()
    {
        if (svc == null || svc.Model == null) return;

        foreach (var add in adderPanel.GetComponentsInChildren<ConditionAddButton>(true))
        {
            bool isOn = Convert.ToBoolean(svc.GetValue(add.FullPath) ?? false);

            // keep adder buttons inactive while the condition is present
            var addBtn = add.GetComponent<Button>();
            if (addBtn) addBtn.interactable = !isOn;

            bool hasBtn = panelButtons.ContainsKey(add.FullPath);
            if (isOn && !hasBtn) CreatePanelButton(add.FullPath, add.DisplayName);
        }

        // prune removed conditions from the panel
        var keys = new List<string>(panelButtons.Keys);
        foreach (var path in keys)
        {
            bool isOn = Convert.ToBoolean(svc.GetValue(path) ?? false);
            if (!isOn)
            {
                Destroy(panelButtons[path].gameObject);
                panelButtons.Remove(path);
            }
        }
    }

    void CreatePanelButton(string fullPath, string displayName)
    {
        var btn = Instantiate(panelButtonPrefab, panelButtonsParent);
        var label = btn.GetComponentInChildren<TMP_Text>();
        if (label) label.text = displayName;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => RemoveCondition(fullPath)); // <—
        btn.interactable = true; // now usable

        panelButtons[fullPath] = btn;
    }
    void OpenAdder()
    {
        // FIX: Activate the panel *before* applying canvas sorting properties.
        adderPanel.SetActive(true);
        EnsureTopCanvas(adderPanel, TopOrder() + 20);

        adderPanel.transform.SetAsLastSibling();
    }

    void EnsureTopCanvas(GameObject go, int order)
    {
        if (!go) return;
        var c = go.GetComponent<Canvas>(); if (!c) c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = order;

        if (!go.GetComponent<UnityEngine.UI.GraphicRaycaster>())
            go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        if (!go.GetComponent<CanvasGroup>())
            go.AddComponent<CanvasGroup>();
    }

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

    public void RemoveCondition(string fullPath)
    {
        #if UNITY_2023_1_OR_NEWER
                if (svc == null) svc = UnityEngine.Object.FindFirstObjectByType<StatsService>();
        #else
                if (svc == null) svc = UnityEngine.Object.FindObjectOfType<StatsService>();
        #endif
        if (svc == null || svc.Model == null) return;

        bool wasOn = Convert.ToBoolean(svc.GetValue(fullPath) ?? false);
        if (!wasOn) return;

        // mirror the add flow (old -> new)
        ChangeBuffer.BeginEdit(svc, $"Remove {fullPath}");
        ChangeBuffer.Record(fullPath, true, false);
        ChangeBuffer.EndEdit(); // will fire StatsRecomputed -> RebuildFromModel
    }

}