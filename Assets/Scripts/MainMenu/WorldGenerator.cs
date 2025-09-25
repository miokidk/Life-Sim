using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using LifeSim.Units;

public static class WorldGenerator
{
    // A temporary class to group intersecting road segments during generation.
    private class TempIntersection
    {
        public Vector2 center;
        public HashSet<int> segmentIndices = new HashSet<int>();

        // Adds a segment to this intersection group and updates the center point.
        public void AddSegment(int index, Vector2 point)
        {
            // Update center with a running average to keep it centered on all discovered crossing points
            if (segmentIndices.Count > 0)
            {
                center = (center * segmentIndices.Count + point) / (segmentIndices.Count + 1);
            }
            else
            {
                center = point;
            }
            segmentIndices.Add(index);
        }
    }
    
    public static IEnumerator GenerateAsync(
        WorldSave save,
        Vector2 worldSize,
        Vector2 parkSize,
        int mainCount,
        int sideCount,
        int extraCount,
        System.Action<float> onProgress = null,
        System.Action<string> onStatus = null)
{
        // Local helpers for UI without forcing callers to wire delegates
        void prog(float v) { onProgress?.Invoke(v); LoadingUI.SetProgressStatic(v); }
        void stat(string s) { onStatus?.Invoke(s); LoadingUI.SetStatusStatic(s); }
        // Kick the canvas a frame early
        prog(0f);
        stat("Preparing world…");
        yield return null;

        prog(0f);
        if (save == null) yield break;
        if (save.roadNetwork == null) save.roadNetwork = new List<RoadSegment>();
        else save.roadNetwork.Clear();
        save.layout = new WorldLayoutData();
        save.layout.intersections = new List<IntersectionData>(); // Initialize list

        // ---------- Dials ----------
        stat("Setting up dials…");
        float gridEdgePadding        = 60f;     // absolute buffer to world edge
        float gridEdgePaddingPercent = 0.10f;   // % of min(world W/H)

        int   patchesX       = 3;
        int   patchesY       = 3;
        float seedJitter     = 0.33f;
        int   angleStepDeg   = 15;
        Vector2 cellWRange   = new Vector2(80f, 180f);
        Vector2 cellHRange   = new Vector2(80f, 180f);

        // Arterials
        Vector2 arterialEveryRange = new Vector2(3, 5); // int inclusive

        // Lanes (imperial → Unity units)
        float localLaneFt        = 11f;   // 1 lane each way
        int   localLanesPerDir   = 1;
        float arterialLaneFt     = 12f;   // 2 lanes each way
        int   arterialLanesPerDir= 2;

        // curb-to-curb pavement widths (no sidewalks here)
        float localWidth    = Size.Feet(2 * localLanesPerDir * localLaneFt);       // 22 ft
        float arterialWidth = Size.Feet(2 * arterialLanesPerDir * arterialLaneFt); // 48 ft

        // ==== Sidewalk & clearance (used to expand intersections so sidewalks don't collide) ====
        // Keep sidewalkFt in sync with the visualizer's sidewalk setting.
        float sidewalkFt   = 6f;                      // per side
        float clearanceFt  = 1.5f;                    // small fudge so curbs/stripes don't kiss
        float expandU      = Size.Feet(sidewalkFt + clearanceFt);

        // OUTWARD-CORNER connectors (one per corner)
        float minConnectorSpacing = 40f;    // min distance between connector start/end points
        float connectorMin        = 10.0f;  // avoid tiny stubs
        float baseConnectorMax    = 60f;    // base reach
        float maxConnectorScale   = 5.0f;   // adaptive reach: typicalCell*scale
        float connectorHitBias    = 0.001f; // tiny epsilon for ray tests

        // EDGE (stripe) connectors (periodic, only if stripe has none)
        int   edgeEveryCells               = 3;              // “every few cells”
        float edgeStripeHasConnectorDist   = 0.8f;           // in *typical cell* units, see below

        // Fray outer ends with extreme length variation
        float   frayDetectRadius = 25f;
        Vector2 frayLengthRange  = new Vector2(-80f, 400f);
        
        // ---------- Bounds ----------
        stat("Laying out world & park…");
        yield return null;
        Rect W = RectFromSizeCentered(worldSize);
        save.layout.worldBounds = W;

        Rect P = RectFromSizeCentered(parkSize);
        save.layout.centerParkBounds = P;

        float pctPad = gridEdgePaddingPercent > 0f ? Mathf.Min(W.width, W.height) * gridEdgePaddingPercent : 0f;
        float pad    = Mathf.Max(gridEdgePadding, pctPad);
        pad = Mathf.Clamp(pad, 10f, Mathf.Min(W.width, W.height) * 0.45f);

        Rect R = new Rect(W.xMin + pad, W.yMin + pad, W.width - 2f * pad, W.height - 2f * pad);

        // ---------- Helpers ----------
        static Rect RectFromSizeCentered(Vector2 s)
        {
            float hx = s.x * 0.5f, hy = s.y * 0.5f;
            return new Rect(-hx, -hy, s.x, s.y);
        }
        
        var all = new List<TempSeg>(26000);
        #region Unchanged Generation Steps
        var edgeKeys = new HashSet<ulong>();
        void AddUniqueSeg(List<TempSeg> list, Vector2 a, Vector2 b, float w, RoadType t, int patchId) { if ((a - b).sqrMagnitude < 0.01f) return; if (!SegmentAABBClip(R, a, b, out var ca, out var cb)) return; ulong k = EdgeKey(ca, cb, t); if (edgeKeys.Add(k)) list.Add(new TempSeg { a = ca, b = cb, width = w, type = t, patchId = patchId }); }
        var patches = new List<Patch>();
        float stepX = R.width / patchesX; float stepY = R.height / patchesY; for (int py = 0; py < patchesY; py++) for (int px = 0; px < patchesX; px++) { Vector2 cBase = new Vector2(R.xMin + (px + 0.5f) * stepX, R.yMin + (py + 0.5f) * stepY); float jx = (Random.value * 2f - 1f) * seedJitter * stepX; float jy = (Random.value * 2f - 1f) * seedJitter * stepY; Vector2 center = cBase + new Vector2(jx, jy); float ang = Mathf.Round(Random.Range(0f, 180f) / angleStepDeg) * angleStepDeg; int every = Mathf.Clamp(Mathf.RoundToInt(Random.Range(arterialEveryRange.x, arterialEveryRange.y)), 2, 7); int arterialPhase = Random.Range(0, every); var patch = new Patch { id = patches.Count, center = center, angleDeg = ang, arterialEvery = every, arterialPhase = arterialPhase, uLines = new List<float>(64), vLines = new List<float>(64) }; BuildLocalBounds(patch, R, out float uMin, out float uMax, out float vMin, out float vMax); float currentU = uMin - Random.Range(0f, cellWRange.y); while(currentU < uMax) { patch.uLines.Add(currentU); currentU += Random.Range(cellWRange.x, cellWRange.y); } patch.uLines.Add(currentU); float currentV = vMin - Random.Range(0f, cellHRange.y); while (currentV < vMax) { patch.vLines.Add(currentV); currentV += Random.Range(cellHRange.x, cellHRange.y); } patch.vLines.Add(currentV); patches.Add(patch); }
        float avgCellW = (cellWRange.x + cellWRange.y) * 0.5f; float avgCellH = (cellHRange.x + cellHRange.y) * 0.5f; float typicalCell = Mathf.Max(10f, Mathf.Min(avgCellW, avgCellH)); float connectorMax = Mathf.Max(baseConnectorMax, typicalCell * maxConnectorScale); float edgeNearDist = Mathf.Max(2f, typicalCell * edgeStripeHasConnectorDist); int NearestPatchId(Vector2 p) { int best = -1; float bestD = float.MaxValue; for (int i = 0; i < patches.Count; i++) { float d = (p - patches[i].center).sqrMagnitude; if (d < bestD) { bestD = d; best = i; } } return best; }
        var outwardCorners = new List<CornerSample>(6000); var stripes = new Dictionary<StripeKey, List<EdgeSample>>(512); for (int pidx = 0; pidx < patches.Count; pidx++) { var pa = patches[pidx]; int nu = pa.uLines.Count > 1 ? pa.uLines.Count - 1 : 0; int nv = pa.vLines.Count > 1 ? pa.vLines.Count - 1 : 0; if (nu == 0 || nv == 0) continue; var valid = new bool[nu, nv]; for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++) { float uL = pa.uLines[i]; float uR = pa.uLines[i+1]; float vB = pa.vLines[j]; float vT = pa.vLines[j+1]; Vector2 c = LocalToWorld(pa, (uL + uR) * 0.5f, (vB + vT) * 0.5f); float epsU = (uR - uL) * 0.02f; float epsV = (vT - vB) * 0.02f; Vector2 p00 = LocalToWorld(pa, uL + epsU, vB + epsV); Vector2 p10 = LocalToWorld(pa, uR - epsU, vB + epsV); Vector2 p01 = LocalToWorld(pa, uL + epsU, vT - epsV); Vector2 p11 = LocalToWorld(pa, uR - epsU, vT - epsV); bool inside = InRect(R, c) && NearestPatchId(c) == pa.id && InRect(R, p00) && NearestPatchId(p00) == pa.id && InRect(R, p10) && NearestPatchId(p10) == pa.id && InRect(R, p01) && NearestPatchId(p01) == pa.id && InRect(R, p11) && NearestPatchId(p11) == pa.id; valid[i, j] = inside; } for (int j = 0; j < nv; j++) for (int i = 0; i < nu; i++) { if (!valid[i, j]) continue; float uL = pa.uLines[i]; float uR = pa.uLines[i+1]; float vB = pa.vLines[j]; float vT = pa.vLines[j+1]; Vector2 w00 = LocalToWorld(pa, uL, vB); Vector2 w10 = LocalToWorld(pa, uR, vB); Vector2 w01 = LocalToWorld(pa, uL, vT); Vector2 w11 = LocalToWorld(pa, uR, vT); Vector2 c = LocalToWorld(pa, (uL + uR) * 0.5f, (vB + vT) * 0.5f); bool IsArtV(int k) => (((k + pa.arterialPhase) % pa.arterialEvery) == 0); bool IsArtH(int k) => (((k + pa.arterialPhase) % pa.arterialEvery) == 0); bool leftMissing = (i - 1 < 0) || !valid[i - 1, j]; bool rightMissing = (i + 1 >= nu) || !valid[i + 1, j]; bool bottomMissing = (j - 1 < 0) || !valid[i, j - 1]; bool topMissing = (j + 1 >= nv) || !valid[i, j + 1]; { bool art = IsArtV(i); AddUniqueSeg(all, w00, w01, art ? arterialWidth : localWidth, art ? RoadType.Arterial : RoadType.Local, pa.id); } { bool art = IsArtH(j); AddUniqueSeg(all, w00, w10, art ? arterialWidth : localWidth, art ? RoadType.Arterial : RoadType.Local, pa.id); } if (rightMissing) { bool art = IsArtV(i + 1); AddUniqueSeg(all, w10, w11, art ? arterialWidth : localWidth, art ? RoadType.Arterial : RoadType.Local, pa.id); var m = (w10 + w11) * 0.5f; var n = (m - c).normalized; var key = new StripeKey(pa.id, StripeDir.Right, i + 1); if (!stripes.TryGetValue(key, out var list)) stripes[key] = list = new List<EdgeSample>(16); list.Add(new EdgeSample { pos = m, normal = n, patchId = pa.id, param = j }); } if (topMissing) { bool art = IsArtH(j + 1); AddUniqueSeg(all, w01, w11, art ? arterialWidth : localWidth, art ? RoadType.Arterial : RoadType.Local, pa.id); var m = (w01 + w11) * 0.5f; var n = (m - c).normalized; var key = new StripeKey(pa.id, StripeDir.Top, j + 1); if (!stripes.TryGetValue(key, out var list)) stripes[key] = list = new List<EdgeSample>(16); list.Add(new EdgeSample { pos = m, normal = n, patchId = pa.id, param = i }); } if (rightMissing && topMissing) outwardCorners.Add(new CornerSample { pos = w11, normal = (w11 - c).normalized, patchId = pa.id }); if (rightMissing && bottomMissing) outwardCorners.Add(new CornerSample { pos = w10, normal = (w10 - c).normalized, patchId = pa.id }); if (leftMissing && topMissing) outwardCorners.Add(new CornerSample { pos = w01, normal = (w01 - c).normalized, patchId = pa.id }); if (leftMissing && bottomMissing) outwardCorners.Add(new CornerSample { pos = w00, normal = (w00 - c).normalized, patchId = pa.id }); } }
        var connectorAnchors = new List<Vector2>(4096); var connectorEndpoints = new List<Vector2>(4096); var conKeys = new HashSet<ulong>(); float minConnectorSpacingSq = minConnectorSpacing * minConnectorSpacing; for (int i = 0; i < outwardCorners.Count; i++) { var sp = outwardCorners[i]; if (!InRect(R, sp.pos) || DistToRectEdge(R, sp.pos) < 0.2f) continue; float bestT = float.PositiveInfinity; Vector2 bestHit = Vector2.zero; int bestHitSegIndex = -1; for (int j = 0; j < all.Count; j++) { int otherPatch = all[j].patchId; if (otherPatch == sp.patchId || otherPatch < 0) continue; if (RaySegment(sp.pos, sp.normal, all[j].a, all[j].b, out float t, out Vector2 hit)) { if (t >= connectorMin && t <= connectorMax + connectorHitBias && InRect(R, hit)) if (t < bestT) { bestT = t; bestHit = hit; bestHitSegIndex = j; } } } if (!float.IsFinite(bestT)) { float bestD = connectorMax + 1f; for (int j = 0; j < all.Count; j++) { int otherPatch = all[j].patchId; if (otherPatch == sp.patchId || otherPatch < 0) continue; Vector2 proj = ProjectPointOnSegment(sp.pos, all[j].a, all[j].b, out float t01); if (t01 < 0f || t01 > 1f) continue; float d = Vector2.Distance(sp.pos, proj); if (d >= connectorMin && d <= connectorMax && InRect(R, proj)) if (d < bestD) { bestD = d; bestHit = proj; bestHitSegIndex = j; } } if (bestD <= connectorMax) bestT = bestD; } if (float.IsFinite(bestT)) { bool startTooClose = connectorAnchors.Any(a => (sp.pos - a).sqrMagnitude < minConnectorSpacingSq) || connectorEndpoints.Any(e => (sp.pos - e).sqrMagnitude < minConnectorSpacingSq); bool endTooClose = connectorEndpoints.Any(e => (bestHit - e).sqrMagnitude < minConnectorSpacingSq) || connectorAnchors.Any(a => (bestHit - a).sqrMagnitude < minConnectorSpacingSq); if (!startTooClose && !endTooClose) { bool intersects = false; for (int k = 0; k < all.Count; k++) { if (k == bestHitSegIndex) continue; if (SegmentsIntersect(sp.pos, bestHit, all[k].a, all[k].b)) { intersects = true; break; } } if (!intersects) { ulong key = EdgeKey(sp.pos, bestHit, RoadType.Local); if (conKeys.Add(key)) { AddUniqueSeg(all, sp.pos, bestHit, localWidth, RoadType.Local, -1); connectorAnchors.Add(sp.pos); connectorEndpoints.Add(bestHit); } } } } }
        foreach (var kv in stripes) { var samples = kv.Value; if (samples.Count == 0) continue; bool hasAny = samples.Any(s => connectorAnchors.Any(a => (a - s.pos).sqrMagnitude <= edgeNearDist * edgeNearDist)); if (hasAny) continue; samples.Sort((a, b) => a.param.CompareTo(b.param)); for (int si = edgeEveryCells / 2; si < samples.Count; si += edgeEveryCells) { var sp = samples[si]; float bestT = float.PositiveInfinity; Vector2 bestHit = Vector2.zero; int bestHitSegIndex = -1; for (int j = 0; j < all.Count; j++) { int otherPatch = all[j].patchId; if (otherPatch == sp.patchId || otherPatch < 0) continue; if (RaySegment(sp.pos, sp.normal, all[j].a, all[j].b, out float t, out Vector2 hit)) { if (t >= connectorMin && t <= connectorMax + connectorHitBias && InRect(R, hit)) if (t < bestT) { bestT = t; bestHit = hit; bestHitSegIndex = j; } } } if (!float.IsFinite(bestT)) { float bestD = connectorMax + 1f; for (int j = 0; j < all.Count; j++) { int otherPatch = all[j].patchId; if (otherPatch == sp.patchId || otherPatch < 0) continue; Vector2 proj = ProjectPointOnSegment(sp.pos, all[j].a, all[j].b, out float t01); if (t01 < 0f || t01 > 1f) continue; float d = Vector2.Distance(sp.pos, proj); if (d >= connectorMin && d <= connectorMax && InRect(R, proj)) if (d < bestD) { bestD = d; bestHit = proj; bestHitSegIndex = j; } } if (bestD <= connectorMax) bestT = bestD; } if (float.IsFinite(bestT)) { bool startTooClose = connectorAnchors.Any(a => (sp.pos - a).sqrMagnitude < minConnectorSpacingSq) || connectorEndpoints.Any(e => (sp.pos - e).sqrMagnitude < minConnectorSpacingSq); bool endTooClose = connectorEndpoints.Any(e => (bestHit - e).sqrMagnitude < minConnectorSpacingSq) || connectorAnchors.Any(a => (bestHit - a).sqrMagnitude < minConnectorSpacingSq); if (!startTooClose && !endTooClose) { bool intersects = false; for (int k = 0; k < all.Count; k++) { if (k == bestHitSegIndex) continue; if (SegmentsIntersect(sp.pos, bestHit, all[k].a, all[k].b)) { intersects = true; break; } } if (!intersects) { ulong key = EdgeKey(sp.pos, bestHit, RoadType.Local); if (conKeys.Add(key)) { AddUniqueSeg(all, sp.pos, bestHit, localWidth, RoadType.Local, -1); connectorAnchors.Add(sp.pos); connectorEndpoints.Add(bestHit); } } } } } }
        for (int i = 0; i < all.Count; i++) {
                if ((i & 31) == 0) { yield return null; } var s = all[i]; Vector2 a = s.a; Vector2 b = s.b; float segmentLength = (a - b).magnitude; if (segmentLength < 1f) continue; float da = DistToRectEdge(R, a); float db = DistToRectEdge(R, b); bool aIsOuter = da <= frayDetectRadius && da < db; bool bIsOuter = db <= frayDetectRadius && db < da; if (da <= frayDetectRadius && db <= frayDetectRadius) { float pullBackRatio = Random.Range(0.3f, 0.6f); s.a = Vector2.Lerp(a, b, pullBackRatio); s.b = Vector2.Lerp(b, a, pullBackRatio); all[i] = s; continue; } if (aIsOuter) { Vector2 dir = (a - b).normalized; float minLen = -(segmentLength * 0.95f); float len = Random.Range(Mathf.Max(frayLengthRange.x, minLen), frayLengthRange.y); s.a = a + dir * len; all[i] = s; } else if (bIsOuter) { Vector2 dir = (b - a).normalized; float minLen = -(segmentLength * 0.95f); float len = Random.Range(Mathf.Max(frayLengthRange.x, minLen), frayLengthRange.y); s.b = b + dir * len; all[i] = s; } }
        #endregion

