using UnityEngine;

public class WanderWithinArea : MonoBehaviour
{
    // REMOVED: [SerializeField] private Transform area;
    [Header("Motion")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private float noiseFrequency = 0.25f;   // lower = smoother turns
    [SerializeField] private float turnDegreesPerSec = 360f;
    [SerializeField] private float edgeMargin = 0.5f;        // keep this far inside bounds

    public Vector2 SpeedRange { get => speedRange; set => speedRange = value; }
    public float NoiseFrequency { get => noiseFrequency; set => noiseFrequency = Mathf.Max(0.01f, value); }
    public float EdgeMargin { get => edgeMargin; set => edgeMargin = Mathf.Max(0f, value); }

    [SerializeField] private float groundRayTop = 10f;       // ray height for grounding

    [Header("Animator (optional)")]
    [SerializeField] private Animator animator;              // auto-found if null
    [SerializeField] private string isMovingBool = "IsMoving";
    [SerializeField] private string speedFloat = "Speed";

    [Header("Pacing")]
    [SerializeField] private Vector2 moveDurationRange = new Vector2(5f, 15f); // how long to drift
    [SerializeField] private Vector2 pauseDurationRange = new Vector2(2f, 30f); // how long to stop
    [SerializeField] private bool startPaused = false;
    [SerializeField] private bool externallyPaused = false;
    [SerializeField] private Vector2 frequencyJitter = new Vector2(0.9f, 1.1f);
    float phase, freqMul;

    public Vector2 MoveDurationRange { get => moveDurationRange; set => moveDurationRange = value; }
    public Vector2 PauseDurationRange { get => pauseDurationRange; set => pauseDurationRange = value; }

    bool moving = true;
    Coroutine stateCo;

    float speed, seedX, seedZ;
    CharacterController cc;
    Bounds areaBounds;

    // This now accepts a Bounds struct directly.
    public void SetArea(Bounds b) { areaBounds = b; }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        phase   = Random.Range(0f, 1000f);
        freqMul = Random.Range(frequencyJitter.x, frequencyJitter.y);
        cc = GetComponent<CharacterController>();
        speed = Random.Range(speedRange.x, speedRange.y);
        seedX = (Random.value + GetInstanceID() * 0.013f) * 1000f;
        seedZ = (Random.value + GetInstanceID() * 0.029f) * 1000f;
    }

    void Update()
    {
        // We now check if the bounds have been set (size is not zero).
        if (areaBounds.extents == Vector3.zero) return;

        if (!moving || externallyPaused)
        {
            if (cc) cc.SimpleMove(Vector3.zero);
            return;
        }

        float t = (Time.time + phase) * noiseFrequency * freqMul;
        Vector3 dir = new Vector3(
            Mathf.PerlinNoise(seedX, t) * 2f - 1f,
            0f,
            Mathf.PerlinNoise(seedZ, t) * 2f - 1f
        ).normalized;
        if (dir.sqrMagnitude < 1e-6f) return;

        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        Vector3 step = dir * speed * dt;
        Vector3 next = pos + step;

        float minX = areaBounds.min.x + edgeMargin, maxX = areaBounds.max.x - edgeMargin;
        float minZ = areaBounds.min.z + edgeMargin, maxZ = areaBounds.max.z - edgeMargin;
        bool clamped = false;
        if (next.x < minX) { next.x = minX; clamped = true; }
        if (next.x > maxX) { next.x = maxX; clamped = true; }
        if (next.z < minZ) { next.z = minZ; clamped = true; }
        if (next.z > maxZ) { next.z = maxZ; clamped = true; }
        if (clamped) { dir = (next - pos).normalized; step = dir * speed * dt; }

        Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnDegreesPerSec * dt);

        if (Physics.Raycast(next + Vector3.up * groundRayTop, Vector3.down, out var hit, groundRayTop + 20f))
            next.y = hit.point.y;
        else // Fallback uses the bottom of the bounds.
            next.y = areaBounds.center.y - areaBounds.extents.y;

        if (cc)
        {
            cc.SimpleMove(dir * speed);
        }
        else
        {
            transform.position = next;
        }

        SetAnim(true, speed);
    }

    void SetAnim(bool moving, float spd)
    {
        if (!animator) return;
        if (!string.IsNullOrEmpty(isMovingBool) && HasParam(isMovingBool)) animator.SetBool(isMovingBool, moving);
        if (!string.IsNullOrEmpty(speedFloat) && HasParam(speedFloat)) animator.SetFloat(speedFloat, spd);
    }
    bool HasParam(string n) { foreach (var p in animator.parameters) if (p.name == n) return true; return false; }

    void OnEnable()
    {
        // FIXED: Removed the call to the non-existent CacheBounds() method.
        if (stateCo != null) StopCoroutine(stateCo);
        stateCo = StartCoroutine(StateLoop());
    }

    void OnDisable()
    {
        if (stateCo != null) StopCoroutine(stateCo);
        stateCo = null;
        moving = false;
    }

    System.Collections.IEnumerator StateLoop()
    {
        moving = !startPaused;
        while (true)
        {
            float dur = moving
                ? Random.Range(moveDurationRange.x, moveDurationRange.y)
                : Random.Range(pauseDurationRange.x, pauseDurationRange.y);

            float tEnd = Time.time + dur;
            // set animator instantly when state flips
            SetAnim(moving, moving ? speed : 0f);

            while (Time.time < tEnd) yield return null;
            moving = !moving; // flip state
        }
    }

    public void PauseMovement(bool pause)
    {
        externallyPaused = pause;
        if (pause) SetAnim(false, 0f);
    }
}