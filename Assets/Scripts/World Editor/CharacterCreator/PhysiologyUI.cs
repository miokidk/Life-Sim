using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PhysiologyUI : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private GameObject physiologyRoot;   // this panel/root
    [SerializeField] private GameObject psychologyRoot;   // Psychology panel/root
    [SerializeField] private Button     openPsychologyButton;

    [System.Serializable]
    public class Section
    {
        public Button topButton;           // may be null
        public Button bottomButton;        // may be null
        public GameObject contentRoot;     // panel/scrollview (null for Reproductive section)
    }

    [Header("Sections (top → bottom)")]
    [SerializeField] private List<Section> sections = new List<Section>();

    [Header("Reproductive Organs (special section)")]
    [Tooltip("Index in the Sections list that corresponds to Reproductive Organs.")]
    [SerializeField] private int reproductiveIndex = -1;
    [SerializeField] private GameObject maleReproductivePanel;
    [SerializeField] private GameObject femaleReproductivePanel;
    [SerializeField] private GameObject intersexReproductivePanel;

    [Header("Model / Stats")]
    [SerializeField] private StatsService stats;  // auto-found if left empty

    [SerializeField] private int defaultOpenIndex = 0;
    private int currentIndex = -1;

    private void Awake()
    {
        if (!stats) stats = FindObjectOfType<StatsService>();

        // Wire section buttons
        for (int i = 0; i < sections.Count; i++)
        {
            int idx = i;
            var s = sections[i];

            if (s.topButton)
            {
                s.topButton.onClick.RemoveAllListeners();
                s.topButton.onClick.AddListener(() => Show(idx));
            }
            if (s.bottomButton)
            {
                s.bottomButton.onClick.RemoveAllListeners();
                s.bottomButton.onClick.AddListener(() => Show(idx));
            }
        }

        // Tab button
        if (openPsychologyButton)
        {
            openPsychologyButton.onClick.RemoveAllListeners();
            openPsychologyButton.onClick.AddListener(OpenPsychology);
        }
    }

    private void OnEnable()
    {
        StatsService.StatsRecomputed += HandleStatsRecomputed;
    }

    private void OnDisable()
    {
        StatsService.StatsRecomputed -= HandleStatsRecomputed;
    }

    private void Start()
    {
        Show(defaultOpenIndex);
    }

    public void OpenPsychology()
    {
        if (psychologyRoot) psychologyRoot.SetActive(true);
        if (physiologyRoot) physiologyRoot.SetActive(false);
    }

    private void HandleStatsRecomputed()
    {
        // If the Reproductive section is currently open and sex changed,
        // refresh which panel is visible.
        if (currentIndex == reproductiveIndex)
            Show(currentIndex);
    }

    private void Show(int index)
    {
        if (index < 0 || index >= sections.Count) return;
        currentIndex = index;

        for (int i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            bool isOpen = (i == index);

            if (i == reproductiveIndex)
            {
                RefreshReproductivePanels(isOpen);
            }
            else
            {
                if (s.contentRoot) s.contentRoot.SetActive(isOpen);
            }

            // Button visibility (same pattern as Psychology)
            if (isOpen)
            {
                if (s.topButton)    s.topButton.gameObject.SetActive(false);
                if (s.bottomButton) s.bottomButton.gameObject.SetActive(false);
            }
            else
            {
                bool isAbove = i < index;
                if (s.topButton)    s.topButton.gameObject.SetActive(isAbove);
                if (s.bottomButton) s.bottomButton.gameObject.SetActive(!isAbove);
            }
        }
    }

    private void RefreshReproductivePanels(bool open)
    {
        // Hide all first
        if (maleReproductivePanel)    maleReproductivePanel.SetActive(false);
        if (femaleReproductivePanel)  femaleReproductivePanel.SetActive(false);
        if (intersexReproductivePanel) intersexReproductivePanel.SetActive(false);

        if (!open) return;

        var panel = GetReproductivePanelForCurrentSex();
        if (panel) panel.SetActive(true);
    }

    private GameObject GetReproductivePanelForCurrentSex()
    {
        var model = stats ? stats.Model : null;
        var sex = model != null ? model.profile.sex : characterStats.SexType.Male;

        switch (sex)
        {
            case characterStats.SexType.Female:   return femaleReproductivePanel;
            case characterStats.SexType.Intersex: return intersexReproductivePanel;
            default:                              return maleReproductivePanel;
        }
    }
}
