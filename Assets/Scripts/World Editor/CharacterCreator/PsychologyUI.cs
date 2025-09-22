using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PsychologyUI : MonoBehaviour
{
    [Header("Tabs")]
    [SerializeField] private GameObject physiologyRoot;
    [SerializeField] private GameObject psychologyRoot;

    [Header("Tab Buttons")]
    [SerializeField] private Button physiologyButton;  // the "Physiology" button that lives on the Psychology screen

    [System.Serializable]
    public class Section
    {
        public Button topButton;          // may be null
        public Button bottomButton;       // may be null
        public GameObject contentRoot;    // panel/scrollview
    }

    [Header("Sections (top → bottom)")]
    [SerializeField] private List<Section> sections;

    [SerializeField] private int defaultOpenIndex = 0; // e.g., Qualities
    private int currentIndex = -1;

    void Awake()
    {
        // Wire section buttons
        for (int i = 0; i < sections.Count; i++)
        {
            int idx = i;
            if (sections[i].topButton)
            {
                sections[i].topButton.onClick.RemoveAllListeners();
                sections[i].topButton.onClick.AddListener(() => Show(idx));
            }
            if (sections[i].bottomButton)
            {
                sections[i].bottomButton.onClick.RemoveAllListeners();
                sections[i].bottomButton.onClick.AddListener(() => Show(idx));
            }
        }

        // Wire tab buttons
        if (physiologyButton)
        {
            physiologyButton.onClick.RemoveAllListeners();
            physiologyButton.onClick.AddListener(OpenPhysiology);
        }
    }

    void Start() => Show(defaultOpenIndex);

    public void OpenPhysiology()
    {
        psychologyRoot.SetActive(false);
        physiologyRoot.SetActive(true);
    }

    private void Show(int index)
    {
        if (index < 0 || index >= sections.Count) return;
        currentIndex = index;

        for (int i = 0; i < sections.Count; i++)
        {
            var s = sections[i];
            bool isOpen = (i == index);

            if (s.contentRoot) s.contentRoot.SetActive(isOpen);

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
}
