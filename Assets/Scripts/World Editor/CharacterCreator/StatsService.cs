using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public sealed class StatsService : MonoBehaviour
{
    [SerializeField] private characterStats target;   // assign in scene

    public static event Action StatsRecomputed;       // UI rows listen and refresh

    private readonly List<Toggle> _allToggles = new();

    public characterStats Model => target;

    /* ---------- public API ---------- */
    public object GetValue(string path) => GetByPath(target, path);
    public void SetValue(string path, object value)
    {
        SetByPath(target, path, value);
    }

    public void CommitAndRecompute()
    {
        if (target == null) return;
        StatLimits.RecomputeLimits(target);
        target.RefreshHierarchyName();
        RaiseRecomputed(); // <-- instead of StatsRecomputed?.Invoke();
    }


    /* ---------- reflection helpers (field path: a.b.c) ---------- */
    static object GetByPath(object root, string path)
    {
        object cur = root;
        foreach (var seg in path.Split('.'))
        {
            if (cur == null) return null;

            // --- Handle list/array access like "people[0]" ---
            if (seg.Contains("[") && seg.EndsWith("]"))
            {
                try
                {
                    var openBracket = seg.IndexOf('[');
                    var fieldName = seg.Substring(0, openBracket);
                    var indexStr = seg.Substring(openBracket + 1, seg.Length - openBracket - 2);
                    int index = int.Parse(indexStr);

                    var listMember = GetMember(cur.GetType(), fieldName);
                    if (listMember == null)
                    {
                        Debug.LogError($"StatsService GetByPath: List/Array segment '{fieldName}' not found on {cur.GetType().Name}");
                        return null;
                    }

                    var listObj = GetMemberValue(listMember, cur) as System.Collections.IList;
                    if (listObj == null)
                    {
                        Debug.LogError($"StatsService GetByPath: Member '{fieldName}' is not an IList on {cur.GetType().Name}");
                        return null;
                    }
                    
                    if (index >= 0 && index < listObj.Count)
                    {
                        cur = listObj[index];
                        continue; // Proceed to the next path segment (e.g., "closeness")
                    }
                    else
                    {
                        Debug.LogError($"StatsService GetByPath: Index {index} out of bounds for '{fieldName}' on {cur.GetType().Name}");
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"StatsService GetByPath: Failed to parse segment '{seg}'. Error: {ex.Message}");
                    return null;
                }
            }

            // Original logic for simple fields (a.b.c)
            var m = GetMember(cur.GetType(), seg);
            if (m != null)
            {
                cur = GetMemberValue(m, cur);
                continue;
            }

            Debug.LogError($"StatsService: segment '{seg}' not found on {cur.GetType().Name}");
            return null;
        }
        return cur;
    }

    static MemberInfo GetMember(Type t, string name) =>
    (MemberInfo)t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    ?? t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    
    // START FIX: Safer Reflection Helpers
    static Type GetMemberType(MemberInfo m) => m switch
    {
        FieldInfo fi => fi.FieldType,
        PropertyInfo pi => pi.PropertyType,
        _ => null
    };

    static object GetMemberValue(MemberInfo m, object obj) => m switch
    {
        FieldInfo fi => fi.GetValue(obj),
        PropertyInfo pi => pi.GetValue(obj),
        _ => null
    };

    static void SetMemberValue(MemberInfo m, object obj, object val)
    {
        if (m is FieldInfo fi) fi.SetValue(obj, val);
        else if (m is PropertyInfo pi) pi.SetValue(obj, val);
    }
    // END FIX

    // START FIX: Rewritten SetByPath to handle indexers
    static void SetByPath(object root, string path, object value)
    {
        var parts = path.Split('.');
        var parentChain = new List<object>();
        var memberChain = new List<MemberInfo>();
        
        object current = root;

        // Traverse path to build parent chain for struct propagation
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string part = parts[i];
            if (current == null)
            {
                Debug.LogError($"StatsService SetByPath: Null encountered in path '{path}' at segment '{part}'.");
                return;
            }

            // --- Handle list/array access ---
            if (part.Contains("[") && part.EndsWith("]"))
            {
                try
                {
                    int openBracket = part.IndexOf('[');
                    string fieldName = part.Substring(0, openBracket);
                    string indexStr = part.Substring(openBracket + 1, part.Length - openBracket - 2);
                    int index = int.Parse(indexStr);

                    var listMember = GetMember(current.GetType(), fieldName);
                    if (listMember == null) { Debug.LogError($"Segment '{fieldName}' not found on type {current.GetType().Name}"); return; }

                    var listObj = GetMemberValue(listMember, current) as System.Collections.IList;
                    if (listObj == null) { Debug.LogError($"Member '{fieldName}' is not an IList."); return; }
                    
                    if (index < 0 || index >= listObj.Count) { Debug.LogError($"Index {index} out of bounds for '{fieldName}'."); return; }
                    
                    // Since all lists in characterStats contain classes (not structs), we don't need
                    // complex propagation for list elements. We can just advance 'current'.
                    current = listObj[index];
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to parse list segment '{part}': {ex.Message}");
                    return;
                }
            }
            else // --- Handle simple field/property access ---
            {
                var member = GetMember(current.GetType(), part);
                if (member == null) { Debug.LogError($"Segment '{part}' not found on type {current.GetType().Name} in path '{path}'."); return; }
                
                parentChain.Add(current);
                memberChain.Add(member);
                current = GetMemberValue(member, current);
            }
        }
        
        // At this point, `current` is the final object on which to set the value.
        if (current == null) { Debug.LogError($"Final object in path '{path}' is null."); return; }

        string lastPart = parts[^1];
        var finalMember = GetMember(current.GetType(), lastPart);
        if (finalMember == null) { Debug.LogError($"Final segment '{lastPart}' not found on type {current.GetType().Name}."); return; }

        object finalValue = Coerce(value, GetMemberType(finalMember));
        SetMemberValue(finalMember, current, finalValue);

        // Propagate changes back up the chain if we modified a field within a struct
        // (This part of the logic remains unchanged and is crucial for non-list structs)
        if (current.GetType().IsValueType)
        {
            object child = current;
            for (int i = parentChain.Count - 1; i >= 0; i--)
            {
                SetMemberValue(memberChain[i], parentChain[i], child);
                child = parentChain[i];
                if (!child.GetType().IsValueType) break; // Stop when we hit a class
            }
        }
    }
    // END FIX


    static object Coerce(object value, Type targetType)
    {
        if (value == null) return null;
        var vType = value.GetType();
        if (targetType.IsAssignableFrom(vType)) return value;

        if (targetType.IsEnum)
        {
            if (value is int i) return Enum.ToObject(targetType, i);
            if (value is string s) return Enum.Parse(targetType, s, true);
        }
        if (targetType == typeof(int)) return Convert.ToInt32(value);
        if (targetType == typeof(float)) return Convert.ToSingle(value);
        if (targetType == typeof(bool))
        {
            if (value is int ii) return ii != 0;
            if (value is float ff) return Math.Abs(ff) > 0.0001f;
            if (value is string ss) return bool.Parse(ss);
        }
        return Convert.ChangeType(value, targetType);
    }

    public void SetTarget(characterStats newTarget)
    {
        target = newTarget;
        CommitAndRecompute();   // applies limits + refreshes all UI
    }

    public void RefreshUIBindings()
    {
        _allToggles.Clear();
        // Pull fresh toggles from the CURRENT scene (include inactive).
#if UNITY_2023_1_OR_NEWER
        _allToggles.AddRange(GetComponentsInChildren<Toggle>(true));
#else
        _allToggles.AddRange(GetComponentsInChildren<Toggle>(true));
#endif
        _allToggles.RemoveAll(t => t == null); // drop destroyed refs
    }

    void Awake() { RefreshUIBindings(); }
    void OnEnable() { RefreshUIBindings(); }

    // If you register listeners on toggles, clean them up
    void OnDisable()
    {
        foreach (var t in _allToggles)
            if (t != null) t.onValueChanged.RemoveAllListeners();
    }
    
    public static void RaiseRecomputed()
    {
        if (StatsRecomputed == null) return;

        // Prune destroyed Unity targets from the invocation list
        foreach (var d in StatsRecomputed.GetInvocationList())
        {
            var targetAsUnityObj = d.Target as UnityEngine.Object;
            if (targetAsUnityObj == null) // destroyed or missing
                StatsRecomputed -= (System.Action)d;
        }

        StatsRecomputed?.Invoke();
    }
}