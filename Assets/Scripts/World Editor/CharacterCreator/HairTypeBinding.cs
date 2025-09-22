using UnityEngine;
using TMPro;
using System.Collections;

public sealed class HairTypeBinding : MonoBehaviour
{
    [Header("Wiring")]
    public StatsService service;                 // assign in scene
    public TMP_Dropdown numberDropdown;          // 1..4
    public TMP_Dropdown letterDropdown;          // A..C
    public string path = "profile.hair_type";    // enum: Type1A..Type4C

    [Header("Optional UI→Value maps (leave empty if UI is 1..4 and A..C)")]
    [Tooltip("Map UI index → hair number (1..4). Example: [1,2,3,4]")]
    public int[] numberOptionToValue = System.Array.Empty<int>();
    [Tooltip("Map UI index → letter index (A=0,B=1,C=2). Example: [0,1,2]")]
    public int[] letterOptionToIndex = System.Array.Empty<int>();

    bool suppress;

    // --- Lifecycle ----------------------------------------------------------

    void OnEnable()
    {
        EnsureOptionsExist();
        WireListeners(true);
        StatsService.StatsRecomputed += PullFromModel;

        // Initial sync + make sure captions render even if layout hasn't settled yet
        PullFromModel();
        UpdateLetterUiForNumber(MapUiIndexToNumberValue(numberDropdown ? numberDropdown.value : 0));
        RefreshBothShownValues();
        StartCoroutine(CoRefreshNextFrame());
    }

    void OnDisable()
    {
        StatsService.StatsRecomputed -= PullFromModel;
        WireListeners(false);
    }

    // --- Init helpers -------------------------------------------------------

    void EnsureOptionsExist()
    {
        if (numberDropdown && numberDropdown.options.Count == 0)
        {
            numberDropdown.options.Clear();
            numberDropdown.options.Add(new TMP_Dropdown.OptionData("1"));
            numberDropdown.options.Add(new TMP_Dropdown.OptionData("2"));
            numberDropdown.options.Add(new TMP_Dropdown.OptionData("3"));
            numberDropdown.options.Add(new TMP_Dropdown.OptionData("4"));
            numberDropdown.RefreshShownValue();
        }
        if (letterDropdown && letterDropdown.options.Count == 0)
        {
            letterDropdown.options.Clear();
            letterDropdown.options.Add(new TMP_Dropdown.OptionData("A"));
            letterDropdown.options.Add(new TMP_Dropdown.OptionData("B"));
            letterDropdown.options.Add(new TMP_Dropdown.OptionData("C"));
            letterDropdown.RefreshShownValue();
        }
    }

    void WireListeners(bool add)
    {
        if (numberDropdown)
        {
            numberDropdown.onValueChanged.RemoveListener(OnNumberChanged);
            if (add) numberDropdown.onValueChanged.AddListener(OnNumberChanged);
        }
        if (letterDropdown)
        {
            letterDropdown.onValueChanged.RemoveListener(OnLetterChanged);
            if (add) letterDropdown.onValueChanged.AddListener(OnLetterChanged);
        }
    }

    IEnumerator CoRefreshNextFrame()
    {
        yield return null; // let TMP/Canvas layout settle
        RefreshBothShownValues();
    }

    void RefreshBothShownValues()
    {
        if (numberDropdown) numberDropdown.RefreshShownValue();
        if (letterDropdown) letterDropdown.RefreshShownValue();
    }

    // --- Model → UI ---------------------------------------------------------

    void PullFromModel()
    {
        if (suppress || service?.Model == null) return;

        var ht = (characterStats.HairType)(service.GetValue(path) ?? characterStats.HairType.Type1A);

        // Enum order: 1A,1B,1C, 2A,2B,2C, 3A,3B,3C, 4A,4B,4C
        int enumIndex = (int)ht;
        int numberVal = (enumIndex / 3) + 1;   // 1..4
        int letterIdx = enumIndex % 3;         // 0..2

        // MIGRATION: collapse 1B/1C -> 1A
        if (numberVal == 1 && letterIdx != 0)
        {
            var before = ht;
            var after  = characterStats.HairType.Type1A;
            ChangeBuffer.BeginEdit(service, "Migrate hair type 1B/1C → 1");
            ChangeBuffer.Record(path, before, after);
            ChangeBuffer.EndEdit();
            ht = after;
            enumIndex = (int)ht;
            numberVal = 1;
            letterIdx = 0;
        }

        int uiNumIndex = MapNumberValueToUiIndex(numberVal);
        int uiLetIndex = MapLetterIndexToUiIndex(letterIdx);

        suppress = true;
        if (numberDropdown)
        {
            numberDropdown.value = Mathf.Clamp(uiNumIndex, 0, Mathf.Max(0, numberDropdown.options.Count - 1));
            numberDropdown.RefreshShownValue(); // <- important
        }
        if (letterDropdown)
        {
            letterDropdown.value = Mathf.Clamp(uiLetIndex, 0, Mathf.Max(0, letterDropdown.options.Count - 1));
            letterDropdown.RefreshShownValue(); // <- important
        }
        suppress = false;

        UpdateLetterUiForNumber(numberVal);
    }

