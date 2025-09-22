using UnityEngine;
using System.Collections;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject loadPanel;
    [SerializeField] private LoadingUI loadingUI;

    [Header("Initial World Sizes")]
    public int mainCount = 1;
    public int sideCount = 8;
    public int extraCount = 40;

    public void OnClickNewGame()
    {
        // Ensure we show the loading UI *first* and give Unity a frame to render it
        // before kicking off heavy work so we don't get the spinning beachball.
        StartCoroutine(NewGameFlow());
    }

    private IEnumerator NewGameFlow()
    {
        if (!loadingUI) loadingUI = FindObjectOfType<LoadingUI>(includeInactive: true);
        if (loadingUI)
        {
            loadingUI.Show("Generating World…");
            loadingUI.SetProgress(0f);
        }

        // yield one frame so the canvas renders before doing any heavy work
        yield return null;

        // Delegate to your Game singleton. Internally it should call WorldGenerator.GenerateAsync,
        // which now yields frequently so the UI stays responsive.
        Game.Instance.StartNewGame(mainCount, sideCount, extraCount);
    }

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
