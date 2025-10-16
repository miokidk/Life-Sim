using System;
using System.Collections;
using System.Collections.Generic;
using LifeSim.Units;
using UnityEngine;

/// <summary>
/// Renders world debug visuals (roads, sidewalks, crosswalks, intersections).
/// This version fixes compile errors by:
///  - Using IntersectionData.points (not .polygon)
///  - Providing both sync and coroutine ("Async") visualizers
/// Nothing here mutates Unity objects off the main thread; the async method yields between batches.
/// </summary>
public class WorldVisualizer : MonoBehaviour
{
    [Header("Materials (optional, for background planes)")]
    [SerializeField] private Material worldMaterial;
    [SerializeField] private Material parkMaterial;
    [SerializeField] private float parkYOffset = 0.01f;

    // --- Runtime Texture Settings ---
    [Header("Runtime Texture Settings")]
    [SerializeField] private bool generateRuntimeTextures = true;
    [SerializeField, Min(64)] private int textureSize = 512;
    [SerializeField, Min(0.1f)] private float worldTileMeters = 8f;
    [SerializeField, Min(0.1f)] private float parkTileMeters = 3f;

    [Header("Park Grass")]
    [SerializeField] private Color grassDark = new Color(0.36f, 0.50f, 0.32f);
    [SerializeField] private Color grassLight = new Color(0.46f, 0.62f, 0.40f);
    [SerializeField, Range(0.002f, 0.06f)] private float grassLowFreq = 0.009f;
    [SerializeField, Range(0.01f, 0.20f)] private float grassHiFreq = 0.045f;
    [SerializeField, Range(0.0f, 1.0f)] private float stripeStrength = 0.12f;
    [SerializeField, Range(0.0f, 1.0f)] private float speckleStrength = 0.06f;

    [Header("World Grass")]
    [SerializeField] private Color worldGrassDark = new(0.26f, 0.36f, 0.22f);
    [SerializeField] private Color worldGrassLight = new(0.32f, 0.44f, 0.28f);
    [SerializeField, Range(0.002f, 0.06f)] private float worldGrassLowFreq = 0.0075f;
    [SerializeField, Range(0.01f, 0.20f)] private float worldGrassHiFreq = 0.035f;
    [SerializeField, Range(0.0f, 1.0f)] private float worldStripeStrength = 0.10f;
    [SerializeField, Range(0.0f, 1.0f)] private float worldSpeckleStrength = 0.05f;

    // shader property ids (works with URP lit + Standard)
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");

    [Header("Crosswalks")]
    [SerializeField] private bool drawCrosswalks = true;
    [SerializeField, Min(0f)] private float crosswalkBandFeet = 8f;   // depth of zebra band along road
    [SerializeField, Min(0f)] private float crosswalkStripeFeet = 2f;
    [SerializeField, Min(0f)] private float crosswalkGapFeet = 2f;
    [SerializeField, Min(0f)] private float crosswalkInsetFeet = 1f;
    [SerializeField] private Color crosswalkColor = Color.white;

