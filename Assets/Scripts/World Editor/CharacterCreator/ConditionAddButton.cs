using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ConditionAddButton : MonoBehaviour
{
    [Tooltip("Field name inside NegativeFeelings_Health (e.g., acne, tinnitus, gingivitis, ibs, anxious, depressed, bipolar, socially_anxious, insomniac, gender_dysphoric, narcissistic).")]
    public string healthFieldName;

    [Tooltip("Optional label override; otherwise uses button text minus 'Add '.")]
    public string displayLabelOverride;

    Button btn;
    ConditionsUI owner;
    TMP_Text btnText;

    public string FullPath => ConditionsUI.HealthPrefix + healthFieldName;
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayLabelOverride)) return displayLabelOverride;
            var t = btnText ? btnText.text : healthFieldName;
            return t.StartsWith("Add ") ? t.Substring(4) : t;
        }
    }

    public void Initialize(ConditionsUI o)
    {
        owner = o;
        btn = GetComponent<Button>();
        btnText = GetComponentInChildren<TMP_Text>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => owner.TryAddCondition(FullPath, DisplayName));
    }
}
