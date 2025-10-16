using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class CenterParkSpawner : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private EditorController controller;
    [SerializeField] private GameObject characterPrefab;
    [SerializeField] private Transform container;
    [Header("Timing")]
    [SerializeField, Range(0f, 5f)] private float spawnDelayOnEnter = 1.5f;
    [SerializeField, Range(0f, 5f)] private float hideDelayOnExit = 0f;
    [SerializeField] private bool useUnscaledTime = true;

    Coroutine stateCo;

    [Header("Spawn Settings")]
    [SerializeField] private float minSpacing = 1.5f;
    [SerializeField] private int maxAttemptsPerSpawn = 25;

    [Header("Indicators")]
    [SerializeField] private GameObject mainIndicatorPrefab;
    [SerializeField] private GameObject sideIndicatorPrefab;
    [SerializeField, Range(0.01f, 2f)] private float indicatorScale = 0.2f;
    [SerializeField] private float indicatorYOffset = 0.02f;


    private readonly Dictionary<string, GameObject> spawned = new();
    private Bounds parkBounds;
    private Vector3 parkCenter;
    private Vector3 parkRight;
    private Vector3 parkForward;
    private float parkHalfWidth;
    private float parkHalfDepth;
    private float parkGroundY;
    private bool parkAreaValid;

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<EditorController>(true);
        if (!container)
        {
            container = new GameObject("CenterParkCharacters").transform;
            container.SetParent(transform, worldPositionStays: false);
        }
    }

    void OnEnable()
    {
        if (controller) controller.OnStateChanged += HandleStateChanged;
    }
    void OnDisable()
    {
        if (controller) controller.OnStateChanged -= HandleStateChanged;
        if (stateCo != null) StopCoroutine(stateCo);
    }

    void Start()
    {
        HandleStateChanged(controller ? controller.State : EditorController.EditorState.CenterPark);
    }

    void HandleStateChanged(EditorController.EditorState s)
    {
        if (stateCo != null) StopCoroutine(stateCo);
        stateCo = StartCoroutine(StateRoutine(s));
    }

    IEnumerator StateRoutine(EditorController.EditorState s)
    {
        if (s == EditorController.EditorState.CenterPark)
        {
            if (spawnDelayOnEnter > 0f)
                yield return useUnscaledTime ? new WaitForSecondsRealtime(spawnDelayOnEnter)
                                            : new WaitForSeconds(spawnDelayOnEnter);
            SpawnAllFromSave();
        }
        else if (s == EditorController.EditorState.CharacterCreator)
        {
            ShowAll();
        }
        else
        {
            if (hideDelayOnExit > 0f)
                yield return useUnscaledTime ? new WaitForSecondsRealtime(hideDelayOnExit)
                                            : new WaitForSeconds(hideDelayOnExit);
            HideAll();
        }
        stateCo = null;
    }

    void ShowAll()
    {
        foreach (var kv in spawned)
            if (kv.Value) kv.Value.SetActive(true);
    }

    public void SpawnAllFromSave()
    {
        var save = (Game.Instance != null) ? Game.Instance.CurrentSave : null;
        if (save == null || characterPrefab == null) return;

        if (save.layout == null) {
            Debug.LogError("WorldSave has no layout data! Cannot determine park area.");
            return;
        }

        var rect = save.layout.centerParkBounds;

        float rotationDeg = save.layout.centerParkRotationDeg;
        float groundY = FindGroundYAt(rect.center);

        parkCenter = new Vector3(rect.center.x, groundY, rect.center.y);
        parkHalfWidth = Mathf.Max(0f, rect.width * 0.5f);
        parkHalfDepth = Mathf.Max(0f, rect.height * 0.5f);
        parkGroundY = groundY;
        parkAreaValid = parkHalfWidth > 0.01f && parkHalfDepth > 0.01f;

        float rad = rotationDeg * Mathf.Deg2Rad;
        parkRight = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
        parkForward = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

        if (parkRight.sqrMagnitude < 1e-6f) parkRight = Vector3.right;
        else parkRight.Normalize();
        if (parkForward.sqrMagnitude < 1e-6f) parkForward = Vector3.forward;
        else parkForward.Normalize();

        transform.position = parkCenter;
        transform.rotation = Quaternion.Euler(0f, rotationDeg, 0f);

        var corners = save.layout.centerParkCorners;
        if (corners != null && corners.Length > 0)
        {
            var bounds = new Bounds(parkCenter, Vector3.zero);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 c2 = corners[i];
                bounds.Encapsulate(new Vector3(c2.x, groundY, c2.y));
            }
            bounds.Expand(new Vector3(0f, 0.2f, 0f));
            parkBounds = bounds;
        }
        else
        {
            parkBounds = new Bounds(parkCenter, new Vector3(rect.width, 0.2f, rect.height));
        }

        SpawnList(save.mains);
        SpawnList(save.sides);
        SpawnList(save.extras);
    }

    // NEW helper
    float FindGroundYAt(Vector2 xz)
    {
        const float castTop = 1000f;
        var origin = new Vector3(xz.x, castTop, xz.y);
        if (Physics.Raycast(origin, Vector3.down, out var hit, castTop * 2f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;

        // Sensible fallback if nothing is hit
        return 0f;
    }
    
    void SpawnList(List<characterStats> list)
    {
        if (list == null) return;
        foreach (var stats in list)
        {
            if (stats == null) continue;
            if (string.IsNullOrEmpty(stats.id)) stats.id = System.Guid.NewGuid().ToString();

            if (spawned.TryGetValue(stats.id, out var existing))
            {
                if (existing) existing.SetActive(true);
                continue;
            }

            Vector3 pos = FindFreeSpot();
            Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var go = Instantiate(characterPrefab, pos, rot, container);

            // NEW: hard snap to ground at spawn
            SnapToGround(go);

            TryInvokeBind(go, stats);
            AttachIndicator(go, stats);

            var handle = go.GetComponent<CharacterHandle>() ?? go.AddComponent<CharacterHandle>();
            handle.Bind(stats);

            var wander = go.GetComponent<WanderWithinArea>() ?? go.AddComponent<WanderWithinArea>();
            if (parkAreaValid)
                wander.SetOrientedArea(parkCenter, parkRight, parkForward, parkHalfWidth, parkHalfDepth, parkGroundY);
            else
                wander.SetArea(parkBounds);
            wander.NoiseFrequency = Random.Range(0.18f, 0.35f);
            wander.EdgeMargin = 0.6f;
            wander.SpeedRange = new Vector2(0.75f, 1.15f);

            spawned[stats.id] = go;
        }
    }

    // NEW helper
    void SnapToGround(GameObject go)
    {
        var pos = go.transform.position;
        if (Physics.Raycast(pos + Vector3.up * 1000f, Vector3.down, out var hit, 2000f, ~0, QueryTriggerInteraction.Ignore))
        {
            var cc = go.GetComponent<CharacterController>();
            if (cc)
            {
                cc.enabled = false;
                float footOffset = cc.center.y - (cc.height * 0.5f); // local offset from pivot to feet
                pos.y = hit.point.y - footOffset + cc.skinWidth;      // plant feet on ground
                go.transform.position = pos;
                cc.enabled = true;
            }
            else
            {
                pos.y = hit.point.y;
                go.transform.position = pos;
            }
        }
    }

    
    Vector3 FindFreeSpot()
    {
        Vector3 p = RandomPointInPark();

        for (int i = 0; i < maxAttemptsPerSpawn; i++)
        {
            if (IsFarEnough(p)) return p;
            p = RandomPointInPark();
        }
        return p;
    }
    
    Vector3 RandomPointInPark()
    {
        if (parkAreaValid)
        {
            float halfW = Mathf.Max(0f, parkHalfWidth);
            float halfD = Mathf.Max(0f, parkHalfDepth);
            float u = (halfW > 0f) ? Random.Range(-halfW, halfW) : 0f;
            float v = (halfD > 0f) ? Random.Range(-halfD, halfD) : 0f;
            Vector3 offset = parkRight * u + parkForward * v;
            Vector3 pos = parkCenter + offset;
            pos.y = parkGroundY;
            return pos;
        }

        float x = Random.Range(parkBounds.min.x, parkBounds.max.x);
        float z = Random.Range(parkBounds.min.z, parkBounds.max.z);
        return new Vector3(x, parkBounds.center.y, z);
    }
    
    public CharacterHandle GetOrSpawnHandle(characterStats stats)
    {
        if (stats == null) return null;
        if (string.IsNullOrEmpty(stats.id)) stats.id = System.Guid.NewGuid().ToString();
        if (spawned.TryGetValue(stats.id, out var existing) && existing) return existing.GetComponent<CharacterHandle>();

        Vector3 pos = FindFreeSpot();
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var go = Instantiate(characterPrefab, pos, rot, container);
        SnapToGround(go); 
        
        // Apply the same robust grounding logic here for consistency
        var characterController = go.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
            float footOffset = characterController.center.y - (characterController.height / 2f);
            pos.y -= footOffset; 
            go.transform.position = pos;
            characterController.enabled = true;
        }

        TryInvokeBind(go, stats);
        var handle = go.GetComponent<CharacterHandle>() ?? go.AddComponent<CharacterHandle>();
        handle.Bind(stats);
        AttachIndicator(go, stats);
        var wander = go.GetComponent<WanderWithinArea>() ?? go.AddComponent<WanderWithinArea>();
        if (parkAreaValid)
            wander.SetOrientedArea(parkCenter, parkRight, parkForward, parkHalfWidth, parkHalfDepth, parkGroundY);
        else
            wander.SetArea(parkBounds);
        wander.NoiseFrequency = Random.Range(0.18f, 0.35f);
        wander.EdgeMargin = 0.6f;
        wander.SpeedRange = new Vector2(0.75f, 1.15f);

        spawned[stats.id] = go;
        return handle;
    }

    void TryInvokeBind(GameObject go, characterStats stats)
    {
        go.SendMessage("Bind", stats, SendMessageOptions.DontRequireReceiver);
    }

    bool IsFarEnough(Vector3 point)
    {
        float sq = minSpacing * minSpacing;
        foreach (var kv in spawned)
        {
            var go = kv.Value;
            if (!go || !go.activeInHierarchy) continue;
            if ((go.transform.position - point).sqrMagnitude < sq) return false;
        }
        return true;
    }

    void HideAll()
    {
        foreach (var kv in spawned) if (kv.Value) kv.Value.SetActive(false);
    }

    public void DestroyAll()
    {
        foreach (var kv in spawned) if (kv.Value) Destroy(kv.Value);
        spawned.Clear();
    }

    void AttachIndicator(GameObject characterGO, characterStats stats)
    {
        GameObject prefab = null;
        switch (stats.character_type)
        {
            case characterStats.CharacterType.Main: prefab = mainIndicatorPrefab; break;
            case characterStats.CharacterType.Side: prefab = sideIndicatorPrefab; break;
        }
        if (!prefab) return;

        var existing = characterGO.transform.Find("Indicator");
        if (existing != null) return;

        var ind = Instantiate(prefab, characterGO.transform);
        ind.name = "Indicator";

        var baseScale = prefab.transform.localScale;
        ind.transform.localScale = baseScale * indicatorScale;

        ind.transform.localPosition = new Vector3(0f, indicatorYOffset, 0f);
        ind.transform.localRotation = Quaternion.identity;
    }
    
    public void RefreshTypeIndicator(GameObject characterGO, characterStats stats)
    {
        if (!characterGO || !stats) return;
        StartCoroutine(RefreshIndicatorNextFrame(characterGO, stats));
    }

    private IEnumerator RefreshIndicatorNextFrame(GameObject characterGO, characterStats stats)
    {
        var old = characterGO.transform.Find("Indicator");
        if (old) Destroy(old.gameObject);
        yield return null;
        AttachIndicator(characterGO, stats);
    }
    
    public void RefreshAllIndicators()
    {
        var toRemove = new List<string>();
        foreach (var kv in spawned)
        {
            var go = kv.Value;
            if (!go) { toRemove.Add(kv.Key); continue; }
            var handle = go.GetComponent<CharacterHandle>();
            if (handle != null && handle.Stats != null)
            {
                RefreshTypeIndicator(go, handle.Stats);
            }
        }
        foreach (var id in toRemove) spawned.Remove(id);
    }
}
