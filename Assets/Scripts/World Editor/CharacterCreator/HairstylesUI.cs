using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;


public class HairstylesUI : MonoBehaviour
{
    [Header("Style Panel")]
    [SerializeField] private Button styleButton;
    [SerializeField] private GameObject stylePanel;
    [SerializeField] private bool panelStartsHidden = true;

    bool stylePanelOpen;

    [Header("Data")]
    [SerializeField] private TextAsset hairstylesJson;
    [SerializeField] private StatsService stats;

    [Header("Controls")]
    [SerializeField] private TMP_Dropdown styleDropdown;
    [SerializeField] private TMP_Dropdown configDropdown;
    [SerializeField] private Slider lengthSlider;
    [SerializeField] private TMP_Text lengthValueText;
    [SerializeField] private Button applyButton;

    [Header("Section Labels (shows selected style name)")]
    [SerializeField] private TMP_Text frontStyleLabel;
    [SerializeField] private TMP_Text topStyleLabel;
    [SerializeField] private TMP_Text leftStyleLabel;
    [SerializeField] private TMP_Text rightStyleLabel;
    [SerializeField] private TMP_Text backStyleLabel;

    const int UnitsPerInch = 10;

    // ---------- JSON shapes ----------
    [Serializable] class HairstyleDB { public string[] sections; public Style[] styles; }
    [Serializable] class Style
    {
        public string id;
        public string display_name;
        public string[] configs;

        // optional
        public int[] types; // 1..4
        public LengthProfile[] length_profiles;
    }
    [Serializable] class LengthProfile
    {
        public string config;
        public Sections sections;
    }
    [Serializable] class Sections
    {
        public MinMax front, top, left, right, back;
    }
    [Serializable] class MinMax { public float min, max; }

    // ---------- runtime ----------
    HairstyleDB db;
    Style[] filteredStyles;
    Style currentStyle;

    // map config keywords to actual sections
    static readonly string[] AllSections = { "front", "top", "left", "right", "back" };
    static readonly Dictionary<string, string> PathMap = new()
    {
        { "front", "head.front.hair" },
        { "top", "head.top.hair" },
        { "left", "head.left_side.hair" },
        { "right", "head.right_side.hair" },
        { "back", "head.back.hair" },
    };

    void Awake()
    {
        if (!stats)
        {
    #if UNITY_2023_1_OR_NEWER
            stats = FindFirstObjectByType<StatsService>(FindObjectsInactive.Include);
    #else
            stats = FindObjectOfType<StatsService>(); // pre-2023 Unity
    #endif
            if (!stats) Debug.LogWarning("HairstylesUI: StatsService not found in scene.");
        }
        LoadDB();
    }

    void OnEnable()
    {
        StatsService.StatsRecomputed += RefreshFromModel;
        Wire();
        BuildUI();

        if (panelStartsHidden) SetStylePanelActive(false);
    }
    void OnDisable()
    {
        StatsService.StatsRecomputed -= RefreshFromModel;
        Unwire();
    }

    void Wire()
    {
        Unwire();
        if (styleDropdown)  styleDropdown.onValueChanged.AddListener(_ => OnStyleChanged());
        if (configDropdown) configDropdown.onValueChanged.AddListener(_ => OnConfigChanged());
        if (applyButton)    applyButton.onClick.AddListener(ApplyToCharacter);
        if (styleButton)    styleButton.onClick.AddListener(ToggleStylePanel);   // <— new
    }

    void Unwire()
    {
        if (styleDropdown)  styleDropdown.onValueChanged.RemoveAllListeners();
        if (configDropdown) configDropdown.onValueChanged.RemoveAllListeners();
        if (lengthSlider)   lengthSlider.onValueChanged.RemoveAllListeners();
        if (applyButton)    applyButton.onClick.RemoveAllListeners();
        if (styleButton)    styleButton.onClick.RemoveAllListeners();            // <— new
    }

