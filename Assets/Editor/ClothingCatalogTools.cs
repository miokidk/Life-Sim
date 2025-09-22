#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class ClothingCatalogTools
{
    [MenuItem("LifeSim/Wardrobe/Create Catalog (with Defaults)")]
    public static void CreateWithDefaults()
    {
        var catalog = ScriptableObject.CreateInstance<ClothingCatalog>();
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Clothing Catalog", "ClothingCatalog", "asset", "Save catalog asset");
        if (string.IsNullOrEmpty(path)) return;

        AssetDatabase.CreateAsset(catalog, path);
        FillDefaults(catalog, overwrite:true);
        catalog.BuildIndex();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
    }

    [MenuItem("LifeSim/Wardrobe/Overwrite Selected Catalog (with Defaults)")]
    public static void OverwriteSelected()
    {
        var catalog = Selection.activeObject as ClothingCatalog;
        if (!catalog)
        {
            EditorUtility.DisplayDialog("No ClothingCatalog selected",
                "Select a ClothingCatalog asset in the Project window.", "OK");
            return;
        }

        FillDefaults(catalog, overwrite:true);
        catalog.BuildIndex();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    private static void FillDefaults(ClothingCatalog catalog, bool overwrite)
    {
        var so = new SerializedObject(catalog);
        var typesProp = so.FindProperty("types");
        if (overwrite) typesProp.ClearArray();

        void Add(string id, string label, ClothingCategory cat)
        {
            int i = typesProp.arraySize;
            typesProp.InsertArrayElementAtIndex(i);
            var elem = typesProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("id").stringValue = id;
            elem.FindPropertyRelative("label").stringValue = label;
            elem.FindPropertyRelative("category").enumValueIndex = (int)cat;
        }

        // ===== Short Sleeve Shirt =====
        Add("crew_tee","Crew Tee",ClothingCategory.ShortSleeveShirt);
        Add("vneck_tee","V-Neck Tee",ClothingCategory.ShortSleeveShirt);
        Add("henley_ss","Short-Sleeve Henley",ClothingCategory.ShortSleeveShirt);
        Add("polo_ss","Short-Sleeve Polo",ClothingCategory.ShortSleeveShirt);
        Add("buttonup_ss","Short-Sleeve Button-Up",ClothingCategory.ShortSleeveShirt);
        Add("camp_collar","Camp/Cuban Collar Shirt",ClothingCategory.ShortSleeveShirt);
        Add("rugby_ss","Short-Sleeve Rugby",ClothingCategory.ShortSleeveShirt);
        Add("jersey_athletic_ss","Athletic Jersey/Tee",ClothingCategory.ShortSleeveShirt);
        Add("mesh_tee","Mesh Tee",ClothingCategory.ShortSleeveShirt);
        Add("graphic_tee","Graphic Tee",ClothingCategory.ShortSleeveShirt);
        Add("crop_tee","Crop Tee",ClothingCategory.ShortSleeveShirt);
        Add("work_shirt_ss","Work Shirt (SS)",ClothingCategory.ShortSleeveShirt);

        // ===== Long Sleeve Shirt =====
        Add("ls_tee","Long-Sleeve Tee",ClothingCategory.LongSleeveShirt);
        Add("henley_ls","Long-Sleeve Henley",ClothingCategory.LongSleeveShirt);
        Add("oxford_buttonup","Oxford/Button-Up",ClothingCategory.LongSleeveShirt);
        Add("flannel","Flannel",ClothingCategory.LongSleeveShirt);
        Add("denim_shirt","Denim/Chambray Shirt",ClothingCategory.LongSleeveShirt);
        Add("rugby_ls","Long-Sleeve Rugby",ClothingCategory.LongSleeveShirt);
        Add("turtleneck","Turtleneck/Mock Neck",ClothingCategory.LongSleeveShirt);
        Add("thermal_waffle","Thermal/Waffle Shirt",ClothingCategory.LongSleeveShirt);
        Add("polo_ls","Long-Sleeve Polo",ClothingCategory.LongSleeveShirt);
        Add("work_shirt_ls","Work Shirt (LS)",ClothingCategory.LongSleeveShirt);

        // ===== Bottoms =====
        Add("jeans","Jeans",ClothingCategory.Bottoms);
        Add("chinos","Chinos",ClothingCategory.Bottoms);
        Add("trousers","Trousers/Dress Pants",ClothingCategory.Bottoms);
        Add("cargo_pants","Cargo Pants",ClothingCategory.Bottoms);
        Add("utility_work_pants","Utility/Work Pants",ClothingCategory.Bottoms);
        Add("joggers","Joggers",ClothingCategory.Bottoms);
        Add("sweatpants","Sweatpants",ClothingCategory.Bottoms);
        Add("track_pants","Track Pants",ClothingCategory.Bottoms);
        Add("leggings","Leggings",ClothingCategory.Bottoms);
        Add("yoga_pants","Yoga Pants",ClothingCategory.Bottoms);
        Add("denim_shorts","Denim Shorts",ClothingCategory.Bottoms);
        Add("chino_shorts","Chino Shorts",ClothingCategory.Bottoms);
        Add("athletic_shorts","Athletic Shorts",ClothingCategory.Bottoms);
        Add("cargo_shorts","Cargo Shorts",ClothingCategory.Bottoms);
        Add("biker_shorts","Biker Shorts",ClothingCategory.Bottoms);
        Add("skirt_pencil","Skirt — Pencil",ClothingCategory.Bottoms);
        Add("skirt_pleated","Skirt — Pleated",ClothingCategory.Bottoms);
        Add("skirt_a_line_midi","Skirt — A-Line/Midi",ClothingCategory.Bottoms);
        Add("skirt_mini","Skirt — Mini",ClothingCategory.Bottoms);
        Add("skirt_maxi","Skirt — Maxi",ClothingCategory.Bottoms);
        Add("skort","Skort",ClothingCategory.Bottoms);
        Add("overalls","Overalls/Dungarees",ClothingCategory.Bottoms);
        Add("culottes","Culottes/Gauchos",ClothingCategory.Bottoms);

        // ===== Overgarments =====
        Add("hoodie_pullover","Hoodie (Pullover)",ClothingCategory.Overgarments);
        Add("hoodie_zip","Hoodie (Zip)",ClothingCategory.Overgarments);
        Add("sweatshirt_crew","Sweatshirt (Crewneck)",ClothingCategory.Overgarments);
        Add("cardigan","Cardigan",ClothingCategory.Overgarments);
        Add("knit_sweater","Knit Sweater",ClothingCategory.Overgarments);
        Add("denim_jacket","Denim Jacket",ClothingCategory.Overgarments);
        Add("leather_jacket","Leather Jacket",ClothingCategory.Overgarments);
        Add("bomber","Bomber/Varsity",ClothingCategory.Overgarments);
        Add("shacket","Shirt Jacket (Shacket)",ClothingCategory.Overgarments);
        Add("windbreaker","Windbreaker",ClothingCategory.Overgarments);
        Add("raincoat","Raincoat",ClothingCategory.Overgarments);
        Add("trench","Trench Coat",ClothingCategory.Overgarments);
        Add("overcoat","Overcoat/Wool Coat",ClothingCategory.Overgarments);
        Add("puffer","Puffer Jacket/Coat",ClothingCategory.Overgarments);
        Add("parka","Parka",ClothingCategory.Overgarments);
        Add("blazer","Blazer/Sport Coat",ClothingCategory.Overgarments);
        Add("suit_jacket","Suit Jacket",ClothingCategory.Overgarments);
        Add("vest_insulated","Vest (Puffer/Insulated)",ClothingCategory.Overgarments);
        Add("utility_vest","Utility Vest",ClothingCategory.Overgarments);
        Add("poncho_cape","Poncho/Cape",ClothingCategory.Overgarments);
        Add("fleece","Fleece",ClothingCategory.Overgarments);

        // ===== Socks =====
        Add("no_show","No-Show",ClothingCategory.Socks);
        Add("ankle","Ankle",ClothingCategory.Socks);
        Add("quarter","Quarter",ClothingCategory.Socks);
        Add("crew","Crew",ClothingCategory.Socks);
        Add("boot","Boot/Mid-Calf",ClothingCategory.Socks);
        Add("knee_high","Knee-High",ClothingCategory.Socks);
        Add("thigh_high","Thigh-High",ClothingCategory.Socks);
        Add("dress_thin","Dress/Thin",ClothingCategory.Socks);
        Add("athletic_cushioned","Athletic/Cushioned",ClothingCategory.Socks);
        Add("compression","Compression",ClothingCategory.Socks);
        Add("thermal_wool","Thermal/Wool",ClothingCategory.Socks);
        Add("toe_socks","Toe Socks",ClothingCategory.Socks);

        // ===== Underwear =====
        Add("briefs","Briefs",ClothingCategory.Underwear);
        Add("boxers","Boxers",ClothingCategory.Underwear);
        Add("boxer_briefs","Boxer Briefs",ClothingCategory.Underwear);
        Add("trunks","Trunks",ClothingCategory.Underwear);
        Add("long_johns","Long Johns/Thermal Bottoms",ClothingCategory.Underwear);
        Add("undershirt_tee","Undershirt (Tee)",ClothingCategory.Underwear);
        Add("undershirt_tank","Undershirt (Tank)",ClothingCategory.Underwear);
        Add("camisole","Camisole",ClothingCategory.Underwear);
        Add("bralette","Bralette",ClothingCategory.Underwear);
        Add("sports_bra","Sports Bra",ClothingCategory.Underwear);
        Add("bikini_brief","Bikini Brief",ClothingCategory.Underwear);
        Add("hipster","Hipster",ClothingCategory.Underwear);
        Add("thong","Thong",ClothingCategory.Underwear);
        Add("boyshort","Boyshort",ClothingCategory.Underwear);
        Add("slip","Slip",ClothingCategory.Underwear);
        Add("slip_shorts","Slip Shorts",ClothingCategory.Underwear);
        Add("shapewear_top","Shapewear (Top)",ClothingCategory.Underwear);
        Add("shapewear_bottom","Shapewear (Bottom)",ClothingCategory.Underwear);
        Add("tights_hosiery","Tights/Hosiery",ClothingCategory.Underwear);

        // ===== Shoes =====
        Add("sneakers_casual","Sneakers (Casual)",ClothingCategory.Shoes);
        Add("running_shoes","Running Shoes",ClothingCategory.Shoes);
        Add("training_basketball","Training/Basketball",ClothingCategory.Shoes);
        Add("skate_shoes","Skate Shoes",ClothingCategory.Shoes);
        Add("hiking_shoes","Hiking Shoes/Boots",ClothingCategory.Shoes);
        Add("work_boots","Work/Combat Boots",ClothingCategory.Shoes);
        Add("chelsea_boots","Chelsea Boots",ClothingCategory.Shoes);
        Add("chukka_boots","Chukka/Desert Boots",ClothingCategory.Shoes);
        Add("oxfords","Oxfords",ClothingCategory.Shoes);
        Add("derby","Derby",ClothingCategory.Shoes);
        Add("loafers","Loafers",ClothingCategory.Shoes);
        Add("boat_shoes","Boat Shoes",ClothingCategory.Shoes);
        Add("flats_ballet","Flats/Ballet",ClothingCategory.Shoes);
        Add("mary_janes","Mary Janes",ClothingCategory.Shoes);
        Add("heels_pumps","Heels/Pumps",ClothingCategory.Shoes);
        Add("wedges","Wedges",ClothingCategory.Shoes);
        Add("mules_clogs","Mules/Clogs",ClothingCategory.Shoes);
        Add("sandals_slides","Sandals/Slides",ClothingCategory.Shoes);
        Add("flip_flops","Flip-Flops",ClothingCategory.Shoes);
        Add("sport_sandals","Sport Sandals",ClothingCategory.Shoes);
        Add("slippers","Slippers",ClothingCategory.Shoes);
        Add("rain_boots","Rain Boots/Wellies",ClothingCategory.Shoes);
        Add("cleats","Cleats (Sport)",ClothingCategory.Shoes);

        // ===== Headwear =====
        Add("baseball_cap","Baseball Cap",ClothingCategory.Headwear);
        Add("dad_cap","Dad Cap",ClothingCategory.Headwear);
        Add("snapback_fitted","Snapback/Fitted",ClothingCategory.Headwear);
        Add("trucker_cap","Trucker Cap",ClothingCategory.Headwear);
        Add("beanie","Beanie/Knit Cap",ClothingCategory.Headwear);
        Add("bucket_hat","Bucket Hat",ClothingCategory.Headwear);
        Add("wide_brim","Wide-Brim (Felt/Straw)",ClothingCategory.Headwear);
        Add("fedora_panama","Fedora/Panama",ClothingCategory.Headwear);
        Add("beret","Beret",ClothingCategory.Headwear);
        Add("visor","Visor",ClothingCategory.Headwear);
        Add("headband","Headband",ClothingCategory.Headwear);
        Add("durag","Durag",ClothingCategory.Headwear);
        Add("bonnet","Bonnet (Sleep)",ClothingCategory.Headwear);
        Add("tam_loc_cap","Tam/Loc Cap",ClothingCategory.Headwear);
        Add("headwrap_turban","Headwrap/Turban",ClothingCategory.Headwear);
        Add("headscarf_hijab","Headscarf/Hijab",ClothingCategory.Headwear);
        Add("bandana","Bandana",ClothingCategory.Headwear);
        Add("earmuffs","Earmuffs",ClothingCategory.Headwear);
        Add("balaclava","Balaclava",ClothingCategory.Headwear);
        Add("bike_helmet","Bike Helmet",ClothingCategory.Headwear);

        so.ApplyModifiedProperties();
    }
}
#endif
