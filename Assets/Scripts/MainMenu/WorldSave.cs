using System;
using System.Collections.Generic;
using UnityEngine; // For Vector2 and Rect

// NEW: A struct to hold data about a single road's connection to an intersection.
[System.Serializable]
public struct IntersectionConnector
{
    public Vector2 point;  // Midpoint on the polygon edge where the road connects.
    public Vector2 normal; // The road's direction, pointing AWAY from the intersection center.
    public float width;    // The width of the connecting road.
}

[System.Serializable]
public class WorldLayoutData
{
    public Rect worldBounds;
    public Rect centerParkBounds;
    public Vector2[] centerParkCorners;
    public float centerParkRotationDeg;
    public List<LotData> lots;
    public List<IntersectionData> intersections; 
}

[System.Serializable]
public struct IntersectionData
{
    public List<Vector2> points;
    public List<IntersectionConnector> connectors; // NEW: List of road connection points.
}

[Serializable]
public enum RoadType { Arterial, Local }

[Serializable]
public class RoadSegment
{
    public Vector2 start;
    public Vector2 end;
    public float width;
    public RoadType type;
}

[Serializable]
public class LotData
{
    public string lotId = Guid.NewGuid().ToString();
    public Rect bounds;
    public Vector2[] corners = Array.Empty<Vector2>();
    public float rotationDeg;
    public string ownerCharacterId; // Can be null if unowned
}

[Serializable]
public class WorldSave
{
    public string saveId = Guid.NewGuid().ToString();

    // World Structure
    public WorldLayoutData layout = new();
    public List<RoadSegment> roadNetwork = new();
    public List<LotData> lots = new();
    
    // Characters
    public List<characterStats> mains = new();
    public List<characterStats> sides = new();
    public List<characterStats> extras = new();
}
