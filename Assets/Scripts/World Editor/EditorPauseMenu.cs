// <pre>
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EditorPauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup menuGroup;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button saveButton;

    [Header("Config")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool pauseTime = false;
    [SerializeField] private GameObject loadPanel;

    private bool isOpen;

    void Awake()
    {
        if (menuGroup != null) HideImmediate();
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnClickMainMenu);
        if (saveButton != null) saveButton.onClick.AddListener(OnClickSave);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Toggle();
    }

    public void Toggle() { if (isOpen) Close(); else Open(); }
    public void Open()
    {
        isOpen = true;
        if (!menuGroup) return;
        menuGroup.gameObject.SetActive(true);
        menuGroup.alpha = 1f;
        menuGroup.blocksRaycasts = true;
        menuGroup.interactable = true;
        if (pauseTime) Time.timeScale = 0f;
    }
    public void Close()
    {
        isOpen = false;
        if (!menuGroup) return;
        menuGroup.alpha = 0f;
        menuGroup.blocksRaycasts = false;
        menuGroup.interactable = false;
        menuGroup.gameObject.SetActive(false);
        if (pauseTime) Time.timeScale = 1f;
    }
    private void HideImmediate()
    {
        isOpen = false;
        menuGroup.alpha = 0f;
        menuGroup.blocksRaycasts = false;
        menuGroup.interactable = false;
        menuGroup.gameObject.SetActive(false);
    }
    void OnDestroy() { if (pauseTime) Time.timeScale = 1f; }

    private void OnClickSave()
    {
        // The async save provides UI feedback, so we can remove the synchronous one.
        // var wrote = Game.Instance?.SaveCurrentWorld("autosave") ?? false;
        Game.Instance?.SaveCurrentWorldAsync("autosave");
        Debug.Log("Game saving (autosave)...");
    }
    private void OnClickMainMenu() { Game.Instance?.ClearCurrentWorld(); SceneManager.LoadSceneAsync(mainMenuSceneName); }

    public void OnClickOpenLoad()
    {
        loadPanel.SetActive(true);
        loadPanel.GetComponent<SaveListUI>()?.Refresh();
    }

    public void OnClickCloseLoad()
    {
        loadPanel.SetActive(false);
    }

}
// </pre>