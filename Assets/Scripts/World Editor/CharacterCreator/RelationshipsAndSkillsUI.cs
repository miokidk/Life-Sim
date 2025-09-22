using UnityEngine;
using UnityEngine.UI;

public class RelationshipsAndSkillsUI : MonoBehaviour
{
    [Header("Hook in Inspector")]
    [SerializeField] private Button rsButton;                 // "Relationships & Skills" Button
    [SerializeField] private GameObject rsPanel;              // Panel root

    [SerializeField] private Button relationshipsLabel;       // make these Buttons
    [SerializeField] private Button skillsLabel;

    [Tooltip("Graphic to tint for Relationships (defaults to label.targetGraphic)")]
    [SerializeField] private Graphic relationshipsLabelGfx;
    [Tooltip("Graphic to tint for Skills (defaults to label.targetGraphic)")]
    [SerializeField] private Graphic skillsLabelGfx;

    [SerializeField] private GameObject relationshipsMenu;    // tab content roots
    [SerializeField] private GameObject skillsMenu;

    [Header("Layout parents (with VerticalLayoutGroup)")]
    [SerializeField] private RectTransform relationshipsContent;
    [SerializeField] private RectTransform skillsContent;

    [Header("Label colors")]
    [SerializeField] private Color activeColor   = Color.yellow;
    [SerializeField] private Color inactiveColor = Color.gray;

    void Awake()
    {
        // Fallback graphics
        if (!relationshipsLabelGfx && relationshipsLabel) relationshipsLabelGfx = relationshipsLabel.targetGraphic;
        if (!skillsLabelGfx && skillsLabel) skillsLabelGfx = skillsLabel.targetGraphic;

        // Listeners
        if (rsButton) rsButton.onClick.AddListener(TogglePanel);
        if (relationshipsLabel) relationshipsLabel.onClick.AddListener(() => SelectTab(true));
        if (skillsLabel)        skillsLabel.onClick.AddListener(() => SelectTab(false));

        // ---- DEFAULT TAB ON LAUNCH ----
        SelectTab(true); // Relationships highlighted + menu active
    }

    void OnEnable()
    {
        if (rsPanel && rsPanel.activeSelf) RebuildAll();
    }

    void OnDestroy()
    {
        if (rsButton) rsButton.onClick.RemoveListener(TogglePanel);
        if (relationshipsLabel) relationshipsLabel.onClick.RemoveAllListeners();
        if (skillsLabel) skillsLabel.onClick.RemoveAllListeners();
    }

    // Public helpers for spawning rows
    public RectTransform AddRelationshipRow(RectTransform prefab)
    {
        if (!prefab || !relationshipsContent) return null;
        var row = Instantiate(prefab);
        row.SetParent(relationshipsContent, false);
        row.SetAsLastSibling();
        Rebuild(relationshipsContent);
        return row;
    }

    public RectTransform AddSkillRow(RectTransform prefab)
    {
        if (!prefab || !skillsContent) return null;
        var row = Instantiate(prefab);
        row.SetParent(skillsContent, false);
        row.SetAsLastSibling();
        Rebuild(skillsContent);
        return row;
    }

    // UI actions
    void TogglePanel()
    {
        if (!rsPanel) return;
        bool show = !rsPanel.activeSelf;
        rsPanel.SetActive(show);
        if (show)
        {
            rsPanel.transform.SetAsLastSibling();
            RebuildAll();
        }
    }

    void SelectTab(bool showRelationships)
    {
        if (relationshipsLabelGfx) relationshipsLabelGfx.color = showRelationships ? activeColor : inactiveColor;
        if (skillsLabelGfx)        skillsLabelGfx.color        = showRelationships ? inactiveColor : activeColor;

        if (relationshipsMenu) relationshipsMenu.SetActive(showRelationships);
        if (skillsMenu)        skillsMenu.SetActive(!showRelationships);

        Rebuild(showRelationships ? relationshipsContent : skillsContent);
    }

    // Layout utils
    void RebuildAll()
    {
        Canvas.ForceUpdateCanvases();
        Rebuild(relationshipsContent);
        Rebuild(skillsContent);
    }

    void Rebuild(RectTransform rt)
    {
        if (!rt) return;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
    }

    [ContextMenu("Normalize Anchors (Top-Stretch)")]
    void NormalizeAnchors()
    {
        ApplyTopStretch(relationshipsContent);
        ApplyTopStretch(skillsContent);
    }

    static void ApplyTopStretch(RectTransform rt)
    {
        if (!rt) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        var pos = rt.anchoredPosition; pos.y = 0f; rt.anchoredPosition = pos;
    }
}
