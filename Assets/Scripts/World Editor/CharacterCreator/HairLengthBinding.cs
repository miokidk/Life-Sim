using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Slider))]
public sealed class HairLengthBinding : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Model")]
    public StatsService service;
    [Tooltip("If your hair length is a single field, use this.")]
    public string singlePath; // e.g., "hair.length.units"
    [Tooltip("If you store per-section lengths, fill these. If set, overrides singlePath.")]
    public string[] sectionPaths; // e.g., "hair.front.length","hair.top.length",...

    [Header("UI")]
    public TMP_Text valueText;
    public float unitsPerInch = 10f;   // 10 units = 1 inch
    public int minUnits = 0;
    public int maxUnits = 500;

    Slider slider;

    // originals for undo batching
    object singleBefore;
    object[] sectionBefore;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = minUnits;
        slider.maxValue = maxUnits;
        slider.wholeNumbers = true; // hair length uses whole unit steps (0–500)

        slider.onValueChanged.AddListener(OnSliderChanged);
        StatsService.StatsRecomputed += PullFromModel;
    }

    void OnDestroy()
    {
        StatsService.StatsRecomputed -= PullFromModel;
    }

    void Start() => PullFromModel();

    public void PullFromModel()
    {
        if (service == null || service.Model == null) { slider.interactable = false; return; }
        slider.interactable = true;

        // If per-section, show average; otherwise show the single value.
        if (sectionPaths != null && sectionPaths.Length > 0)
        {
            float sum = 0f; int count = 0;
            foreach (var p in sectionPaths)
            {
                var v = service.GetValue(p);
                sum += ToFloat(v);
                count++;
            }
            float avg = count > 0 ? sum / count : 0f;
            slider.SetValueWithoutNotify(Mathf.RoundToInt(avg));
            UpdateLabel(avg);
        }
        else if (!string.IsNullOrEmpty(singlePath))
        {
            float v = ToFloat(service.GetValue(singlePath));
            slider.SetValueWithoutNotify(Mathf.RoundToInt(v));
            UpdateLabel(v);
        }
        else
        {
            // nothing to bind
            slider.interactable = false;
        }
    }

    void OnSliderChanged(float v)
    {
        // Live UI feedback only; no writes to the model until pointer up.
        UpdateLabel(v);
    }

    void UpdateLabel(float units)
    {
        if (!valueText) return;
        float inches = units / Mathf.Max(0.0001f, unitsPerInch);
        valueText.text = $"{inches:0.0}\"";
    }

    public void OnPointerDown(PointerEventData _)
    {
        if (service == null || service.Model == null) return;

        // snapshot originals and begin a batch
        ChangeBuffer.BeginEdit(service, "Set hair length");

        if (sectionPaths != null && sectionPaths.Length > 0)
        {
            sectionBefore = new object[sectionPaths.Length];
            for (int i = 0; i < sectionPaths.Length; i++)
                sectionBefore[i] = service.GetValue(sectionPaths[i]);
        }
        else if (!string.IsNullOrEmpty(singlePath))
        {
            singleBefore = service.GetValue(singlePath);
        }
    }

    public void OnPointerUp(PointerEventData _)
    {
        if (service == null || service.Model == null)
        { ChangeBuffer.EndEdit(); return; }

        int newUnits = Mathf.RoundToInt(slider.value);

        if (sectionPaths != null && sectionPaths.Length > 0)
        {
            for (int i = 0; i < sectionPaths.Length; i++)
            {
                var path = sectionPaths[i];
                var before = sectionBefore != null && i < sectionBefore.Length ? sectionBefore[i] : service.GetValue(path);
                ChangeBuffer.Record(path, before, newUnits);
            }
        }
        else if (!string.IsNullOrEmpty(singlePath))
        {
            ChangeBuffer.Record(singlePath, singleBefore, newUnits);
        }

        // applies changes + recompute once, gives you undo/redo
        ChangeBuffer.EndEdit();
    }

    static float ToFloat(object v)
    {
        if (v is int i) return i;
        if (v is float f) return f;
        return 0f;
    }
}
