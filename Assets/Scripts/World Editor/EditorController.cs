using UnityEngine;
using System.Linq;

public class EditorController : MonoBehaviour
{
    public enum EditorState { CharacterCreator, CenterPark, WorldView }

    [Header("State Roots (assign what you have; leave others null)")]
    [SerializeField] private GameObject characterCreatorRoot;
    [SerializeField] private GameObject centerParkRoot;
    [SerializeField] private GameObject worldViewRoot;

    public WorldSave Save { get; private set; }
    public characterStats Selected { get; private set; }

    public EditorState State { get; private set; } = EditorState.CenterPark;

    public event System.Action<characterStats> OnSelectedChanged;
    public event System.Action<EditorState> OnStateChanged;

    public void Load(WorldSave save)
    {
        Save = save ?? new WorldSave();

        if (Save.mains.Count > 0) Selected = Save.mains[0];
        else if (Save.sides.Count > 0) Selected = Save.sides[0];
        else if (Save.extras.Count > 0) Selected = Save.extras[0];
        else
        {
            Selected = CharacterStatGenerator.Create(characterStats.CharacterType.Main);
            Save.mains.Add(Selected);
        }

        OnSelectedChanged?.Invoke(Selected);
        ApplyState(State);
    }

    public void SetState(EditorState s)
    {
        if (State == s) return;

        bool returningFromCC = (State == EditorState.CharacterCreator && s == EditorState.CenterPark);

        State = s;
        ApplyState(s);

        if (returningFromCC)
        {
            Deselect();
        }
    }

    private void ApplyState(EditorState s)
    {
        if (characterCreatorRoot) characterCreatorRoot.SetActive(s == EditorState.CharacterCreator);
        if (centerParkRoot)       centerParkRoot.SetActive(s == EditorState.CenterPark);
        if (worldViewRoot)        worldViewRoot.SetActive(s == EditorState.WorldView);

        OnStateChanged?.Invoke(s);
    }
    
    public void Select(characterStats s)
    {
        if (s == null) return;
        Selected = s;
        OnSelectedChanged?.Invoke(s);
    }

    public void SelectById(string id)
    {
        if (Save == null || string.IsNullOrEmpty(id)) return;
        var all = Save.mains.Concat(Save.sides).Concat(Save.extras);
        Select(all.FirstOrDefault(x => x != null && x.id == id));
    }

    public void Deselect()
    {
        if (Selected == null) return;
        Selected = null;
        OnSelectedChanged?.Invoke(null);
    }
}