    void LoadDB()
    {
        if (!hairstylesJson || string.IsNullOrWhiteSpace(hairstylesJson.text))
        {
            // tiny fallback so the UI still works in-editor
            db = new HairstyleDB
            {
                sections = (string[])AllSections.Clone(),
                styles = new[]
                {
                    new Style { id="clean-shave", display_name="Clean Shave", configs=new[]{ "whole_head" } },
                    new Style { id="bob", display_name="Bob", configs=new[]{ "whole_head" } }
                }
            };
            Debug.LogWarning("HairstylesUI: hairstylesJson not assigned; using fallback.");
            return;
        }
        db = JsonUtility.FromJson<HairstyleDB>(hairstylesJson.text);
        if (db == null || db.styles == null || db.styles.Length == 0)
            Debug.LogError("HairstylesUI: failed to parse hairstyles JSON (is it wrapped with { sections, styles }?).");
        if (db.sections == null || db.sections.Length == 0) db.sections = (string[])AllSections.Clone();
    }


    void BuildUI()
    {
        RebuildStyleDropdown(false);
        RefreshFromModel(); // sets slider to avg + syncs labels
    }


    int GetHairFamily()
    {
        // default to wide acceptance if there’s no model yet
        var type = (int?)null;
        if (stats && stats.Model != null)
        {
            // profile.hair_type is an enum (Type1A..Type4C). We reduce it to 1..4 family.
            var enumVal = (int)stats.Model.profile.hair_type;
            // families are 1:Type1*, 2:Type2*, 3:Type3*, 4:Type4*
            type = Mathf.Clamp(1 + enumVal / 3, 1, 4);
        }
        return type ?? 0; // 0 = accept all
    }

    static bool MatchesHairType(Style s, int hairFamily) =>
        s.types == null || s.types.Length == 0 || hairFamily == 0 || s.types.Contains(hairFamily);

    void BuildConfigOptionsAndRange()
    {
        if (currentStyle == null)
        {
            configDropdown.options = new List<TMP_Dropdown.OptionData>();
            return;
        }

        var cfgs = ExpandConfigs(currentStyle);
        configDropdown.options = cfgs.Select(c => new TMP_Dropdown.OptionData(c)).ToList();
        configDropdown.value = 0;
        configDropdown.RefreshShownValue();

        ApplyRangeForCurrentSelection();
    }

    List<string> ExpandConfigs(Style s)
    {
        var list = new List<string>();
        foreach (var c in s.configs ?? Array.Empty<string>())
        {
            var key = c.Trim().ToLowerInvariant();
            if (key == "whole_head")
            {
                list.Add("whole_head");
            }
            else if (key == "section_independent")
            {
                list.AddRange(AllSections);
            }
            else
            {
                // explicit comma list like "front, top, back"
                var parts = key.Split(',').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                if (parts.Length > 0) list.Add(string.Join(", ", parts));
            }
        }
        if (list.Count == 0) list.Add("whole_head");
        return list;
    }

    void OnStyleChanged()
    {
        currentStyle = (styleDropdown.value >= 0 && styleDropdown.value < filteredStyles.Length)
            ? filteredStyles[styleDropdown.value]
            : null;
        BuildConfigOptionsAndRange();
        SetLabelsFromModel(); // keep labels showing what's actually applied
    }

    void OnConfigChanged()
    {
        ApplyRangeForCurrentSelection();
    }

    void ApplyRangeForCurrentSelection()
    {
        lengthSlider.wholeNumbers = true;
        lengthSlider.minValue = 0f;
        lengthSlider.maxValue = 500f;
        lengthSlider.SetValueWithoutNotify(
            Mathf.Clamp(lengthSlider.value, lengthSlider.minValue, lengthSlider.maxValue)
        );
        UpdateLengthReadout(lengthSlider.value);
    }

    string SelectedConfigKey()
    {
        if (configDropdown.options == null || configDropdown.options.Count == 0) return "whole_head";
        var label = configDropdown.options[configDropdown.value].text.Trim().ToLowerInvariant();
        return label switch
        {
            "front" or "top" or "left" or "right" or "back" => label,
            "whole_head" => "whole_head",
            _ => label // already comma list "front, top, back"
        };
    }

    IEnumerable<string> SectionsFor(string cfgKey)
    {
        if (cfgKey == "whole_head") return AllSections;
        if (AllSections.Contains(cfgKey)) return new[] { cfgKey }; // section_independent choice
        return cfgKey.Split(',').Select(p => p.Trim()).Where(p => AllSections.Contains(p));
    }

