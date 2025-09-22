using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using System.Collections.Generic;

public class Game : MonoBehaviour
{

    [Header("Scene Names")]
    [SerializeField] private string editorSceneName = "Editor"; // match your scene file name

    [Header("World Generation")]
    [SerializeField] private Vector2 worldSize = new Vector2(400f, 400f);
    [SerializeField] private Vector2 centerParkSize = new Vector2(5f, 5f);


    public SaveSystem.EditorStateData PendingEditorState { get; private set; }

    private void EnsureLoadingRef() { if (loading == null) loading = FindLoadingUIInScene(); }

    [SerializeField] private LoadingUI loading;
    public static Game Instance { get; private set; }

    // Replace CharacterStats with your actual type name if it’s lowercased (characterStats).
    public WorldSave CurrentSave { get; private set; }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private characterStats SpawnFromRecord(SaveSystem.CharacterRecord r)
    {
        var go = new GameObject($"{r.type} Character");
        go.transform.SetParent(transform);
        var s = go.AddComponent<characterStats>();
        // Fill fields from JSON
        JsonConvert.PopulateObject(r.json, s, SaveSystem.Settings);
        // type/id safety
        if (System.Enum.TryParse(r.type, out characterStats.CharacterType t)) s.character_type = t;
        if (string.IsNullOrEmpty(s.id)) s.id = System.Guid.NewGuid().ToString();
        return s;
    }

    public void StartNewGame(int mainCount = 1, int sideCount = 8, int extraCount = 40)
    {
        StartCoroutine(NewGameRoutine(mainCount, sideCount, extraCount));
    }

    private IEnumerator NewGameRoutine(int mainCount, int sideCount, int extraCount)
    {
        EnsureLoadingRef();
        loading?.Show("Generating world...");

        var save = new WorldSave();
        // MODIFIED: Call the new generator with world/park sizes
        yield return WorldGenerator.GenerateAsync(save, worldSize, centerParkSize, mainCount, sideCount, extraCount,
            p => loading?.SetProgress(0.5f * p));

        CurrentSave = save;

        loading?.SetStatus("Loading Editor...");
        var op = UnityEngine.SceneManagement.SceneManager
            .LoadSceneAsync(editorSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f)
        {
            loading?.SetProgress(0.5f + 0.5f * (op.progress / 0.9f));
            yield return null;
        }
        loading?.SetProgress(1f);
        op.allowSceneActivation = true;
    }


    public void ClearCurrentWorld()
    {
        CurrentSave = null;
        // destroy any generated characters under Game
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Refresh loading UI reference in the new scene
        loading = FindLoadingUIInScene();

        // If we just entered the Editor and we have a world, draw it now
        if (scene.name == editorSceneName && CurrentSave != null)
        {
            StartCoroutine(BindEditorSceneAndDraw(CurrentSave));
        }
    }

    private LoadingUI FindLoadingUIInScene()
    {
        // Finds even if it's inactive
#if UNITY_2023_1_OR_NEWER
        return UnityEngine.Object.FindFirstObjectByType<LoadingUI>(FindObjectsInactive.Include);
#else
        return UnityEngine.Object.FindObjectOfType<LoadingUI>(true);
#endif
    }

    public bool SaveCurrentWorld(string slot = "autosave")
    {
        if (CurrentSave == null)
        {
            Debug.LogWarning("No world to save.");
            return false;
        }

        var data = new SaveSystem.WorldSaveData { saveId = CurrentSave.saveId };

        // --- ADDED: Copy world structure data to the save object ---
        data.layout = CurrentSave.layout;
        data.roadNetwork = CurrentSave.roadNetwork;
        data.lots = CurrentSave.lots;

        foreach (var c in CurrentSave.mains) data.mains.Add(SaveSystem.Capture(c));
        foreach (var c in CurrentSave.sides) data.sides.Add(SaveSystem.Capture(c));
        foreach (var c in CurrentSave.extras) data.extras.Add(SaveSystem.Capture(c));

        var editor = FindObjectOfType<EditorController>(true);
        if (editor != null)
        {
            data.editor.state = editor.State.ToString();
            data.editor.selectedCharacterId = editor.Selected?.id;
        }

        bool changed;
        bool wrote = SaveSystem.WriteIfChanged(CurrentSave.saveId, slot, data, out changed);

        if (!changed) Debug.Log("Save skipped: no changes.");
        else Debug.Log($"Saved world {CurrentSave.saveId} → {SaveSystem.PathFor(CurrentSave.saveId, slot)}");

        return wrote;
    }

