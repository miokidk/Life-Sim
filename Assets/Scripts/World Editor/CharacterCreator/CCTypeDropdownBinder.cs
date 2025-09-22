// CCTypeDropdownBinder.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CCTypeDropdownBinder : MonoBehaviour
{
    [SerializeField] private StatsService stats;     // drag your CC StatsService
    [Header("Either is fine")]
    [SerializeField] private TMP_Dropdown tmpDropdown;
    [SerializeField] private Dropdown     uiDropdown;

    bool applying;

    void Awake()
    {
        if (!stats)
        {
    #if UNITY_2023_1_OR_NEWER
            stats = UnityEngine.Object.FindFirstObjectByType<StatsService>(FindObjectsInactive.Include);
    #else
            // Older Unity: FindObjectOfType has the bool overload
            stats = UnityEngine.Object.FindObjectOfType<StatsService>(true);
    #endif
        }
    }


    void OnEnable()
    {
        StatsService.StatsRecomputed += SyncFromModel;   // refresh when CC retargets or values change
        SyncFromModel();
        Wire(true);
    }
    void OnDisable()
    {
        StatsService.StatsRecomputed -= SyncFromModel;
        Wire(false);
    }

    void Wire(bool on)
    {
        if (tmpDropdown)
        {
            tmpDropdown.onValueChanged.RemoveListener(OnChangedTMP);
            if (on) tmpDropdown.onValueChanged.AddListener(OnChangedTMP);
        }
        if (uiDropdown)
        {
            uiDropdown.onValueChanged.RemoveListener(OnChangedUI);
            if (on) uiDropdown.onValueChanged.AddListener(OnChangedUI);
        }
    }

    void SyncFromModel()
    {
        if (stats?.Model == null) return;
        var type = stats.Model.character_type; // enum on root model
        int idx = TypeToIndex(type);

        applying = true;
        if (tmpDropdown) tmpDropdown.SetValueWithoutNotify(idx);
        if (uiDropdown)  uiDropdown.SetValueWithoutNotify(idx);
        applying = false;
    }

    void OnChangedTMP(int i) { if (!applying) Apply(i); }
    void OnChangedUI (int i) { if (!applying) Apply(i); }

    void Apply(int idx)
    {
        if (stats?.Model == null) return;

        var before = stats.Model.character_type;
        var after  = IndexToType(idx);
        if (before == after) return;

        // Batch edit through the existing change buffer (Undo/Redo friendly)
        ChangeBuffer.BeginEdit(stats, "Change Character Type");
        ChangeBuffer.Record("character_type", (int)before, (int)after); // root path
        ChangeBuffer.EndEdit();  // applies via StatsService.SetValue + CommitAndRecompute() :contentReference[oaicite:0]{index=0}

        // Keep save lists consistent with the new type
        SyncSaveLists(stats.Model, after);
    }

    static int TypeToIndex(characterStats.CharacterType t) => t switch
    {
        characterStats.CharacterType.Main  => 0,
        characterStats.CharacterType.Side  => 1,
        characterStats.CharacterType.Extra => 2,
        _ => 1, // treat World as Side in UI
    };
    static characterStats.CharacterType IndexToType(int i) => i switch
    {
        0 => characterStats.CharacterType.Main,
        1 => characterStats.CharacterType.Side,
        2 => characterStats.CharacterType.Extra,
        _ => characterStats.CharacterType.Side,
    };

    static void SyncSaveLists(characterStats ch, characterStats.CharacterType newType)
    {
        var save = Game.Instance?.CurrentSave;
        if (save == null || ch == null) return;

        // remove from all
        save.mains?.Remove(ch);
        save.sides?.Remove(ch);
        save.extras?.Remove(ch);

        // add to correct
        switch (newType)
        {
            case characterStats.CharacterType.Main:  if (!save.mains.Contains(ch))  save.mains.Add(ch);  break;
            case characterStats.CharacterType.Side:  if (!save.sides.Contains(ch))  save.sides.Add(ch);  break;
            case characterStats.CharacterType.Extra: if (!save.extras.Contains(ch)) save.extras.Add(ch); break;
        }
    }
}
