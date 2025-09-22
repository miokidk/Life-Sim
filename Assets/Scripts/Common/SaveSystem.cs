// <pre>
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Globalization;
using System.Text.RegularExpressions;



public static class SaveSystem
{

    sealed class FieldsOnlyResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            // Only include instance fields; skip UnityEngine.Object fields
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = type.GetFields(flags)
                .Where(f => !typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType));

            var props = new List<JsonProperty>();
            foreach (var f in fields)
            {
                var jp = base.CreateProperty(f, memberSerialization);
                jp.Readable = true;
                jp.Writable = true;
                props.Add(jp);
            }
            return props;
        }
    }


    public class SaveSummary
    {
        public string saveId;
        public string displayName;
        public string createdUtc;
        public string updatedUtc;
        public int mains, sides, extras;
    }

    public static JsonSerializerSettings Settings => jsonSettings;

    public static List<SaveSummary> ListSaves()
    {
        var list = new List<SaveSummary>();
        if (!Directory.Exists(SavesDir)) return list;

        foreach (var dir in Directory.GetDirectories(SavesDir))
        {
            var autos = Path.Combine(dir, "autosave.json");
            if (!File.Exists(autos)) continue;

            var data = JsonConvert.DeserializeObject<WorldSaveData>(File.ReadAllText(autos), jsonSettings);
            if (data == null || string.IsNullOrEmpty(data.saveId)) continue;

            list.Add(new SaveSummary {
                saveId = data.saveId,
                displayName = string.IsNullOrEmpty(data.displayName)
                    ? data.saveId.Substring(0, Math.Min(6, data.saveId.Length))
                    : data.displayName,
                createdUtc = data.createdUtc,
                updatedUtc = data.updatedUtc,
                mains = data.mains?.Count ?? 0,
                sides = data.sides?.Count ?? 0,
                extras = data.extras?.Count ?? 0
            });
        }

        return list.OrderByDescending(s => s.updatedUtc).ToList();
    }


    [Serializable]
    public class CharacterRecord
    {
        public string id;
        public string type;    // "Main/Side/Extra/World"
        public string json;    // full stats blob
    }

    [Serializable]
    public class EditorStateData
    {
        public string state;            // "CharacterCreator", "CenterPark", "WorldView"
        public string selectedCharacterId;
    }

    [Serializable]
    public class WorldSaveData
    {
        public string version = "0.1";
        public string saveId;
        public string createdUtc;
        public string updatedUtc;

        public string displayName;

        // --- ADDED: World structure data ---
        public WorldLayoutData layout;
        public List<RoadSegment> roadNetwork;
        public List<LotData> lots;

        // snapshot of characters
        public List<CharacterRecord> mains = new();
        public List<CharacterRecord> sides = new();
        public List<CharacterRecord> extras = new();

        public EditorStateData editor = new();

        // used to avoid duplicate writes
        public string contentHash;
    }

    public static string FormatLocal(string utcIso)
    {
        if (string.IsNullOrEmpty(utcIso)) return "";
        if (!DateTime.TryParse(utcIso, null, DateTimeStyles.RoundtripKind, out var dt)) return utcIso;
        return dt.ToLocalTime().ToString("MMM d, yyyy h:mm tt");
    }

    static string NextDisplayName()
    {
        int max = 0;
        foreach (var s in ListSaves())
        {
            var m = Regex.Match(s.displayName ?? "", @"^Save (\d+)$");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var n)) max = Mathf.Max(max, n);
        }
        return $"Save {max + 1}";
    }


    public static string DirFor(string worldId) => Path.Combine(SavesDir, worldId);
    public static string PathFor(string worldId, string slot) =>
        Path.Combine(DirFor(worldId), $"{slot}.json");

    static readonly JsonSerializerSettings jsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        TypeNameHandling = TypeNameHandling.Auto,   // keep polymorphism support (e.g., genitals)
        NullValueHandling = NullValueHandling.Ignore,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        ContractResolver = new FieldsOnlyResolver() // <-- key line
    };

    public static string SavesDir => Path.Combine(Application.persistentDataPath, "Saves");
    public static string PathFor(string slot) => Path.Combine(SavesDir, $"{slot}.json");

    public static void Write(string slot, WorldSaveData data)
    {
        Directory.CreateDirectory(SavesDir);
        data.updatedUtc = DateTime.UtcNow.ToString("o");
        if (string.IsNullOrEmpty(data.createdUtc)) data.createdUtc = data.updatedUtc;
        File.WriteAllText(PathFor(slot), JsonConvert.SerializeObject(data, jsonSettings));
#if UNITY_EDITOR
        Debug.Log($"Saved → {PathFor(slot)}");
#endif
    }

    public static WorldSaveData Read(string worldId, string slot)
    {
        var path = PathFor(worldId, slot);
        if (!File.Exists(path)) return null;
        return JsonConvert.DeserializeObject<WorldSaveData>(File.ReadAllText(path), jsonSettings);
    }

    static string ComputeHash(string s)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    static string ContentJson(WorldSaveData d)
    {
        var lean = new
        {
            d.saveId,
            // --- ADDED: Include world data in hash ---
            d.layout,
            d.roadNetwork,
            d.lots,
            //
            d.mains,
            d.sides,
            d.extras,
            d.editor
        };
        return JsonConvert.SerializeObject(lean, jsonSettings);
    }

    public static bool WriteIfChanged(string worldId, string slot, WorldSaveData data, out bool changed)
    {
        Directory.CreateDirectory(DirFor(worldId));

        // preserve/assign friendly name
        var existing = Read(worldId, slot);
        if (existing != null)
        {
            if (string.IsNullOrEmpty(data.displayName)) data.displayName = existing.displayName;
            if (string.IsNullOrEmpty(data.createdUtc))  data.createdUtc  = existing.createdUtc;
        }
        else
        {
            if (string.IsNullOrEmpty(data.displayName)) data.displayName = NextDisplayName();
        }

        // hash only the content (not timestamps or name)
        string contentJson = ContentJson(data);
        string newHash = ComputeHash(contentJson);

        if (existing != null && existing.contentHash == newHash)
        {
            changed = false;
            return false; // identical → skip write
        }

        data.contentHash = newHash;
        data.updatedUtc = DateTime.UtcNow.ToString("o");
        if (string.IsNullOrEmpty(data.createdUtc)) data.createdUtc = data.updatedUtc;

        File.WriteAllText(PathFor(worldId, slot),
            JsonConvert.SerializeObject(data, jsonSettings));

        changed = true;
        return true;
    }


    public static CharacterRecord Capture(characterStats s)
    {
        return new CharacterRecord
        {
            id = s.id,
            type = s.character_type.ToString(),
            json = JsonConvert.SerializeObject(s, jsonSettings)
        };
    }
    
    public static bool DeleteWorld(string worldId)
    {
        var dir = DirFor(worldId);
        if (!Directory.Exists(dir)) return false;
        try { Directory.Delete(dir, true); return true; }
        catch (Exception e) { Debug.LogError($"Delete failed: {e}"); return false; }
    }

    
}
// </pre>