    void UpdateLengthReadout(float v)
     {
         if (!lengthValueText) return;
         float inches = Mathf.Round(v) / UnitsPerInch;  // 10 units = 1 inch
         lengthValueText.text = $"{inches:0.0}\"";
     }
    void OnLengthSliderChanged(float v)
    {
        UpdateLengthReadout(v); // no stats.SetValue / no CommitAndRecompute
    }


    void UpdatePreviewLabels(string styleName)
    {
        // Previews show what will be applied based on the config
        var targets = SectionsFor(SelectedConfigKey());
        SetLabel(frontStyleLabel, "front", styleName, targets);
        SetLabel(topStyleLabel,   "top",   styleName, targets);
        SetLabel(leftStyleLabel,  "left",  styleName, targets);
        SetLabel(rightStyleLabel, "right", styleName, targets);
        SetLabel(backStyleLabel,  "back",  styleName, targets);
    }

    static void SetLabel(TMP_Text label, string section, string styleName, IEnumerable<string> applyTo)
    {
        if (!label) return;
        bool affected = applyTo.Contains(section);
        label.text = affected ? (styleName ?? "") : "Style";
    }

    void RefreshFromModel()
    {
        RebuildStyleDropdown(true);
        SetLabelsFromModel();
    }



    void ApplyToCharacter()
    {
        var styleId   = currentStyle?.id ?? "";
        int lengthU   = Mathf.RoundToInt(lengthSlider.value);
        var targets   = SectionsFor(SelectedConfigKey());

        foreach (var s in targets)
        {
            var basePath = PathMap[s];
            stats.SetValue(basePath + ".style", styleId);
        }

        stats.CommitAndRecompute();
        SetLabelsFromModel(); // now labels reflect the newly applied style
    }

    // helpers
    static bool StringEquals(string a, string b) =>
        string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    void SetLabelsFromModel()
    {
        if (stats == null || stats.Model == null) return;

        string NameFor(string styleId)
        {
            if (string.IsNullOrWhiteSpace(styleId)) return "Style";
            // prefer filtered (hair-type) list, then whole DB
            var s = (filteredStyles ?? Array.Empty<Style>()).FirstOrDefault(x => x.id == styleId)
                    ?? (db?.styles ?? Array.Empty<Style>()).FirstOrDefault(x => x.id == styleId);
            return (s?.display_name ?? styleId);
        }

        string GetStyleAt(string sectionKey)
        {
            var obj = stats.GetValue(PathMap[sectionKey] + ".style");
            var id = obj as string;
            if (!string.IsNullOrWhiteSpace(id)) return id;

            // Fallback: majority of non-empty styles on other sections
            var majority = AllSections
                .Where(s => s != sectionKey)
                .Select(s => stats.GetValue(PathMap[s] + ".style") as string)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key;

            return majority ?? "";
        }

        if (frontStyleLabel) frontStyleLabel.text = NameFor(GetStyleAt("front"));
        if (topStyleLabel)   topStyleLabel.text   = NameFor(GetStyleAt("top"));
        if (leftStyleLabel)  leftStyleLabel.text  = NameFor(GetStyleAt("left"));
        if (rightStyleLabel) rightStyleLabel.text = NameFor(GetStyleAt("right"));
        if (backStyleLabel)  backStyleLabel.text  = NameFor(GetStyleAt("back"));
    }

    int GetAverageHairLengthUnits()
    {
        if (stats == null || stats.Model == null) return 0;
        int sum = 0, count = 0;
        foreach (var sec in AllSections)
        {
            var obj = stats.GetValue(PathMap[sec] + ".amount");
            if (obj is int u)
            {
                sum += Mathf.Clamp(u, 0, 500);
                count++;
            }
        }
        return count > 0 ? Mathf.RoundToInt(sum / (float)count) : 0;
    }

    int GetSectionLengthUnits(string sectionKey)
    {
        var obj = stats?.GetValue(PathMap[sectionKey] + ".amount");
        return (obj is int u) ? Mathf.Clamp(u, 0, 500) : 0;
    }
    static bool InRange(int len, MinMax mm) => mm == null || (len >= mm.min && len <= mm.max);

