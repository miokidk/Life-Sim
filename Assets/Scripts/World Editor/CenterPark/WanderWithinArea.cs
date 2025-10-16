using UnityEngine;

public class WanderWithinArea : MonoBehaviour
{
    // REMOVED: [SerializeField] private Transform area;
    [Header("Motion")]
    [SerializeField] private Vector2 speedRange = new Vector2(0.8f, 1.2f);
    [SerializeField] private float noiseFrequency = 0.25f;   // lower = smoother turns
    [SerializeField] private float noiseStrength = 0.35f;     // how strongly noise steers away from the target vector
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
    [Header("Destinations")]
    [SerializeField] private Vector2 retargetIntervalRange = new Vector2(4f, 10f);
    [SerializeField, Min(0.05f)] private float targetReachDistance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float minTravelDistanceFraction = 0.35f;

    float phase, freqMul;

    public Vector2 MoveDurationRange { get => moveDurationRange; set => moveDurationRange = value; }
    public Vector2 PauseDurationRange { get => pauseDurationRange; set => pauseDurationRange = value; }

    bool moving = true;
    Coroutine stateCo;

    float speed, seedX, seedZ;
    Vector3 currentTarget;
    float nextRetarget;
    bool hasTarget;
    CharacterController cc;
    Bounds areaBounds;
    bool hasOrientedArea;
    Vector3 orientedCenter;
    Vector3 orientedRight;
    Vector3 orientedForward;
    float orientedHalfWidth;
    float orientedHalfDepth;
    float orientedY;

    // This now accepts a Bounds struct directly.
    public void SetArea(Bounds b)
    {
        hasOrientedArea = false;
        areaBounds = b;
        if (isActiveAndEnabled)
            ChooseNewTarget(true);
    }

    public void SetOrientedArea(
        Vector3 center,
        Vector3 right,
        Vector3 forward,
        float halfWidth,
        float halfDepth,
        float y)
    {
        orientedCenter = center;
        orientedRight = right.sqrMagnitude > 1e-6f ? right.normalized : Vector3.right;
        orientedForward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        orientedHalfWidth = Mathf.Max(0f, halfWidth);
        orientedHalfDepth = Mathf.Max(0f, halfDepth);
        orientedY = y;
        hasOrientedArea = orientedHalfWidth > 0f && orientedHalfDepth > 0f;

        if (hasOrientedArea)
        {
            areaBounds = new Bounds(center, Vector3.zero);
            areaBounds.Encapsulate(center + orientedRight * orientedHalfWidth + orientedForward * orientedHalfDepth);
            areaBounds.Encapsulate(center + orientedRight * orientedHalfWidth - orientedForward * orientedHalfDepth);
            areaBounds.Encapsulate(center - orientedRight * orientedHalfWidth + orientedForward * orientedHalfDepth);
            areaBounds.Encapsulate(center - orientedRight * orientedHalfWidth - orientedForward * orientedHalfDepth);
            areaBounds.Expand(new Vector3(0f, 0.2f, 0f));
        }
        else
        {
            areaBounds = new Bounds(center, Vector3.zero);
        }

        if (isActiveAndEnabled)
            ChooseNewTarget(true);
    }

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>();
        phase   = Random.Range(0f, 1000f);
        freqMul = Random.Range(frequencyJitter.x, frequencyJitter.y);
        cc = GetComponent<CharacterController>();
        speed = Random.Range(speedRange.x, speedRange.y);
        seedX = (Random.value + GetInstanceID() * 0.013f) * 1000f;
        seedZ = (Random.value + GetInstanceID() * 0.029f) * 1000f;
        ChooseNewTarget(true);
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

        if (!hasTarget || Time.time >= nextRetarget)
            ChooseNewTarget();

        Vector3 pos = transform.position;
        Vector3 toTarget = currentTarget - pos;
        toTarget.y = 0f;
        float distToTarget = toTarget.magnitude;
        if (distToTarget <= targetReachDistance)
        {
            ChooseNewTarget(true);
            return;
        }

        Vector3 dir = toTarget / Mathf.Max(0.0001f, distToTarget);