    [Header("Sidewalks (debug)")]
    [SerializeField] private bool drawSidewalks = true;
    [SerializeField, Min(0f)] private float sidewalkFeet = 6f;
    [SerializeField] private Color curbEdgeColor = Color.white;
    [SerializeField] private Color sidewalkEdgeColor = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField, Min(0f)] private float edgeLift = 0.02f;

    [Header("Lane Markings (debug)")]
    [SerializeField] private Color centerlineYellow = new Color(1f, 0.85f, 0f, 1f);
    [SerializeField] private Color laneDashWhite = Color.white;
    [SerializeField, Min(0f)] private float dashFeet = 10f;
    [SerializeField, Min(0f)] private float gapFeet = 30f;
    [SerializeField, Min(0f)] private float doubleYellowGapFeet = 0.5f; // curb-to-curb center gap between the two yellow lines
    [SerializeField, Min(0f)] private float markingLift = 0.03f;

    [Header("Lots (debug)")]
    [SerializeField] private bool drawLots = true;
    [SerializeField] private Color lotColor = new Color(0f, 1f, 0.1f, 0.9f);

    private struct CornerPoints
    {
        public Vector2 curbLeft;
        public Vector2 curbRight;
        public Vector2 outerLeft;
        public Vector2 outerRight;
        public bool hasLeft;
        public bool hasRight;
        public bool isInterior;
    }

    private struct DebugLine
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;
    }

    private struct GeometrySettingsSnapshot : IEquatable<GeometrySettingsSnapshot>
    {
        public bool drawSidewalks;
        public bool drawCrosswalks;
        public bool drawLots;
        public float dashFeet;
        public float gapFeet;
        public float doubleYellowGapFeet;
        public float markingLift;
        public float edgeLift;
        public float sidewalkFeet;
        public float crosswalkBandFeet;
        public float crosswalkStripeFeet;
        public float crosswalkGapFeet;
        public float crosswalkInsetFeet;
        public float parkYOffset;
        public Color centerlineYellow;
        public Color laneDashWhite;
        public Color curbEdgeColor;
        public Color sidewalkEdgeColor;
        public Color crosswalkColor;
        public Color lotColor;

        public bool Equals(GeometrySettingsSnapshot other)
        {
            return drawSidewalks == other.drawSidewalks &&
                   drawCrosswalks == other.drawCrosswalks &&
                   drawLots == other.drawLots &&
                   dashFeet == other.dashFeet &&
                   gapFeet == other.gapFeet &&
                   doubleYellowGapFeet == other.doubleYellowGapFeet &&
                   markingLift == other.markingLift &&
                   edgeLift == other.edgeLift &&
                   sidewalkFeet == other.sidewalkFeet &&
                   crosswalkBandFeet == other.crosswalkBandFeet &&
                   crosswalkStripeFeet == other.crosswalkStripeFeet &&
                   crosswalkGapFeet == other.crosswalkGapFeet &&
                   crosswalkInsetFeet == other.crosswalkInsetFeet &&
                   parkYOffset == other.parkYOffset &&
                   centerlineYellow == other.centerlineYellow &&
                   laneDashWhite == other.laneDashWhite &&
                   curbEdgeColor == other.curbEdgeColor &&
                   sidewalkEdgeColor == other.sidewalkEdgeColor &&
                   crosswalkColor == other.crosswalkColor &&
                   lotColor == other.lotColor;
        }
    }

    private readonly Dictionary<(int index, bool isStart), CornerPoints> _cornerCache = new();
    private bool _cornerCacheDirty = true;
    private readonly List<DebugLine> _roadLines = new();
    private readonly List<DebugLine> _sidewalkLines = new();
    private readonly List<DebugLine> _crosswalkLines = new();
    private readonly List<DebugLine> _lotLines = new();
    private bool _geometryDirty = true;
    private bool _hasGeometrySettingsSnapshot;
    private GeometrySettingsSnapshot _lastGeometrySettings;

    private void Update()
    {
        if (_saveToVisualize == null) return;

        EnsureGeometry();

        DrawLines(_roadLines);

        if (drawSidewalks && _sidewalkLines.Count > 0)
            DrawLines(_sidewalkLines);

        if (drawCrosswalks && _crosswalkLines.Count > 0)
            DrawLines(_crosswalkLines);

        if (drawLots && _lotLines.Count > 0)
            DrawLines(_lotLines);
    }

    private static void DrawLines(List<DebugLine> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            Debug.DrawLine(line.start, line.end, line.color);
        }
    }

    // Cache the save so we can redraw debug lines every frame
    private WorldSave _saveToVisualize;
    public  void     SetLiveSave(WorldSave save)
    {
        _saveToVisualize = save;
        _cornerCacheDirty = true;
        _geometryDirty = true;
    }

    // ===== Background planes (world + park) =====
    private Transform _worldContainer;

    private void EnsureContainer()
    {
        if (_worldContainer != null) return;
        _worldContainer = new GameObject("GeneratedWorld").transform;
        _worldContainer.SetParent(transform, worldPositionStays: false);
    }

    private void ClearContainer()
    {
        if (_worldContainer == null) return;
        for (int i = _worldContainer.childCount - 1; i >= 0; i--)
            Destroy(_worldContainer.GetChild(i).gameObject);
    }

    #region Geometry Cache
    private GeometrySettingsSnapshot CaptureGeometrySettings()
    {
        return new GeometrySettingsSnapshot
        {
            drawSidewalks = drawSidewalks,
            drawCrosswalks = drawCrosswalks,
            drawLots = drawLots,
            dashFeet = dashFeet,
            gapFeet = gapFeet,
            doubleYellowGapFeet = doubleYellowGapFeet,
            markingLift = markingLift,
            edgeLift = edgeLift,
            sidewalkFeet = sidewalkFeet,
            crosswalkBandFeet = crosswalkBandFeet,
            crosswalkStripeFeet = crosswalkStripeFeet,
            crosswalkGapFeet = crosswalkGapFeet,
            crosswalkInsetFeet = crosswalkInsetFeet,
            parkYOffset = parkYOffset,
            centerlineYellow = centerlineYellow,
            laneDashWhite = laneDashWhite,
            curbEdgeColor = curbEdgeColor,
            sidewalkEdgeColor = sidewalkEdgeColor,
            crosswalkColor = crosswalkColor,
            lotColor = lotColor
        };
    }

    private void EnsureGeometry()
    {
        if (_saveToVisualize == null)
            return;

        var currentSettings = CaptureGeometrySettings();
        bool settingsChanged = !_hasGeometrySettingsSnapshot || !_lastGeometrySettings.Equals(currentSettings);

        if (settingsChanged)
        {
            if (_hasGeometrySettingsSnapshot && _lastGeometrySettings.sidewalkFeet != currentSettings.sidewalkFeet)
                _cornerCacheDirty = true;

            _geometryDirty = true;
        }

        if (!_geometryDirty)
            return;

        _roadLines.Clear();
        _sidewalkLines.Clear();
        _crosswalkLines.Clear();
        _lotLines.Clear();

        var save = _saveToVisualize;
        var roads = save.roadNetwork;
        if (roads != null && roads.Count > 0)
        {
            BuildRoadLines(roads);

            if (drawSidewalks)
                BuildSidewalkLines(roads);
        }

        var intersections = save.layout?.intersections;
        if (drawCrosswalks && intersections != null && intersections.Count > 0)
            BuildCrosswalkLines(intersections);

        var lots = save.layout?.lots;
        if (drawLots)
            BuildLotLines(lots);

        _lastGeometrySettings = currentSettings;
        _hasGeometrySettingsSnapshot = true;
        _geometryDirty = false;
    }

    private void BuildRoadLines(List<RoadSegment> roads)
    {
        float dashU = Size.Feet(dashFeet);
        float gapU = Size.Feet(gapFeet);
        float halfGap = Size.Feet(doubleYellowGapFeet) * 0.5f;
        Vector3 lift = Vector3.up * markingLift;

        for (int i = 0; i < roads.Count; i++)
        {
            var road = roads[i];
            Vector3 a = new Vector3(road.start.x, parkYOffset, road.start.y);
            Vector3 b = new Vector3(road.end.x, parkYOffset, road.end.y);

            Vector3 v = b - a;
            Vector2 dir2 = new Vector2(v.x, v.z);
            if (dir2.sqrMagnitude < 1e-6f) continue;
            dir2.Normalize();
            Vector2 perp2 = new Vector2(-dir2.y, dir2.x);
            Vector3 perp = new Vector3(perp2.x, 0f, perp2.y);

            Vector3 startLifted = a + lift;
            Vector3 endLifted = b + lift;

            if (road.type == RoadType.Arterial)
            {
                Vector3 offset = perp * halfGap;
                _roadLines.Add(new DebugLine { start = startLifted + offset, end = endLifted + offset, color = centerlineYellow });
                _roadLines.Add(new DebugLine { start = startLifted - offset, end = endLifted - offset, color = centerlineYellow });
            }
            else
            {
                AddDashedLineSegments(_roadLines, startLifted, endLifted, laneDashWhite, dashU, gapU);
            }
        }
    }

    private void BuildSidewalkLines(List<RoadSegment> roads)
    {
        float sidewalkU = Size.Feet(sidewalkFeet);
        if (sidewalkU <= 1e-4f)
            return;

        Dictionary<(int index, bool isStart), CornerPoints> cornerMap = EnsureCornerMap();
        Vector3 lift = Vector3.up * edgeLift;

        for (int roadIndex = 0; roadIndex < roads.Count; roadIndex++)
        {
            var road = roads[roadIndex];
            Vector3 a = new Vector3(road.start.x, parkYOffset, road.start.y);
            Vector3 b = new Vector3(road.end.x, parkYOffset, road.end.y);

            Vector3 v = b - a;
            Vector2 dir2 = new Vector2(v.x, v.z);
            if (dir2.sqrMagnitude < 1e-6f) continue;
            dir2.Normalize();
            Vector2 perp2 = new Vector2(-dir2.y, dir2.x);
            Vector3 perp = new Vector3(perp2.x, 0f, perp2.y);

            float halfRoad = road.width * 0.5f;
            Vector3 curbOff = perp * halfRoad;
            Vector3 sideOff = perp * (halfRoad + sidewalkU);

            Vector3 aCurbL = a + curbOff + lift;
            Vector3 aCurbR = a - curbOff + lift;
            Vector3 bCurbL = b + curbOff + lift;
            Vector3 bCurbR = b - curbOff + lift;

            Vector3 aSideL = a + sideOff + lift;
            Vector3 aSideR = a - sideOff + lift;
            Vector3 bSideL = b + sideOff + lift;
            Vector3 bSideR = b - sideOff + lift;

            CornerPoints startCorners = default;
            CornerPoints endCorners = default;
            bool hasStartCorners = cornerMap != null && OverrideCorner(cornerMap, roadIndex, true, ref aCurbL, ref aCurbR, ref aSideL, ref aSideR, out startCorners);
            bool hasEndCorners = cornerMap != null && OverrideCorner(cornerMap, roadIndex, false, ref bCurbL, ref bCurbR, ref bSideL, ref bSideR, out endCorners);

            if (hasStartCorners && hasEndCorners && startCorners.isInterior && endCorners.isInterior)
                continue;

            _sidewalkLines.Add(new DebugLine { start = aCurbL, end = bCurbL, color = curbEdgeColor });
            _sidewalkLines.Add(new DebugLine { start = aCurbR, end = bCurbR, color = curbEdgeColor });
            _sidewalkLines.Add(new DebugLine { start = aSideL, end = bSideL, color = sidewalkEdgeColor });
            _sidewalkLines.Add(new DebugLine { start = aSideR, end = bSideR, color = sidewalkEdgeColor });
        }
    }

    private void BuildCrosswalkLines(List<IntersectionData> intersections)
    {
        float bandU = Size.Feet(crosswalkBandFeet);
        float stripeU = Size.Feet(crosswalkStripeFeet);
        float gapU = Size.Feet(crosswalkGapFeet);
        float insetU = Size.Feet(crosswalkInsetFeet);

        if (bandU <= 1e-4f)
            return;

        for (int i = 0; i < intersections.Count; i++)
        {
            var inter = intersections[i];
            if (inter.connectors == null) continue;

            for (int j = 0; j < inter.connectors.Count; j++)
            {
                var connector = inter.connectors[j];
                Vector2 n2 = connector.normal;
                if (n2.sqrMagnitude < 1e-6f) continue;
                n2.Normalize();

                Vector2 t2 = new Vector2(-n2.y, n2.x);
                Vector3 perp = new Vector3(t2.x, 0f, t2.y);
                Vector3 bandDir = new Vector3(n2.x, 0f, n2.y);

                float halfRoad = connector.width * 0.5f;
                Vector3 basePos = new Vector3(
                    connector.point.x + n2.x * insetU,
                    parkYOffset + markingLift,
                    connector.point.y + n2.y * insetU
                );

                float placed = 0f;
                while (placed < bandU - 1e-3f)
                {
                    float seg = Mathf.Min(stripeU, bandU - placed);
                    if (seg <= 1e-4f)
                        seg = bandU - placed;

                    Vector3 mid = basePos + bandDir * (placed + seg * 0.5f);
                    Vector3 a = mid - perp * halfRoad;
                    Vector3 b = mid + perp * halfRoad;
                    _crosswalkLines.Add(new DebugLine { start = a, end = b, color = crosswalkColor });

                    if (stripeU <= 1e-4f && gapU <= 1e-4f)
                        break;

                    if (stripeU <= 1e-4f)
                        break;

                    placed += stripeU + gapU;
                }
            }
        }
    }

    private void BuildLotLines(List<LotData> lots)
    {
        float y = parkYOffset + 0.01f;

        int lotCount = lots?.Count ?? 0;
        for (int i = 0; i < lotCount; i++)
        {
            var lot = lots[i];
            var footprint = lot.corners;

            if (footprint != null && footprint.Length >= 3)
            {
                int count = footprint.Length;
                for (int c = 0; c < count; c++)
                {
                    Vector2 a2 = footprint[c];
                    Vector2 b2 = footprint[(c + 1) % count];
                    Vector3 a = new Vector3(a2.x, y, a2.y);
                    Vector3 b = new Vector3(b2.x, y, b2.y);
                    _lotLines.Add(new DebugLine { start = a, end = b, color = lotColor });
                }
            }
            else
            {
                var rect = lot.bounds;
                Vector3 p1 = new Vector3(rect.xMin, y, rect.yMin);
                Vector3 p2 = new Vector3(rect.xMax, y, rect.yMin);
                Vector3 p3 = new Vector3(rect.xMax, y, rect.yMax);
                Vector3 p4 = new Vector3(rect.xMin, y, rect.yMax);

                _lotLines.Add(new DebugLine { start = p1, end = p2, color = lotColor });
                _lotLines.Add(new DebugLine { start = p2, end = p3, color = lotColor });
                _lotLines.Add(new DebugLine { start = p3, end = p4, color = lotColor });
                _lotLines.Add(new DebugLine { start = p4, end = p1, color = lotColor });
            }
        }

        var layout = _saveToVisualize?.layout;
        if (layout != null)
        {
            var parkFootprint = layout.centerParkCorners;
            if (parkFootprint != null && parkFootprint.Length >= 3)
            {
                int count = parkFootprint.Length;
                for (int c = 0; c < count; c++)
                {
                    Vector2 a2 = parkFootprint[c];
                    Vector2 b2 = parkFootprint[(c + 1) % count];
                    Vector3 a = new Vector3(a2.x, y, a2.y);
                    Vector3 b = new Vector3(b2.x, y, b2.y);
                    _lotLines.Add(new DebugLine { start = a, end = b, color = lotColor });
                }
            }
            else
            {
                var rect = layout.centerParkBounds;
                Vector3 p1 = new Vector3(rect.xMin, y, rect.yMin);
                Vector3 p2 = new Vector3(rect.xMax, y, rect.yMin);
                Vector3 p3 = new Vector3(rect.xMax, y, rect.yMax);
                Vector3 p4 = new Vector3(rect.xMin, y, rect.yMax);

                _lotLines.Add(new DebugLine { start = p1, end = p2, color = lotColor });
                _lotLines.Add(new DebugLine { start = p2, end = p3, color = lotColor });
                _lotLines.Add(new DebugLine { start = p3, end = p4, color = lotColor });
                _lotLines.Add(new DebugLine { start = p4, end = p1, color = lotColor });
            }
        }
    }

    private static void AddDashedLineSegments(List<DebugLine> target, Vector3 start, Vector3 end, Color color, float dashU, float gapU)
    {
        float length = Vector3.Distance(start, end);
        if (length < 1e-4f)
            return;

        if (dashU <= 1e-4f || gapU <= 1e-4f)
        {
            target.Add(new DebugLine { start = start, end = end, color = color });
            return;
        }

        Vector3 dir = (end - start).normalized;
        float t = 0f;

        while (t < length - 1e-4f)
        {
            float seg = Mathf.Min(dashU, length - t);
            if (seg <= 1e-4f)
                seg = length - t;

            Vector3 s = start + dir * t;
            Vector3 e = s + dir * seg;
            target.Add(new DebugLine { start = s, end = e, color = color });

            t += dashU + gapU;
        }
    }
    #endregion

    private GameObject CreatePlane(
        string name,
        Rect bounds,
        float rotationDeg,
        Material material,
        float yOffset,
        bool isPark,
        Vector2[] orientedCorners = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        go.transform.SetParent(_worldContainer, worldPositionStays: false);

        Vector3 center = new Vector3(bounds.center.x, yOffset, bounds.center.y);
        float width = bounds.width;
        float height = bounds.height;
        Quaternion rotation = Quaternion.Euler(0f, rotationDeg, 0f);

        if (orientedCorners != null && orientedCorners.Length >= 3)
        {
            Vector3 c0 = new Vector3(orientedCorners[0].x, yOffset, orientedCorners[0].y);
            Vector3 c1 = new Vector3(orientedCorners[1].x, yOffset, orientedCorners[1].y);
            Vector3 c3 = new Vector3(orientedCorners[^1].x, yOffset, orientedCorners[^1].y);

            Vector3 right = (c1 - c0);
            Vector3 forward = (c3 - c0);
            float rightMag = right.magnitude;
            float forwardMag = forward.magnitude;

            if (rightMag > 1e-4f && forwardMag > 1e-4f)
            {
                width = rightMag;
                height = forwardMag;

                right /= rightMag;
                forward /= forwardMag;
                Vector3 up = Vector3.Cross(right, forward).normalized;
                if (up.sqrMagnitude < 1e-4f)
                {
                    up = Vector3.up;
                    forward = Vector3.Cross(up, right).normalized;
                }
                else
                {
                    forward = Vector3.Cross(up, right).normalized;
                }
                rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            if (orientedCorners.Length >= 4)
            {
                Vector3 c2 = new Vector3(orientedCorners[2].x, yOffset, orientedCorners[2].y);
                center = (c0 + c1 + c2 + c3) * 0.25f;
            }
            else
            {
                center = (c0 + c1 + c3) / 3f;
            }
        }

        // Unity Plane is 10x10 units
        go.transform.position = center;
        go.transform.localScale = new Vector3(width * 0.1f, 1f, height * 0.1f);
        go.transform.rotation = rotation;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr && material)
        {
            mr.material = material;

            if (generateRuntimeTextures)
            {
                Vector2 sizeMeters = new(bounds.width, bounds.height);

                // make & apply a grass texture
                Texture2D tex = isPark
                    ? MakeGrass(textureSize, grassLowFreq, grassHiFreq, stripeStrength, speckleStrength, grassDark, grassLight)
                    : MakeGrass(textureSize, worldGrassLowFreq, worldGrassHiFreq, worldStripeStrength, worldSpeckleStrength, worldGrassDark, worldGrassLight);

                ApplyTiled(mr.material, tex, sizeMeters, isPark ? parkTileMeters : worldTileMeters);
            }
        }

        // keep collider for clicks
        if (!go.TryGetComponent<Collider>(out _)) go.AddComponent<MeshCollider>();
        return go;
    }

    private void BuildBackgroundPlanes(WorldSave save)
    {
        if (save?.layout == null) return;
        EnsureContainer();
        ClearContainer();

        if (worldMaterial)
            CreatePlane("World Plane", save.layout.worldBounds, 0f, worldMaterial, 0f, false);
        if (parkMaterial)
            CreatePlane("Center Park", save.layout.centerParkBounds, save.layout.centerParkRotationDeg, parkMaterial, parkYOffset, true, save.layout.centerParkCorners);
    }

    #region Entry Points
    public void VisualizeWorldLayout(WorldSave save)
    {
        SetLiveSave(save);
        StartCoroutine(VisualizeWorldLayoutAsync(save, null, null));
    }

    /// <summary>
    /// Draw in small batches so the loading UI can update.
    /// </summary>
    public IEnumerator VisualizeWorldLayoutAsync(
        WorldSave save,
        System.Action<float> onProgress = null,
        System.Action<string> onStatus = null)
    {
        if (save == null) yield break;
        SetLiveSave(save); 

        BuildBackgroundPlanes(save);

        if (_worldContainer == null)
        {
            var go = new GameObject("WorldDebug");
            _worldContainer = go.transform;
        }

        onStatus?.Invoke("Drawing roads…");
        yield return DrawRoadNetworkAsync(save, onProgress);

        if (drawSidewalks)
        {
            onStatus?.Invoke("Drawing sidewalks…");
            yield return DrawSidewalksAsync(save);
        }

        if (drawCrosswalks && save.layout != null && save.layout.intersections != null)
        {
            onStatus?.Invoke("Painting crosswalks…");
            yield return DrawCrosswalksAsync(save.layout.intersections);
        }

        if (drawLots && save.layout?.lots != null && save.layout.lots.Count > 0)
        {
            onStatus?.Invoke("Outlining lots…");
            yield return DrawLotsAsync(save.layout.lots);
        }

        onProgress?.Invoke(1f);
    }
    #endregion

    #region Roads / Markings
    IEnumerator DrawRoadNetworkAsync(WorldSave save, System.Action<float> onProgress)
    {
        EnsureGeometry();
        _ = save;

        if (_roadLines.Count == 0)
        {
            onProgress?.Invoke(0.4f);
            yield break;
        }

        const float progressScale = 0.4f;
        float total = _roadLines.Count;
        float done = 0f;

        for (int i = 0; i < _roadLines.Count; i++)
        {
            var line = _roadLines[i];
            Debug.DrawLine(line.start, line.end, line.color);
            done += 1f;

            if ((i & 31) == 31)
            {
                onProgress?.Invoke(done / total * progressScale);
                yield return null;
            }
        }

        onProgress?.Invoke(progressScale);
    }

    IEnumerator DrawSidewalksAsync(WorldSave save)
    {
        EnsureGeometry();
        _ = save;

        if (!drawSidewalks || _sidewalkLines.Count == 0)
            yield break;

        for (int i = 0; i < _sidewalkLines.Count; i++)
        {
            var line = _sidewalkLines[i];
            Debug.DrawLine(line.start, line.end, line.color);

            if (((i + 1) % 32) == 0)
                yield return null;
        }
    }

    #endregion

    #region Intersections & Crosswalks
    IEnumerator DrawCrosswalksAsync(List<IntersectionData> intersections)
    {
        EnsureGeometry();
        _ = intersections;

        if (!drawCrosswalks || _crosswalkLines.Count == 0)
            yield break;

        for (int i = 0; i < _crosswalkLines.Count; i++)
        {
            var line = _crosswalkLines[i];
            Debug.DrawLine(line.start, line.end, line.color);

            if ((i & 15) == 15)
                yield return null;
        }
    }

    #endregion

    #region Lots
    IEnumerator DrawLotsAsync(List<LotData> lots)
    {
        EnsureGeometry();
        _ = lots;

        if (!drawLots || _lotLines.Count == 0)
            yield break;

        for (int i = 0; i < _lotLines.Count; i++)
        {
            var line = _lotLines[i];
            Debug.DrawLine(line.start, line.end, line.color);

            if ((i & 15) == 15)
                yield return null;
        }
    }
    #endregion

    #region Sidewalk Corner Helpers
    private Dictionary<(int index, bool isStart), CornerPoints> EnsureCornerMap()
    {
        if (!_cornerCacheDirty)
            return _cornerCache;

        _cornerCache.Clear();
        if (_saveToVisualize?.roadNetwork == null) { _cornerCacheDirty = false; return _cornerCache; }
        if (_saveToVisualize.layout?.intersections == null) { _cornerCacheDirty = false; return _cornerCache; }

        var roads = _saveToVisualize.roadNetwork;
        var intersections = _saveToVisualize.layout.intersections;
        float sidewalkU = Size.Feet(sidewalkFeet);
        float midpointEpsSq = Size.Feet(0.25f) * Size.Feet(0.25f);
        float connectorMatchSq = Size.Feet(10f) * Size.Feet(10f);
        float interiorThresholdSq = Size.Feet(1f) * Size.Feet(1f);

        for (int interIndex = 0; interIndex < intersections.Count; interIndex++)
        {
            var inter = intersections[interIndex];
            var pts = inter.points;
            var connectors = inter.connectors;
            if (pts == null || connectors == null || pts.Count == 0) continue;

            int vertexCount = pts.Count;
            var outerCorners = PrecomputeOuterCorners(pts, sidewalkU);
            foreach (var connector in connectors)
            {
                int indexA = -1;
                Vector2 cornerA = default;
                Vector2 cornerB = default;
                bool foundCorners = false;

                for (int v = 0; v < vertexCount; v++)
                {
                    Vector2 v0 = pts[v];
                    Vector2 v1 = pts[(v + 1) % vertexCount];
                    Vector2 mid = (v0 + v1) * 0.5f;
                    if ((mid - connector.point).sqrMagnitude <= midpointEpsSq)
                    {
                        indexA = v;
                        cornerA = v0;
                        cornerB = v1;
                        foundCorners = true;
                        break;
                    }
                }

                if (!foundCorners) continue;

                int indexB = (indexA + 1) % vertexCount;

                int bestRoad = -1;
                bool bestIsStart = true;
                float bestDistSq = float.MaxValue;

                for (int r = 0; r < roads.Count; r++)
                {
                    var road = roads[r];
                    float dStart = (road.start - connector.point).sqrMagnitude;
                    if (dStart < bestDistSq && dStart <= connectorMatchSq)
                    {
                        bestDistSq = dStart;
                        bestRoad = r;
                        bestIsStart = true;
                    }

                    float dEnd = (road.end - connector.point).sqrMagnitude;
                    if (dEnd < bestDistSq && dEnd <= connectorMatchSq)
                    {
                        bestDistSq = dEnd;
                        bestRoad = r;
                        bestIsStart = false;
                    }
                }

                if (bestRoad < 0) continue;

                var seg = roads[bestRoad];
                Vector2 endpoint = bestIsStart ? seg.start : seg.end;
                Vector2 dir = seg.end - seg.start;
                if (dir.sqrMagnitude < 1e-6f) continue;
                dir.Normalize();
                Vector2 perp = new Vector2(-dir.y, dir.x);

                float dotA = Vector2.Dot(cornerA - endpoint, perp);
                float dotB = Vector2.Dot(cornerB - endpoint, perp);

                bool useAAsLeft = dotA >= dotB;
                Vector2 innerLeft = useAAsLeft ? cornerA : cornerB;
                Vector2 innerRight = useAAsLeft ? cornerB : cornerA;
                Vector2 outerLeft = useAAsLeft ? outerCorners[indexA] : outerCorners[indexB];
                Vector2 outerRight = useAAsLeft ? outerCorners[indexB] : outerCorners[indexA];

                var key = (bestRoad, bestIsStart);
                var cp = _cornerCache.TryGetValue(key, out var existing) ? existing : default;
                cp.curbLeft = innerLeft;
                cp.curbRight = innerRight;
                cp.outerLeft = outerLeft;
                cp.outerRight = outerRight;
                cp.hasLeft = true;
                cp.hasRight = true;
                cp.isInterior = bestDistSq <= interiorThresholdSq;
                _cornerCache[key] = cp;
            }
        }

        _cornerCacheDirty = false;
        return _cornerCache;
    }

    private static Vector2[] PrecomputeOuterCorners(List<Vector2> pts, float sidewalkU)
    {
        int n = pts.Count;
        var result = new Vector2[n];
        if (n < 3 || sidewalkU <= 1e-4f)
        {
            for (int i = 0; i < n; i++) result[i] = pts[i];
            return result;
        }

        float area = 0f;
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = pts[i];
            Vector2 p1 = pts[(i + 1) % n];
            area += (p0.x * p1.y - p1.x * p0.y);
        }
        bool isCCW = area > 0f;

        var normals = new Vector2[n];
        var ds = new float[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 a = pts[i];
            Vector2 b = pts[(i + 1) % n];
            Vector2 edge = b - a;
            if (edge.sqrMagnitude < 1e-10f)
            {
                normals[i] = Vector2.zero;
                ds[i] = 0f;
                continue;
            }

            Vector2 nrm = isCCW ? new Vector2(edge.y, -edge.x) : new Vector2(-edge.y, edge.x);
            nrm.Normalize();
            normals[i] = nrm;
            ds[i] = Vector2.Dot(nrm, a) + sidewalkU;
        }

        for (int i = 0; i < n; i++)
        {
            var nPrev = normals[(i - 1 + n) % n];
            var nCurr = normals[i];
            float dPrev = ds[(i - 1 + n) % n];
            float dCurr = ds[i];

            if (nPrev == Vector2.zero || nCurr == Vector2.zero)
            {
                Vector2 fallback = nPrev != Vector2.zero ? nPrev : nCurr;
                if (fallback == Vector2.zero) fallback = Vector2.up;
                fallback.Normalize();
                result[i] = pts[i] + fallback * sidewalkU;
                continue;
            }

            float det = nPrev.x * nCurr.y - nPrev.y * nCurr.x;
            if (Mathf.Abs(det) < 1e-6f)
            {
                // nearly parallel — fall back to simple offset along average normal
                Vector2 avg = nPrev + nCurr;
                if (avg.sqrMagnitude < 1e-8f)
                    avg = nCurr != Vector2.zero ? nCurr : nPrev;
                avg.Normalize();
                result[i] = pts[i] + avg * sidewalkU;
                continue;
            }

            float x = (dPrev * nCurr.y - nPrev.y * dCurr) / det;
            float y = (nPrev.x * dCurr - dPrev * nCurr.x) / det;
            result[i] = new Vector2(x, y);
        }

        return result;
    }

    private static bool OverrideCorner(
        Dictionary<(int index, bool isStart), CornerPoints> cornerMap,
        int roadIndex,
        bool isStart,
        ref Vector3 curbLeft,
        ref Vector3 curbRight,
        ref Vector3 sideLeft,
        ref Vector3 sideRight,
        out CornerPoints corners)
    {
        if (!cornerMap.TryGetValue((roadIndex, isStart), out corners))
            return false;

        if (corners.hasLeft)
        {
            var curbWorld = new Vector3(corners.curbLeft.x, curbLeft.y, corners.curbLeft.y);
            var outerWorld = new Vector3(corners.outerLeft.x, sideLeft.y, corners.outerLeft.y);
            curbLeft = curbWorld;
            sideLeft = outerWorld;
        }

        if (corners.hasRight)
        {
            var curbWorld = new Vector3(corners.curbRight.x, curbRight.y, corners.curbRight.y);
            var outerWorld = new Vector3(corners.outerRight.x, sideRight.y, corners.outerRight.y);
            curbRight = curbWorld;
            sideRight = outerWorld;
        }

        return true;
    }
    #endregion
    
    private static void ApplyTiled(Material mat, Texture2D tex, Vector2 planeSizeMeters, float tilesInMeters)
    {
        if (!mat || !tex) return;

        Vector2 scale = new(
            Mathf.Max(1f, planeSizeMeters.x / Mathf.Max(0.0001f, tilesInMeters)),
            Mathf.Max(1f, planeSizeMeters.y / Mathf.Max(0.0001f, tilesInMeters))
        );

        if (mat.HasProperty(BaseMap))
        {
            mat.SetTexture(BaseMap, tex);
            mat.SetTextureScale(BaseMap, scale);
        }
        if (mat.HasProperty(MainTex)) // legacy/Standard
        {
            mat.SetTexture(MainTex, tex);
            mat.SetTextureScale(MainTex, scale);
        }
    }

    private static Texture2D MakeGrass(
        int size,
        float lowFreq, float hiFreq,
        float stripeStrength, float speckleStrength,
        Color dark, Color light)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        const float off1x = 13.73f, off1y = 91.17f;
        const float off2x = 47.91f, off2y = 7.31f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float n1 = Mathf.PerlinNoise(x * lowFreq + off1x, y * lowFreq + off1y);
            float n2 = Mathf.PerlinNoise(x * hiFreq  + off2x, y * hiFreq  + off2y);
            float t  = Mathf.Clamp01(n1 * 0.70f + n2 * 0.30f);

            // faint directional striping
            float s = Mathf.Sin((x * 0.9f + y * 0.25f) * 0.5f) * 0.5f + 0.5f;
            t += (s - 0.5f) * stripeStrength;

            // tiny speckles
            float h = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
            h = h - Mathf.Floor(h);
            t += (h - 0.5f) * speckleStrength;

            t = Mathf.Clamp01(t);
            tex.SetPixel(x, y, Color.Lerp(dark, light, t));
        }

        FinalizeTex(tex);
        return tex;
    }

    private static void FinalizeTex(Texture2D tex)
    {
        tex.wrapMode  = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 8;
        tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
    }
}