    bool FitsCurrentLengths(Style s)
    {
        // No length profiles? accept (type filter still applies)
        if (s.length_profiles == null || s.length_profiles.Length == 0) return true;

        // Snapshot current lengths
        var L = new Dictionary<string, int>
        {
            ["front"] = GetSectionLengthUnits("front"),
            ["top"]   = GetSectionLengthUnits("top"),
            ["left"]  = GetSectionLengthUnits("left"),
            ["right"] = GetSectionLengthUnits("right"),
            ["back"]  = GetSectionLengthUnits("back"),
        };

        foreach (var lp in s.length_profiles)
        {
            var cfg = (lp.config ?? "whole_head").Trim().ToLowerInvariant();
            // Which sections does this profile speak to?
            IEnumerable<string> sections;
            bool sectionIndependent = false;

            if (cfg == "whole_head") sections = AllSections;
            else if (cfg == "section_independent") { sections = AllSections; sectionIndependent = true; }
            else if (AllSections.Contains(cfg)) sections = new[] { cfg }; // single section
            else sections = cfg.Split(',').Select(p => p.Trim()).Where(AllSections.Contains);

            // Map to the mm ranges in the profile
            MinMax For(string sec) => sec switch
            {
                "front" => lp.sections.front,
                "top"   => lp.sections.top,
                "left"  => lp.sections.left,
                "right" => lp.sections.right,
                "back"  => lp.sections.back,
                _       => null
            };

            bool ok = sectionIndependent
                ? sections.Any(sec => InRange(L[sec], For(sec)))              // any one section fits
                : sections.All(sec => InRange(L[sec], For(sec)));             // all target sections fit

            if (ok) return true; // this style is viable for at least one config/profile
        }
        return false;
    }

    void RebuildStyleDropdown(bool preserveSelection)
    {
        var hairType = GetHairFamily();
        var keepId = preserveSelection ? currentStyle?.id : null;

        filteredStyles = (db?.styles ?? Array.Empty<Style>())
            .Where(s => MatchesHairType(s, hairType) && FitsCurrentLengths(s))
            .OrderBy(s => s.display_name ?? s.id)
            .ToArray();

        var options = filteredStyles.Select(s => new TMP_Dropdown.OptionData(s.display_name ?? s.id)).ToList();
        styleDropdown.options = options;

        int newIndex = 0;
        if (!string.IsNullOrEmpty(keepId))
        {
            int idx = Array.FindIndex(filteredStyles, s => s.id == keepId);
            if (idx >= 0) newIndex = idx;
        }
        styleDropdown.value = options.Count > 0 ? Mathf.Clamp(newIndex, 0, options.Count - 1) : 0;
        styleDropdown.RefreshShownValue();

        currentStyle = (styleDropdown.value >= 0 && styleDropdown.value < filteredStyles.Length)
            ? filteredStyles[styleDropdown.value]
            : null;

        BuildConfigOptionsAndRange();
    }

    void ToggleStylePanel() => SetStylePanelActive(!stylePanelOpen);

    void SetStylePanelActive(bool active)
    {
        stylePanelOpen = active;
        if (stylePanel) stylePanel.SetActive(active);
    }

    void Update()
    {
        if (!stylePanelOpen) return;

        bool pointerDown = Input.GetMouseButtonDown(0) ||
                        (Input.touchCount > 0 && Input.touches[0].phase == TouchPhase.Began);
        if (!pointerDown) return;

        if (EventSystem.current == null) { SetStylePanelActive(false); return; }

        Vector2 pos = Input.touchCount > 0 ? (Vector2)Input.touches[0].position
                                        : (Vector2)Input.mousePosition;

        if (!PointerOverStyleUI(pos)) SetStylePanelActive(false);
    }

    bool PointerOverStyleUI(Vector2 screenPos)
    {
        var pe = new PointerEventData(EventSystem.current) { position = screenPos };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pe, hits);

        foreach (var h in hits)
        {
            var t = h.gameObject.transform;
            // inside panel or button?
            if ((stylePanel && (t == stylePanel.transform || t.IsChildOf(stylePanel.transform))) ||
                (styleButton && (t == styleButton.transform || t.IsChildOf(styleButton.transform))))
                return true;

            // TMP_Dropdown popups spawned outside the panel still count
            var dd = t.GetComponentInParent<TMP_Dropdown>();
            if (dd && stylePanel && dd.transform.IsChildOf(stylePanel.transform))
                return true;
        }
        return false;
    }


}