        float t = (Time.time + phase) * noiseFrequency * freqMul;
        Vector3 wander = new Vector3(
            Mathf.PerlinNoise(seedX, t) * 2f - 1f,
            0f,
            Mathf.PerlinNoise(seedZ, t) * 2f - 1f
        );
        dir = (dir + wander * noiseStrength).normalized;
        if (dir.sqrMagnitude < 1e-4f)
            dir = toTarget.normalized;

        float dt = Time.deltaTime;
        Vector3 step = dir * speed * dt;
        Vector3 next = pos + step;

        bool clamped = false;
        if (hasOrientedArea)
        {
            float minU = -orientedHalfWidth + edgeMargin;
            float maxU = orientedHalfWidth - edgeMargin;
            float minV = -orientedHalfDepth + edgeMargin;
            float maxV = orientedHalfDepth - edgeMargin;

            if (minU >= maxU)
            {
                float mid = (minU + maxU) * 0.5f;
                minU = maxU = mid;
            }
            if (minV >= maxV)
            {
                float mid = (minV + maxV) * 0.5f;
                minV = maxV = mid;
            }

            Vector3 delta = next - orientedCenter;
            float u = Vector3.Dot(delta, orientedRight);
            float v = Vector3.Dot(delta, orientedForward);
            float clampedU = Mathf.Clamp(u, minU, maxU);
            float clampedV = Mathf.Clamp(v, minV, maxV);
            if (!Mathf.Approximately(clampedU, u) || !Mathf.Approximately(clampedV, v))
                clamped = true;
            next = orientedCenter + orientedRight * clampedU + orientedForward * clampedV;
        }
        else
        {
            float minX = areaBounds.min.x + edgeMargin;
            float maxX = areaBounds.max.x - edgeMargin;
            float minZ = areaBounds.min.z + edgeMargin;
            float maxZ = areaBounds.max.z - edgeMargin;

            if (minX >= maxX)
            {
                float midX = areaBounds.center.x;
                minX = maxX = midX;
            }
            if (minZ >= maxZ)
            {
                float midZ = areaBounds.center.z;
                minZ = maxZ = midZ;
            }

            if (next.x < minX) { next.x = minX; clamped = true; }
            if (next.x > maxX) { next.x = maxX; clamped = true; }
            if (next.z < minZ) { next.z = minZ; clamped = true; }
            if (next.z > maxZ) { next.z = maxZ; clamped = true; }
        }

        if (clamped)
        {
            dir = (next - pos);
            dir.y = 0f;
            float len = dir.magnitude;
            if (len > 0.0001f)
                dir /= len;
            step = dir * speed * dt;
            ChooseNewTarget(true);
        }

