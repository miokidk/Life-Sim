using UnityEngine;
using TMPro;
using System;
using System.Linq;

public sealed class HairColorBinding : MonoBehaviour
{
    [Header("Wiring")]
    public StatsService service;      // assign in scene
    public TMP_Dropdown dropdown;     // single dropdown

    [Header("Options")]
    [Tooltip("Allowed colors (lowercase). Leave empty to use default list.")]
    public string[] allowedColors = new[]
    {
        "black", "dark brown", "brown", "light brown", "blonde", "red", "auburn"
    };

    [Tooltip("If your dropdown already has labels, map UI index -> value here (lowercase strings). Leave empty to use allowedColors order.")]
    public string[] optionToValue = Array.Empty<string>();

    bool suppress;

    void Awake()
    {
        StatsService.StatsRecomputed += PullFromModel;
    }

    void OnDestroy()
    {
        StatsService.StatsRecomputed -= PullFromModel;
        if (dropdown) dropdown.onValueChanged.RemoveListener(OnChanged);
    }

    void Start()
    {
        // Auto-populate if dropdown has no options
        if (dropdown && dropdown.options.Count == 0)
        {
            dropdown.options.Clear();
            foreach (var c in allowedColors)
                dropdown.options.Add(new TMP_Dropdown.OptionData(ToTitle(c)));
        }

        if (dropdown) dropdown.onValueChanged.AddListener(OnChanged);
        PullFromModel();
    }

    void PullFromModel()
    {
        if (suppress || service?.Model == null || dropdown == null) return;

        // Try to read any existing scalp color; prefer top.roots, then mids/ends, then others
        string current = ReadAnyHeadHairColor();
        int uiIndex = MapValueToUiIndex((current ?? allowedColors[0]).ToLowerInvariant());

        suppress = true;
        dropdown.value = Mathf.Clamp(uiIndex, 0, dropdown.options.Count - 1);
        dropdown.RefreshShownValue();
        suppress = false;
    }

    void OnChanged(int _)
    {
        if (suppress || service?.Model == null || dropdown == null) return;

        string value = MapUiIndexToValue(dropdown.value);
        if (string.IsNullOrWhiteSpace(value)) return;
        value = value.ToLowerInvariant();

        EnsureHeadHairObjects(); // make sure nested objects exist before writing

        // All scalp segments + all bands
        string[] paths =
        {
            "head.top.hair.color.roots",
            "head.top.hair.color.mids",
            "head.top.hair.color.ends",
            "head.back.hair.color.roots",
            "head.back.hair.color.mids",
            "head.back.hair.color.ends",
            "head.left_side.hair.color.roots",
            "head.left_side.hair.color.mids",
            "head.left_side.hair.color.ends",
            "head.right_side.hair.color.roots",
            "head.right_side.hair.color.mids",
            "head.right_side.hair.color.ends",
        };

        // Batch the change for undo/redo and a single recompute
        ChangeBuffer.BeginEdit(service, "Set hair color");
        foreach (var p in paths)
        {
            var before = service.GetValue(p) as string;
            if (before == value) continue;
            ChangeBuffer.Record(p, before, value);
        }
        ChangeBuffer.EndEdit();
    }

    /* ---------- helpers ---------- */

    string ReadAnyHeadHairColor()
    {
        string[] tryPaths =
        {
            "head.top.hair.color.roots",
            "head.top.hair.color.mids",
            "head.top.hair.color.ends",
            "head.back.hair.color.roots",
            "head.left_side.hair.color.roots",
            "head.right_side.hair.color.roots",
        };
        foreach (var p in tryPaths)
        {
            var v = service.GetValue(p) as string;
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
    }

    void EnsureHeadHairObjects()
    {
        // Ensure each section object exists, and its hair/color objects too.
        EnsureSection("head.top");
        EnsureSection("head.back");
        EnsureSection("head.left_side");
        EnsureSection("head.right_side");

        void EnsureSection(string basePath)
        {
            // head.<section>
            var section = service.GetValue(basePath);
            if (section == null)
            {
                var obj = new characterStats.HeadStats.SkullSectionWithHair();
                service.SetValue(basePath, obj);
                section = obj;
            }

            // head.<section>.hair
            var hairPath = basePath + ".hair";
            var hair = service.GetValue(hairPath);
            if (hair == null)
            {
                var obj = new characterStats.HeadStats.SkullSectionHair();
                service.SetValue(hairPath, obj);
                hair = obj;
            }

            // head.<section>.hair.color
            var colorPath = hairPath + ".color";
            var color = service.GetValue(colorPath);
            if (color == null)
            {
                var obj = new characterStats.HeadStats.SkullSectionHairColor();
                service.SetValue(colorPath, obj);
            }
        }
    }

    string MapUiIndexToValue(int uiIndex)
    {
        if (optionToValue != null && optionToValue.Length > 0)
        {
            if (uiIndex >= 0 && uiIndex < optionToValue.Length) return optionToValue[uiIndex];
            return optionToValue[0];
        }
        if (allowedColors == null || allowedColors.Length == 0)
            allowedColors = new[] { "black", "dark brown", "brown", "light brown", "blonde", "red", "auburn" };
        return allowedColors[Mathf.Clamp(uiIndex, 0, allowedColors.Length - 1)];
    }

    int MapValueToUiIndex(string value)
    {
        if (optionToValue != null && optionToValue.Length > 0)
        {
            for (int i = 0; i < optionToValue.Length; i++)
                if (string.Equals(optionToValue[i], value, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }
        var idx = Array.IndexOf(allowedColors, value);
        if (idx >= 0) return idx;
        // fallback: try case-insensitive
        return Array.FindIndex(allowedColors, c => string.Equals(c, value, StringComparison.OrdinalIgnoreCase)).Clamp01(allowedColors.Length);
    }

    static string ToTitle(string s) =>
        string.Join(" ", s.Split(' ').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));

}

static class IntClampExt
{
    public static int Clamp01(this int _, int max) => Mathf.Clamp(_, 0, Mathf.Max(0, max - 1));
}
