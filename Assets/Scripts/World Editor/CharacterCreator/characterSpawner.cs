using UnityEngine;

public class CharacterSpawner : MonoBehaviour
{
    [SerializeField] characterStats characterPrefab;
    [SerializeField] Transform      spawnPoint;     // optional now
    [SerializeField] StatsService   statsService;   // optional for Center Park flow

    [Header("Defaults")]
    [SerializeField] public characterStats.CharacterType defaultType = characterStats.CharacterType.Side;

    void Awake()
    {
        if (!statsService)
        {
#if UNITY_2023_1_OR_NEWER
            statsService = Object.FindFirstObjectByType<StatsService>(FindObjectsInactive.Include);
#else
            statsService = Object.FindObjectOfType<StatsService>();
#endif
        }
    }

    public characterStats CreateRandomCharacter(characterStats.CharacterType? typeOverride = null)
    {
        var type  = typeOverride ?? defaultType;

        // Create a fresh data object (no scene GameObject)
        var stats = CharacterStatGenerator.Create(type);

        StatLimits.ResetPreviousRanges();
        CharacterStatGenerator.Randomize(stats);

        if (string.IsNullOrEmpty(stats.id))
            stats.id = System.Guid.NewGuid().ToString();

        if (Game.Instance) Game.Instance.RegisterCharacter(stats);
        return stats;
    }


    /// <summary>Legacy CC flow: spawn + focus CC immediately (kept for convenience).</summary>
    public void SpawnNewCharacter()
    {
        var stats = CreateRandomCharacter(defaultType);

        statsService?.RefreshUIBindings();
        statsService?.SetTarget(stats);

        var editor = FindObjectOfType<EditorController>(true);
        if (editor)
        {
            editor.Select(stats);
            editor.SetState(EditorController.EditorState.CharacterCreator);
        }
    }
    
}