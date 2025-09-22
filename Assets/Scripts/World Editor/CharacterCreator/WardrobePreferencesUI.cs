using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // safe: we’ll fall back to Text if TMP isn’t present

public class WardrobePreferencesUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ClothingCatalog catalog;           // assign SO
    [SerializeField] private CharacterWardrobe wardrobe;        // character component (auto-found if null)

    [Header("Category Buttons")]
    [SerializeField] private Button underwearBtn;
    [SerializeField] private Button shortSleeveBtn;
    [SerializeField] private Button longSleeveBtn;
    [SerializeField] private Button headwearBtn;
    [SerializeField] private Button socksBtn;
    [SerializeField] private Button bottomsBtn;
    [SerializeField] private Button overgarmentsBtn;
    [SerializeField] private Button shoesBtn;

    [Header("Articles List")]
    [SerializeField] private Transform listContent;             // Articles Scroll View/Viewport/Content
    [SerializeField] private Button articleButtonTemplate;      // disabled template child under Content

    // ADDED: New fields to control the panel's open/close behavior
    [Header("Panel Behavior")]
    [Tooltip("The parent object of the scroll view that lists the articles.")]
    [SerializeField] private GameObject articlesPanel;
    [Tooltip("Should the articles panel start hidden?")]
    [SerializeField] private bool startsHidden = true;
    [Tooltip("Should the panel close when clicking outside of it and its category buttons?")]
    [SerializeField] private bool closeOnClickAway = true;

    [Header("Visuals")]
    [SerializeField] private Color selectedColor = new Color(0.25f, 0.55f, 1f, 0.9f);
    [SerializeField] private Color normalColor   = Color.white;

    private StatsService _svc;
    private characterStats _boundModel;

    // keep the last category so we can refresh after toggles
    private ClothingCategory _activeCategory;
    
    // ADDED: Variables for click-away logic
    private RectTransform _articlesPanelRect;
    private readonly List<RectTransform> _categoryButtonRects = new List<RectTransform>();
    private Camera _uiCamera;

    void Awake()
    {
        _svc = FindAnyObjectByType<StatsService>();

        if (wardrobe == null)
        {
            var svc = FindObjectOfType<StatsService>(); // uses your StatsService.target (characterStats)
            if (svc != null && svc.Model != null)
                wardrobe = svc.Model.GetComponent<CharacterWardrobe>();
        }

        // ADDED: Initialize panel state and variables for click detection
        if (articlesPanel != null)
        {
            _articlesPanelRect = articlesPanel.GetComponent<RectTransform>();
            if (startsHidden)
            {
                articlesPanel.SetActive(false);
            }
        }
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            _uiCamera = canvas.worldCamera;
        }

        // template must be disabled in the scene
        if (articleButtonTemplate != null) articleButtonTemplate.gameObject.SetActive(false);

        Bind(underwearBtn,     ClothingCategory.Underwear);
        Bind(shortSleeveBtn,   ClothingCategory.ShortSleeveShirt);
        Bind(longSleeveBtn,    ClothingCategory.LongSleeveShirt);
        Bind(headwearBtn,      ClothingCategory.Headwear);
        Bind(socksBtn,         ClothingCategory.Socks);
        Bind(bottomsBtn,       ClothingCategory.Bottoms);
        Bind(overgarmentsBtn,  ClothingCategory.Overgarments);
        Bind(shoesBtn,         ClothingCategory.Shoes);

        if (wardrobe != null) wardrobe.OnWardrobeChanged += RefreshActive;
    }

    void OnDestroy()
    {
        if (wardrobe != null) wardrobe.OnWardrobeChanged -= RefreshActive;
    }

    private void Bind(Button btn, ClothingCategory cat)
    {
        if (btn == null) return;
        btn.onClick.AddListener(() => OnCategoryClicked(cat));
        // ADDED: Store button's RectTransform for the click-away check
        _categoryButtonRects.Add(btn.GetComponent<RectTransform>());
    }

    // ADDED: New Update method to handle closing the panel when clicking away
    void Update()
    {
        if (!closeOnClickAway || articlesPanel == null || !articlesPanel.activeSelf)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && ClickedOutsideRelevantUI(Input.mousePosition))
        {
            articlesPanel.SetActive(false);
        }
    }
    
    // ADDED: Helper function to check if a click is outside all relevant UI
    private bool ClickedOutsideRelevantUI(Vector2 screenPos)
    {
        // Check if the click was inside the articles panel
        if (_articlesPanelRect != null && RectTransformUtility.RectangleContainsScreenPoint(_articlesPanelRect, screenPos, _uiCamera))
        {
            return false;
        }

        // Check if the click was on any of the category buttons
        foreach (var rect in _categoryButtonRects)
        {
            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, _uiCamera))
            {
                return false;
            }
        }

        // Click was outside all relevant UI
        return true;
    }

    private void OnCategoryClicked(ClothingCategory category)
    {
        // ADDED: Show the panel when a category button is clicked
        if (articlesPanel != null && !articlesPanel.activeSelf)
        {
            articlesPanel.SetActive(true);
        }
        
        _activeCategory = category;
        Populate(category);
    }

    private void RefreshActive() => Populate(_activeCategory);

    private void Populate(ClothingCategory category)
    {
        if (catalog == null || listContent == null || articleButtonTemplate == null) return;

        // clear old (keep template)
        for (int i = listContent.childCount - 1; i >= 0; i--)
        {
            var c = listContent.GetChild(i);
            if (c == articleButtonTemplate.transform) continue;
            Destroy(c.gameObject);
        }

        var preferred = new HashSet<string>(wardrobe != null
            ? wardrobe.GetPreferredTypes(category)
            : Array.Empty<string>());

        var types = catalog.GetByCategory(category);
        foreach (var t in types)
        {
            var btn = Instantiate(articleButtonTemplate, listContent);
            btn.gameObject.SetActive(true);
            SetLabel(btn.gameObject, string.IsNullOrWhiteSpace(t.label) ? t.id : t.label);

            // capture for closure
            string typeId = t.id;
            var img = btn.GetComponent<Image>();
            ApplySelectedVisual(img, preferred.Contains(typeId));

            btn.onClick.AddListener(() =>
            {
                TogglePreference(category, typeId);
                // refresh entire list so visuals stay in sync
                // Populate(category); // This is now handled by the OnWardrobeChanged event
            });
        }
    }

    private void TogglePreference(ClothingCategory cat, string typeId)
    {
        if (wardrobe == null) return;

        var set = new HashSet<string>(wardrobe.GetPreferredTypes(cat), StringComparer.Ordinal);
        if (set.Contains(typeId)) set.Remove(typeId);
        else set.Add(typeId);

        wardrobe.SetPreferredTypes(cat, set);
        // wardrobe.OnWardrobeChanged will trigger RefreshActive()
    }

    private void ApplySelectedVisual(Image img, bool selected)
    {
        if (img == null) return;
        img.color = selected ? selectedColor : normalColor;
    }



    private static void SetLabel(GameObject go, string text)
    {
        // Try TMP first
        var tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) { tmp.text = text; return; }

        // Fallback to legacy Text
        var ui = go.GetComponentInChildren<Text>(true);
        if (ui != null) ui.text = text;
    }

    void LateUpdate() {
        if (_svc == null) return;
        var model = _svc.Model; // current characterStats
        if (model != _boundModel) {
            _boundModel = model;
            wardrobe = model ? model.GetComponent<CharacterWardrobe>() : null;
            if (wardrobe != null) {
                wardrobe.OnWardrobeChanged -= RefreshActive; // Unsubscribe from old
                wardrobe.OnWardrobeChanged += RefreshActive; // Subscribe to new
            }
            RefreshActive(); // repopulate current category
        }
    }
}