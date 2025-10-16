using UnityEngine;
using System.Reflection;

public class CharacterHandle : MonoBehaviour
{
    public characterStats Stats { get; private set; }

    [Tooltip("Optional: child transform at the head height (e.g., a bone)")]
    public Transform headAnchor;

    public void Bind(characterStats s)
    {
        Stats = s;

        if (s)
        {
            s.RefreshHierarchyName();

            // Keep any local characterStats component pointing at the live data so the Inspector displays it.
            MirrorFields(s, GetComponent<characterStats>());

            // Ensure we have a holder that references the real stats object for quick selection/debugging.
            var holder = GetComponent<CharacterStatsHolder>() ?? gameObject.AddComponent<CharacterStatsHolder>();
            holder.Bind(s);

            // Build display name from profile (fallback to "Character")
            var p = s.profile;
            string f = (p?.first_name ?? string.Empty).Trim();
            string l = (p?.last_name ?? string.Empty).Trim();
            string full = (f + " " + l).Trim();
            gameObject.name = string.IsNullOrEmpty(full) ? "Character" : full;
        }
        else
        {
            gameObject.name = "Character";
        }
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

    static readonly BindingFlags CopyFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    static void MirrorFields(characterStats source, characterStats target)
    {
        if (!source || !target || ReferenceEquals(source, target)) return;

        var fields = typeof(characterStats).GetFields(CopyFlags);
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            if (field.IsStatic) continue;
            field.SetValue(target, field.GetValue(source));
        }
    }
}
