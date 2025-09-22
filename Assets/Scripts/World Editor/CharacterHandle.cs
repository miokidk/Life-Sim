using UnityEngine;

public class CharacterHandle : MonoBehaviour
{
    public characterStats Stats { get; private set; }

    [Tooltip("Optional: child transform at the head height (e.g., a bone)")]
    public Transform headAnchor;

    public void Bind(characterStats s)
    {
        Stats = s;

        // Build display name from profile (fallback to "Character")
        var p = s != null ? s.profile : null;
        string f = p != null ? (p.first_name ?? "").Trim() : "";
        string l = p != null ? (p.last_name ?? "").Trim() : "";
        string full = (f + " " + l).Trim();
        name = string.IsNullOrEmpty(full) ? "Character" : full;
    }

    public Vector3 GetHeadWorldPosition()
    {
        if (headAnchor) return headAnchor.position;

        // Try CharacterController height first
        float h = 1.7f;
        var cc = GetComponent<CharacterController>();
        if (cc) h = cc.height + cc.center.y;
        else
        {
            var col = GetComponent<Collider>();
            if (col) h = col.bounds.size.y;
        }
        return transform.position + Vector3.up * h;
    }
    
    public Vector3 GetLookPoint(float heightBias = 0.68f)
    {
        heightBias = Mathf.Clamp01(heightBias);

        // Prefer explicit head anchor for stable aiming
        if (headAnchor)
        {
            float baseY = transform.position.y;
            float headY = headAnchor.position.y;
            float y = Mathf.Lerp(baseY, headY, heightBias);
            var p = transform.position; p.y = y; return p;
        }

        // Fallbacks: CharacterController → Collider → heuristic
        var cc = GetComponent<CharacterController>();
        if (cc)
        {
            float footY = transform.position.y + cc.center.y - cc.height * 0.5f;
            float headY = footY + cc.height;
            float y = Mathf.Lerp(footY, headY, heightBias);
            var p = transform.position; p.y = y; return p;
        }

        var col = GetComponent<Collider>();
        if (col)
        {
            var b = col.bounds;
            float y = Mathf.Lerp(b.min.y, b.max.y, heightBias);
            var p = transform.position; p.y = y; return p;
        }

        return transform.position + Vector3.up * (1.7f * heightBias);
    }


}
