using UnityEngine;

public class EditorBootstrap : MonoBehaviour
{
    [SerializeField] private EditorController editor; // assign in Inspector or auto-find



    private void Start()
    {
        if (editor == null) editor = FindObjectOfType<EditorController>(true);

        var save = Game.Instance != null ? Game.Instance.CurrentSave : null;
        if (save == null)
        {
            Debug.LogWarning("No WorldSave found from Main Menu — making a temp one.");
            save = new WorldSave();
            var c = CharacterStatGenerator.Create(characterStats.CharacterType.Main);
            save.mains.Add(c);
        }

        editor?.Load(save);

        var pending = Game.Instance?.ConsumePendingEditorState();
        if (pending != null)
        {
            if (!string.IsNullOrEmpty(pending.selectedCharacterId))
                editor.SelectById(pending.selectedCharacterId);

            if (System.Enum.TryParse<EditorController.EditorState>(pending.state, out var st))
                editor.SetState(st);
        }
        else
        {
            editor.SetState(EditorController.EditorState.CenterPark); // default
        }



    }
    
}

