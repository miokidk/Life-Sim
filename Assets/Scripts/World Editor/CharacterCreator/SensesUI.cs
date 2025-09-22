using UnityEngine;
using UnityEngine.UI;

public class SensesUI : MonoBehaviour
{
    [Header("Hook these in Inspector")]
    [SerializeField] private Button sensesButton;      // "Senses" button
    [SerializeField] private GameObject sensesPanel;   // "Senses Panel" root

    [Header("Behavior")]
    [SerializeField] private bool closeOnEsc = true;
    [SerializeField] private bool makeTopMost = true;  // keep panel above everything
    [SerializeField] private int sortingOrder = 5000;  // big number wins

    private RectTransform panelRect;
    private Camera uiCamera;
    private Canvas panelCanvas;

    void Awake()
    {
        if (sensesPanel != null)
        {
            panelRect = sensesPanel.GetComponent<RectTransform>();

            if (makeTopMost)
            {
                panelCanvas = sensesPanel.GetComponent<Canvas>();
                if (!panelCanvas) panelCanvas = sensesPanel.AddComponent<Canvas>();
                panelCanvas.overrideSorting = true;
                panelCanvas.sortingOrder = sortingOrder;

                if (!sensesPanel.GetComponent<GraphicRaycaster>())
                    sensesPanel.AddComponent<GraphicRaycaster>();
            }

            sensesPanel.SetActive(false);
        }

        if (sensesButton != null)
            sensesButton.onClick.AddListener(TogglePanel);

        var canvas = GetComponentInParent<Canvas>();
        if (canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;
    }

    void OnDestroy()
    {
        if (sensesButton != null)
            sensesButton.onClick.RemoveListener(TogglePanel);
    }

    void TogglePanel()
    {
        if (!sensesPanel) return;

        bool show = !sensesPanel.activeSelf;
        sensesPanel.SetActive(show);

        if (show)
        {
            // FIX: Re-apply sorting properties after activating the panel.
            // This resolves a potential timing issue in Unity's UI system where a
            // re-enabled Canvas might not be sorted correctly in the same frame.
            if (makeTopMost && panelCanvas != null)
            {
                panelCanvas.overrideSorting = true;
                panelCanvas.sortingOrder = sortingOrder;
            }

            if (panelRect != null)
                panelRect.SetAsLastSibling(); // also last among its siblings
        }
    }

    void Update()
    {
        if (!sensesPanel || !sensesPanel.activeSelf) return;

        if (closeOnEsc && Input.GetKeyDown(KeyCode.Escape))
        {
            sensesPanel.SetActive(false);
            return;
        }

        if (Input.GetMouseButtonDown(0) && ClickedOutsidePanelAndButton(Input.mousePosition))
            sensesPanel.SetActive(false);

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            if (ClickedOutsidePanelAndButton(Input.GetTouch(0).position))
                sensesPanel.SetActive(false);
        }
    }

    bool ClickedOutsidePanelAndButton(Vector2 screenPos)
    {
        bool overPanel = panelRect &&
            RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPos, uiCamera);

        var btnRect = sensesButton ? sensesButton.GetComponent<RectTransform>() : null;
        bool overButton = btnRect &&
            RectTransformUtility.RectangleContainsScreenPoint(btnRect, screenPos, uiCamera);

        return !overPanel && !overButton;
    }
}