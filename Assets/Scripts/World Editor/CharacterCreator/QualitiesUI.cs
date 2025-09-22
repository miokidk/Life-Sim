using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class QualitiesUI : MonoBehaviour
{
    [System.Serializable]
    public class Section
    {
        public Button openButton;       // e.g. Moral Button
        public GameObject panelRoot;    // e.g. Moral Panel (root object)
        public Button backButton;       // Back button inside panel
    }

    [Header("Configure all sections here, 1 per row")]
    public Section[] sections;

    // How far above the highest canvas we should render the opened panel
    public int modalPadding = 10;

    // Cache: which buttons we hid so we can re-show them on Back
    private readonly List<GameObject> _hiddenButtons = new List<GameObject>();

    void Awake()
    {
        foreach (var s in sections)
        {
            if (s == null) continue;

            if (s.panelRoot)
            {
                s.panelRoot.SetActive(false);
                EnsureModalCanvas(s.panelRoot);
            }

            if (s.openButton)
            {
                var local = s;
                s.openButton.onClick.AddListener(() => Open(local));
            }

            if (s.backButton)
            {
                var local = s;
                s.backButton.onClick.AddListener(() => Close(local));
            }
        }
    }

    void EnsureModalCanvas(GameObject root)
    {
        var canvas = root.GetComponent<Canvas>();
        if (!canvas) canvas = root.AddComponent<Canvas>();
        canvas.overrideSorting = true;           // allows us to set a custom order
        if (!root.GetComponent<GraphicRaycaster>())
            root.AddComponent<GraphicRaycaster>();
    }

    int HighestSortingOrderInScene()
    {
        int maxOrder = 0;

        #if UNITY_2022_2_OR_NEWER
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        #else
        var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
        #endif

        foreach (var c in canvases)
        {
            // sortingOrder only matters when overrideSorting is true
            if (c.overrideSorting && c.sortingOrder > maxOrder)
                maxOrder = c.sortingOrder;
        }
        return maxOrder;
    }


    void Open(Section target)
    {
        if (!target.panelRoot) return;

        // 1) Put the panel visually on top of everything
        var canvas = target.panelRoot.GetComponent<Canvas>();
        canvas.sortingOrder = HighestSortingOrderInScene() + modalPadding;
        target.panelRoot.transform.SetAsLastSibling(); // belt & suspenders
        target.panelRoot.SetActive(true);

        // 2) Hide all OTHER section buttons so nothing peeks above
        _hiddenButtons.Clear();
        foreach (var s in sections)
        {
            if (s == null || s == target) continue;
            if (s.openButton && s.openButton.gameObject.activeSelf)
            {
                _hiddenButtons.Add(s.openButton.gameObject);
                s.openButton.gameObject.SetActive(false);
            }
        }
    }

    void Close(Section target)
    {
        if (target.panelRoot) target.panelRoot.SetActive(false);

        // Re-show any buttons we hid
        foreach (var go in _hiddenButtons)
            if (go) go.SetActive(true);
        _hiddenButtons.Clear();
    }
}
