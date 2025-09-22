// CharacterWardrobe.cs
using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterWardrobe : MonoBehaviour
{
    [Header("Catalog")]
    [SerializeField] private ClothingCatalog catalog;

    [Header("Preferences (per category)")]
    [SerializeField] private List<PreferenceEntry> preferences = new(); // serialized for inspector
    private Dictionary<ClothingCategory, HashSet<string>> _prefMap;     // runtime index

    [Header("Owned Items")]
    [SerializeField] private List<WardrobeItem> items = new();          // everything this character owns
    private Dictionary<string, WardrobeItem> _itemsById;                // runtime index

    [Header("Saved Outfits (optional)")]
    [SerializeField] private List<Outfit> outfits = new();

    public event Action OnWardrobeChanged;

    [Serializable]
    public class PreferenceEntry
    {
        public ClothingCategory category;
        public List<string> typeIds = new(); // refs to ClothingCatalog.type.id
    }

    [Serializable]
    public class WardrobeItem
    {
        public string uid;                 // unique per item on this character
        public ClothingCategory category;  // must match the ClothingType category
        public string typeId;              // e.g., "crew_tee"
        public Color color = Color.white;
        public string material;            // e.g., "cotton", "leather"
        public string size;                // free-form or "S/M/L", "30x32", "US 11"
        [Range(0f, 1f)] public float formality; // 0 casual → 1 formal
        [Range(0f, 1f)] public float warmth;    // 0 summer → 1 deep winter
        public string notes;               // any extra
    }

    [Serializable]
    public class Outfit
    {
        public string name = "Everyday";
        public OutfitSelection selection = new();
    }

    [Serializable]
    public class OutfitSelection
    {
        // Each field holds the WardrobeItem.uid the outfit uses (empty = none)
        public string shortSleeveShirt;
        public string longSleeveShirt;
        public string bottoms;
        public List<string> overgarments = new(); // layering allowed
        public string socks;
        public string underwear;
        public string shoes;
        public string headwear;
    }

    void Awake()
    {
        RebuildIndices();
    }

    void OnValidate()
    {
        // Make sure runtime maps are fresh in editor changes too.
        RebuildIndices();
    }

    private void RebuildIndices()
    {
        if (catalog != null) catalog.BuildIndex();

        _prefMap = new Dictionary<ClothingCategory, HashSet<string>>();
        foreach (var p in preferences)
        {
            if (!_prefMap.TryGetValue(p.category, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                _prefMap[p.category] = set;
            }
            foreach (var id in p.typeIds)
            {
                if (!string.IsNullOrWhiteSpace(id)) set.Add(id);
            }
        }

        _itemsById = new Dictionary<string, WardrobeItem>(StringComparer.Ordinal);
        foreach (var it in items)
        {
            if (string.IsNullOrWhiteSpace(it.uid))
                it.uid = Guid.NewGuid().ToString("N");
            _itemsById[it.uid] = it;
        }
    }

    // ---------- Preferences API ----------
    public IReadOnlyCollection<string> GetPreferredTypes(ClothingCategory cat)
        => _prefMap != null && _prefMap.TryGetValue(cat, out var set) ? set : Array.Empty<string>();

    public void SetPreferredTypes(ClothingCategory cat, IEnumerable<string> typeIds)
    {
        // Update serialized list
        var entry = preferences.Find(e => e.category == cat);
        if (entry == null)
        {
            entry = new PreferenceEntry { category = cat };
            preferences.Add(entry);
        }
        entry.typeIds.Clear();
        if (typeIds != null) entry.typeIds.AddRange(typeIds);

        RebuildIndices();
        OnWardrobeChanged?.Invoke();
    }

    public bool IsTypePreferred(ClothingCategory cat, string typeId)
        => _prefMap != null && _prefMap.TryGetValue(cat, out var set) && set.Contains(typeId);

    // ---------- Items API ----------
    public WardrobeItem AddItem(ClothingCategory cat, string typeId, Color color, string material, string size,
                                float formality = 0f, float warmth = 0f, string notes = null)
    {
        // Validate type ↔ category
        if (catalog != null && catalog.TryGetById(typeId, out var t) && t.category != cat)
            Debug.LogWarning($"Type '{typeId}' is cataloged as {t.category} but item was added as {cat}. Using catalog category.");
        if (catalog != null && catalog.TryGetById(typeId, out var t2))
            cat = t2.category;

        var item = new WardrobeItem {
            uid = Guid.NewGuid().ToString("N"),
            category = cat,
            typeId = typeId,
            color = color,
            material = material,
            size = size,
            formality = Mathf.Clamp01(formality),
            warmth = Mathf.Clamp01(warmth),
            notes = notes
        };
        items.Add(item);
        _itemsById[item.uid] = item;
        OnWardrobeChanged?.Invoke();
        return item;
    }

    public bool RemoveItem(string uid)
    {
        var idx = items.FindIndex(i => i.uid == uid);
        if (idx >= 0)
        {
            items.RemoveAt(idx);
            _itemsById.Remove(uid);
            // Also remove from all outfits
            foreach (var o in outfits)
            {
                RemoveUidFromSelection(o.selection, uid);
            }
            OnWardrobeChanged?.Invoke();
            return true;
        }
        return false;
    }

    public List<WardrobeItem> GetItemsByCategory(ClothingCategory cat)
        => items.FindAll(i => i.category == cat);

    public bool TryGetItem(string uid, out WardrobeItem item)
        => _itemsById.TryGetValue(uid, out item);

    // ---------- Outfits API ----------
    public Outfit CreateOutfit(string name)
    {
        var o = new Outfit { name = name ?? "Outfit" };
        outfits.Add(o);
        OnWardrobeChanged?.Invoke();
        return o;
    }

    public bool DeleteOutfit(string name)
    {
        var idx = outfits.FindIndex(o => o.name == name);
        if (idx >= 0)
        {
            outfits.RemoveAt(idx);
            OnWardrobeChanged?.Invoke();
            return true;
        }
        return false;
    }

    public Outfit GetOutfit(string name) => outfits.Find(o => o.name == name);

    public void SetOutfitSelection(string name, OutfitSelection sel)
    {
        var o = GetOutfit(name);
        if (o == null) o = CreateOutfit(name);
        o.selection = sel ?? new OutfitSelection();
        OnWardrobeChanged?.Invoke();
    }

    // ---------- Helpers ----------
    private static void RemoveUidFromSelection(OutfitSelection s, string uid)
    {
        if (s == null || string.IsNullOrEmpty(uid)) return;
        if (s.shortSleeveShirt == uid) s.shortSleeveShirt = null;
        if (s.longSleeveShirt == uid)  s.longSleeveShirt  = null;
        if (s.bottoms == uid)          s.bottoms          = null;
        s.overgarments?.RemoveAll(x => x == uid);
        if (s.socks == uid)            s.socks            = null;
        if (s.underwear == uid)        s.underwear        = null;
        if (s.shoes == uid)            s.shoes            = null;
        if (s.headwear == uid)         s.headwear         = null;
    }

    public ClothingCatalog Catalog => catalog;
    public IReadOnlyList<WardrobeItem> Items => items;
    public IReadOnlyList<Outfit> Outfits => outfits;
}
