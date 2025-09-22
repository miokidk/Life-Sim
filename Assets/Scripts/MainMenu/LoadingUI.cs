using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Very small loading UI helper with fade in/out and static accessors so any code can nudge it
/// without tight coupling. It is safe to leave this in scenes where you might not always show it.
/// </summary>
public class LoadingUI : MonoBehaviour
{
    public static LoadingUI Instance { get; private set; }

    [Header("Wiring")]
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Slider progress;
    [SerializeField] private TMP_Text status;

    [Header("Timings")]
    [SerializeField] private float fadeDuration = 0.15f;

    void Awake()
    {
        if (Instance == null) Instance = this;
        if (!group) group = GetComponent<CanvasGroup>();
        HideImmediate();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------- Public API --------
    public void Show(string initialStatus = null)
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        if (initialStatus != null) SetStatus(initialStatus);
        // start from transparent so we actually render a frame before work begins
        group.alpha = 0f;
        group.blocksRaycasts = true;
        group.interactable = true;
        StartCoroutine(FadeTo(1f));
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeTo(0f, thenDisable:true));
    }

    public void HideImmediate()
    {
        if (!group) return;
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        gameObject.SetActive(false);
    }

    public void SetProgress(float value)
    {
        if (progress) progress.value = Mathf.Clamp01(value);
    }

    public void SetStatus(string text)
    {
        if (status) status.text = text ?? string.Empty;
    }

    // -------- Static helpers (optional) --------
    public static void SetProgressStatic(float value) => Instance?.SetProgress(value);
    public static void SetStatusStatic(string text) => Instance?.SetStatus(text);
    public static void ShowStatic(string initialStatus = null) => Instance?.Show(initialStatus);
    public static void HideStatic() => Instance?.Hide();

    // -------- Internals --------
    private IEnumerator FadeTo(float target, bool thenDisable = false)
    {
        float start = group.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(start, target, Mathf.Clamp01(t / fadeDuration));
            group.alpha = a;
            yield return null;
        }
        group.alpha = target;

        if (target <= 0f)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
            if (thenDisable) gameObject.SetActive(false);
        }
    }
}