    public SaveSystem.EditorStateData ConsumePendingEditorState()
    {
        var tmp = PendingEditorState;
        PendingEditorState = null;
        return tmp;
    }

    public void RegisterCharacter(characterStats s)
    {
        if (s == null) return;
        if (CurrentSave == null) CurrentSave = new WorldSave();   // safety
        if (string.IsNullOrEmpty(s.id)) s.id = System.Guid.NewGuid().ToString();

        // keep them under the DDoL root
        s.transform.SetParent(transform, worldPositionStays: true);

        // de-dupe across all lists first
        CurrentSave.mains.Remove(s);
        CurrentSave.sides.Remove(s);
        CurrentSave.extras.Remove(s);

        switch (s.character_type)
        {
            case characterStats.CharacterType.Main: CurrentSave.mains.Add(s); break;
            case characterStats.CharacterType.Side: CurrentSave.sides.Add(s); break;
            case characterStats.CharacterType.Extra: CurrentSave.extras.Add(s); break;
            default: CurrentSave.extras.Add(s); break;
        }
    }

    public void UnregisterCharacter(characterStats s)
    {
        if (s == null || CurrentSave == null) return;
        CurrentSave.mains.Remove(s);
        CurrentSave.sides.Remove(s);
        CurrentSave.extras.Remove(s);
    }

    public void ResyncCharacterType(characterStats s)
    {
        if (s == null) return;
        RegisterCharacter(s); // removes from old list & re-adds to the right one
    }

    // ------------- SAVE (async, with loader) -------------
    public void SaveCurrentWorldAsync(string slot = "autosave")
    {
        StartCoroutine(SaveCurrentWorldRoutine(slot));
    }

    private IEnumerator SaveCurrentWorldRoutine(string slot)
    {
        if (CurrentSave == null)
        {
            EnsureLoadingRef();
            loading?.Show("Nothing to save");
            loading?.SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.25f);
            loading?.Hide();
            yield break;
        }

        EnsureLoadingRef();
        loading?.Show("Saving…");
        loading?.SetProgress(0.05f);

        var data = new SaveSystem.WorldSaveData { saveId = CurrentSave.saveId };

        // --- ADDED: Copy world structure data to the save object ---
        data.layout = CurrentSave.layout;
        data.roadNetwork = CurrentSave.roadNetwork;
        data.lots = CurrentSave.lots;

        // snapshot characters with progress (0.10 → 0.80)
        int total = (CurrentSave.mains?.Count ?? 0)
                + (CurrentSave.sides?.Count ?? 0)
                + (CurrentSave.extras?.Count ?? 0);
        total = Mathf.Max(1, total);
        int done = 0;

        IEnumerable<characterStats> Seq()
        {
            foreach (var c in CurrentSave.mains) yield return c;
            foreach (var c in CurrentSave.sides) yield return c;
            foreach (var c in CurrentSave.extras) yield return c;
        }

        foreach (var c in Seq())
        {
            // capture (JsonConvert via our SaveSystem resolver)
            var rec = SaveSystem.Capture(c);
            switch (c.character_type)
            {
                case characterStats.CharacterType.Main: data.mains.Add(rec); break;
                case characterStats.CharacterType.Side: data.sides.Add(rec); break;
                default: data.extras.Add(rec); break;
            }

            done++;
            if ((done & 3) == 0) yield return null; // breathe every 4
            loading?.SetProgress(Mathf.Lerp(0.10f, 0.80f, done / (float)total));
        }

        // editor state (0.82)
        loading?.SetStatus("Saving editor state…");
        var editor = FindObjectOfType<EditorController>(true);
        if (editor != null)
        {
            data.editor.state = editor.State.ToString();
            data.editor.selectedCharacterId = editor.Selected?.id;
        }
        loading?.SetProgress(0.82f);
        yield return null;

