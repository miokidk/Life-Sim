using UnityEngine;
using TMPro;
using UnityEngine.UI;

public sealed class GenderSexBinding : MonoBehaviour
{
    [Header("Refs")]
    public StatsService service;            // assign in scene
    public TMP_Dropdown genderDropdown;     // options order MUST match enum: Man, Woman, NonBinary
    public TMP_Dropdown sexDropdown;        // options order MUST match enum: Male, Female, Intersex
    public Toggle linkToggle;               // optional; acts as the "lock" switch

    [Header("Mapping knobs")]
    public bool mapNonBinaryToIntersex = false; // if true: NonBinary -> Intersex on link
    public characterStats.SexType nonBinaryMapsTo = characterStats.SexType.Intersex;
    public bool mapIntersexToNonBinary = true;  // if true: Intersex -> NonBinary on link

    bool suppress; // prevent feedback loops

    void Awake()  { StatsService.StatsRecomputed += PullFromModel; }
    void OnDestroy(){ StatsService.StatsRecomputed -= PullFromModel; }

    void Start()
    {
        // wire UI
        if (genderDropdown) genderDropdown.onValueChanged.AddListener(OnGenderChangedUI);
        if (sexDropdown)    sexDropdown.onValueChanged.AddListener(OnSexChangedUI);
        if (linkToggle)     linkToggle.onValueChanged.AddListener(OnLinkToggled);

        PullFromModel();
    }

    void PullFromModel()
    {
        if (service?.Model == null || suppress) return;

        suppress = true;
        var g = (characterStats.GenderType)(service.GetValue("profile.gender") ?? characterStats.GenderType.Man);
        var s = (characterStats.SexType)(service.GetValue("profile.sex") ?? characterStats.SexType.Male);
        if (genderDropdown) genderDropdown.value = (int)g;
        if (sexDropdown)    sexDropdown.value    = (int)s;
        suppress = false;
    }

    void OnLinkToggled(bool on)
    {
        if (!on || service?.Model == null) return;

        // When turning the lock ON, align Sex to current Gender once
        var g = (characterStats.GenderType)service.GetValue("profile.gender");
        var sBefore = (characterStats.SexType)service.GetValue("profile.sex");
        var sAfter  = TypicalSexFor(g, sBefore);
        if (sAfter == sBefore) return;

        suppress = true; if (sexDropdown) sexDropdown.value = (int)sAfter; suppress = false;

        ChangeBuffer.BeginEdit(service, "Link gender→sex");
        ChangeBuffer.Record("profile.sex", sBefore, sAfter);
        ChangeBuffer.EndEdit();
    }

    void OnGenderChangedUI(int uiIndex)
    {
        if (suppress || service?.Model == null) return;

        var gBefore = (characterStats.GenderType)service.GetValue("profile.gender");
        var gAfter  = (characterStats.GenderType)uiIndex;

        var sBefore = (characterStats.SexType)service.GetValue("profile.sex");
        var sAfter  = sBefore;

        if (linkToggle && linkToggle.isOn)
        {
            sAfter = TypicalSexFor(gAfter, sBefore);
            if (sexDropdown && (int)sAfter != sexDropdown.value)
            { suppress = true; sexDropdown.value = (int)sAfter; suppress = false; }
        }

        ChangeBuffer.BeginEdit(service, linkToggle && linkToggle.isOn ? "Set gender (linked)" : "Set gender");
        if (gAfter != gBefore) ChangeBuffer.Record("profile.gender", gBefore, gAfter);
        if (linkToggle && linkToggle.isOn && sAfter != sBefore) ChangeBuffer.Record("profile.sex", sBefore, sAfter);
        ChangeBuffer.EndEdit();
    }

    void OnSexChangedUI(int uiIndex)
    {
        if (suppress || service?.Model == null) return;

        var sBefore = (characterStats.SexType)service.GetValue("profile.sex");
        var sAfter  = (characterStats.SexType)uiIndex;

        var gBefore = (characterStats.GenderType)service.GetValue("profile.gender");
        var gAfter  = gBefore;

        if (linkToggle && linkToggle.isOn)
        {
            gAfter = TypicalGenderFor(sAfter, gBefore);
            if (genderDropdown && (int)gAfter != genderDropdown.value)
            { suppress = true; genderDropdown.value = (int)gAfter; suppress = false; }
        }

        ChangeBuffer.BeginEdit(service, linkToggle && linkToggle.isOn ? "Set sex (linked)" : "Set sex");
        if (sAfter != sBefore) ChangeBuffer.Record("profile.sex", sBefore, sAfter);
        if (linkToggle && linkToggle.isOn && gAfter != gBefore) ChangeBuffer.Record("profile.gender", gBefore, gAfter);
        ChangeBuffer.EndEdit();
    }

    // --- helpers -------------------------------------------------------------

    characterStats.SexType TypicalSexFor(characterStats.GenderType g, characterStats.SexType fallback)
    {
        switch (g)
        {
            case characterStats.GenderType.Man:   return characterStats.SexType.Male;
            case characterStats.GenderType.Woman: return characterStats.SexType.Female;
            case characterStats.GenderType.NonBinary:
                return mapNonBinaryToIntersex ? nonBinaryMapsTo : fallback;
            default: return fallback;
        }
    }

    characterStats.GenderType TypicalGenderFor(characterStats.SexType s, characterStats.GenderType fallback)
    {
        switch (s)
        {
            case characterStats.SexType.Male:     return characterStats.GenderType.Man;
            case characterStats.SexType.Female:   return characterStats.GenderType.Woman;
            case characterStats.SexType.Intersex: return mapIntersexToNonBinary ? characterStats.GenderType.NonBinary : fallback;
            default: return fallback;
        }
    }

    // Optional: one-shot button hook if you prefer a simple button instead of a Toggle
    public void SetSexToTypicalForCurrentGender()
    {
        var g = (characterStats.GenderType)service.GetValue("profile.gender");
        var sBefore = (characterStats.SexType)service.GetValue("profile.sex");
        var sAfter  = TypicalSexFor(g, sBefore);
        if (sAfter == sBefore) return;

        suppress = true; if (sexDropdown) sexDropdown.value = (int)sAfter; suppress = false;

        ChangeBuffer.BeginEdit(service, "Align sex to gender");
        ChangeBuffer.Record("profile.sex", sBefore, sAfter);
        ChangeBuffer.EndEdit();
    }
}
