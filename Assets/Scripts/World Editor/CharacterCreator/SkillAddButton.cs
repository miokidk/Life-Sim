using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class SkillAddButton : MonoBehaviour
{
    [Tooltip("Skill id/name exactly as used in your model (e.g., \"Drawing\", \"Sprinting\", \"Brush Teeth\").")]
    public string skillId;

    [Tooltip("Optional label override; otherwise uses the button's text minus 'Add '.")]
    public string displayLabelOverride;

    Button btn;
    TMP_Text btnText;
    SkillsAdderUI owner;

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayLabelOverride)) return displayLabelOverride;
            var t = btnText ? btnText.text : skillId;
            return t.StartsWith("Add ") ? t.Substring(4) : t;
        }
    }

    public void Initialize(SkillsAdderUI o)
    {
        owner = o;
        btn = GetComponent<Button>();
        btnText = GetComponentInChildren<TMP_Text>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => owner.TryAddSkill(skillId, DisplayName));
    }
}