        Quaternion face = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, face, turnDegreesPerSec * dt);

        if (Physics.Raycast(next + Vector3.up * groundRayTop, Vector3.down, out var hit, groundRayTop + 20f))
            next.y = hit.point.y;
        else // Fallback uses the bottom of the bounds.
            next.y = hasOrientedArea ? orientedY : areaBounds.center.y - areaBounds.extents.y;

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

        if (areaBounds.extents != Vector3.zero)
            ChooseNewTarget(true);
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

    void ChooseNewTarget(bool force = false)
    {
        if (hasOrientedArea)
        {
            ChooseNewTargetOriented(force);
            return;
        }

        if (areaBounds.extents == Vector3.zero) return;

        if (!force && Time.time < nextRetarget && hasTarget) return;

        float minX = areaBounds.min.x + edgeMargin;
        float maxX = areaBounds.max.x - edgeMargin;
        float minZ = areaBounds.min.z + edgeMargin;
        float maxZ = areaBounds.max.z - edgeMargin;

        if (minX >= maxX)
        {
            float midX = areaBounds.center.x;
            minX = maxX = midX;
        }
        if (minZ >= maxZ)
        {
            float midZ = areaBounds.center.z;
            minZ = maxZ = midZ;
        }

        Vector3 origin = transform.position;
        origin.y = 0f;

        float longestSide = Mathf.Max(areaBounds.size.x, areaBounds.size.z);
        float minDist = longestSide * minTravelDistanceFraction;
        float minDistSq = minDist * minDist;

        const int maxAttempts = 12;
        Vector3 candidate = currentTarget;
        bool picked = false;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector3 sample = new Vector3(
                Random.Range(minX, maxX),
                areaBounds.max.y + groundRayTop,
                Random.Range(minZ, maxZ)
            );

            Vector3 flat = sample;
            flat.y = 0f;
            float distSq = (flat - origin).sqrMagnitude;

            if (minTravelDistanceFraction > 0f && distSq < minDistSq)
                continue;

            candidate = sample;
            picked = true;
            break;
        }

        if (!picked)
        {
            candidate = new Vector3(
                Random.Range(minX, maxX),
                areaBounds.max.y + groundRayTop,
                Random.Range(minZ, maxZ)
            );
        }

        if (Physics.Raycast(candidate, Vector3.down, out var hit, groundRayTop + areaBounds.size.y + 10f))
            candidate.y = hit.point.y;
        else
            candidate.y = areaBounds.center.y - areaBounds.extents.y;

        currentTarget = candidate;
        hasTarget = true;

        float minInterval = Mathf.Max(0.1f, Mathf.Min(retargetIntervalRange.x, retargetIntervalRange.y));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(retargetIntervalRange.x, retargetIntervalRange.y));

        Vector3 flatCurrent = currentTarget;
        flatCurrent.y = 0f;
        Vector3 flatPos = transform.position;
        flatPos.y = 0f;
        float estimatedTravelTime = Vector3.Distance(flatPos, flatCurrent) / Mathf.Max(0.05f, speed);

        float interval = Random.Range(minInterval, maxInterval);
        interval = Mathf.Max(interval, estimatedTravelTime * 0.85f);
        nextRetarget = Time.time + interval;
    }

    void ChooseNewTargetOriented(bool force)
    {
        if (!force && Time.time < nextRetarget && hasTarget) return;

        float minU = -orientedHalfWidth + edgeMargin;
        float maxU = orientedHalfWidth - edgeMargin;
        float minV = -orientedHalfDepth + edgeMargin;
        float maxV = orientedHalfDepth - edgeMargin;

        if (minU >= maxU)
        {
            float mid = (minU + maxU) * 0.5f;
            minU = maxU = mid;
        }
        if (minV >= maxV)
        {
            float mid = (minV + maxV) * 0.5f;
            minV = maxV = mid;
        }

        Vector3 origin = transform.position;
        origin.y = 0f;

        float longestSide = Mathf.Max(orientedHalfWidth * 2f, orientedHalfDepth * 2f);
        float minDist = longestSide * minTravelDistanceFraction;
        float minDistSq = minDist * minDist;

        const int maxAttempts = 12;
        Vector3 candidate = currentTarget;
        bool picked = false;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float u = (maxU > minU) ? Random.Range(minU, maxU) : minU;
            float v = (maxV > minV) ? Random.Range(minV, maxV) : minV;
            Vector3 sample = orientedCenter + orientedRight * u + orientedForward * v;

            Vector3 flat = sample;
            flat.y = 0f;
            float distSq = (flat - origin).sqrMagnitude;

            if (minTravelDistanceFraction > 0f && distSq < minDistSq)
                continue;

            candidate = sample;
            picked = true;
            break;
        }

        if (!picked)
        {
            float u = (maxU > minU) ? Random.Range(minU, maxU) : minU;
            float v = (maxV > minV) ? Random.Range(minV, maxV) : minV;
            candidate = orientedCenter + orientedRight * u + orientedForward * v;
        }

        Vector3 rayOrigin = candidate + Vector3.up * groundRayTop;
        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, groundRayTop + 20f))
            candidate.y = hit.point.y;
        else
            candidate.y = orientedY;

        currentTarget = candidate;
        hasTarget = true;

        float minInterval = Mathf.Max(0.1f, Mathf.Min(retargetIntervalRange.x, retargetIntervalRange.y));
        float maxInterval = Mathf.Max(minInterval, Mathf.Max(retargetIntervalRange.x, retargetIntervalRange.y));

        Vector3 flatCurrent = currentTarget;
        flatCurrent.y = 0f;
        Vector3 flatPos = transform.position;
        flatPos.y = 0f;
        float estimatedTravelTime = Vector3.Distance(flatPos, flatCurrent) / Mathf.Max(0.05f, speed);

        float interval = Random.Range(minInterval, maxInterval);
        interval = Mathf.Max(interval, estimatedTravelTime * 0.85f);
        nextRetarget = Time.time + interval;
    }
}
