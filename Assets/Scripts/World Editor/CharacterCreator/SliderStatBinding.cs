using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections.Generic; // For List<T>
using System.Reflection; // For reflection in IsPathValid

[RequireComponent(typeof(Slider))]
public sealed class SliderStatBinding : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // ... (Your existing ShoeSizeMap and GetShoeSizeString method remain unchanged) ...
    private static readonly string[] ShoeSizeMap = new string[] {
        "0C", "0½C", "1C", "1½C", "2C", "2½C", "3C", "3½C", "4C", "4½C", "5C", "5½C", "6C", "6½C", "7C", "7½C", "8C", "8½C", "9C", "9½C", "10C", "10½C", "11C", "11½C", "12C", "12½C", "13C", "13½C", "1Y", "1½Y", "2Y", "2½Y", "3Y", "3½Y", "4Y", "4½Y", "5Y", "5½Y", "6Y", "6½Y", "7Y",
        "7½M", "8M", "8½M", "9M", "9½M", "10M", "10½M", "11M", "11½M", "12M", "12½M", "13M", "13½M", "14M", "14½M", "15M"
    };

    private static string GetShoeSizeString(int index)
    {
        if (index >= 0 && index < ShoeSizeMap.Length)
        {
            string size = ShoeSizeMap[index].Replace("1/2", "½");
            char category = size[^1];
            string number = size.Substring(0, size.Length - 1);
            return $"Size {number} {category}";
        }
        return "N/A";
    }

    [Header("Mirror (optional)")]
    public string[] mirrorPaths;
    public StatsService service;
    public string path;
    public bool isInteger = true;
    public TMP_Text valueText;

    Slider slider;
    object beforeValue;
    // NEW: A temporary list to hold the dynamically resolved paths during a drag operation
    private List<string> resolvedPathsForEdit;


    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.onValueChanged.AddListener(OnSliderChanged);
        StatsService.StatsRecomputed += PullFromModel;
    }
    void OnDestroy() => StatsService.StatsRecomputed -= PullFromModel;
    void Start() => PullFromModel();

    public void PullFromModel()
    {
        if (service == null || service.Model == null || string.IsNullOrEmpty(path))
        {
            if (slider) slider.interactable = false;
            return;
        }
        slider.interactable = true;

        if (path == "profile.height.inches") { ApplyHeightLimitsAndValue(); return; }
        if (path.EndsWith("foot.size")) { ApplyFootSizeLimitsAndValue(); return; }

        var applicablePaths = GetApplicablePaths();
        if (applicablePaths.Count == 0)
        {
            slider.interactable = false;
            return;
        }

        string sourcePath = applicablePaths[0];
        
        ApplyDynamicLimits(sourcePath);
        var v = service.GetValue(sourcePath);
        float f = ToFloat(v);
        slider.SetValueWithoutNotify(f);
        if (valueText) valueText.text = isInteger ? Mathf.RoundToInt(f).ToString() : f.ToString("0.##");
    }

    void ApplyDynamicLimits(string sourcePath = null)
    {
        string pathToUse = sourcePath ?? path;
        if (service == null || service.Model == null) return;
        
        if (pathToUse == "profile.weight.pounds")
        {
            var stats = service.Model;
            StatLimits.GetBMILimits(stats.profile.age.years, stats.profile.sex, out float minBMI, out float maxBMI);

            float heightCm = stats.profile.height.feet * 30.48f + stats.profile.height.inches * 2.54f;
            float heightM = Mathf.Max(0.01f, heightCm / 100f);
            int minW = Mathf.RoundToInt(minBMI * heightM * heightM * 2.20462f);
            int maxW = Mathf.RoundToInt(maxBMI * heightM * heightM * 2.20462f);
            slider.minValue = minW;
            slider.maxValue = Mathf.Max(minW + 1, maxW);
            slider.wholeNumbers = true;
            return;
        }
        
        TryApplyRangeAttributeLimits(pathToUse);
    }

    void ApplyHeightLimitsAndValue()
    {
        var stats = service.Model;
        StatLimits.GetHeightLimits(stats.profile.age.years, stats.profile.sex, out float minCm, out float maxCm);
        int minTotal = Mathf.RoundToInt(minCm / 2.54f);
        int maxTotal = Mathf.RoundToInt(maxCm / 2.54f);

        slider.wholeNumbers = true;
        slider.minValue = minTotal;
        slider.maxValue = Mathf.Max(minTotal + 1, maxTotal);

        int total = stats.profile.height.feet * 12 + stats.profile.height.inches;
        slider.SetValueWithoutNotify(total);
        if (valueText) valueText.text = $"{stats.profile.height.feet}'{stats.profile.height.inches}\"";
    }

    void ApplyFootSizeLimitsAndValue()
    {
        var stats = service.Model;
        StatLimits.GetFootSizeLimits(stats.profile.age.years, stats.profile.sex, out int minVal, out int maxVal);

        slider.wholeNumbers = true;
        slider.minValue = minVal;
        slider.maxValue = Mathf.Max(minVal + 1, maxVal);

        int currentIndex = (int)ToFloat(service.GetValue(path));
        slider.SetValueWithoutNotify(currentIndex);

        if (valueText)
        {
            valueText.text = GetShoeSizeString(currentIndex);
        }
    }


    void TryApplyRangeAttributeLimits(string pathToUse)
    {
        if (service == null || service.Model == null) return;
        object cur = service.Model;
        var parts = pathToUse.Split('.');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (cur == null) return;
            var t = cur.GetType();
            var f = t.GetField(parts[i]); if (f != null) { cur = f.GetValue(cur); continue; }
            var p = t.GetProperty(parts[i]); if (p != null) { cur = p.GetValue(cur); continue; }
            return;
        }
        if (cur == null) return;
        var leafType = cur.GetType();
        var lf = leafType.GetField(parts[^1]);
        var lp = leafType.GetProperty(parts[^1]);

        object[] attrs = lf != null ? lf.GetCustomAttributes(typeof(RangeAttribute), true)
                                    : lp != null ? lp.GetCustomAttributes(typeof(RangeAttribute), true)
                                                : System.Array.Empty<object>();
        if (attrs.Length > 0 && attrs[0] is RangeAttribute r)
        {
            slider.minValue = r.min;
            slider.maxValue = r.max;
            slider.wholeNumbers = isInteger;
        }
    }

    void OnSliderChanged(float v)
    {
        if (path == "profile.height.inches") { int t = Mathf.RoundToInt(v); if (valueText) valueText.text = $"{t / 12}'{t % 12}\""; return; }
        if (path.EndsWith("foot.size")) { int currentIndex = Mathf.RoundToInt(v); if (valueText) { valueText.text = GetShoeSizeString(currentIndex); } return; }
        if (valueText) valueText.text = isInteger ? Mathf.RoundToInt(v).ToString() : v.ToString("0.##");
    }

    public void OnPointerDown(PointerEventData _)
    {
        if (service == null) return;

        resolvedPathsForEdit = GetApplicablePaths();
        if (resolvedPathsForEdit.Count == 0) return;

        beforeValue = service.GetValue(resolvedPathsForEdit[0]);
        
        ChangeBuffer.BeginEdit(service, $"Adjust {path}");
    }

     public void OnPointerUp(PointerEventData _)
    {
        if (service == null)
        {
            ChangeBuffer.EndEdit();
            return;
        }

        // Special handling for the height slider, which represents a composite value.
        if (path == "profile.height.inches")
        {
            int totalInchesAfter = Mathf.RoundToInt(slider.value);
            int feetAfter = totalInchesAfter / 12;
            int inchesAfter = totalInchesAfter % 12;

            // Read the 'before' values directly from the model for a correct undo state.
            int feetBefore = (int)service.GetValue("profile.height.feet");
            int inchesBefore = (int)service.GetValue("profile.height.inches");

            ChangeBuffer.Record("profile.height.feet", feetBefore, feetAfter);
            ChangeBuffer.Record("profile.height.inches", inchesBefore, inchesAfter);
        }
        // Generic handling for all other sliders.
        else
        {
            if (resolvedPathsForEdit == null || resolvedPathsForEdit.Count == 0)
            {
                ChangeBuffer.EndEdit();
                return;
            }
            object after = isInteger ? (object)Mathf.RoundToInt(slider.value) : (object)slider.value;
            foreach (var validPath in resolvedPathsForEdit)
            {
                ChangeBuffer.Record(validPath, beforeValue, after);
            }
        }

        ChangeBuffer.EndEdit();
        resolvedPathsForEdit = null;
    }
    
    static float ToFloat(object v)
    {
        if (v is int i) return i;
        if (v is float f) return f;
        return 0f;
    }
    
    void ApplyLive(float raw)
    {
        if (service == null || service.Model == null) return;

        object val = isInteger ? (object)Mathf.RoundToInt(raw) : (object)raw;

        var applicablePaths = GetApplicablePaths();
        foreach (var p in applicablePaths)
        {
            service.SetValue(p, val);
        }

        if (applicablePaths.Count > 0)
        {
            service.CommitAndRecompute();
            if (valueText) valueText.text = isInteger ? ((int)val).ToString() : ((float)val).ToString("0.##");
        }
    }
    
    private bool IsPathValid(string pathToCheck)
    {
        if (service == null || service.Model == null || string.IsNullOrEmpty(pathToCheck)) return false;

        object current = service.Model;
        foreach (var segment in pathToCheck.Split('.'))
        {
            if (current == null) return false;
            var type = current.GetType();
            
            MemberInfo member = (MemberInfo)type.GetField(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) 
                             ?? type.GetProperty(segment, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (member != null)
            {
                // FIX #2: The variable 'pi' did not exist. It should cast 'member'.
                current = (member is FieldInfo fi) ? fi.GetValue(current) : ((PropertyInfo)member).GetValue(current);
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private List<string> GetApplicablePaths()
    {
        var allPossiblePaths = new List<string>();
        if (!string.IsNullOrEmpty(path))
        {
            allPossiblePaths.Add(path);
        }
        if (mirrorPaths != null)
        {
            allPossiblePaths.AddRange(mirrorPaths);
        }

        var validPaths = new List<string>();
        foreach (var p in allPossiblePaths)
        {
            if (IsPathValid(p))
            {
                validPaths.Add(p);
            }
        }
        return validPaths;
    }
}