        // ---------- Generate Intersections and Trim Roads ----------
        stat("Building roads & intersections…");
        yield return null;
        {
            var intersections = new List<TempIntersection>();

            float mergeDist   = Mathf.Max(arterialWidth, localWidth) + 2f * expandU;
            float mergeDistSq = mergeDist * mergeDist;

            // 1) Find & group (using a responsive brute-force approach)
            int pairsProcessed = 0;
            for (int i = 0; i < all.Count; i++)
            {
                // Yield periodically in the outer loop to keep things moving.
                if ((i & 31) == 0) {
                    yield return null;
                }

                for (int j = i + 1; j < all.Count; j++)
                {
                    // For very dense road networks, we can also yield here, but less often.
                    if ((pairsProcessed++ & 4095) == 0)
                    {
                        yield return null;
                    }

                    var si = all[i];
                    var sj = all[j];

                    // Check for intersection
                    if (!SegmentIntersectionAny(si.a, si.b, sj.a, sj.b, out _, out _, out Vector2 p))
                        continue;

                    // Skip nearly parallel roads to avoid degenerate intersections
                    Vector2 di = (si.b - si.a).normalized;
                    Vector2 dj = (sj.b - sj.a).normalized;
                    if (Mathf.Abs(Vector2.Dot(di, dj)) > 0.995f) continue;

                    // Find an existing intersection group to merge with
                    TempIntersection match = null;
                    foreach (var g in intersections)
                    {
                        if ((g.center - p).sqrMagnitude < mergeDistSq)
                        {
                            match = g;
                            break;
                        }
                    }

                    // If no nearby group is found, create a new one
                    if (match == null)
                    {
                        match = new TempIntersection();
                        intersections.Add(match);
                    }

                    // Add both segments to the intersection group
                    match.AddSegment(i, p);
                    match.AddSegment(j, p);
                }
            }

            var newEndpoints = new Dictionary<(int segIndex, bool isA), Vector2>();

            // 2) Build polygons, trim roads, and CAPTURE CONNECTORS
            int gi = 0;
            foreach (var group in intersections)
            {
                if ((gi++ & 7) == 0) { yield return null; }

                if (group.segmentIndices.Count < 2) continue;

                var center = group.center;
                var conns = new List<IntersectionConnection>();

                foreach (int si in group.segmentIndices)
                {
                    var s = all[si];
                    float dA = (center - s.a).sqrMagnitude;
                    float dB = (center - s.b).sqrMagnitude;
                    Vector2 entry = (dA < dB) ? s.a : s.b;
                    Vector2 other = (dA < dB) ? s.b : s.a;

                    conns.Add(new IntersectionConnection {
                        segIndex = si,
                        entryPoint = entry,
                        dir = (entry - other).normalized, // toward center
                        width = s.width,
                        type = s.type
                    });
                }

                conns.Sort((a,b)=> Mathf.Atan2(a.dir.y,a.dir.x).CompareTo(Mathf.Atan2(b.dir.y,b.dir.x)));

                var trimPoly = new List<Vector2>(conns.Count);
                var visPoly  = new List<Vector2>(conns.Count);

                for (int k = 0; k < conns.Count; k++)
                {
                    if ((k & 31) == 0) { yield return null; }

                    var c1 = conns[k];
                    var c2 = conns[(k + 1) % conns.Count];

                    Vector2 dir1 = c1.dir, dir2 = c2.dir;
                    Vector2 n1 = new Vector2(dir1.y, -dir1.x);
                    Vector2 n2 = new Vector2(-dir2.y, dir2.x);

                    // curb + clearance (trim)
                    Vector2 e1t = c1.entryPoint + n1 * (c1.width * 0.5f + expandU);
                    Vector2 e2t = c2.entryPoint + n2 * (c2.width * 0.5f + expandU);

                    // curb line + small setback (visual intersection)
                    float curbSetbackFeet = 0.50f;                 // tweak 0–1 ft
                    float curbSetbackU    = Size.Feet(curbSetbackFeet);
                    Vector2 e1v = c1.entryPoint + n1 * (c1.width * 0.5f + curbSetbackU);
                    Vector2 e2v = c2.entryPoint + n2 * (c2.width * 0.5f + curbSetbackU);

                    // build both polygons
                    if (LineLineIntersection(e1t, dir1, e2t, dir2, out var vTrim))
                        trimPoly.Add(vTrim);
                    else
                        trimPoly.Add((e1t + e2t) * 0.5f);

                    if (LineLineIntersection(e1v, dir1, e2v, dir2, out var vVis))
                        visPoly.Add(vVis);
                    else
                        visPoly.Add((e1v + e2v) * 0.5f);
                }

                if (trimPoly.Count < 3 || visPoly.Count < 3) continue;

                var connectors = new List<IntersectionConnector>(conns.Count);

                for (int k = 0; k < conns.Count; k++)
                {
                    if ((k & 31) == 0) { yield return null; }

                    int prev = (k == 0) ? conns.Count - 1 : k - 1;

                    // midpoint of TRIM poly for road endpoints
                    Vector2 trimMid = (trimPoly[prev] + trimPoly[k]) * 0.5f;
                    var conn = conns[k];
                    var seg  = all[conn.segIndex];
                    bool isA = (conn.entryPoint - seg.a).sqrMagnitude < (conn.entryPoint - seg.b).sqrMagnitude;
                    newEndpoints[(conn.segIndex, isA)] = trimMid;

                    // midpoint of VIS poly for connector (sidewalk edge)
                    Vector2 visMid = (visPoly[prev] + visPoly[k]) * 0.5f;

                    connectors.Add(new IntersectionConnector {
                        point  = visMid,
                        normal = -conn.dir,
                        width  = conn.width
                    });
                }

                // save VIS polygon
                save.layout.intersections.Add(new IntersectionData {
                    points = visPoly,
                    connectors = connectors
                });
            }

            // 3) Apply trims
            int ep = 0;
            foreach (var kv in newEndpoints)
            {
                if ((ep++ & 63) == 0) { yield return null; }
                var s = all[kv.Key.segIndex];
                if (kv.Key.isA) s.a = kv.Value; else s.b = kv.Value;
                all[kv.Key.segIndex] = s;
            }
        }


