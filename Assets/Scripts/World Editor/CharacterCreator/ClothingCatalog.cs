using System;
using System.Collections.Generic;
using UnityEngine;

public enum ClothingCategory {
    ShortSleeveShirt,
    LongSleeveShirt,
    Bottoms,
    Overgarments,
    Socks,
    Underwear,
    Shoes,
    Headwear
}

[CreateAssetMenu(menuName = "LifeSim/Wardrobe/Clothing Catalog")]
public class ClothingCatalog : ScriptableObject
{
    [Serializable]
    public class ClothingType {
        public string id;              // e.g., "crew_tee"
        public string label;           // e.g., "Crew Tee" (UI)
        public ClothingCategory category;
    }

    [SerializeField] private List<ClothingType> types = new List<ClothingType>();

    // Runtime index (for quick lookups)
    private Dictionary<string, ClothingType> _byId;
    private Dictionary<ClothingCategory, List<ClothingType>> _byCat;

    public IReadOnlyList<ClothingType> AllTypes => types;

    public void BuildIndex()
    {
        _byId = new Dictionary<string, ClothingType>(StringComparer.Ordinal);
        _byCat = new Dictionary<ClothingCategory, List<ClothingType>>();
        foreach (var t in types)
        {
            if (string.IsNullOrWhiteSpace(t.id)) continue;
            _byId[t.id] = t;
            if (!_byCat.TryGetValue(t.category, out var list))
            {
                list = new List<ClothingType>();
                _byCat[t.category] = list;
            }
            list.Add(t);
        }
    }

    public bool TryGetById(string id, out ClothingType t)
    {
        if (_byId == null) BuildIndex();
        return _byId.TryGetValue(id, out t);
    }

    public List<ClothingType> GetByCategory(ClothingCategory category)
    {
        if (_byCat == null) BuildIndex();
        return _byCat.TryGetValue(category, out var list) ? list : new List<ClothingType>();
    }
}
