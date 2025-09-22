using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TMP_InputField))]
public sealed class InputStatBinding : MonoBehaviour, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    public StatsService service;     // assign in scene
    public string path;              // e.g., "profile.first_name" or "profile.last_name"

    [Header("Optional")]
    public bool trimWhitespace = true;
    public bool titleCase = false;           // e.g., "khalid" -> "Khalid"
    public bool restrictToNameChars = true;  // keep letters, spaces, hyphen, apostrophe, dot

    TMP_InputField field;
    string beforeText;

    void Awake()
    {
        field = GetComponent<TMP_InputField>();
        StatsService.StatsRecomputed += PullFromModel;
    }
    void OnDestroy() => StatsService.StatsRecomputed -= PullFromModel;
    void Start() => PullFromModel();

    public void PullFromModel()
    {
        if (service == null || service.Model == null || string.IsNullOrEmpty(path))
        { if (field) field.interactable = false; return; }

        field.interactable = true;

        // don't clobber while user is editing
        if (field.isFocused) return;

        var v = service.GetValue(path) as string ?? string.Empty;
        field.SetTextWithoutNotify(v);
    }

    public void OnSelect(BaseEventData _)
    {
        if (service == null || service.Model == null) return;
        beforeText = (service.GetValue(path) as string) ?? string.Empty;
        ChangeBuffer.BeginEdit(service, path);
    }

    public void OnDeselect(BaseEventData _) => CommitIfChanged();
    public void OnSubmit(BaseEventData _)   => CommitIfChanged();

    void CommitIfChanged()
    {
        if (service == null || service.Model == null) { ChangeBuffer.EndEdit(); return; }

        string after = Sanitize(field.text);
        field.SetTextWithoutNotify(after);

        if (after != beforeText)
            ChangeBuffer.Record(path, beforeText, after);

        ChangeBuffer.EndEdit();

        if (path == "profile.first_name" || path == "profile.last_name")
            service.Model.RefreshHierarchyName();
    }


    string Sanitize(string s)
    {
        if (s == null) return string.Empty;
        if (trimWhitespace) s = s.Trim();

        if (restrictToNameChars)
        {
            System.Text.StringBuilder sb = new();
            foreach (char c in s)
            {
                if (char.IsLetter(c) || c == ' ' || c == '-' || c == '\'' || c == '.')
                    sb.Append(c);
            }
            s = sb.ToString();
            // collapse multiple spaces
            while (s.Contains("  ")) s = s.Replace("  ", " ");
        }

        if (titleCase)
        {
            // lower first, then TitleCase (handles "mary ann" -> "Mary Ann")
            var ti = CultureInfo.CurrentCulture.TextInfo;
            s = ti.ToTitleCase(s.ToLower());
        }

        return s;
    }
}