        // ---------- Emit ----------
        stat("Finalizing world data…");
        yield return null;
        foreach (var s in all)
        {
            if ((s.a - s.b).sqrMagnitude < 0.25f) continue;
            save.roadNetwork.Add(new RoadSegment { start = s.a, end = s.b, width = s.width, type = s.type });
        }

        // ---------- Characters ----------
        stat("Generating characters…");
        onProgress?.Invoke(0.9f);
        int totalChars = mainCount + sideCount + extraCount;
        int done = 0;
        System.Action bump = () => { done++; onProgress?.Invoke(Mathf.Lerp(0.9f, 1f, totalChars == 0 ? 1f : (done / (float)totalChars))); };

        for (int m = 0; m < mainCount; m++)  { save.mains.Add(CharacterStatGenerator.Create(characterStats.CharacterType.Main));  bump(); yield return null; }
        for (int s = 0; s < sideCount; s++)  { save.sides.Add(CharacterStatGenerator.Create(characterStats.CharacterType.Side));  bump(); yield return null; }
        for (int e = 0; e < extraCount; e++) { save.extras.Add(CharacterStatGenerator.Create(characterStats.CharacterType.Extra)); bump(); yield return null; }
        prog(1f);

        // ---------- utils ----------
        static bool InRect(Rect r, Vector2 p) => p.x >= r.xMin && p.x <= r.xMax && p.y >= r.yMin && p.y <= r.yMax;
        static float DistToRectEdge(Rect r, Vector2 p) { float dx = Mathf.Min(Mathf.Abs(p.x - r.xMin), Mathf.Abs(r.xMax - p.x)); float dy = Mathf.Min(Mathf.Abs(p.y - r.yMin), Mathf.Abs(r.yMax - p.y)); return Mathf.Min(dx, dy); }
        static void BuildLocalBounds(Patch pa, Rect rect, out float uMin, out float uMax, out float vMin, out float vMax) { float ang = pa.angleDeg * Mathf.Deg2Rad; float c = Mathf.Cos(-ang), s = Mathf.Sin(-ang); Vector2[] corners = new[] { new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax) }; float umin = float.PositiveInfinity, umax = float.NegativeInfinity; float vmin = float.PositiveInfinity, vmax = float.NegativeInfinity; foreach (var w in corners) { var d = w - pa.center; float u = d.x * c + d.y * s; float v = -d.x * s + d.y * c; umin = Mathf.Min(umin, u); umax = Mathf.Max(umax, u); vmin = Mathf.Min(vmin, v); vmax = Mathf.Max(vmax, v); } uMin = umin - 2f; uMax = umax + 2f; vMin = vmin - 2f; vMax = vmax + 2f; }
        static Vector2 LocalToWorld(Patch pa, float u, float v) { float ang = pa.angleDeg * Mathf.Deg2Rad; float c = Mathf.Cos(ang), s = Mathf.Sin(ang); float x = u * c - v * s + pa.center.x; float y = u * s + v * c + pa.center.y; return new Vector2(x, y); }
        static bool RaySegment(Vector2 p, Vector2 n, Vector2 a, Vector2 b, out float t, out Vector2 hit) { Vector2 s = b - a; float denom = Cross(n, s); if (Mathf.Abs(denom) < 1e-6f) { t = 0; hit = Vector2.zero; return false; } Vector2 ap = a - p; float tRay = Cross(ap, s) / denom; float uSeg = Cross(ap, n) / denom; if (tRay < 0f || uSeg < 0f || uSeg > 1f) { t = 0; hit = Vector2.zero; return false; } t = tRay; hit = a + s * uSeg; return true; }
        static bool SegmentIntersectionAny(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2, out float ti, out float tj, out Vector2 p, float eps = 1e-6f) { Vector2 r = q1 - p1, s = q2 - p2; float rxs = r.x * s.y - r.y * s.x; if (Mathf.Abs(rxs) < eps) { ti = tj = 0f; p = default; return false; } Vector2 qp = p2 - p1; ti = (qp.x * s.y - qp.y * s.x) / rxs; tj = (qp.x * r.y - qp.y * r.x) / rxs; bool on1 = ti >= -eps && ti <= 1f + eps; bool on2 = tj >= -eps && tj <= 1f + eps; if (!(on1 && on2)) { p = default; return false; } p = p1 + r * ti; return true; }
        static bool LineLineIntersection(Vector2 p1, Vector2 dir1, Vector2 p2, Vector2 dir2, out Vector2 intersection) { intersection = Vector2.zero; float cross = Cross(dir1, dir2); if (Mathf.Abs(cross) < 1e-6f) return false; float t = Cross(p2 - p1, dir2) / cross; intersection = p1 + dir1 * t; return true; }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static bool SegmentsIntersect(Vector2 p1, Vector2 q1, Vector2 p2, Vector2 q2, float epsilon = 1e-5f) { Vector2 r = q1 - p1; Vector2 s = q2 - p2; float rxs = Cross(r, s); Vector2 qp = p2 - p1; if (Mathf.Abs(rxs) < epsilon) { return false; } float t = Cross(qp, s) / rxs; float u = Cross(qp, r) / rxs; return (t > epsilon && t < 1.0f - epsilon && u > epsilon && u < 1.0f - epsilon); }
        static Vector2 ProjectPointOnSegment(Vector2 p, Vector2 a, Vector2 b, out float t01) { Vector2 ab = b - a; float ab2 = Vector2.Dot(ab, ab); if (ab2 <= 1e-6f) { t01 = 0f; return a; } t01 = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab2); return a + ab * t01; }
        static bool SegmentAABBClip(Rect r, Vector2 a, Vector2 b, out Vector2 ca, out Vector2 cb) { float u1 = 0f, u2 = 1f; float dx = b.x - a.x, dy = b.y - a.y; bool Clip(float P, float Q) { if (Mathf.Abs(P) < 1e-8f) return Q >= 0f; float t = Q / P; if (P < 0) { if (t > u2) return false; if (t > u1) u1 = t; } else { if (t < u1) return false; if (t < u2) u2 = t; } return true; } if (!Clip(-dx, a.x - r.xMin)) { ca = cb = Vector2.zero; return false; } if (!Clip(dx, r.xMax - a.x)) { ca = cb = Vector2.zero; return false; } if (!Clip(-dy, a.y - r.yMin)) { ca = cb = Vector2.zero; return false; } if (!Clip(dy, r.yMax - a.y)) { ca = cb = Vector2.zero; return false; } ca = new Vector2(a.x + u1 * dx, a.y + u1 * dy); cb = new Vector2(a.x + u2 * dx, a.y + u2 * dy); return (cb - ca).sqrMagnitude > 0.01f; }
        static ulong EdgeKey(Vector2 a, Vector2 b, RoadType type)
        {
            int qx(float v) => Mathf.RoundToInt(v * 10f);
            int qy(float v) => Mathf.RoundToInt(v * 10f);

            Vector2 p0 = a, p1 = b;
            if (p1.x < p0.x || (Mathf.Approximately(p1.x, p0.x) && p1.y < p0.y))
            {
                var t = p0;
                p0 = p1;
                p1 = t;
            }

            int ax = qx(p0.x), ay = qy(p0.y), bx = qx(p1.x), by = qy(p1.y);

            // Store quantised coordinates in 16-bit buckets to build a stable key.
            ushort ax16 = unchecked((ushort)ax);
            ushort ay16 = unchecked((ushort)ay);
            ushort bx16 = unchecked((ushort)bx);
            ushort by16 = unchecked((ushort)by);
            byte typeBits = (byte)(((int)type) & 0x03);

            unchecked
            {
                ulong key = 0;
                key |= ((ulong)ax16) << 48;
                key |= ((ulong)ay16) << 32;
                key |= ((ulong)bx16) << 16;
                key |= ((ulong)by16);
                key ^= ((ulong)typeBits) << 8;
                return key;
            }
        }
    }

    // ---------- data ----------
    // Internal struct for processing intersection connections
    struct IntersectionConnection
    {
        public int segIndex;
        public Vector2 entryPoint;
        public Vector2 dir; // NOTE: points TOWARD the center here
        public float width;
        public RoadType type;
    }

    enum StripeDir : byte { Right = 0, Top = 1 }
    struct Cut { public float t; public float padT; }
    struct StripeKey { public int patchId; public StripeDir dir; public int index; public StripeKey(int p, StripeDir d, int i) { patchId = p; dir = d; index = i; } public override int GetHashCode() => (patchId * 73856093) ^ ((int)dir * 19349663) ^ (index * 83492791); public override bool Equals(object obj) => obj is StripeKey o && o.patchId == patchId && o.dir == dir && o.index == index; }
    struct Patch { public int id; public Vector2 center; public float angleDeg; public int arterialEvery; public int arterialPhase; public List<float> uLines; public List<float> vLines; }
    struct TempSeg { public Vector2 a, b; public float width; public RoadType type; public int patchId; }
    struct CornerSample { public Vector2 pos; public Vector2 normal; public int patchId; }
    struct EdgeSample { public Vector2 pos; public Vector2 normal; public int patchId; public int param; }
}
