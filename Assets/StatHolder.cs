using UnityEngine;

public sealed class CharacterStatsHolder : MonoBehaviour
{
    public characterStats Stats;
    void Reset() => Stats = GetComponent<characterStats>();
}
