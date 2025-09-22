using System.Linq;
using UnityEngine;
using TMPro;

public sealed class SkinColorBinding : MonoBehaviour
{
    public StatsService service;                 // assign
    public TMP_Dropdown dropdown;                // this dropdown
    public string path = "profile.skin_color";   // matches your stats

    [Tooltip("Map UI index → enum int if your dropdown order differs from the enum.")]
    public int[] optionToEnum = System.Array.Empty<int>();

    bool suppress;

    void Awake()  { StatsService.StatsRecomputed += PullFromModel; }
    void OnDestroy(){ StatsService.StatsRecomputed -= PullFromModel; }

    void Start()
    {
        if (!dropdown) dropdown = GetComponent<TMP_Dropdown>();
        if (dropdown.options.Count == 0)
            dropdown.AddOptions(System.Enum.GetNames(typeof(characterStats.SkinColor)).ToList());

        dropdown.onValueChanged.AddListener(OnChanged);
        PullFromModel();
    }

    void PullFromModel()
    {
        if (service?.Model == null || dropdown == null || suppress) return;

        var tone = (characterStats.SkinColor)(service.GetValue(path) ?? characterStats.SkinColor.Ivory);
        int enumInt = (int)tone;
        int uiIndex = MapEnumToIndex(enumInt);

        suppress = true;
        dropdown.value = Mathf.Clamp(uiIndex, 0, dropdown.options.Count - 1);
        suppress = false;
    }

    void OnChanged(int uiIndex)
    {
        if (suppress || service?.Model == null) return;

        var before = (characterStats.SkinColor)(service.GetValue(path) ?? characterStats.SkinColor.Ivory);
        var after  = (characterStats.SkinColor)MapIndexToEnum(uiIndex);

        if (!Equals(before, after))
        {
            ChangeBuffer.BeginEdit(service, "Set skin color");
            ChangeBuffer.Record(path, before, after);
            ChangeBuffer.EndEdit();
        }
    }

    int MapIndexToEnum(int index)
    {
        if (optionToEnum != null && optionToEnum.Length > 0)
            return (index >= 0 && index < optionToEnum.Length) ? optionToEnum[index] : optionToEnum[0];
        return index; // assumes UI order == enum order
    }

    int MapEnumToIndex(int enumInt)
    {
        if (optionToEnum != null && optionToEnum.Length > 0)
        {
            for (int i = 0; i < optionToEnum.Length; i++)
                if (optionToEnum[i] == enumInt) return i;
            return 0;
        }
        return enumInt;
    }
}
