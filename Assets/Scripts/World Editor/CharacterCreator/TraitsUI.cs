using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TraitsUI : MonoBehaviour
{
    [Header("Services (runtime)")]
    [SerializeField] private StatsService svc;

    [Header("Panel (selected traits)")]
    [SerializeField] private Button plusButton;
    [SerializeField] private Transform panelGrid;               // GridLayoutGroup parent
    [SerializeField] private Button panelTraitButtonPrefab;     // clicked to REMOVE the trait

    [Header("Adder (all traits)")]
    [SerializeField] private GameObject adderRoot;              // show/hide
    [SerializeField] private Transform adderContent;            // Viewport/Content
    [SerializeField] private Button adderTraitButtonPrefab;     // prefab used to build adder buttons

    [Header("Editor generation")]
    [Tooltip("If true, regenerates Adder buttons whenever something changes in the inspector.")]
    [SerializeField] private bool autoRebuildInEditor = true;

    // Runtime maps
    private readonly Dictionary<string, Button> _adderButtons = new();
    private readonly Dictionary<string, Button> _panelButtons = new();

    private FieldInfo[] _traitFields;

    // --- References for "click outside" logic ---
    private RectTransform _adderRect;
    private RectTransform _plusButtonRect;
    private Camera _uiCamera;


    /* -------------------- Lifecycle -------------------- */

    void Awake()
    {
        CacheTraitFields();

        if (Application.isPlaying)
        {
            BuildAdderButtons_Runtime();

            if (plusButton != null)
                plusButton.onClick.AddListener(OpenAdder);

            CloseAdder(); // Ensure adder is closed on start

            StatsService.StatsRecomputed += SyncAll;
            SyncAll();

            // --- Get references needed for click checks ---
            if (adderRoot != null)
                _adderRect = adderRoot.GetComponent<RectTransform>();
            if (plusButton != null)
                _plusButtonRect = plusButton.GetComponent<RectTransform>();
            
            var canvas = GetComponentInParent<Canvas>();
            if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                _uiCamera = canvas.worldCamera;
        }
    }

    void OnEnable()
    {
        if (Application.isPlaying)
        {
            StatsService.StatsRecomputed += SyncAll;
            if (_adderButtons.Count > 0) 
                SyncAll();
        }
    }

    void OnDisable()
    {
        if (Application.isPlaying)
            StatsService.StatsRecomputed -= SyncAll;
    }

    // --- Update method to check for clicks/touches outside the UI ---
    void Update()
    {
        if (!Application.isPlaying) return;
        if (adderRoot == null || !adderRoot.activeSelf) return;

        if (Input.GetMouseButtonDown(0) && ClickedOutsideAdder(Input.mousePosition))
            CloseAdder();

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (ClickedOutsideAdder(Input.GetTouch(0).position))
                CloseAdder();
        }
    }

    // --- Helper method to determine if a screen position is outside the adder and its opening button ---
    bool ClickedOutsideAdder(Vector2 screenPos)
    {
        bool overAdder = _adderRect &&
            RectTransformUtility.RectangleContainsScreenPoint(_adderRect, screenPos, _uiCamera);

        bool overButton = _plusButtonRect &&
            RectTransformUtility.RectangleContainsScreenPoint(_plusButtonRect, screenPos, _uiCamera);

        return !overAdder && !overButton;
    }

    void OnValidate()
    {
        CacheTraitFields();

        #if UNITY_EDITOR
        if (!Application.isPlaying && autoRebuildInEditor)
        {
            // Defer the rebuild to avoid issues during serialization
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                    RebuildAdderButtons_Editor();
            };
        }
        #endif
    }
    
    void OpenAdder()
    {
        if (adderRoot != null) adderRoot.SetActive(true);
    }

    void CloseAdder()
    {
        if (adderRoot != null) adderRoot.SetActive(false);
    }

    /* -------------------- Build / Rebuild -------------------- */

    void CacheTraitFields()
    {
        _traitFields = typeof(characterStats.TraitsStats)
            .GetFields(BindingFlags.Instance | BindingFlags.Public);
    }

    void BuildAdderButtons_Runtime()
    {
        if (adderContent == null || adderTraitButtonPrefab == null) return;

        foreach (Transform c in adderContent) Destroy(c.gameObject);
        _adderButtons.Clear();

        foreach (var f in _traitFields)
        {
            string key = f.Name;
            var btn = Instantiate(adderTraitButtonPrefab, adderContent, false);
            btn.gameObject.name = $"Trait_{key}";
            SetButtonLabel(btn, ToTitle(key));
            btn.onClick.AddListener(() => { OnAdderClicked(key); });
            _adderButtons[key] = btn;
        }
    }

    #if UNITY_EDITOR
    [ContextMenu("Rebuild Adder Buttons (Editor)")]
    public void RebuildAdderButtons_Editor()
    {
        // CRITICAL: Don't run during play mode
        if (Application.isPlaying) return;
        
        if (adderContent == null || adderTraitButtonPrefab == null) return;

        // Clear existing children
        var toDelete = new List<GameObject>();
        foreach (Transform c in adderContent) 
            toDelete.Add(c.gameObject);
        
        foreach (var go in toDelete)
        {
            // Only use DestroyImmediate in Editor mode when NOT playing
            if (!Application.isPlaying)
                DestroyImmediate(go);
            else
                Destroy(go);
        }

        _adderButtons.Clear();

        foreach (var f in _traitFields)
        {
            string key = f.Name;
            GameObject newGO = null;
            
            // Only use PrefabUtility when in Editor and NOT playing
            if (!Application.isPlaying)
            {
                var prefab = adderTraitButtonPrefab.gameObject;
                var instance = PrefabUtility.InstantiatePrefab(prefab, adderContent);
                newGO = instance as GameObject;
            }
            
            // Fallback to regular instantiate if PrefabUtility didn't work or we're in play mode
            if (newGO == null) 
                newGO = Instantiate(adderTraitButtonPrefab.gameObject, adderContent, false);
            
            // Only register undo if we're in editor and not playing
            if (!Application.isPlaying)
                Undo.RegisterCreatedObjectUndo(newGO, "Create Trait Button");

            
            var btn = newGO.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning("Adder Button Prefab must have a Button component.");
                continue;
            }
            
            SetButtonLabel(btn, ToTitle(key));
            btn.onClick = new Button.ButtonClickedEvent();
            newGO.name = $"Trait_{key}";
            _adderButtons[key] = btn;
        }
        
        if (!Application.isPlaying)
            EditorUtility.SetDirty(this);
    }
    #endif

    /* -------------------- Wiring (runtime) -------------------- */

    void SyncAll()
    {
        if (!Application.isPlaying) return;

        foreach (var f in _traitFields)
        {
            string key = f.Name;
            bool hasTrait = GetBool($"traits.{key}");

            if (_adderButtons.TryGetValue(key, out var abtn) && abtn != null)
                abtn.interactable = !hasTrait;

            if (hasTrait) EnsurePanelButton(key);
            else          RemovePanelButton(key);
        }
    }

    void EnsurePanelButton(string traitKey)
    {
        if (_panelButtons.ContainsKey(traitKey)) return;
        var btn = Instantiate(panelTraitButtonPrefab, panelGrid, false);
        SetButtonLabel(btn, ToTitle(traitKey));
        btn.onClick.AddListener(() => { RemoveTrait(traitKey); });
        _panelButtons[traitKey] = btn;
    }

    void RemovePanelButton(string traitKey)
    {
        if (_panelButtons.TryGetValue(traitKey, out var b) && b != null)
            Destroy(b.gameObject);
        _panelButtons.Remove(traitKey);
    }

    /* -------------------- Actions -------------------- */

    void OnAdderClicked(string traitKey)
    {
        AddTrait(traitKey);
        CloseAdder();
    }

    void AddTrait(string traitKey)
    {
        string path = $"traits.{traitKey}";
        bool before = GetBool(path);
        if (before) return;
        ChangeBuffer.BeginEdit(svc, $"Add trait: {ToTitle(traitKey)}");
        ChangeBuffer.Record(path, before, true);
        ChangeBuffer.EndEdit(); 
    }

    void RemoveTrait(string traitKey)
    {
        string path = $"traits.{traitKey}";
        bool before = GetBool(path);
        if (!before) return;
        ChangeBuffer.BeginEdit(svc, $"Remove trait: {ToTitle(traitKey)}");
        ChangeBuffer.Record(path, before, false);
        ChangeBuffer.EndEdit();
    }

    /* -------------------- Helpers -------------------- */

    bool GetBool(string path)
    {
        var obj = svc != null ? svc.GetValue(path) : null;
        return obj is bool b && b;
    }

    static string ToTitle(string snakeOrLower)
    {
        var chars = snakeOrLower.Replace('_', ' ').ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (i == 0 || chars[i - 1] == ' ')
                chars[i] = Char.ToUpperInvariant(chars[i]);
        return new string(chars);
    }

    static void SetButtonLabel(Button b, string text)
    {
        var tmp = b.GetComponentInChildren<TMP_Text>();
        if (tmp) { tmp.text = text; return; }
        var legacy = b.GetComponentInChildren<UnityEngine.UI.Text>();
        if (legacy) legacy.text = text;
    }
}