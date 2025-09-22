
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using LifeSim.Units;

/// <summary>
/// Renders world debug visuals (roads, sidewalks, crosswalks, intersections).
/// This version fixes compile errors by:
///  - Using IntersectionData.points (not .polygon)
///  - Adding the missing drawIntersections toggle
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

    [Header("Intersection & Crosswalks")]
    [SerializeField] private bool drawIntersections = true;   // <-- was missing
    [SerializeField] private bool drawCrosswalks = true;
    [SerializeField, Min(0f)] private float crosswalkBandFeet = 8f;   // depth of zebra band along road
    [SerializeField, Min(0f)] private float crosswalkStripeFeet = 2f;
    [SerializeField, Min(0f)] private float crosswalkGapFeet = 2f;
    [SerializeField, Min(0f)] private float crosswalkInsetFeet = 1f;
    [SerializeField] private Color crosswalkColor = Color.white;
    [SerializeField] private Color intersectionOutline = new Color(1f, 1f, 1f, 0.85f);

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
    [SerializeField] private bool drawLots = false;
    [SerializeField] private Color lotColor = new Color(0f, 1f, 0.1f, 0.9f);

    private void Update()
    {
        if (_saveToVisualize == null) return;

        // ---- Intersections outline ----
        if (_saveToVisualize.layout?.intersections != null)
        {
            foreach (var inter in _saveToVisualize.layout.intersections)
            {
                var pts = inter.points;
                if (pts == null || pts.Count < 3) continue;
                for (int i = 0; i < pts.Count; i++)
                {
                    var p0 = pts[i];
                    var p1 = pts[(i + 1) % pts.Count];
                    var a = new Vector3(p0.x, parkYOffset + markingLift, p0.y);
                    var b = new Vector3(p1.x, parkYOffset + markingLift, p1.y);
                    Debug.DrawLine(a, b, intersectionOutline);
                }

                // Crosswalks (re-draw every frame)
                if (drawCrosswalks && inter.connectors != null)
                {
                    foreach (var c in inter.connectors)
                        DrawCrosswalk(c);
                }
            }
        }

        // ---- Roads + sidewalks ----
        if (_saveToVisualize.roadNetwork != null)
        {
            float dashU = Size.Feet(dashFeet);
            float gapU  = Size.Feet(gapFeet);
            float sidewalkU = Size.Feet(sidewalkFeet);

            foreach (var road in _saveToVisualize.roadNetwork)
            {
                Vector3 a = new Vector3(road.start.x, parkYOffset + markingLift, road.start.y);
                Vector3 b = new Vector3(road.end.x,   parkYOffset + markingLift, road.end.y);

                // Build perpendicular
                Vector3 v = b - a;
                Vector2 dir2 = new Vector2(v.x, v.z);
                if (dir2.sqrMagnitude < 1e-6f) continue;
                dir2.Normalize();
                Vector2 perp2 = new Vector2(-dir2.y, dir2.x);
                Vector3 perp  = new Vector3(perp2.x, 0f, perp2.y);

                // Centerlines / lane markings
                if (road.type == RoadType.Arterial)
                {
                    float halfGap = Size.Feet(doubleYellowGapFeet) * 0.5f;
                    DrawSolidOffset(a, b,  perp * (+halfGap), centerlineYellow, markingLift);
                    DrawSolidOffset(a, b,  perp * (-halfGap), centerlineYellow, markingLift);
                }
                else
                {
                    DrawDashedOffset(a, b, Vector3.zero, laneDashWhite, dashU, gapU, markingLift);
                }

                // Sidewalk curb & outer edge
                if (drawSidewalks)
                {
                    float halfRoad = road.width * 0.5f;
                    Vector3 curbOff = perp * halfRoad;
                    Vector3 sideOff = perp * (halfRoad + sidewalkU);
                    Debug.DrawLine(a + curbOff + Vector3.up * edgeLift, b + curbOff + Vector3.up * edgeLift, curbEdgeColor);
                    Debug.DrawLine(a - curbOff + Vector3.up * edgeLift, b - curbOff + Vector3.up * edgeLift, curbEdgeColor);
                    Debug.DrawLine(a + sideOff + Vector3.up * edgeLift, b + sideOff + Vector3.up * edgeLift, sidewalkEdgeColor);
                    Debug.DrawLine(a - sideOff + Vector3.up * edgeLift, b - sideOff + Vector3.up * edgeLift, sidewalkEdgeColor);
                }
            }
        }
    }

    // Cache the save so we can redraw debug lines every frame
    private WorldSave _saveToVisualize;
    public  void     SetLiveSave(WorldSave save) { _saveToVisualize = save; }

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

    private GameObject CreatePlane(string name, Rect bounds, Material material, float yOffset, bool isPark)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = name;
        go.transform.SetParent(_worldContainer, worldPositionStays: false);

        // Unity Plane is 10x10 units
        go.transform.position = new Vector3(bounds.center.x, yOffset, bounds.center.y);
        go.transform.localScale = new Vector3(bounds.width * 0.1f, 1f, bounds.height * 0.1f);

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
            CreatePlane("World Plane", save.layout.worldBounds, worldMaterial, 0f, false);
        if (parkMaterial)
            CreatePlane("Center Park", save.layout.centerParkBounds, parkMaterial, parkYOffset, true);
    }

    #region Entry Points
    public void VisualizeWorldLayout(WorldSave save)
    {
        _saveToVisualize = save;
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
        _saveToVisualize = save; 

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

        if (save.layout != null && save.layout.intersections != null)
        {
            onStatus?.Invoke("Drawing intersections…");
            yield return DrawIntersectionsAsync(save.layout.intersections);
        }

        if (drawCrosswalks && save.layout != null && save.layout.intersections != null)
        {
            onStatus?.Invoke("Painting crosswalks…");
            yield return DrawCrosswalksAsync(save.layout.intersections);
        }

        if (drawLots && save.layout?.lots != null)
        {
            onStatus?.Invoke("Outlining lots…");
            foreach (var lot in save.layout.lots)
            {
                DrawRect(lot.bounds, lotColor, parkYOffset + 0.01f);
                yield return null;
            }
        }

        onProgress?.Invoke(1f);
    }
    #endregion

    #region Roads / Markings
    IEnumerator DrawRoadNetworkAsync(WorldSave save, System.Action<float> onProgress)
    {
        if (save.roadNetwork == null || save.roadNetwork.Count == 0) yield break;

        float total = save.roadNetwork.Count;
        float done = 0f;

        float dashU = Size.Feet(dashFeet);
        float gapU = Size.Feet(gapFeet);

        foreach (var road in save.roadNetwork)
        {
            // world-space coords (XZ plane)
            Vector3 a = new Vector3(road.start.x, parkYOffset, road.start.y);
            Vector3 b = new Vector3(road.end.x, parkYOffset, road.end.y);

            // center dashes or double yellow
            Vector3 dir3 = (b - a);
            Vector2 dir2 = new Vector2(dir3.x, dir3.z);
            if (dir2.sqrMagnitude < 1e-6f) { done += 1f; continue; }
            dir2.Normalize();
            Vector2 perp2 = new Vector2(-dir2.y, dir2.x);
            Vector3 perp = new Vector3(perp2.x, 0f, perp2.y);

            if (road.type == RoadType.Arterial)
            {
                // double yellow (two solid lines offset from center)
                float halfGap = Size.Feet(doubleYellowGapFeet) * 0.5f;
                DrawSolidOffset(a, b, perp * (+halfGap), centerlineYellow, markingLift);
                DrawSolidOffset(a, b, perp * (-halfGap), centerlineYellow, markingLift);
            }
            else
            {
                // dashed white center
                DrawDashedOffset(a, b, Vector3.zero, laneDashWhite, dashU, gapU, markingLift);
            }

            done += 1f;
            if ((int)done % 16 == 0)
            {
                onProgress?.Invoke(done / total * 0.4f);
                yield return null;
            }
        }
        onProgress?.Invoke(0.4f);
    }

    IEnumerator DrawSidewalksAsync(WorldSave save)
    {
        float sidewalkU = Size.Feet(sidewalkFeet);

        int i = 0;
        foreach (var road in save.roadNetwork)
        {
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

            // curbs
            Debug.DrawLine(a + curbOff + Vector3.up * edgeLift, b + curbOff + Vector3.up * edgeLift, curbEdgeColor);
            Debug.DrawLine(a - curbOff + Vector3.up * edgeLift, b - curbOff + Vector3.up * edgeLift, curbEdgeColor);

            // sidewalk outer edges
            Debug.DrawLine(a + sideOff + Vector3.up * edgeLift, b + sideOff + Vector3.up * edgeLift, sidewalkEdgeColor);
            Debug.DrawLine(a - sideOff + Vector3.up * edgeLift, b - sideOff + Vector3.up * edgeLift, sidewalkEdgeColor);

            if ((++i % 24) == 0) yield return null;
        }
    }

    void DrawSolidOffset(Vector3 start, Vector3 end, Vector3 offset, Color color, float lift)
    {
        Debug.DrawLine(start + offset + Vector3.up * lift, end + offset + Vector3.up * lift, color);
    }

    void DrawDashedOffset(Vector3 start, Vector3 end, Vector3 offset, Color color, float dashU, float gapU, float lift)
    {
        Vector3 a = start + offset + Vector3.up * lift;
        Vector3 b = end + offset + Vector3.up * lift;
        float len = Vector3.Distance(a, b);
        if (len < 1e-4f) return;

        Vector3 dir = (b - a).normalized;
        float t = 0f;
        while (t < len)
        {
            float seg = Mathf.Min(dashU, len - t);
            Vector3 s = a + dir * t;
            Vector3 e = s + dir * seg;
            Debug.DrawLine(s, e, color);
            t += dashU + gapU;
        }
    }
    #endregion

    #region Intersections & Crosswalks
    IEnumerator DrawIntersectionsAsync(List<IntersectionData> intersections)
    {
        if (!drawIntersections) yield break;
        if (intersections == null) yield break;

        int i = 0;
        foreach (var inter in intersections)
        {
            var pts = inter.points; // <-- property name fix
            if (pts == null || pts.Count < 3) continue;

            float y = parkYOffset + 0.02f;
            for (int k = 0; k < pts.Count; k++)
            {
                Vector2 p0 = pts[k];
                Vector2 p1 = pts[(k + 1) % pts.Count];
                Debug.DrawLine(new Vector3(p0.x, y, p0.y), new Vector3(p1.x, y, p1.y), intersectionOutline);
            }

            if ((++i % 12) == 0) yield return null;
        }
    }

    IEnumerator DrawCrosswalksAsync(List<IntersectionData> intersections)
    {
        int i = 0;
        foreach (var inter in intersections)
        {
            if (inter.connectors == null) continue;
            foreach (var c in inter.connectors)
            {
                DrawCrosswalk(c);
                if ((++i % 8) == 0) yield return null;
            }
        }
    }

    void DrawCrosswalk(IntersectionConnector c)
    {
        // units
        float bandU = Size.Feet(crosswalkBandFeet);
        float stripeU = Size.Feet(crosswalkStripeFeet);
        float gapU = Size.Feet(crosswalkGapFeet);
        float insetU = Size.Feet(crosswalkInsetFeet);

        // road basis
        Vector2 n2 = c.normal; n2.Normalize();               // outward from intersection
        Vector2 t2 = new Vector2(-n2.y, n2.x);               // along-road tangent
        Vector3 perp = new Vector3(t2.x, 0f, t2.y);

        // curb-to-curb approximate width (use connector width)
        float halfRoad = c.width * 0.5f;

        // start line position just beyond the polygon edge (we just offset from connector point)
        Vector3 basePos = new Vector3(c.point.x + n2.x * insetU, parkYOffset + markingLift, c.point.y + n2.y * insetU);

        // lay stripes along the band (perpendicular to road direction)
        float placed = 0f;
        while (placed < bandU - 1e-3f)
        {
            float seg = Mathf.Min(stripeU, bandU - placed);
            Vector3 mid = basePos + new Vector3(n2.x, 0f, n2.y) * (placed + seg * 0.5f);

            Vector3 a = mid - perp * halfRoad;
            Vector3 b = mid + perp * halfRoad;

            Debug.DrawLine(a, b, crosswalkColor);
            placed += stripeU + gapU;
        }
    }
    #endregion

    #region Lots helper
    private void DrawRect(Rect rect, Color color, float y)
    {
        Vector3 p1 = new Vector3(rect.xMin, y, rect.yMin);
        Vector3 p2 = new Vector3(rect.xMax, y, rect.yMin);
        Vector3 p3 = new Vector3(rect.xMax, y, rect.yMax);
        Vector3 p4 = new Vector3(rect.xMin, y, rect.yMax);
        Debug.DrawLine(p1, p2, color);
        Debug.DrawLine(p2, p3, color);
        Debug.DrawLine(p3, p4, color);
        Debug.DrawLine(p4, p1, color);
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
