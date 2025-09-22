using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed class UndoRedoController : MonoBehaviour
{
    [Header("Optional UI")]
    public Button undoButton;
    public Button redoButton;
    public TMP_Text undoLabel;   // shows last action label (optional)
    public TMP_Text redoLabel;

    void Start()
    {
        if (undoButton) undoButton.onClick.AddListener(() => ChangeBuffer.Undo());
        if (redoButton) redoButton.onClick.AddListener(() => ChangeBuffer.Redo());
    }

    void Update()
    {
        // Don’t trigger while typing in an input field
        var go = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
        if (go && (go.GetComponent<TMP_InputField>() || go.GetComponent<UnityEngine.UI.InputField>()))
            return;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                 || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);

        if (!ctrl) return;

        // Undo: Ctrl/Cmd+Z  |  Redo: Ctrl+Y or Shift+Ctrl/Cmd+Z
        if (Input.GetKeyDown(KeyCode.Z) && !(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            ChangeBuffer.Undo();
        else if (Input.GetKeyDown(KeyCode.Y) || (Input.GetKeyDown(KeyCode.Z) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))))
            ChangeBuffer.Redo();
    }
}
