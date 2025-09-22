// EditorStatsBinder.cs
using UnityEngine;

public sealed class EditorStatsBinder : MonoBehaviour
{
    [SerializeField] private EditorController controller;  // optional, auto-find
    [SerializeField] private StatsService stats;           // optional, auto-find

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<EditorController>(true);
        if (!stats)      stats      = GetComponent<StatsService>() 
                                   ?? FindObjectOfType<StatsService>(true);
    }

    void OnEnable()
    {
        if (controller)
        {
            controller.OnSelectedChanged += HandleSelected;
            controller.OnStateChanged    += HandleStateChanged;
        }

        // If CC is already active, bind immediately.
        BindNow(refreshUI: true);
    }

    void OnDisable()
    {
        if (controller)
        {
            controller.OnSelectedChanged -= HandleSelected;
            controller.OnStateChanged    -= HandleStateChanged;
        }
    }

    void HandleSelected(characterStats s) => BindNow(false);
    void HandleStateChanged(EditorController.EditorState s)
    {
        if (s == EditorController.EditorState.CharacterCreator)
            BindNow(refreshUI: true);
    }

    void BindNow(bool refreshUI)
    {
        if (!controller || !stats) return;
        if (refreshUI) stats.RefreshUIBindings();  // UI just got enabled
        stats.SetTarget(controller.Selected);      // point CC at current selection
    }
}