        // write file (0.82 → 1.0)
        loading?.SetStatus("Writing file…");
        bool changed;
        SaveSystem.WriteIfChanged(CurrentSave.saveId, slot, data, out changed);
        loading?.SetProgress(1f);
        loading?.SetStatus(changed ? "Saved." : "No changes.");
        yield return new WaitForSecondsRealtime(0.25f);
        loading?.Hide();
    }

    // ------------- LOAD (enhanced progress) -------------
    public void LoadWorld(string worldId, string slot = "autosave")
    {
        StartCoroutine(LoadWorldRoutine(worldId, slot));
    }

    private IEnumerator LoadWorldRoutine(string worldId, string slot)
    {
        EnsureLoadingRef();
        loading?.Show("Loading save…");
        loading?.SetProgress(0.05f);

        var data = SaveSystem.Read(worldId, slot);
        if (data == null)
        {
            loading?.SetStatus("Save not found");
            loading?.SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.25f);
            loading?.Hide();
            yield break;
        }

        // clear old world (0.10)
        loading?.SetStatus("Clearing world…");
        ClearCurrentWorld();
        loading?.SetProgress(0.10f);
        yield return null;

        // rebuild characters (0.10 → 0.70)
        loading?.SetStatus("Rebuilding world…");
        var save = new WorldSave { saveId = data.saveId };

        // --- ADDED: Restore world structure from loaded data ---
        // Use null-coalescing for safety with older save files that may not have this data.
        save.layout = data.layout;
        save.roadNetwork = data.roadNetwork ?? new List<RoadSegment>();
        save.lots = data.lots ?? new List<LotData>();

        int total = (data.mains?.Count ?? 0) + (data.sides?.Count ?? 0) + (data.extras?.Count ?? 0);
        total = Mathf.Max(1, total);
        int done = 0;

        characterStats SpawnFromRecord(SaveSystem.CharacterRecord r)
        {
            var go = new GameObject($"{r.type} Character");
            go.transform.SetParent(transform);
            var s = go.AddComponent<characterStats>();
            JsonConvert.PopulateObject(r.json, s, SaveSystem.Settings);
            if (System.Enum.TryParse(r.type, out characterStats.CharacterType t)) s.character_type = t;
            if (string.IsNullOrEmpty(s.id)) s.id = System.Guid.NewGuid().ToString();
            return s;
        }

        foreach (var r in data.mains) { save.mains.Add(SpawnFromRecord(r)); done++; loading?.SetProgress(Mathf.Lerp(0.10f, 0.70f, done / (float)total)); if ((done & 3) == 0) yield return null; }
        foreach (var r in data.sides) { save.sides.Add(SpawnFromRecord(r)); done++; loading?.SetProgress(Mathf.Lerp(0.10f, 0.70f, done / (float)total)); if ((done & 3) == 0) yield return null; }
        foreach (var r in data.extras) { save.extras.Add(SpawnFromRecord(r)); done++; loading?.SetProgress(Mathf.Lerp(0.10f, 0.70f, done / (float)total)); if ((done & 3) == 0) yield return null; }

        CurrentSave = save;
        PendingEditorState = data.editor;

        // load the Editor scene (0.70 → 1.0)
        loading?.SetStatus("Loading Editor…");
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(editorSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        while (!op.isDone)
        {
            // Unity reports up to 0.9 until activation; still gives us a feel
            float p = Mathf.Clamp01(op.progress / 0.9f);
            loading?.SetProgress(Mathf.Lerp(0.70f, 1.00f, p));
            yield return null;
        }
        // no Hide() — MainMenu’s LoadingPanel is destroyed; Editor has its own and starts hidden
    }
    
    // Draws the world after the Editor scene is loaded.
    private IEnumerator BindEditorSceneAndDraw(WorldSave save)
    {
        if (save == null) yield break;

        // wait one frame so all scene objects are present/enabled
        yield return null;

        // Hand the save to the Editor
        var editor = FindObjectOfType<EditorController>(true);
        if (editor != null) editor.Load(save);

        // Find the visualizer and draw the layout in small batches
    #if UNITY_2023_1_OR_NEWER
        var vis = UnityEngine.Object.FindFirstObjectByType<WorldVisualizer>(FindObjectsInactive.Include);
    #else
        var vis = UnityEngine.Object.FindObjectOfType<WorldVisualizer>(true);
    #endif
        if (vis != null)
        {
            vis.SetLiveSave(save);

            System.Action<float> prog = p => loading?.SetProgress(Mathf.Lerp(0.80f, 0.98f, Mathf.Clamp01(p)));
            System.Action<string> stat = s => loading?.SetStatus(s);

            yield return vis.VisualizeWorldLayoutAsync(save, prog, stat);
        }
    }

}