    // --- UI → Model ---------------------------------------------------------

    void OnNumberChanged(int uiIndex)
    {
        if (suppress || service?.Model == null) return;
        int num = MapUiIndexToNumberValue(uiIndex);
        UpdateLetterUiForNumber(num);
        ApplyCombinedChange();
    }

    void OnLetterChanged(int uiIndex)
    {
        if (suppress || service?.Model == null) return;
        ApplyCombinedChange();
    }

    void ApplyCombinedChange()
    {
        int num = MapUiIndexToNumberValue(numberDropdown ? numberDropdown.value : 0); // 1..4
        int let = MapUiIndexToLetterIndex(letterDropdown ? letterDropdown.value : 0); // 0..2

        // Force single “1” type by locking letter to A (index 0)
        if (num == 1) { let = 0; SafeSetLetterUiIndex(0); }

        num = Mathf.Clamp(num, 1, 4);
        let = Mathf.Clamp(let, 0, 2);

        int enumIndex = (num - 1) * 3 + let;
        var after  = (characterStats.HairType)enumIndex;
        var before = (characterStats.HairType)(service.GetValue(path) ?? characterStats.HairType.Type1A);

        if (before.Equals(after)) return;

        ChangeBuffer.BeginEdit(service, "Set hair type");
        ChangeBuffer.Record(path, before, after);
        ChangeBuffer.EndEdit(); // triggers CommitAndRecompute + UI refresh

        RefreshBothShownValues(); // keep captions in sync after programmatic changes
    }

    // Hide/lock the letter UI when number == 1
    void UpdateLetterUiForNumber(int numberVal)
    {
        bool showLetter = (numberVal != 1);
        if (letterDropdown)
        {
            if (!showLetter) SafeSetLetterUiIndex(0);
            letterDropdown.interactable = showLetter;

            // Toggle visibility, then refresh caption to avoid blank text after re-enable
            bool currently = letterDropdown.gameObject.activeSelf;
            if (currently != showLetter)
            {
                letterDropdown.gameObject.SetActive(showLetter);
                letterDropdown.RefreshShownValue();
            }
        }
    }

    void SafeSetLetterUiIndex(int uiIndex)
    {
        if (!letterDropdown) return;
        suppress = true;
        letterDropdown.value = Mathf.Clamp(uiIndex, 0, Mathf.Max(0, letterDropdown.options.Count - 1));
        letterDropdown.RefreshShownValue();
        suppress = false;
    }

    // --- Mapping helpers ----------------------------------------------------

    int MapUiIndexToNumberValue(int uiIndex)
    {
        if (numberOptionToValue != null && numberOptionToValue.Length > 0)
        {
            if (uiIndex >= 0 && uiIndex < numberOptionToValue.Length) return numberOptionToValue[uiIndex];
            return numberOptionToValue[0];
        }
        return uiIndex + 1; // default UI order is 1,2,3,4
    }

    int MapUiIndexToLetterIndex(int uiIndex)
    {
        if (letterOptionToIndex != null && letterOptionToIndex.Length > 0)
        {
            if (uiIndex >= 0 && uiIndex < letterOptionToIndex.Length) return letterOptionToIndex[uiIndex];
            return letterOptionToIndex[0];
        }
        return uiIndex; // default UI order is A(0),B(1),C(2)
    }

    int MapNumberValueToUiIndex(int numberVal)
    {
        if (numberOptionToValue != null && numberOptionToValue.Length > 0)
        {
            for (int i = 0; i < numberOptionToValue.Length; i++)
                if (numberOptionToValue[i] == numberVal) return i;
            return 0;
        }
        return Mathf.Clamp(numberVal - 1, 0, (numberDropdown ? numberDropdown.options.Count : 1) - 1);
    }

    int MapLetterIndexToUiIndex(int letterIdx)
    {
        if (letterOptionToIndex != null && letterOptionToIndex.Length > 0)
        {
            for (int i = 0; i < letterOptionToIndex.Length; i++)
                if (letterOptionToIndex[i] == letterIdx) return i;
            return 0;
        }
        return Mathf.Clamp(letterIdx, 0, (letterDropdown ? letterDropdown.options.Count : 1) - 1);
    }
}
