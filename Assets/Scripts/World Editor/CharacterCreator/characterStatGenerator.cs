using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;



public static class CharacterStatGenerator
{
    /* ────────────── helpers ────────────── */

    [Serializable] class HairDB { public string[] sections; public Style[] styles; }
    [Serializable] class Style { public string id, display_name; public string[] configs; public int[] types; public LengthProfile[] length_profiles; }
    [Serializable] class LengthProfile { public string config; public Sections sections; }
    [Serializable] class Sections { public MinMax front, top, left, right, back; }
    [Serializable] class MinMax { public float min, max; }

    // Overload: set the type, then reuse the existing Randomize(s)
    public static void Randomize(characterStats s, characterStats.CharacterType type)
    {
        s.character_type = type;
        Randomize(s); // calls your existing one-arg entry point
    }

    static void EnsureRefs(characterStats s) => DeepInit(s);

    static void DeepInit(object obj, int depth = 0)
    {
        if (obj == null || depth > 6) return; // prevent runaway recursion

        var t = obj.GetType();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        foreach (var f in t.GetFields(flags))
        {
            var ft = f.FieldType;

            // skip primitives/strings/enums/UnityEngine.Object refs
            if (ft.IsPrimitive || ft.IsEnum || ft == typeof(string)) continue;
            if (typeof(UnityEngine.Object).IsAssignableFrom(ft)) continue;

            // Lists
            if (typeof(System.Collections.IList).IsAssignableFrom(ft))
            {
                var list = (System.Collections.IList)f.GetValue(obj);
                if (list == null && ft.IsGenericType)
                {
                    var gen = typeof(List<>).MakeGenericType(ft.GetGenericArguments()[0]);
                    list = (System.Collections.IList)Activator.CreateInstance(gen);
                    f.SetValue(obj, list);
                }
                if (list != null)
                    foreach (var it in list) DeepInit(it, depth + 1);
                continue;
            }

            // Arrays – leave size to gameplay code
            if (ft.IsArray) continue;

            // Other reference types → create if null, then recurse
            var cur = f.GetValue(obj);
            if (cur == null)
            {
                try { cur = Activator.CreateInstance(ft); f.SetValue(obj, cur); }
                catch { continue; } // types without default ctor
            }
            DeepInit(cur, depth + 1);
        }

        // (Optional) properties with setters
        foreach (var p in t.GetProperties(flags))
        {
            if (!p.CanRead || !p.CanWrite) continue;
            var pt = p.PropertyType;
            if (pt.IsPrimitive || pt.IsEnum || pt == typeof(string)) continue;
            if (typeof(UnityEngine.Object).IsAssignableFrom(pt)) continue;

            object cur;
            try { cur = p.GetValue(obj); } catch { continue; }
            if (typeof(System.Collections.IList).IsAssignableFrom(pt)) continue; // handled by fields commonly

            if (cur == null && !pt.IsArray)
            {
                try { cur = Activator.CreateInstance(pt); p.SetValue(obj, cur); }
                catch { continue; }
            }
            DeepInit(cur, depth + 1);
        }
    }



    static HairDB _hairDB;
    static bool TryLoadHairDB()
    {
        if (_hairDB != null) return true;

        // Try a few common Resources paths; fall back gracefully if not found.
        TextAsset ta =
            Resources.Load<TextAsset>("hairstyles.min") ??
            Resources.Load<TextAsset>("hairstyles") ??
            Resources.Load<TextAsset>("Data/hairstyles.min");

        if (!ta) return false;

        _hairDB = JsonUtility.FromJson<HairDB>(ta.text);
        return _hairDB != null && _hairDB.styles != null && _hairDB.styles.Length > 0;
    }

    static int HairFamily(characterStats.HairType ht)
    {
        // map Type1A..Type4C → 1..4 family (Type1*, Type2*, Type3*, Type4*)
        return Mathf.Clamp(1 + ((int)ht) / 3, 1, 4);
    }

    static bool FitsType(Style s, int family)
    {
        return s.types == null || s.types.Length == 0 || Array.IndexOf(s.types, family) >= 0;
    }
    static bool InRange(MinMax mm, int v)
    {
        if (mm == null) return true;
        return v >= mm.min && v <= mm.max;
    }
    static bool FitsLength(Style s, int lenU)
    {
        if (s.length_profiles == null || s.length_profiles.Length == 0) return true;

        foreach (var lp in s.length_profiles)
        {
            string cfg = (lp.config ?? "whole_head").Trim().ToLowerInvariant();
            bool sectionIndependent = cfg == "section_independent";

            IEnumerable<string> targets =
                cfg == "whole_head" ? AllSections :
                AllSections.Contains(cfg) ? new[] { cfg } :
                cfg.Split(',').Select(p => p.Trim()).Where(AllSections.Contains);

            bool ok = sectionIndependent
                ? targets.Any(sec => InRange(Sec(lp.sections, sec), lenU))   // ANY section fits
                : targets.All(sec => InRange(Sec(lp.sections, sec), lenU));  // ALL target sections fit

            if (ok) return true;
        }
        return false;
    }


    // Gender-weighted pick that ALSO filters by hair type + length.
    // Returns a STYLE ID when JSON is available; otherwise falls back to old display-name picker.
    static string PickStyleThatFits(characterStats s, int lengthU)
    {
        var gender = s.profile.gender;
        if (!TryLoadHairDB())
            return RandomHairStyle(gender, s.profile.age.years); // DB missing → legacy fallback

        int family = HairFamily(s.profile.hair_type);

        // 1) type+length
        List<Style> pool = _hairDB.styles.Where(st => FitsType(st, family) && FitsLength(st, lengthU)).ToList();
        // 2) type-only
        if (pool.Count == 0) pool = _hairDB.styles.Where(st => FitsType(st, family)).ToList();
        // 3) anything
        if (pool.Count == 0) pool = _hairDB.styles.ToList();

        // light gender weighting
        var maleNames = new HashSet<string>{
            "Clean Shave","Stubble","Horseshoe","Induction","Burr","Crew","Ivy","Caesar","Crop",
            "French Crop","Textured Crop","Curly Crop","Waves","High & Tight","Short Fringe",
            "Shag (Mini)","Curtain (Mini)","Flat Top","Slick Back","Quiff","Pompadour","Comb-Over",
            "Bowl","Wolf","Mullet","Undercut","Mohawk","Faux Hawk","Deathhawk"
        };
        var femaleNames = new HashSet<string>{
            "Pixie","Wavy Pixie","Curly Pixie","Bob","Lob","Wavy Bob","Curly Bob","A-Line Bob","Asym Bob",
            "Layers","One-Length","Shag","Curtain","Face-Frame","Layered Curls","Silk Press","Puffs",
            "Cornrows — Design","Stitch Braids","Lemonade Braids","Feed-Ins","Box Braids","Jumbo Braids",
            "Jumbo Box Braids","Knotless Braids","Boho Braids","Goddess Box Braids","Fulani Braids",
            "Senegalese Twists","Marley Twists","Passion Twists","Faux Locs","Goddess Locs","Crochet",
            "Ponytail","Braided Ponytail","Bun","Top Knot","Space Buns","Half-Up","Crown Braid",
            "Chignon","French Twist","Drawstring Ponytail","Hime","Fishtail Braid","3-Strand Braid"
        };

        float total = 0f;
        var weights = new float[pool.Count];
        for (int i = 0; i < pool.Count; i++)
        {
            float w = 1f;
            string dn = pool[i].display_name ?? pool[i].id;
            if (gender == characterStats.GenderType.Man && maleNames.Contains(dn)) w += 1.5f;
            if (gender == characterStats.GenderType.Woman && femaleNames.Contains(dn)) w += 1.5f;
            weights[i] = w; total += w;
        }
        float r = UnityEngine.Random.Range(0f, total), acc = 0f;
        for (int i = 0; i < pool.Count; i++) { acc += weights[i]; if (r <= acc) return pool[i].id; }
        return pool[UnityEngine.Random.Range(0, pool.Count)].id;
    }

    static readonly string[] AllSections = { "front", "top", "left", "right", "back" };
    static MinMax Sec(Sections s, string k) => k switch
    {
        "front" => s.front,
        "top" => s.top,
        "left" => s.left,
        "right" => s.right,
        "back" => s.back,
        _ => null
    };

    static characterStats.HabitsHobbiesStats.Frequency Every(int amount, characterStats.HabitsHobbiesStats.FrequencyUnit unit)
    {
        return new characterStats.HabitsHobbiesStats.Frequency
        {
            mode = characterStats.HabitsHobbiesStats.FrequencyMode.Every,
            every_amount = Mathf.Max(1, amount),
            every_unit = unit
        };
    }
    static characterStats.HabitsHobbiesStats.Frequency TimesPer(int count, characterStats.HabitsHobbiesStats.FrequencyUnit unit)
    {
        return new characterStats.HabitsHobbiesStats.Frequency
        {
            mode = characterStats.HabitsHobbiesStats.FrequencyMode.TimesPer,
            times_per_count = Mathf.Max(1, count),
            times_per_unit = unit
        };
    }
    static T Pick<T>(IList<T> list) => list[UnityEngine.Random.Range(0, list.Count)];
    static List<string> PickSome(IEnumerable<string> src, int min, int max)
        => src.OrderBy(_ => UnityEngine.Random.value).Take(UnityEngine.Random.Range(min, max + 1)).ToList();

    static readonly string[] _sessShort = { "1–2", "5–10", "10–20" };
    static readonly string[] _sessMed = { "20–40", "30–60" };

    static readonly string[] _triggersCommon =
    {
        "on wake","before bed","at bedtime","before a meal","after a meal",
        "at breakfast time","at lunch time","at dinner time",
        "when thirsty","when bladder feels full","after toilet","after shower",
        "on alarm","on daily reset"
    };

    static readonly (string type, string name)[] _hobbyPool =
    {
        ("Creative","Drawing"), ("Creative","Creative writing"),
        ("Music & movement","Rap"), ("Music & movement","Sing"),
        ("Games","PC game"), ("Games","Console game"), ("Games","Board game"),
        ("Sports & outdoor","Basketball"), ("Sports & outdoor","Soccer"),
        ("Mindful & reflective","Meditation"), ("Story & comedy","Jokes & bits")
    };

    // ---- Generator ----

    // --- Conditions (tiny chance) -----------------------------------------------
    const string kConditionsRoot = "thoughtsAndFeelings.feeling_categories.negative_feelings.health_conditions";
    const float kChanceOneCondition = .9f;
    const float kChanceTwoConditions = 0.005f;  // 0.5% → exactly two (total 5% incidence)

    // reflection helpers (trimmed copy of StatsService helpers)
    static object GetByPath(object root, string path)
    {
        object cur = root;
        foreach (var seg in path.Split('.'))
        {
            if (cur == null) return null;
            var t = cur.GetType();
            var f = t.GetField(seg, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null) { cur = f.GetValue(cur); continue; }
            var p = t.GetProperty(seg, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null) { cur = p.GetValue(cur); continue; }
            return null;
        }
        return cur;
    }
    static MemberInfo GetMember(Type t, string name) =>
        (MemberInfo)t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
    ?? t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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
    static object Coerce(object value, Type targetType)
    {
        if (value == null) return null;
        var vType = value.GetType();
        if (targetType.IsAssignableFrom(vType)) return value;
        if (targetType == typeof(bool))
        {
            if (value is int ii) return ii != 0;
            if (value is float ff) return Mathf.Abs(ff) > 0.0001f;
            if (value is string ss) return bool.Parse(ss);
        }
        return Convert.ChangeType(value, targetType);
    }
    static void SetByPath(object root, string path, object value)
    {
        var parts = path.Split('.');
        var containers = new List<object>();
        var accessors = new List<MemberInfo>();

        object cur = root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var m = GetMember(cur.GetType(), parts[i]);
            containers.Add(cur);
            accessors.Add(m);

            var next = GetMemberValue(m, cur);
            if (next == null)
            {
                var t = GetMemberType(m);
                var created = Activator.CreateInstance(t);
                SetMemberValue(m, cur, created);
                next = created;
            }
            cur = next;
        }

        var last = GetMember(cur.GetType(), parts[^1]);
        var cast = Coerce(value, GetMemberType(last));
        SetMemberValue(last, cur, cast);

        if (cur.GetType().IsValueType && !(cur is string))
        {
            object child = cur;
            for (int i = containers.Count - 1; i >= 0; i--)
            {
                SetMemberValue(accessors[i], containers[i], child);
                child = containers[i];
            }
        }
    }

    static List<string> DiscoverConditionPaths(characterStats s)
    {
        // Prefer whatever the UI exposes (ConditionAddButton.FullPath)
        var uiButtons = Resources.FindObjectsOfTypeAll<ConditionAddButton>();
        if (uiButtons != null && uiButtons.Length > 0)
            return uiButtons.Select(b => b.FullPath).Distinct().ToList();

        // Fallback: reflect booleans under health_conditions
        var root = GetByPath(s, kConditionsRoot);
        if (root == null) return new List<string>();
        var t = root.GetType();
        var members = t.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return members
            .Where(m =>
            {
                var mt = GetMemberType(m);
                return mt == typeof(bool);
            })
            .Select(m => $"{kConditionsRoot}.{m.Name}")
            .Distinct()
            .ToList();
    }

    static void RandomizeConditions(characterStats s)
    {
        // reflect the available booleans under health_conditions
        var pool = DiscoverConditionPaths(s);
        if (pool.Count == 0) return;

        // quick lookup helpers
        bool Has(string key) => pool.Any(p => p.EndsWith("." + key));
        string PathOf(string key) => pool.First(p => p.EndsWith("." + key));
        bool Flip(float p) => UnityEngine.Random.value < Mathf.Clamp01(p);

        int age = Mathf.Clamp(s.profile.age.years, 0, 110);
        var sex = s.profile.sex; // characterStats.SexType

        // --- First pass: sample "driver" flags that influence others
        bool sick = false, insomniac = false;

        if (Has("sick"))
        {
            float p =
                age < 12 ? 0.08f :
                age < 20 ? 0.06f :
                age < 60 ? 0.05f : 0.07f;
            sick = Flip(p);
            if (sick) SetByPath(s, PathOf("sick"), true);
        }

        if (Has("insomniac"))
        {
            float p =
                age < 13 ? 0.05f :
                age < 65 ? 0.10f : 0.15f;
            insomniac = Flip(p);
            if (insomniac) SetByPath(s, PathOf("insomniac"), true);
        }

        // --- Second pass: per-condition IRL-ish base rates, with a few modifiers
        foreach (var path in pool)
        {
            string key = path.Substring(path.LastIndexOf('.') + 1);
            if (key == "sick" || key == "insomniac") continue; // already handled

            float p = key switch
            {
                // Acute symptoms – daily point prevalence (low), boosted if "sick"
                "nauseous" => 0.02f,
                "dizzy" => age >= 60 ? 0.05f : 0.02f,
                "headache" => 0.12f, // headaches are common
                "lightheaded" => age >= 60 ? 0.03f : 0.015f,
                "vertiginous" => age >= 60 ? 0.03f : 0.01f,
                "disoriented" => age >= 60 ? 0.03f : 0.01f,
                "blurred_vision" => age >= 60 ? 0.10f : 0.02f,

                // Longer-term conditions – rough population rates with age/sex tilt
                "acne" => age switch
                {
                    < 8 => 0.05f,
                    < 12 => 0.20f,
                    <= 24 => 0.30f,
                    <= 34 => 0.12f,
                    <= 44 => 0.08f,
                    _ => 0.03f
                },
                "tinnitus" => age switch
                {
                    < 30 => 0.05f,
                    < 50 => 0.10f,
                    < 65 => 0.15f,
                    _ => 0.25f
                },
                "gingivitis" => age switch
                {
                    < 12 => 0.05f,
                    < 20 => 0.12f,
                    < 40 => 0.25f,
                    < 65 => 0.35f,
                    _ => 0.45f
                },
                "erectile_dysfunction" => sex == characterStats.SexType.Male ? (age switch
                {
                    < 20 => 0.01f,
                    < 30 => 0.03f,
                    < 40 => 0.05f,
                    < 50 => 0.10f,
                    < 60 => 0.20f,
                    < 70 => 0.30f,
                    _ => 0.40f
                }) : 0f,
                "ibs" => age < 12 ? 0.05f : 0.10f,

                "anxious" => age switch
                {
                    < 13 => 0.07f,
                    < 18 => 0.15f,
                    _ => 0.18f
                },
                "socially_anxious" => age switch
                {
                    < 15 => 0.05f,
                    <= 29 => 0.08f,
                    <= 64 => 0.05f,
                    _ => 0.03f
                },
                "depressed" => age switch
                {
                    < 13 => 0.02f,
                    < 18 => 0.05f,
                    _ => 0.08f
                },
                "bipolar" => age switch
                {
                    < 13 => 0.002f,
                    < 18 => 0.008f,
                    _ => 0.015f
                },
                "gender_dysphoric" => (age >= 13 && age <= 24) ? 0.007f : 0.005f,
                "narcissistic" => 0.01f,

                _ => 0.01f // safe tiny default for any future keys
            };

            // Modifiers from "driver" states
            if (sick && (key == "nauseous" || key == "dizzy" || key == "lightheaded" ||
                        key == "disoriented" || key == "blurred_vision" || key == "vertiginous"))
                p = Mathf.Clamp01(p * 3f);

            if (sick && key == "headache")
                p = Mathf.Clamp01(p * 1.5f);

            if (insomniac && key == "depressed")
                p = Mathf.Clamp01(p * 1.3f);

            if (Flip(p))
                SetByPath(s, path, true);
        }
    }



    static characterStats.HabitsHobbiesStats.Adherence AdhLoose()
    {
        var vals = (characterStats.HabitsHobbiesStats.Adherence[])
            System.Enum.GetValues(typeof(characterStats.HabitsHobbiesStats.Adherence));
        return vals.Length > 0 ? vals[0] : default;
    }
    static characterStats.HabitsHobbiesStats.Adherence AdhMedium()
    {
        var vals = (characterStats.HabitsHobbiesStats.Adherence[])
            System.Enum.GetValues(typeof(characterStats.HabitsHobbiesStats.Adherence));
        if (vals.Length == 0) return default;
        return vals[vals.Length / 2];
    }
    static characterStats.HabitsHobbiesStats.Adherence AdhStrict()
    {
        var vals = (characterStats.HabitsHobbiesStats.Adherence[])
            System.Enum.GetValues(typeof(characterStats.HabitsHobbiesStats.Adherence));
        return vals.Length > 0 ? vals[vals.Length - 1] : default;
    }

    private class HabitHobbyDefinition
    {
        public string Name { get; set; }
        public characterStats.HabitsHobbiesStats.Category Category { get; set; }
        public string Type { get; set; }
        public string[] SessionLengths { get; set; }
        public int MinAge { get; set; } = 0;
        public int MaxAge { get; set; } = 999;
        public float Likelihood { get; set; } = 0.5f; // Default 50% chance
        public bool IsCore { get; set; } = false; // Is this a fundamental habit everyone has?
        public List<string> Triggers { get; set; } = new List<string>();
    }

    static void RandomizeHabitsAndHobbies(characterStats s)
    {
        if (s.habitsAndHobbies == null) s.habitsAndHobbies = new characterStats.HabitsHobbiesStats();
        if (s.habitsAndHobbies.list == null) s.habitsAndHobbies.list = new List<characterStats.HabitsHobbiesStats.Entry>();
        var list = s.habitsAndHobbies.list;
        list.Clear();

        int age = s.profile.age.years;

        // ====================================================================
        // MASTER LIST OF ALL HABITS AND HOBBIES
        // This list is derived from your provided data, with logical assignments
        // for age, likelihood, and category to ensure balanced character generation.
        // ====================================================================
        var allDefinitions = new List<HabitHobbyDefinition>
        {
            // --- Core Bodily Routines (Guaranteed) ---
            new HabitHobbyDefinition { Name = "Sleep", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Bodily routines", SessionLengths = new[] { "Until complete" }, IsCore = true, Triggers = new List<string> { "at bedtime" } },
            new HabitHobbyDefinition { Name = "Eat", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Bodily routines", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, IsCore = true, Triggers = new List<string> { "at breakfast time", "at lunch time", "at dinner time" } },
            new HabitHobbyDefinition { Name = "Drink", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Bodily routines", SessionLengths = new[] { "1–2 minutes" }, IsCore = true, Triggers = new List<string> { "when thirsty" } },
            new HabitHobbyDefinition { Name = "Urinate", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Bodily routines", SessionLengths = new[] { "Until complete" }, IsCore = true, Triggers = new List<string> { "when bladder feels full" } },
            new HabitHobbyDefinition { Name = "Defecate", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Bodily routines", SessionLengths = new[] { "Until complete" }, IsCore = true, Likelihood = 0.98f },

            // --- Daily Self-care & Hygiene (High Likelihood / Optional) ---
            new HabitHobbyDefinition { Name = "Wash hands", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "1–2 minutes" }, IsCore = true, Triggers = new List<string> { "after toilet", "before a meal" } },
            new HabitHobbyDefinition { Name = "Shower", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "5–10 minutes", "10–20 minutes" }, Likelihood = 0.95f, MinAge = 5 },
            new HabitHobbyDefinition { Name = "Bathe", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.15f, MinAge = 5 },
            new HabitHobbyDefinition { Name = "Wash face", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "1–2 minutes" }, Likelihood = 0.65f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Brush teeth", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "1–2 minutes" }, Likelihood = 0.98f, MinAge = 3, Triggers = new List<string> { "on wake", "before bed" } },
            new HabitHobbyDefinition { Name = "Floss", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "1–2 minutes" }, Likelihood = 0.30f, MinAge = 8, Triggers = new List<string> { "before bed" } },
            new HabitHobbyDefinition { Name = "Rinse mouth", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Daily self-care & hygiene", SessionLengths = new[] { "1–2 minutes" }, Likelihood = 0.20f, MinAge = 6 },

            // --- Personal Maintenance & Appearance ---
            new HabitHobbyDefinition { Name = "Apply makeup", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Personal maintenance & appearance", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.25f, MinAge = 14 },
            new HabitHobbyDefinition { Name = "Cut nails", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Personal maintenance & appearance", SessionLengths = new[] { "5-10 minutes", "10–20 minutes" }, Likelihood = 0.8f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Paint nails", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Personal maintenance & appearance", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.15f, MinAge = 12 },
            
            // --- Fitness Routine ---
            new HabitHobbyDefinition { Name = "Strolling", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Fitness routine", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.4f, MinAge = 6 },
            new HabitHobbyDefinition { Name = "Walking", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Fitness routine", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.35f, MinAge = 6 },
            new HabitHobbyDefinition { Name = "Jogging", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Fitness routine", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.25f, MinAge = 12 },
            new HabitHobbyDefinition { Name = "Light running", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Fitness routine", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.15f, MinAge = 12 },
            new HabitHobbyDefinition { Name = "Stretching", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Fitness routine", SessionLengths = new[] { "5–10 minutes", "10–20 minutes" }, Likelihood = 0.3f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Exercising", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Fitness routine", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.2f, MinAge = 15 },
            
            // --- Reading/Writing & Thinking Habits ---
            new HabitHobbyDefinition { Name = "Journaling", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Reading/writing & thinking", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.20f, MinAge = 12 },
            new HabitHobbyDefinition { Name = "Pondering", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Reading/writing & thinking", SessionLengths = new[] { "5–10 minutes", "10–20 minutes" }, Likelihood = 0.4f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Checking in with emotions", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Reading/writing & thinking", SessionLengths = new[] { "5–10 minutes", "10–20 minutes" }, Likelihood = 0.25f, MinAge = 14 },
            new HabitHobbyDefinition { Name = "Daydreaming", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Reading/writing & thinking", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.5f, MinAge = 6 },

            // --- Tics & Recurring Social Patterns (Good & Bad) ---
            new HabitHobbyDefinition { Name = "Biting nails", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Tics & micro-expressions", SessionLengths = new[] { "Until complete" }, Likelihood = 0.15f, MinAge = 5 },
            new HabitHobbyDefinition { Name = "Biting lip", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Tics & micro-expressions", SessionLengths = new[] { "Until complete" }, Likelihood = 0.20f, MinAge = 7 },
            new HabitHobbyDefinition { Name = "Gossiping", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Recurring social patterns", SessionLengths = new[] { "10–20 minutes" }, Likelihood = 0.20f, MinAge = 13 },
            new HabitHobbyDefinition { Name = "Arguing", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Recurring social patterns", SessionLengths = new[] { "10–20 minutes" }, Likelihood = 0.15f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Interrupting", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Recurring social patterns", SessionLengths = new[] { "Until complete" }, Likelihood = 0.10f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Lying", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Recurring social patterns", SessionLengths = new[] { "Until complete" }, Likelihood = 0.08f, MinAge = 8 },
            
            // --- Commuting & Errands ---
            new HabitHobbyDefinition { Name = "Driving", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Commuting & errands", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.85f, MinAge = 16 },
            new HabitHobbyDefinition { Name = "Shopping", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Commuting & errands", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.7f, MinAge = 18 },
            new HabitHobbyDefinition { Name = "Checking mail", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Commuting & errands", SessionLengths = new[] { "1–2 minutes", "5–10 minutes" }, Likelihood = 0.6f, MinAge = 18 },

            // --- Sexual/Private (Adult Only) ---
            new HabitHobbyDefinition { Name = "Masturbating", Category = characterStats.HabitsHobbiesStats.Category.Habit, Type = "Sexual/private (adult)", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.5f, MinAge = 18 },

            // ========================= HOBBIES =========================
            // --- Creative ---
            new HabitHobbyDefinition { Name = "Creative writing", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Creative", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.15f, MinAge = 12 },
            new HabitHobbyDefinition { Name = "Drawing", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Creative", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.20f, MinAge = 7 },
            new HabitHobbyDefinition { Name = "Painting", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Creative", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.10f, MinAge = 10 },

            // --- Music & Movement ---
            new HabitHobbyDefinition { Name = "Singing", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Music & movement", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.25f, MinAge = 5 },
            new HabitHobbyDefinition { Name = "Dancing", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Music & movement", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.18f, MinAge = 7 },
            new HabitHobbyDefinition { Name = "Rapping", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Music & movement", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.08f, MinAge = 12 },
            new HabitHobbyDefinition { Name = "Vocal exercises", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Music & movement", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.05f, MinAge = 10 },

            // --- Games ---
            new HabitHobbyDefinition { Name = "Playing PC games", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.35f, MinAge = 8 },
            new HabitHobbyDefinition { Name = "Playing console games", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.30f, MinAge = 6 },
            new HabitHobbyDefinition { Name = "Playing mobile games", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.4f, MinAge = 6 },
            new HabitHobbyDefinition { Name = "Playing board games", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.15f, MinAge = 6 },
            new HabitHobbyDefinition { Name = "Playing cards", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.20f, MinAge = 8 },
            new HabitHobbyDefinition { Name = "Playing tag", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.25f, MinAge = 4, MaxAge = 12 },
            new HabitHobbyDefinition { Name = "Playing hide and seek", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Games", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.25f, MinAge = 4, MaxAge = 10 },

            // --- Sports & Outdoor ---
            new HabitHobbyDefinition { Name = "Playing basketball", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Sports & outdoor", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.12f, MinAge = 8 },
            new HabitHobbyDefinition { Name = "Playing soccer", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Sports & outdoor", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.12f, MinAge = 7 },
            new HabitHobbyDefinition { Name = "Hiking", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Sports & outdoor", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.15f, MinAge = 10 },
            new HabitHobbyDefinition { Name = "Target practice", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Sports & outdoor", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.07f, MinAge = 14 },
            
            // --- Fitness as Pastime ---
            new HabitHobbyDefinition { Name = "Strength training", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Fitness as pastime", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.20f, MinAge = 16 },
            new HabitHobbyDefinition { Name = "Treadmill sessions", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Fitness as pastime", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.15f, MinAge = 16 },

            // --- Food & Home ---
            new HabitHobbyDefinition { Name = "Cooking", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Food & home", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.25f, MinAge = 14 },
            new HabitHobbyDefinition { Name = "Mixology/Hosting", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Food & home", SessionLengths = new[] { "20–40 minutes" }, Likelihood = 0.08f, MinAge = 21 },
            
            // --- Mindful & Reflective ---
            new HabitHobbyDefinition { Name = "Meditating", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Mindful & reflective", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.10f, MinAge = 16 },
            new HabitHobbyDefinition { Name = "Reading for pleasure", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Mindful & reflective", SessionLengths = new[] { "30–60 minutes", "60–90 minutes" }, Likelihood = 0.30f, MinAge = 7 },
            
            // --- Collecting/Consumer ---
            new HabitHobbyDefinition { Name = "Collecting", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Collecting/consumer", SessionLengths = new[] { "20–40 minutes", "30–60 minutes" }, Likelihood = 0.12f, MinAge = 8 },
            new HabitHobbyDefinition { Name = "Thrifting", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Collecting/consumer", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.10f, MinAge = 15 },
            
            // --- Social / Community ---
            new HabitHobbyDefinition { Name = "Volunteering", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Community & causes", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.05f, MinAge = 16 },
            new HabitHobbyDefinition { Name = "Participating in clubs", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Clubs & discourse", SessionLengths = new[] { "60–90 minutes" }, Likelihood = 0.10f, MinAge = 14 },
            new HabitHobbyDefinition { Name = "Partying", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Celebration & leisure", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.30f, MinAge = 16 },
            new HabitHobbyDefinition { Name = "Hosting friends", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Celebration & leisure", SessionLengths = new[] { "60–90 minutes", "Indefinitely" }, Likelihood = 0.20f, MinAge = 16 },
            
            // --- Story & Comedy ---
            new HabitHobbyDefinition { Name = "Telling jokes and stories", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Story & comedy", SessionLengths = new[] { "10–20 minutes", "20–40 minutes" }, Likelihood = 0.25f, MinAge = 8 },
            new HabitHobbyDefinition { Name = "Pranking", Category = characterStats.HabitsHobbiesStats.Category.Hobby, Type = "Story & comedy", SessionLengths = new[] { "10–20 minutes" }, Likelihood = 0.07f, MinAge = 10 },
        };

        // --- Helper function to create an Entry ---
        characterStats.HabitsHobbiesStats.Entry CreateEntryFromDef(HabitHobbyDefinition def, characterStats.HabitsHobbiesStats.Frequency frequency, characterStats.HabitsHobbiesStats.Adherence adherence)
        {
            return new characterStats.HabitsHobbiesStats.Entry
            {
                category = def.Category,
                type = def.Type,
                name = def.Name,
                session_length = def.SessionLengths[UnityEngine.Random.Range(0, def.SessionLengths.Length)],
                frequency = frequency,
                adherence = adherence,
                triggers = new List<string>(def.Triggers) // Create a copy
            };
        }

        // --- 1. Add Core Habits ---
        var coreHabits = allDefinitions.Where(d => d.IsCore);
        foreach (var habitDef in coreHabits)
        {
            // Core bodily functions have specific frequencies from original logic
            characterStats.HabitsHobbiesStats.Frequency freq;
            if (habitDef.Name == "Eat") freq = TimesPer(UnityEngine.Random.Range(2, 5), characterStats.HabitsHobbiesStats.FrequencyUnit.Day);
            else if (habitDef.Name == "Drink") freq = TimesPer(UnityEngine.Random.Range(6, 11), characterStats.HabitsHobbiesStats.FrequencyUnit.Day);
            else if (habitDef.Name == "Urinate") freq = TimesPer(UnityEngine.Random.Range(4, 8), characterStats.HabitsHobbiesStats.FrequencyUnit.Day);
            else if (habitDef.Name == "Defecate") freq = TimesPer(UnityEngine.Random.Range(1, 3), characterStats.HabitsHobbiesStats.FrequencyUnit.Day);
            else if (habitDef.Name == "Wash hands") freq = TimesPer(UnityEngine.Random.Range(6, 13), characterStats.HabitsHobbiesStats.FrequencyUnit.Day);
            else freq = Every(1, characterStats.HabitsHobbiesStats.FrequencyUnit.Day);

            list.Add(CreateEntryFromDef(habitDef, freq, AdhMedium()));
        }

        // --- 2. Add other Non-Core Habits based on Likelihood ---
        var potentialHabits = allDefinitions.Where(d => !d.IsCore && d.Category == characterStats.HabitsHobbiesStats.Category.Habit);
        foreach (var habitDef in potentialHabits)
        {
            if (age >= habitDef.MinAge && age <= habitDef.MaxAge && UnityEngine.Random.value < habitDef.Likelihood)
            {
                characterStats.HabitsHobbiesStats.Frequency freq;
                characterStats.HabitsHobbiesStats.Adherence adh;

                // Assign reasonable default frequencies and adherences based on type
                if (habitDef.Type == "Daily self-care & hygiene" || habitDef.Type == "Personal maintenance & appearance")
                {
                    freq = (habitDef.Name == "Brush teeth")
                        ? TimesPer(2, characterStats.HabitsHobbiesStats.FrequencyUnit.Day)
                        : Every(age < 12 ? UnityEngine.Random.Range(1, 3) : 1, characterStats.HabitsHobbiesStats.FrequencyUnit.Day);
                    adh = AdhMedium();
                }
                else if (habitDef.Type == "Fitness routine")
                {
                    freq = TimesPer(UnityEngine.Random.Range(2, 5), characterStats.HabitsHobbiesStats.FrequencyUnit.Week);
                    adh = AdhLoose();
                }
                else // For tics, social patterns, adult habits, errands, etc.
                {
                    freq = TimesPer(UnityEngine.Random.Range(1, 6), characterStats.HabitsHobbiesStats.FrequencyUnit.Week);
                    adh = AdhLoose();
                }
                list.Add(CreateEntryFromDef(habitDef, freq, adh));
            }
        }

        // --- 3. Add Hobbies based on Likelihood ---
        int hobbyCount = 0;
        int maxHobbies = UnityEngine.Random.Range(3, 8); // Let's aim for 3-7 hobbies

        var potentialHobbies = allDefinitions
            .Where(d => d.Category == characterStats.HabitsHobbiesStats.Category.Hobby && age >= d.MinAge && age <= d.MaxAge)
            .OrderBy(_ => UnityEngine.Random.value) // Shuffle the list
            .ToList();

        foreach (var hobbyDef in potentialHobbies)
        {
            if (hobbyCount >= maxHobbies) break;

            if (UnityEngine.Random.value < hobbyDef.Likelihood)
            {
                var freq = TimesPer(UnityEngine.Random.Range(1, 4), characterStats.HabitsHobbiesStats.FrequencyUnit.Week);
                list.Add(CreateEntryFromDef(hobbyDef, freq, AdhLoose()));
                hobbyCount++;
            }
        }
    }



    // Helper for generating skin stats based on age - Head
    static void GenerateSkinStatsHead(characterStats.HeadStats.SkinStats skinStats, int age, characterStats.SexType sex)
    {
        // Hydration peaks in youth and gradually declines
        skinStats.hydration = StatLimits.AgeScaledValue(age, 20, 35, 90, 70, 1.2f);

        // Smoothness best in youth, declines with age
        skinStats.smoothness = StatLimits.AgeScaledValue(age, 18, 30, 95, 70, 1.5f);

        // Elasticity highest in youth, significantly declines with age
        skinStats.elasticity = StatLimits.AgeScaledValue(age, 16, 25, 95, 75, 2.0f);

        // Stretch marks can appear at various life stages
        int stretchBase = 5;
        if (age >= 12 && age <= 20) stretchBase += 15; // Growth spurts
        if (age >= 20 && age <= 40 && sex == characterStats.SexType.Female) stretchBase += 10; // Pregnancy years
        if (age >= 30) stretchBase += age / 10; // General aging
        skinStats.stretch_marks = Mathf.Clamp(stretchBase + UnityEngine.Random.Range(-10, 10), 0, 100);
    }

    // Helper for generating skin stats based on age - Arms
    static void GenerateSkinStatsArms(characterStats.ArmsStats.SkinStats skinStats, int age, characterStats.SexType sex)
    {
        // Hydration peaks in youth and gradually declines
        skinStats.hydration = StatLimits.AgeScaledValue(age, 20, 35, 90, 70, 1.2f);

        // Smoothness best in youth, declines with age
        skinStats.smoothness = StatLimits.AgeScaledValue(age, 18, 30, 95, 70, 1.5f);

        // Elasticity highest in youth, significantly declines with age
        skinStats.elasticity = StatLimits.AgeScaledValue(age, 16, 25, 95, 75, 2.0f);
    }

    // Helper for generating skin stats - Neck
    static void GenerateSkinStatsNeck(characterStats.NeckStats.SkinStats skinStats, int age, characterStats.SexType sex)
    {
        skinStats.hydration = StatLimits.AgeScaledValue(age, 20, 35, 90, 70, 1.2f);
        skinStats.smoothness = StatLimits.AgeScaledValue(age, 18, 30, 95, 70, 1.5f);
        skinStats.elasticity = StatLimits.AgeScaledValue(age, 16, 25, 95, 75, 2.0f);

        int stretchBase = 5;
        if (age >= 12 && age <= 20) stretchBase += 15;
        if (age >= 20 && age <= 40 && sex == characterStats.SexType.Female) stretchBase += 10;
        if (age >= 30) stretchBase += age / 10;
        skinStats.stretch_marks = Mathf.Clamp(stretchBase + UnityEngine.Random.Range(-10, 10), 0, 100);
    }

    // Helper for generating skin stats - Torso
    static void GenerateSkinStatsTorso(characterStats.TorsoStats.SkinStats skinStats, int age, characterStats.SexType sex)
    {
        skinStats.hydration = StatLimits.AgeScaledValue(age, 20, 35, 90, 70, 1.2f);
        skinStats.smoothness = StatLimits.AgeScaledValue(age, 18, 30, 95, 70, 1.5f);
        skinStats.elasticity = StatLimits.AgeScaledValue(age, 16, 25, 95, 75, 2.0f);

        int stretchBase = 5;
        if (age >= 12 && age <= 20) stretchBase += 15;
        if (age >= 20 && age <= 40 && sex == characterStats.SexType.Female) stretchBase += 10;
        if (age >= 30) stretchBase += age / 10;
        skinStats.stretch_marks = Mathf.Clamp(stretchBase + UnityEngine.Random.Range(-10, 10), 0, 100);
    }

    // Helper for generating skin stats - Legs
    static void GenerateSkinStatsLegs(characterStats.LegsStats.SkinStretchStats skinStats, int age, characterStats.SexType sex)
    {
        skinStats.hydration = StatLimits.AgeScaledValue(age, 20, 35, 90, 70, 1.2f);
        skinStats.smoothness = StatLimits.AgeScaledValue(age, 18, 30, 95, 70, 1.5f);
        skinStats.elasticity = StatLimits.AgeScaledValue(age, 16, 25, 95, 75, 2.0f);

        int stretchBase = 5;
        if (age >= 12 && age <= 20) stretchBase += 15;
        if (age >= 20 && age <= 40 && sex == characterStats.SexType.Female) stretchBase += 10;
        if (age >= 30) stretchBase += age / 10;
        skinStats.stretch_marks = Mathf.Clamp(stretchBase + UnityEngine.Random.Range(-10, 10), 0, 100);
    }

    // Helper for generating hair styles with gender weighting
    static string RandomHairStyle(characterStats.GenderType gender, int age)
    {
        // Full catalog from hairstyles.min.json (display_name)
        string[] ALL = new string[] {
            "Clean Shave","Stubble","Horseshoe","Induction","Burr","Crew","Ivy","Caesar","Crop",
            "French Crop","Textured Crop","Curly Crop","Sponge Coils","Brush Coils","Waves","High & Tight",
            "Short Fringe","Shag (Mini)","Curtain (Mini)","TWA","Finger Coils","Twist-Out","Braid-Out",
            "Bantu Knots","Mini Afro","Mini Twists","Mini Braids","Flat Top","Pixie","Wavy Pixie",
            "Curly Pixie","Bob","Lob","Wavy Bob","Curly Bob","A-Line Bob","Asym Bob","Layers",
            "One-Length","Shag","Curtain","Slick Back","Quiff","Pompadour","Comb-Over","Bowl","Wolf",
            "Mullet","Hime","Face-Frame","Afro","Layered Curls","Wash-n-Go","Silk Press","Puffs",
            "Cornrows — Straight-Backs","Cornrows — Design","Stitch Braids","Lemonade Braids","Feed-Ins",
            "Box Braids","Jumbo Braids","Jumbo Box Braids","Knotless Braids","Boho Braids",
            "Goddess Box Braids","Fulani Braids","Two-Strand Twists","Flat Twists","Senegalese Twists",
            "Marley Twists","Passion Twists","Faux Locs","Locs","Goddess Locs","Crochet","Ponytail",
            "Braided Ponytail","Bun","Top Knot","Space Buns","Half-Up","Crown Braid","Chignon",
            "French Twist","Drawstring Ponytail","Mohawk","Faux Hawk","Undercut","Deathhawk",
            "3-Strand Braid","Fishtail Braid"
        };

        // Heavily male-coded styles
        var MALE = new System.Collections.Generic.HashSet<string> {
            "Clean Shave","Stubble","Horseshoe","Induction","Burr","Crew","Ivy","Caesar","Crop",
            "French Crop","Textured Crop","Curly Crop","Waves","High & Tight","Short Fringe",
            "Shag (Mini)","Curtain (Mini)","Flat Top","Slick Back","Quiff","Pompadour","Comb-Over",
            "Bowl","Wolf","Mullet","Undercut","Mohawk","Faux Hawk","Deathhawk"
        };

        // Heavily female-coded styles
        var FEMALE = new System.Collections.Generic.HashSet<string> {
            "Pixie","Wavy Pixie","Curly Pixie","Bob","Lob","Wavy Bob","Curly Bob","A-Line Bob","Asym Bob",
            "Layers","One-Length","Shag","Curtain","Face-Frame","Layered Curls","Silk Press","Puffs",
            "Cornrows — Design","Stitch Braids","Lemonade Braids","Feed-Ins","Box Braids","Jumbo Braids",
            "Jumbo Box Braids","Knotless Braids","Boho Braids","Goddess Box Braids","Fulani Braids",
            "Senegalese Twists","Marley Twists","Passion Twists","Faux Locs","Goddess Locs","Crochet",
            "Ponytail","Braided Ponytail","Bun","Top Knot","Space Buns","Half-Up","Crown Braid",
            "Chignon","French Twist","Drawstring Ponytail","Hime","Fishtail Braid","3-Strand Braid"
        };

        // Compute neutral set = ALL - (MALE ∪ FEMALE)
        var neutral = new System.Collections.Generic.List<string>();
        foreach (var s in ALL)
            if (!MALE.Contains(s) && !FEMALE.Contains(s)) neutral.Add(s);

        // Bucket pick (Man/Woman/Other) with weights
        System.Collections.Generic.List<string> pool;
        float r = UnityEngine.Random.value;

        if (gender == characterStats.GenderType.Man)
        {
            if (r < 0.65f && MALE.Count > 0) pool = new System.Collections.Generic.List<string>(MALE);
            else if (r < 0.95f && neutral.Count > 0) pool = neutral;
            else pool = FEMALE.Count > 0 ? new System.Collections.Generic.List<string>(FEMALE)
                                        : new System.Collections.Generic.List<string>(ALL);
        }
        else if (gender == characterStats.GenderType.Woman)
        {
            if (r < 0.65f && FEMALE.Count > 0) pool = new System.Collections.Generic.List<string>(FEMALE);
            else if (r < 0.95f && neutral.Count > 0) pool = neutral;
            else pool = MALE.Count > 0 ? new System.Collections.Generic.List<string>(MALE)
                                    : new System.Collections.Generic.List<string>(ALL);
        }
        else
        {
            // Non-binary/unspecified: mostly neutral, slight tilt random
            if (r < 0.70f && neutral.Count > 0) pool = neutral;
            else if (r < 0.85f && FEMALE.Count > 0) pool = new System.Collections.Generic.List<string>(FEMALE);
            else pool = MALE.Count > 0 ? new System.Collections.Generic.List<string>(MALE)
                                    : new System.Collections.Generic.List<string>(ALL);
        }

        return pool[UnityEngine.Random.Range(0, pool.Count)];
    }


    // Helper for generating hair color
    static string RandomHairColor(int age)
    {
        string[] colors = { "black", "dark brown", "brown", "light brown", "blonde", "red", "auburn" };

        // Gray/white hair chances increase with age
        if (age > 30 && UnityEngine.Random.value < (age - 30) / 100f)
        {
            if (age > 60 && UnityEngine.Random.value < 0.5f)
                return "white";
            return UnityEngine.Random.value < 0.5f ? "gray" : "salt and pepper";
        }

        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }

    static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s ?? "") h = h * 31 + c;
            return (h & 0x7fffffff);
        }
    }

    // Skews toward the lower end of [min, max]. Higher skew => stronger low bias.
    static int LowBiased(int min, int max, float skew = 1.8f)
    {
        if (max <= min) return min;
        float t = Mathf.Pow(UnityEngine.Random.value, skew); // 0..1, skew>1 biases low
        int v = min + Mathf.RoundToInt(t * (max - min));
        return Mathf.Clamp(v, min, max);
    }

    // Helper for generating foot size based on age and sex.
    static int GenerateFootSize(int age, characterStats.SexType sex)
    {
        // Based on US shoe sizes, mapped to indices 0-57.
        // C = Child, Y = Youth, M = Men's

        // Ages 0-4 (Toddler sizes 0C-12C, indices 0-24)
        if (age < 1) return UnityEngine.Random.Range(0, 8);   // ~0-9 months: 0C-3.5C
        if (age < 2) return UnityEngine.Random.Range(6, 15);  // ~1 year: 3C-7C
        if (age < 3) return UnityEngine.Random.Range(12, 19); // ~2 years: 6C-9C
        if (age < 5) return UnityEngine.Random.Range(16, 25); // ~3-4 years: 8C-12C

        // Ages 5-11 (Child/Youth sizes 12.5C-6Y, indices 25-38)
        if (age < 7) return UnityEngine.Random.Range(24, 31); // ~5-6 years: 12C-2Y
        if (age < 10) return UnityEngine.Random.Range(28, 37); // ~7-9 years: 1Y-5.5Y
        if (age < 12) return UnityEngine.Random.Range(34, 41); // ~10-11 years: 4Y-7Y

        // Ages 12-17 (Teen transition to adult sizes)
        if (age < 18)
        {
            // Define adult mean size based on sex
            int adultMean;
            if (sex == characterStats.SexType.Male) adultMean = 48; // 10.5M
            else adultMean = 44; // 9M

            // Linearly progress towards the adult mean size from age 11 to 17
            float progress = Mathf.InverseLerp(11f, 17f, age); // 0.0 to 1.0

            // Start from a pre-teen base and move towards the adult mean
            int startSize = 38; // ~6Y
            int currentMean = Mathf.RoundToInt(Mathf.Lerp(startSize, adultMean, progress));

            // Add some variance
            return Mathf.Clamp(currentMean + UnityEngine.Random.Range(-2, 3), 34, 57);
        }

        // Ages 18+ (Adult sizes)
        int mean, stdDev;
        if (sex == characterStats.SexType.Male)
        {
            mean = 48; // 10.5M
            stdDev = 3; // Allows for range roughly 8.5M to 12.5M
        }
        else if (sex == characterStats.SexType.Female)
        {
            mean = 44; // 9M
            stdDev = 3; // Allows for range roughly 7.5M to 10.5M
        }
        else
        { // Intersex
            mean = 46; // 9.5M
            stdDev = 4; // Wider range
        }

        // Simple normal-like distribution by averaging two random ranges
        int r1 = UnityEngine.Random.Range(mean - stdDev, mean + stdDev + 1);
        int r2 = UnityEngine.Random.Range(mean - stdDev, mean + stdDev + 1);
        int finalSize = (r1 + r2) / 2;

        return Mathf.Clamp(finalSize, 0, 57);
    }

    static readonly string[] _defaultLearnOnce = {
    "Sitting", "Crawling", "Walking", "Toilet Independence",
    "Counting", "Reading", "Writing",
    "Ride Bicycle", "Swim Basics", "Drive", "Kneel", "Genuflect", "Crouch",
    "Showering/Bathing", "Handwashing",
    "Brush Teeth", "Floss", "Wash Face", "Rinse Mouth",
    "Wash Hair", "Nail Care", "Open/Close", "Place", "Pour",
    "Wink", "Wave", "Nod"
    };


    static void EnsureDefaultLearnOnce(characterStats s)
    {
        if (s.skills == null) s.skills = new characterStats.SkillsStats();
        if (s.skills.list == null) s.skills.list = new System.Collections.Generic.List<characterStats.SkillEntry>();
        if (s.skills.list.Count == 0)
            foreach (var id in _defaultLearnOnce)
                s.skills.AddOrGet(id, characterStats.SkillMode.LearnOnce);
    }

    static readonly string[] _defaultImprovable = {
        "Sprinting","Jumping","Climbing","Flexibility","Balance","Lifting & Carrying","Pushing & Pulling","Aiming/Targeting",

        "Push-Ups","Situps","Pullups","Curls","Benchpress",

        "Drawing","Painting (Art)", "Dance", "Creative Writing",

        "Meditation","Decision Making","Problem Solving","Emotional Awareness","Imagination",

        "Basketball","Soccer","Board Games","Card Games","Gaming",

        "Driving","Navigation",

        "Voice Control","Conversation","Active Listening","Public Speaking","Storytelling","Debate & Rhetoric","Persuasion","Negotiation","Conflict Resolution","Feedback Delivery",

        "Empathy","Comforting/Consoling","Apologizing & Forgiving","Complimenting","Flirting",
        "Networking","Collaboration","Mentoring/Teaching","Hosting & Facilitation",
        "Motivate","Rally","Planning","Joking","Roasting","Lying","Impersonating","Pranking",

        "Kissing","Making Out","Sex","Oral Sex","Sexual Teasing"
    };

    static void EnsureDefaultImprovable(characterStats s)
    {
        if (s.skills == null) s.skills = new characterStats.SkillsStats();
        if (s.skills.list == null) s.skills.list = new System.Collections.Generic.List<characterStats.SkillEntry>();

        // Add if missing; DO NOT reset values
        foreach (var id in _defaultImprovable)
            s.skills.AddOrGet(id, characterStats.SkillMode.Improvable);
    }



    static (float start, float full) LearnOnceWindow(string id)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        switch (k)
        {
            // motor & self-care (infancy → toddler)
            case "sitting": return (0.25f, 0.75f);   // ~3–9 mo
            case "crawling": return (0.50f, 1.10f);   // ~6–13 mo
            case "walking": return (0.75f, 1.40f);   // ~9–17 mo
            case "kneel": return (0.80f, 2.00f);
            case "crouch": return (0.80f, 2.00f);
            case "genuflect": return (2.50f, 6.00f);   // cultural/intentional

            // hygiene & routines (early childhood)
            case "showering/bathing":
            case "showering":
            case "bathing": return (3.00f, 7.00f);
            case "handwashing":
            case "wash hands": return (2.00f, 5.00f);
            case "brush teeth": return (2.50f, 7.00f);
            case "floss": return (4.50f, 10.00f); // adoption varies
            case "wash face": return (2.50f, 5.00f);
            case "rinse mouth": return (2.50f, 4.50f);
            case "wash hair": return (3.50f, 8.00f);
            case "nail care": return (4.00f, 9.00f);

            // fine-motor manipulation
            case "open/close":
            case "open":
            case "close": return (0.50f, 1.50f);
            case "place": return (0.50f, 1.50f);
            case "pour": return (2.00f, 4.00f);

            // communication & expressions
            case "wave": return (0.60f, 2.00f);
            case "nod": return (0.80f, 2.00f);
            case "wink": return (3.50f, 7.00f);

            // academics & mobility
            case "toilet independence": return (2.00f, 3.50f);
            case "counting": return (3.00f, 6.00f);
            case "reading": return (4.00f, 8.00f);
            case "writing": return (4.50f, 8.50f);
            case "ride bicycle": return (3.00f, 8.00f);  // adoption varies
            case "swim basics": return (3.00f, 10.00f); // adoption varies
            case "drive": return (16.00f, 19.00f);// legal window

            default: return (2.0f, 10.0f);
        }
    }


    // ---------- Commonness weighting ----------
    static readonly string[] _veryCommon = {
        "Conversation","Active Listening","Driving","Navigation",
        "Lifting & Carrying","Pushing & Pulling","Balance","Flexibility",
        "Decision Making","Problem Solving","Planning","Collaboration",
        "Empathy","Comforting/Consoling","Apologizing & Forgiving","Complimenting","Gaming","Dance"
    };

    static readonly string[] _common = {
        "Drawing","Painting (art)","Board Games","Card Games","Public Speaking",
        "Storytelling","Persuasion","Negotiation","Feedback Delivery","Aiming/Targeting",
        "Sprinting","Jumping","Climbing","Meditation"
    };

    static bool InSet(string id, string[] set)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        for (int i = 0; i < set.Length; i++)
            if (string.Equals(k, set[i].ToLowerInvariant())) return true;
        return false;
    }

    static int SkillWeight(string id)
    {
        if (InSet(id, _veryCommon)) return 6;   // prioritize hard
        if (InSet(id, _common)) return 3;   // medium priority
        return 1;                                // niche
    }

    // ───────── Opinions (generator) ─────────
    static readonly string[] _opinionConcepts = {
    // keep short; expand later to match your full UI list
    "Baths",  "Morning showers",  "Night showers"
};

    static characterStats.OpinionValue RandomOpinionValue()
    {
        // skew toward neutral/indifferent, with some positive/negative and rare curious
        float r = UnityEngine.Random.value;
        if (r < 0.08f) return characterStats.OpinionValue.Negative;
        if (r < 0.32f) return characterStats.OpinionValue.Indifferent;
        if (r < 0.70f) return characterStats.OpinionValue.Neutral;
        if (r < 0.92f) return characterStats.OpinionValue.Positive;
        return characterStats.OpinionValue.Curious;
    }

    static void RandomizeOpinions(characterStats s)
    {
        // ensure container exists (matches UI’s path opinions.opinions)
        if (s.opinions == null) s.opinions = new characterStats.OpinionStats();
        if (s.opinions.opinions == null) s.opinions.opinions = new List<characterStats.Opinion>();
        var list = s.opinions.opinions;
        list.Clear();

        int age = s.profile.age.years;
        // older chars hold more explicit opinions
        int min = (age < 13) ? 3 : (age < 18) ? 5 : 10;
        int max = (age < 13) ? 7 : (age < 18) ? 10 : 16;
        int count = UnityEngine.Random.Range(min, max + 1);

        // shuffle pool
        var pool = _opinionConcepts.ToList();
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        // unique topics only
        for (int i = 0; i < pool.Count && list.Count < count; i++)
        {
            list.Add(new characterStats.Opinion
            {
                topic = pool[i],
                value = RandomOpinionValue()
            });
        }

        var allUIs = Resources.FindObjectsOfTypeAll<OpinionsUI>();
        foreach (var ui in allUIs)
        {
            if (ui && ui.isActiveAndEnabled) ui.RefreshFromModel();
        }

    }


    // ---------- Age → how many improvables to add ----------
    static int ImprovablePickCountForAge(int ageY)
    {
        if (ageY < 6) return UnityEngine.Random.Range(0, 1);    // toddlers: essentially none
        if (ageY < 10) return UnityEngine.Random.Range(0, 3);
        if (ageY < 13) return UnityEngine.Random.Range(2, 5);
        if (ageY < 18) return UnityEngine.Random.Range(6, 12);   // teens: growing mix
        if (ageY < 30) return UnityEngine.Random.Range(18, 26);  // young adults: most
        if (ageY < 50) return UnityEngine.Random.Range(20, 30);  // mid adults: most+
        if (ageY < 70) return UnityEngine.Random.Range(14, 22);
        return UnityEngine.Random.Range(10, 18);
    }

    // Ensure adults get a minimum “very common” core
    static int MinVeryCommonForAge(int ageY)
    {
        if (ageY < 13) return 1;
        if (ageY < 18) return UnityEngine.Random.Range(2, 4);
        if (ageY < 50) return UnityEngine.Random.Range(6, 10);
        if (ageY < 70) return UnityEngine.Random.Range(5, 8);
        return UnityEngine.Random.Range(4, 6);
    }

    static System.Collections.Generic.List<string> WeightedPick(string[] pool, int count)
    {
        var picks = new System.Collections.Generic.List<string>();
        var remaining = pool.ToList();
        count = Mathf.Clamp(count, 0, remaining.Count);

        for (int n = 0; n < count; n++)
        {
            // sum weights
            float total = 0f;
            for (int i = 0; i < remaining.Count; i++)
                total += Mathf.Max(1, SkillWeight(remaining[i]));

            float r = UnityEngine.Random.Range(0f, total);
            float acc = 0f;
            int chosenIdx = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                acc += Mathf.Max(1, SkillWeight(remaining[i]));
                if (r <= acc) { chosenIdx = i; break; }
            }
            picks.Add(remaining[chosenIdx]);
            remaining.RemoveAt(chosenIdx);
        }
        return picks;
    }


    static bool IsOptionalLearnOnce(string id)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        return k == "ride bicycle" || k == "swim basics" || k == "drive" || k == "floss";
    }

    // NEW: adult adoption rates for optional skills (coarse, culture-agnostic)
    static float LearnOnceAdoptionRate(string id)
    {
        string k = (id ?? "").Trim().ToLowerInvariant();
        switch (k)
        {
            case "ride bicycle": return 0.92f;
            case "swim basics": return 0.78f;
            case "drive": return 0.94f;
            case "floss": return 0.92f;
            case "genuflect": return 0.98f;
            default: return 0.98f;
        }
    }


    static void RandomizeLearnedSkills(characterStats s)
    {
        if (s.skills == null || s.skills.list == null) return;
        EnsureDefaultLearnOnce(s);

        int ageY = s.profile.age.years;
        float ageF = ageY + (s.profile.age.months / 12f);

        foreach (var e in s.skills.list)
        {
            if (e.mode != characterStats.SkillMode.LearnOnce) continue;

            var (startAge, fullAge) = LearnOnceWindow(e.id);

            // modest jitter (kept tight so most kids complete early)
            startAge += UnityEngine.Random.Range(-0.2f, 0.4f);
            fullAge += UnityEngine.Random.Range(-0.3f, 0.6f);
            fullAge = Mathf.Max(fullAge, startAge + 0.4f);

            int progress;

            if (ageF >= fullAge)
            {
                if (IsOptionalLearnOnce(e.id))
                {
                    // Adults: learned with some chance, else remains low/incomplete
                    bool learned = UnityEngine.Random.value < LearnOnceAdoptionRate(e.id);
                    progress = learned ? 100 : UnityEngine.Random.Range(0, 25);
                }
                else
                {
                    // Universal basics: fully learned
                    progress = 100;
                }
            }
            else if (ageF <= startAge)
            {
                progress = 0;
            }
            else
            {
                // Smooth ramp between start→full
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(startAge, fullAge, ageF));
                progress = Mathf.Clamp(Mathf.RoundToInt(t * 100f + UnityEngine.Random.Range(-3f, 3f)), 0, 100);
            }

            e.SetLearnOnceProgress(progress);
        }
    }


    static int PerfCapMaxFor(string id, int ageY)
    {
        string k = (id ?? "").ToLowerInvariant();
        (int min, int max) perfCap;

        // These mirror StatLimits.ClampSkills categories & ranges
        // Physical power / athleticism
        if (k.Contains("sprint") || k.Contains("push") || k.Contains("lift") ||
            k.Contains("climb") || k.Contains("jump") || k.Contains("bench") ||
            k.Contains("pullups") || k.Contains("situp"))
        {
            perfCap = StatLimits.GetAgeScaledRange(ageY, 20, 35, 95, 20, 1.0f, 30);
        }
        // Fine-motor / cognitive-creative
        else if (k.Contains("drawing") || k.Contains("painting") || k.Contains("meditation") ||
                k.Contains("decision") || k.Contains("problem") || k.Contains("visualization"))
        {
            perfCap = StatLimits.GetAgeScaledRange(ageY, 25, 55, 92, 35, 0.9f, 30);
        }
        // Social / rhetoric
        else if (k.Contains("public speaking") || k.Contains("presentation") || k.Contains("debate") ||
                k.Contains("rhetoric") || k.Contains("persuasion") || k.Contains("negotiation") ||
                k.Contains("conflict") || k.Contains("feedback") || k.Contains("storytelling"))
        {
            perfCap = StatLimits.GetAgeScaledRange(ageY, 28, 60, 90, 20, 0.8f, 30);
        }
        // Driving / navigation
        else if (k.Contains("driv") || k.Contains("navigation"))
        {
            perfCap = StatLimits.GetAgeScaledRange(ageY, 18, 60, 95, 0, 0.9f, 30);
        }
        else
        {
            // General default
            perfCap = StatLimits.GetAgeScaledRange(ageY, 22, 55, 90, 25, 0.9f, 30);
        }
        return perfCap.max;
    }

    static void RandomizeImprovableSkills(characterStats s)
    {
        EnsureDefaultImprovable(s);

        int ageY = s.profile.age.years;
        int awarenessCap = (ageY < 10) ? 0 : (ageY < 18 ? 1 : 2);

        // 1) Bigger pool by age (teens/adults get a lot more)
        int pickCount = ImprovablePickCountForAge(ageY);

        // 2) Enforce a minimum core of very common skills
        int minVeryCommon = MinVeryCommonForAge(ageY);

        var pool = _defaultImprovable.ToList();
        var veryCommonPool = pool.Where(id => InSet(id, _veryCommon)).ToArray();

        var mustHaves = WeightedPick(veryCommonPool, Mathf.Min(minVeryCommon, veryCommonPool.Length));
        foreach (var id in mustHaves) pool.Remove(id);

        // 3) Fill remaining slots with weighted picks from the rest (common > niche)
        var rest = WeightedPick(pool.ToArray(), Mathf.Max(0, pickCount - mustHaves.Count));
        var chosen = mustHaves.Concat(rest);

        // 4) Seed initial values within realistic age caps
        foreach (var id in chosen)
        {
            var e = s.skills.AddOrGet(id, characterStats.SkillMode.Improvable);

            int cap = PerfCapMaxFor(id, ageY);

            float minFactor = ageY < 18 ? 0.08f : 0.18f;
            int minVal = Mathf.RoundToInt(cap * minFactor);

            float skew = ageY < 18 ? 2.3f : 1.7f;
            int f = LowBiased(minVal, cap, skew);
            int c = LowBiased(minVal, cap, skew);
            int ef = LowBiased(minVal, cap, skew);
            int p = LowBiased(minVal, cap, skew);

            e.SetImprovable(UnityEngine.Random.Range(0, awarenessCap + 1), f, c, ef, p);
        }


        // Safety net
        StatLimits.ClampSkills(s);
    }





    /* ────────────── static data ────────────── */

    static readonly string[] manFirst = { "Aaron", "Zade" };
    static readonly string[] womanFirst = { "Aaliyah", "Anya" };
    static readonly string[] lastNames = { "Aarons", "Abbott" };
    static readonly string[] languageNames = { "english", "spanish", "french", "chinese", "arabic" };

    static string RandomFirst(characterStats.GenderType g)
    {
        if (g == characterStats.GenderType.Woman)
            return womanFirst[UnityEngine.Random.Range(0, womanFirst.Length)];
        if (g == characterStats.GenderType.Man)
            return manFirst[UnityEngine.Random.Range(0, manFirst.Length)];
        // Non‑binary – random from combined pool
        int roll = UnityEngine.Random.Range(0, manFirst.Length + womanFirst.Length);
        return roll < manFirst.Length ? manFirst[roll] : womanFirst[roll - manFirst.Length];
    }

    static string RandomLast() => lastNames[UnityEngine.Random.Range(0, lastNames.Length)];


    static void RandomizeLanguages(characterStats.LanguageStats lang)
    {
        // 83% chance of only knowing English (100% - 17% = 83%)
        if (UnityEngine.Random.value < 0.83f)
        {
            lang.english = true;
            lang.spanish = false;
            lang.french = false;
            lang.chinese = false;
            lang.arabic = false;
            lang.primary_language = "english";
            return;
        }

        bool knowsEnglish = UnityEngine.Random.value < 0.92f;
        lang.english = knowsEnglish;

        // Determine how many additional languages (1-3 more)
        int additionalLanguages = UnityEngine.Random.value < 0.7f ? 1 :
                                 UnityEngine.Random.value < 0.9f ? 2 : 3;

        // Select additional languages randomly
        var otherLanguages = new[] { "spanish", "french", "chinese", "arabic" };
        var selectedLanguages = otherLanguages.OrderBy(_ => UnityEngine.Random.value)
                                             .Take(additionalLanguages)
                                             .ToList();

        // If doesn't know English, must know at least one language
        if (!knowsEnglish || selectedLanguages.Count > 0)
        {
            foreach (var l in selectedLanguages)
            {
                switch (l)
                {
                    case "spanish": lang.spanish = true; break;
                    case "french": lang.french = true; break;
                    case "chinese": lang.chinese = true; break;
                    case "arabic": lang.arabic = true; break;
                }
            }
        }

        // Set primary language
        if (knowsEnglish && UnityEngine.Random.value < 0.8f)
        {
            // 80% chance English is primary even if multilingual
            lang.primary_language = "english";
        }
        else if (selectedLanguages.Count > 0)
        {
            // Otherwise pick from known languages
            lang.primary_language = selectedLanguages[UnityEngine.Random.Range(0, selectedLanguages.Count)];
        }
        else
        {
            // Fallback to English
            lang.english = true;
            lang.primary_language = "english";
        }
    }

    /* ────────────── public entry point ────────────── */

    public static void Randomize(characterStats s)
    {
        EnsureRefs(s);


        /* 1) Age - NOW USING StatLimits */
        int years = StatLimits.SampleWeightedAge();
        int months = UnityEngine.Random.Range(0, 12);
        s.profile.age = new characterStats.AgeYearsMonths { years = years, months = months };

        /* 2) Sex & Gender */
        s.profile.sex = UnityEngine.Random.value < 0.49f ? characterStats.SexType.Male :
                        (UnityEngine.Random.value < 0.98f ? characterStats.SexType.Female : characterStats.SexType.Intersex);
        if (UnityEngine.Random.value < 0.94f)
            s.profile.gender = s.profile.sex == characterStats.SexType.Male ? characterStats.GenderType.Man : characterStats.GenderType.Woman;
        else
            s.profile.gender = characterStats.GenderType.NonBinary;

        /* 2b) Naming */
        s.profile.first_name = RandomFirst(s.profile.gender);
        s.profile.last_name = RandomLast();


        /* 3) Appearance */
        s.profile.skin_color = (characterStats.SkinColor)UnityEngine.Random.Range(0, Enum.GetValues(typeof(characterStats.SkinColor)).Length);
        int maxHT = System.Enum.GetValues(typeof(characterStats.HairType)).Length;
        var randHT = (characterStats.HairType)UnityEngine.Random.Range(0, maxHT);
        if (randHT == characterStats.HairType.Type1B || randHT == characterStats.HairType.Type1C)
            randHT = characterStats.HairType.Type1A;
        s.profile.hair_type = randHT;

        /* 3b) Languages */
        if (s.profile.language == null) s.profile.language = new characterStats.LanguageStats();
        RandomizeLanguages(s.profile.language);

        /* 3c) Voice - NOW USING StatLimits */
        int basePitch = s.profile.sex == characterStats.SexType.Male ? 35 :
                        s.profile.sex == characterStats.SexType.Female ? 55 : 45;
        s.profile.voice.pitch = Mathf.Clamp(basePitch + UnityEngine.Random.Range(-10, 10) - years / 4, 1, 100);
        s.profile.voice.tone = StatLimits.AgeScaledValue(years, 25, 55, 80, 20, 1.0f);
        s.profile.voice.timbre = StatLimits.AgeScaledValue(years, 30, 60, 85, 25, 0.9f);

        /* 4) Physique - NOW USING StatLimits */
        float heightCm = StatLimits.SampleHeightCm(years, s.profile.sex);
        StatLimits.CmToFtIn(heightCm, out int ft, out int inch);
        s.profile.height = new characterStats.HeightFtIn { feet = ft, inches = inch };

        float bmi = StatLimits.SampleBMI(years, s.profile.sex);
        int lbs = Mathf.RoundToInt(bmi * Mathf.Pow(heightCm / 100f, 2f) * 2.20462f);
        s.profile.weight = new characterStats.WeightStats { pounds = lbs };

        // strength & body‑composition baseline - NOW USING StatLimits
        int strength = StatLimits.AgeScaledValue(years, 30, 50, 85, 5, 1.2f);
        int muscle = strength - UnityEngine.Random.Range(5, 15);
        int fat = Mathf.RoundToInt(bmi);

        /* Arms & Legs core stats */
        foreach (var side in new[] { s.arms.left, s.arms.right })
        {
            side.strength = strength;
            side.muscle_mass = muscle;
            side.fat = fat;
        }
        foreach (var side in new[] { s.legs.left, s.legs.right })
        {
            side.strength = strength + UnityEngine.Random.Range(5, 15);
            side.muscle_mass = muscle + UnityEngine.Random.Range(5, 15);
            side.fat = fat;
        }
        s.torso.strength = strength;
        s.torso.muscle_mass = muscle;
        s.torso.fat = fat;

        /* 4b) Flexibility / stiffness / fatigue - NOW USING StatLimits */
        int limbFlex = StatLimits.AgeScaledValue(years, 15, 35, 90, 25, 1.5f);
        int limbFatigue = 100 - limbFlex;

        foreach (var arm in new[] { s.arms.left, s.arms.right })
        {
            arm.flexibility = limbFlex;
            arm.stiffness = 100 - limbFlex;
            arm.fatigue = limbFatigue;
        }
        foreach (var leg in new[] { s.legs.left, s.legs.right })
        {
            leg.flexibility = limbFlex;
            leg.stiffness = 100 - limbFlex;
            leg.fatigue = limbFatigue;
        }
        s.torso.flexibility = StatLimits.AgeScaledValue(years, 18, 40, 85, 25, 1.4f);
        s.torso.stiffness = 100 - s.torso.flexibility;
        s.torso.fatigue = StatLimits.AgeScaledValue(years, 60, 80, 90, 10, -0.8f);

        /* 5) Senses - NOW USING StatLimits */
        s.senses.sight.visual_acuity = StatLimits.AgeScaledValue(years, 18, 40, 95, 15, 1.1f);
        s.senses.hearing.auditory_acuity = StatLimits.AgeScaledValue(years, 18, 50, 95, 20, 1.0f);
        s.senses.taste.flavor_discrimination = StatLimits.AgeScaledValue(years, 25, 55, 80, 10, 0.8f);

        // new sensory dimensions
        s.senses.sight.color_perception = StatLimits.AgeScaledValue(years, 18, 50, 95, 20, 1.1f);
        s.senses.sight.light_sensitivity = 100 - StatLimits.AgeScaledValue(years, 18, 40, 90, 10, 0.9f);
        s.senses.hearing.noise_sensitivity = StatLimits.AgeScaledValue(years, 5, 30, 90, 30, 1.0f);

        // Touch senses
        s.senses.touch.heat_sensitivity = StatLimits.AgeScaledValue(years, 10, 40, 85, 60, 0.8f);
        s.senses.touch.cold_sensitivity = StatLimits.AgeScaledValue(years, 10, 40, 85, 60, 0.8f);
        s.senses.touch.pain_tolerance = StatLimits.AgeScaledValue(years, 25, 60, 75, 30, 0.6f);

        /* 6) Neurological & Organ health - NOW USING StatLimits */
        s.head.brain.myelin = 100 - StatLimits.AgeScaledValue(years, 1, 25, 95, 10, 0.5f);
        s.head.brain.plaque = StatLimits.AgeScaledValue(years, 80, 70, 80, 0, -0.7f);
        s.head.brain.shape_regulation = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.head.brain.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 60, 5, -0.6f);
        s.head.brain.lining = StatLimits.AgeScaledValue(years, 20, 45, 90, 75, 1.0f);
        s.head.brain.blood_amount = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.9f);
        s.head.brain.cavity_pressure = StatLimits.AgeScaledValue(years, 60, 50, 50, 10, -0.5f);
        s.head.brain.spinal_fluid_leak = StatLimits.AgeScaledValue(years, 80, 70, 30, 0, -0.3f);
        s.head.brain.bruised = 0; // Excluded injuries

        s.internalOrgans.heart.coronary_arteries_plaque = StatLimits.AgeScaledValue(years, 80, 60, 70, 0, -0.6f);
        s.internalOrgans.heart.blood_vessel = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.9f);
        s.internalOrgans.heart.blood_vessel_size = StatLimits.AgeScaledValue(years, 20, 45, 85, 70, 0.8f);
        s.internalOrgans.heart.blood_vessel_swelling = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.heart.heartbeat_regulation = StatLimits.AgeScaledValue(years, 25, 60, 90, 40, -0.6f);
        s.internalOrgans.heart.shape_regulation = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.internalOrgans.heart.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.heart.leaky_valves = StatLimits.AgeScaledValue(years, 80, 70, 40, 0, -0.4f);
        s.internalOrgans.heart.hole_in_heart = years < 5 ? UnityEngine.Random.Range(0, 20) : 0;
        s.internalOrgans.heart.bruised = 0; // Excluded injuries
        s.internalOrgans.heart.discomfort = 0;
        s.internalOrgans.heart.strength = StatLimits.AgeScaledValue(years, 25, 50, 85, 50, 1.2f);

        // Heart connections
        s.internalOrgans.heart.connections.brain = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.internalOrgans.heart.connections.lungs = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.internalOrgans.heart.connections.kidneys = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.internalOrgans.heart.connections.liver = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.internalOrgans.heart.connections.spine = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);
        s.internalOrgans.heart.connections.spleen = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.8f);

        s.internalOrgans.left_kidney.nephron_inflammation = StatLimits.AgeScaledValue(years, 75, 65, 60, 0, -0.5f);
        s.internalOrgans.right_kidney.nephron_inflammation = s.internalOrgans.left_kidney.nephron_inflammation + UnityEngine.Random.Range(-5, 5);
        s.internalOrgans.left_kidney.small_blood_vessels = StatLimits.AgeScaledValue(years, 20, 50, 90, 70, 0.9f);
        s.internalOrgans.right_kidney.small_blood_vessels = s.internalOrgans.left_kidney.small_blood_vessels + UnityEngine.Random.Range(-5, 5);
        s.internalOrgans.left_kidney.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.right_kidney.inflammation = s.internalOrgans.left_kidney.inflammation + UnityEngine.Random.Range(-5, 5);
        s.internalOrgans.left_kidney.bruised = s.internalOrgans.right_kidney.bruised = 0;
        s.internalOrgans.left_kidney.discomfort = s.internalOrgans.right_kidney.discomfort = 0;

        s.head.left_eye.lens_flexibility = 100 - StatLimits.AgeScaledValue(years, 10, 15, 90, 5, 1.2f);
        s.head.right_eye.lens_flexibility = s.head.left_eye.lens_flexibility + UnityEngine.Random.Range(-3, 3);
        s.head.left_eye.refraction = StatLimits.AgeScaledValue(years, 20, 45, 90, 70, 1.0f);
        s.head.right_eye.refraction = s.head.left_eye.refraction + UnityEngine.Random.Range(-5, 5);
        s.head.left_eye.strain = StatLimits.AgeScaledValue(years, 30, 40, 60, 10, -0.8f);
        s.head.right_eye.strain = s.head.left_eye.strain + UnityEngine.Random.Range(-5, 5);
        s.head.left_eye.dryness = StatLimits.AgeScaledValue(years, 50, 40, 60, 10, -0.7f);
        s.head.right_eye.dryness = s.head.left_eye.dryness + UnityEngine.Random.Range(-5, 5);
        s.head.left_eye.bruised = s.head.right_eye.bruised = 0;
        s.head.left_eye.discomfort = s.head.right_eye.discomfort = 0;

        // lungs & heartbeat fine‑tuning
        foreach (var lung in new[] { s.internalOrgans.left_lung, s.internalOrgans.right_lung })
        {
            lung.mucus_amount = StatLimits.AgeScaledValue(years, 70, 60, 70, 0, -0.5f);
            lung.airway_size = StatLimits.AgeScaledValue(years, 25, 45, 90, 60, -0.4f);
            lung.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
            lung.air_sacks = StatLimits.AgeScaledValue(years, 20, 45, 90, 70, 1.0f);
            lung.bruised = lung.hole = lung.discomfort = 0;
        }

        // Liver
        s.internalOrgans.liver.fat_amount = Mathf.Clamp(fat / 2 + UnityEngine.Random.Range(-10, 10), 0, 100);
        s.internalOrgans.liver.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.liver.bruised = s.internalOrgans.liver.discomfort = 0;

        // Spleen
        s.internalOrgans.spleen.size = StatLimits.AgeScaledValue(years, 20, 50, 70, 50, 0.5f);
        s.internalOrgans.spleen.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.spleen.bruised = s.internalOrgans.spleen.discomfort = 0;

        // Pancreas
        s.internalOrgans.pancreas.insulin_production_rate = StatLimits.AgeScaledValue(years, 20, 50, 90, 60, 1.0f);
        s.internalOrgans.pancreas.insulin_amount = StatLimits.AgeScaledValue(years, 20, 50, 85, 60, 0.9f);
        s.internalOrgans.pancreas.insulin_resistance = StatLimits.AgeScaledValue(years, 60, 40, 60, 10, -0.8f);
        s.internalOrgans.pancreas.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.pancreas.bruised = s.internalOrgans.pancreas.discomfort = 0;

        // Stomach
        s.internalOrgans.stomach.metabolism = StatLimits.AgeScaledValue(years, 18, 35, 90, 60, 1.2f);
        s.internalOrgans.stomach.lactose_intolerance = UnityEngine.Random.value < 0.3f ?
            StatLimits.AgeScaledValue(years, 40, 30, 70, 20, -0.5f) : UnityEngine.Random.Range(0, 20);
        s.internalOrgans.stomach.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 50, 5, -0.5f);
        s.internalOrgans.stomach.bruised = s.internalOrgans.stomach.discomfort = 0;

        /* 7) Qualities */
        var id = s.qualities.identity;
        id.self_awareness = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 35, 65, 90, 10, 1.0f), 0.15f)), 0, 100);
        id.self_esteem   = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 40, 70, 85, 20, 0.8f), 0.15f)), 0, 100);
        id.ego           = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 18, 50, 70, 30, 0.5f), 0.15f)), 0, 100);

        var emoq = s.qualities.emotions;
        emoq.emotional_intelligence = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 35, 70, 90, 20, 0.6f), 0.15f)), 0, 100);
        emoq.optimism               = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 20, 50, 80, 40, 0.7f), 0.15f)), 0, 100);
        emoq.eccentricity           = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 55, 70, 30, 0.2f), 0.15f)), 0, 100);
        emoq.loneliness             = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 75, 15, 60, 20, -0.8f), 0.15f)), 0, 100);

        var cog = s.qualities.cognitive_abilities;
        cog.logic        = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 55, 90, 40, 0.6f), 0.15f)), 0, 100);
        cog.creativity   = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 45, 85, 30, 0.7f), 0.15f)), 0, 100);
        cog.focus        = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 45, 85, 30, 0.7f), 0.15f)), 0, 100);
        cog.intelligence = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 55, 90, 40, 0.6f), 0.15f)), 0, 100);

        var soc = s.qualities.social_skills;
        soc.sociality      = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 20, 45, 85, 30, 0.6f), 0.15f)), 0, 100);
        soc.cooperation    = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 60, 90, 30, 0.7f), 0.15f)), 0, 100);
        soc.politeness     = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 18, 50, 90, 30, 0.8f), 0.15f)), 0, 100);
        soc.charisma       = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 60, 85, 20, 0.7f), 0.15f)), 0, 100);
        soc.humility       = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 35, 70, 90, 30, 0.8f), 0.15f)), 0, 100);
        soc.sense_of_humor = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 60, 95, 40, 0.6f), 0.15f)), 0, 100);

        var res = s.qualities.resilience;
        res.independence  = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 65, 90, 20, 0.9f), 0.15f)), 0, 100);
        res.perseverance  = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 35, 70, 90, 20, 0.8f), 0.15f)), 0, 100);
        res.courage       = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 60, 90, 20, 0.8f), 0.15f)), 0, 100);
        res.patience      = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 35, 70, 90, 25, 0.6f), 0.15f)), 0, 100);
        res.self_control  = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 70, 90, 25, 0.8f), 0.15f)), 0, 100);
        res.decisiveness  = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 65, 90, 25, 0.7f), 0.15f)), 0, 100);

        var rom = s.qualities.romance;
        rom.sensuality = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 50, 85, 20, 1.0f), 0.15f)), 0, 100);
        rom.passion    = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 45, 90, 30, 1.0f), 0.15f)), 0, 100);
        rom.flirtation = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 20, 45, 80, 20, 1.0f), 0.15f)), 0, 100);
        rom.seduction  = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 22, 50, 75, 15, 0.9f), 0.15f)), 0, 100);

        var moral = s.qualities.moral_compass;
        moral.honesty        = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 80, 90, 30, 0.4f), 0.15f)), 0, 100);
        moral.responsibility = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 30, 80, 90, 25, 0.5f), 0.15f)), 0, 100);
        moral.generosity     = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 25, 70, 90, 20, 0.6f), 0.15f)), 0, 100);
        moral.moral_strength = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 35, 80, 90, 30, 0.5f), 0.15f)), 0, 100);
        moral.spirituality   = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(UnityEngine.Random.Range(0f,100f), StatLimits.AgeScaledValue(years, 40, 90, 85, 10, 0.3f), 0.15f)), 0, 100);


        /* 9) Traits – sparse Booleans */
        var traitFields = typeof(characterStats.TraitsStats).GetFields();
        foreach (var f in traitFields)
            f.SetValue(s.traits, UnityEngine.Random.value < 0.2f);

        /* 10) Reproductive specifics (values on a 20‑100 scale) - NOW USING StatLimits */
        switch (s.profile.sex)
        {
            // ──────────────────────────── MALE ────────────────────────────
            case characterStats.SexType.Male:
                {
                    var g = new characterStats.PelvisReproductiveStats.MaleGenitals();

                    g.penis.length = Mathf.Clamp(StatLimits.AgeScaledValue(years, 18, 60, 80, 40, 0.2f), 20, 100);
                    g.penis.girth = Mathf.Clamp(StatLimits.AgeScaledValue(years, 18, 60, 70, 30, 0.2f), 20, 100);
                    g.penis.hair_amount = Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 15, 25, 80, 0, -0.5f) * (30f / 100f));
                    g.penis.bruised = g.penis.cut = g.penis.discomfort = g.penis.soreness = 0;

                    g.testicles.hair_amount = Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 18, 65, 90, 10, 0.6f) * (10f / 100f));
                    g.testicles.left.bruised = g.testicles.left.cut = g.testicles.left.discomfort = 0;
                    g.testicles.right.bruised = g.testicles.right.cut = g.testicles.right.discomfort = 0;

                    s.pelvisAndRepro.genitals = g;
                    break;
                }

            // ─────────────────────────── FEMALE ───────────────────────────
            case characterStats.SexType.Female:
                {
                    var g = new characterStats.PelvisReproductiveStats.FemaleGenitals();

                    // ── vulva  ─────────────────────────────────────
                    g.vulva.hair_amount = Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 18, 65, 90, 10, 0.6f) * (30f / 100f));
                    g.vulva.lip_size = StatLimits.AgeScaledValue(years, 18, 65, 70, 30, 0.5f);
                    g.vulva.vaginal_opening_size = StatLimits.AgeScaledValue(years, 18, 50, 60, 40, 0.3f);
                    g.vulva.bruised = g.vulva.cut = g.vulva.discomfort = g.vulva.soreness = 0;

                    // ── uterus ──────────────────────────────────────────
                    g.uterus.menopausal = years >= 55;          // simple cut‑off

                    // menarche probability ramps from 0 → 1 between ages 8‑16
                    bool past_menarche = !g.uterus.menopausal &&
                                        (years >= 16 || UnityEngine.Random.value < Mathf.InverseLerp(8, 16, years));

                    g.uterus.cycles_active = past_menarche;

                    if (!g.uterus.cycles_active)               // pre‑puberty or post‑menopause
                    {
                        g.uterus.cycle_day = 0;
                        g.uterus.bleeding = false;
                        g.uterus.lining_thickness = 0;
                        g.uterus.fertility_factor = 0;
                    }
                    else
                    {
                        g.uterus.cycle_day = UnityEngine.Random.Range(5, 28);
                        g.uterus.bleeding = false;

                        // age-based fertility decline (max at 18, slides toward 10 by 55)
                        g.uterus.fertility_factor =
                            Mathf.Clamp(100 - Mathf.Max(0, years - 18) * 2, 10, 100);

                        // set lining by phase (no shedding)
                        if (g.uterus.cycle_day < 14) g.uterus.lining_thickness = UnityEngine.Random.Range(20, 70);
                        else if (g.uterus.cycle_day < 21) g.uterus.lining_thickness = UnityEngine.Random.Range(70, 100);
                        else g.uterus.lining_thickness = UnityEngine.Random.Range(20, 60);
                    }

                    // baseline health fields
                    g.uterus.contraction_strength = g.uterus.bleeding ? UnityEngine.Random.Range(10, 40) : 0;
                    g.uterus.inflammation = g.uterus.discomfort = 0;
                    g.uterus.pregnant = false;
                    g.uterus.gestation_weeks = 0;



                    s.pelvisAndRepro.genitals = g;
                    break;
                }


            // ────────────────────────── INTERSEX ──────────────────────────
            default:
                {
                    var g = new characterStats.PelvisReproductiveStats.IntersexGenitals();
                    int mix = StatLimits.AgeScaledValue(years, 18, 65, 80, 20, 0.5f);

                    // blend values
                    g.penis.length = g.penis.girth = mix;
                    g.penis.hair_amount = mix;
                    g.vulva.hair_amount = mix;
                    g.vulva.lip_size = mix / 2;

                    g.penis.bruised = g.penis.cut = g.penis.discomfort = g.penis.soreness = 0;
                    g.vulva.bruised = g.vulva.cut = g.vulva.discomfort = g.vulva.soreness = 0;

                    g.testicles.hair_amount = mix;
                    g.testicles.left.bruised = g.testicles.left.cut = g.testicles.left.discomfort = 0;
                    g.testicles.right.bruised = g.testicles.right.cut = g.testicles.right.discomfort = 0;

                    g.uterus.lining_thickness = StatLimits.AgeScaledValue(years, 25, 45, 90, 30, 0.4f);
                    g.uterus.contraction_strength = 0;   // not in labour
                    g.uterus.inflammation = 0;
                    g.uterus.discomfort = 0;
                    g.uterus.pregnant = false;
                    g.uterus.gestation_weeks = 0;

                    s.pelvisAndRepro.genitals = g;
                    break;
                }
        }


        // Hips
        s.pelvisAndRepro.hips.bruised = s.pelvisAndRepro.hips.cut = s.pelvisAndRepro.hips.discomfort = s.pelvisAndRepro.hips.soreness = 0;
        s.pelvisAndRepro.hips.fractured = false;
        s.pelvisAndRepro.hips.flexibility = StatLimits.AgeScaledValue(years, 16, 35, 85, 30, 1.5f);

        // Buttocks
        s.pelvisAndRepro.buttocks.left.bruised = s.pelvisAndRepro.buttocks.left.cut = s.pelvisAndRepro.buttocks.left.discomfort = s.pelvisAndRepro.buttocks.left.soreness = 0;
        s.pelvisAndRepro.buttocks.right.bruised = s.pelvisAndRepro.buttocks.right.cut = s.pelvisAndRepro.buttocks.right.discomfort = s.pelvisAndRepro.buttocks.right.soreness = 0;

        /* 11) Head Details - NOW USING StatLimits for relevant parts */
        // Head skin
        GenerateSkinStatsHead(s.head.skin, years, s.profile.sex);

        // Skull sections with hair
        string hairColor = RandomHairColor(years);
        int baseHairAmount = years < 1 ? UnityEngine.Random.Range(10, 40) :
                            (years > 50 && s.profile.sex == characterStats.SexType.Male)
                            ? StatLimits.AgeScaledValue(years, 70, 50, 30, 90, -1.5f)
                            : StatLimits.AgeScaledValue(years, 18, 70, 90, 60, 0.5f);

        // choose a style that FITS type + length (returns an id when DB is available)
        string hairStyle = PickStyleThatFits(s, baseHairAmount);

        // include FRONT so everything stays in sync
        var skullSections = new[] { s.head.front, s.head.top, s.head.back, s.head.left_side, s.head.right_side };
        foreach (var section in skullSections)
        {
            section.bruised = section.cut = 0;
            section.fractured = false;
            section.discomfort = 0;
            section.hair.amount = Mathf.RoundToInt(baseHairAmount * (500f / 100f));
            section.hair.style = hairStyle; // id if DB found, else display name (fallback)
            section.hair.messiness = UnityEngine.Random.Range(10, 50);
            section.hair.color.roots = section.hair.color.mids = section.hair.color.ends = hairColor;
        }

        s.head.forehead.bruised = s.head.forehead.cut = 0;
        s.head.forehead.fractured = false;
        s.head.forehead.discomfort = 0;

        // Nose
        s.head.nose.inflammation = StatLimits.AgeScaledValue(years, 70, 60, 40, 5, -0.4f);
        s.head.nose.bruised = s.head.nose.cut = s.head.nose.discomfort = 0;
        s.head.nose.fractured = false;
        s.head.nose.hair_amount = s.profile.sex == characterStats.SexType.Male ?
            Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 30, 25, 60, 0, -0.8f) * (10f / 100f)) :
            UnityEngine.Random.Range(0, 5);

        // Mouth/lips
        var lips = new[] { s.head.lips_top, s.head.lips_bottom };
        foreach (var lip in lips)
        {
            lip.inflammation = StatLimits.AgeScaledValue(years, 60, 50, 30, 5, -0.3f);
            lip.bruised = lip.cut = lip.discomfort = 0;
            lip.hair.amount = s.profile.sex == characterStats.SexType.Male ?
                Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 25, 70, 0, -0.5f) * (150f / 100f)) :
                UnityEngine.Random.Range(0, 10);
            lip.hair.style = "natural";
            lip.hair.messiness = 0;
        }

        // Tongue & tonsils
        s.head.tongue.inflammation = StatLimits.AgeScaledValue(years, 60, 50, 30, 5, -0.3f);
        s.head.tongue.bruised = s.head.tongue.cut = 0;
        s.head.tonsils.inflammation = StatLimits.AgeScaledValue(years, 60, 50, 40, 10, -0.4f);
        s.head.tonsils.bruised = s.head.tonsils.cut = 0;

        // Teeth
        int teethWhiteness = StatLimits.AgeScaledValue(years, 15, 35, 85, 70, 1.2f);
        int teethAlignment = years < 12 ? UnityEngine.Random.Range(40, 80) :
                            UnityEngine.Random.Range(60, 95); // Assume some get braces

        var quadrants = new[] { s.head.teeth.upper_right, s.head.teeth.upper_left,
                               s.head.teeth.lower_right, s.head.teeth.lower_left };
        foreach (var quad in quadrants)
        {
            foreach (var tooth in quad.incisors.Concat(new[] { quad.canine }).Concat(quad.premolars).Concat(quad.molars))
            {
                tooth.alignment = teethAlignment + UnityEngine.Random.Range(-10, 10);
                tooth.whiteness = teethWhiteness + UnityEngine.Random.Range(-10, 10);
                tooth.chipped = UnityEngine.Random.value < 0.05f;
                tooth.missing = years > 50 ? UnityEngine.Random.value < (years - 50) / 200f : false;
            }
            quad.discomfort = 0;
        }

        // Global teeth sliders – start high-biased instead of 0
        s.head.teeth.overall_whiteness = StatLimits.SampleHighBiased(60, 100, 2.8f);
        s.head.teeth.overall_alignment = StatLimits.SampleHighBiased(
            s.profile.age.years < 12 ? 55 : 70, 100, 2.4f);

        // Make individual teeth match (respects your jitter/extremes logic)
        StatLimits.ApplyTeethFromGlobals(s);


        s.head.teeth.breath_freshness = StatLimits.AgeScaledValue(years, 20, 60, 80, 50, 0.8f);
        s.head.teeth.moisture = StatLimits.AgeScaledValue(years, 20, 60, 85, 65, 0.7f);

        // Cheekbones
        var cheekbones = new[] { s.head.cheekbone_left, s.head.cheekbone_right };
        foreach (var cheek in cheekbones)
        {
            cheek.bruised = cheek.cut = cheek.discomfort = 0;
            cheek.fractured = false;
            cheek.hair.amount = s.profile.sex == characterStats.SexType.Male ?
                Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 25, 80, 0, -0.6f) * (150f / 100f)) :
                UnityEngine.Random.Range(0, 5);
            cheek.hair.style = "natural";
            cheek.hair.messiness = 0;
        }

        // Jaw
        s.head.jaw.left.bruised = s.head.jaw.left.cut = s.head.jaw.left.discomfort = 0;
        s.head.jaw.left.fractured = false;
        s.head.jaw.left.hair.amount = s.profile.sex == characterStats.SexType.Male ?
            Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 25, 85, 0, -0.7f) * (150f / 100f)) :
            UnityEngine.Random.Range(0, 5);
        s.head.jaw.left.hair.style = "natural";
        s.head.jaw.left.hair.messiness = 0;

        s.head.jaw.right.bruised = s.head.jaw.right.cut = s.head.jaw.right.discomfort = 0;
        s.head.jaw.right.fractured = false;
        s.head.jaw.right.hair.amount = s.head.jaw.left.hair.amount + UnityEngine.Random.Range(-5, 5);
        s.head.jaw.right.hair.style = s.head.jaw.left.hair.style;
        s.head.jaw.right.hair.messiness = s.head.jaw.left.hair.messiness;

        s.head.jaw.strength = StatLimits.AgeScaledValue(years, 25, 50, 85, 40, 1.0f);

        // Chin
        s.head.Chin.chin.bruised = s.head.Chin.chin.cut = s.head.Chin.chin.discomfort = 0;
        s.head.Chin.chin.fractured = false;

        s.head.Chin.hair.amount = s.profile.sex == characterStats.SexType.Male
            ? Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 25, 85, 0, -0.7f) * (150f / 100f))
            : UnityEngine.Random.Range(0, 5);
        s.head.Chin.hair.style = "natural";
        s.head.Chin.hair.messiness = 0;

        /* 12) Neck - NOW USING StatLimits */
        GenerateSkinStatsNeck(s.neck.skin, years, s.profile.sex);
        s.neck.inflammation = StatLimits.AgeScaledValue(years, 60, 50, 40, 5, -0.4f);
        s.neck.bruised = s.neck.cut = s.neck.discomfort = 0;
        s.neck.fractured = false;
        s.neck.soreness = StatLimits.AgeScaledValue(years, 40, 35, 50, 10, -0.6f);
        s.neck.muscle_mass = muscle - UnityEngine.Random.Range(10, 20);
        s.neck.fat = fat - UnityEngine.Random.Range(5, 15);

        /* 13) Torso details - NOW USING StatLimits */
        GenerateSkinStatsTorso(s.torso.skin, years, s.profile.sex);

        // Chest
        s.torso.chest.bruised = s.torso.chest.cut = s.torso.chest.discomfort = 0;
        s.torso.chest.hair_amount = s.profile.sex == characterStats.SexType.Male ?
            Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 30, 80, 0, -0.6f) * (25f / 100f)) :
            UnityEngine.Random.Range(0, 4);
        s.torso.chest.soreness = StatLimits.AgeScaledValue(years, 40, 35, 50, 10, -0.6f);
        s.torso.chest.strength = strength + UnityEngine.Random.Range(-5, 5);
        s.torso.chest.muscle_mass = muscle;
        s.torso.chest.fat = fat;

        // Upper back
        s.torso.upper_back.bruised = s.torso.upper_back.cut = s.torso.upper_back.discomfort = 0;
        s.torso.upper_back.hair_amount = s.profile.sex == characterStats.SexType.Male ?
            Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 20, 35, 60, 0, -0.5f) * (25f / 100f)) :
            UnityEngine.Random.Range(0, 3);
        s.torso.upper_back.soreness = StatLimits.AgeScaledValue(years, 35, 30, 60, 15, -0.7f);
        s.torso.upper_back.strength = strength;
        s.torso.upper_back.muscle_mass = muscle;
        s.torso.upper_back.fat = fat;

        // Lower back
        s.torso.lower_back.bruised = s.torso.lower_back.cut = s.torso.lower_back.discomfort = 0;
        s.torso.lower_back.soreness = StatLimits.AgeScaledValue(years, 30, 25, 70, 20, -0.8f);
        s.torso.lower_back.muscle_mass = muscle - UnityEngine.Random.Range(5, 10);
        s.torso.lower_back.fat = fat;

        // Abdomen
        s.torso.abdomen.bruised = s.torso.abdomen.cut = s.torso.abdomen.discomfort = 0;
        s.torso.abdomen.hair_amount = s.profile.sex == characterStats.SexType.Male ?
            Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 30, 75, 0, -0.6f) * (25f / 100f)) :
            UnityEngine.Random.Range(0, 8);
        s.torso.abdomen.soreness = StatLimits.AgeScaledValue(years, 40, 35, 50, 10, -0.6f);
        s.torso.abdomen.strength = strength - UnityEngine.Random.Range(5, 10);
        s.torso.abdomen.muscle_mass = muscle - UnityEngine.Random.Range(5, 15);
        s.torso.abdomen.fat = fat + UnityEngine.Random.Range(0, 10);

        // Ribs, spine, collarbones
        s.torso.ribs.bruised = s.torso.ribs.cut = s.torso.ribs.discomfort = 0;
        s.torso.ribs.fractured = false;

        s.torso.spine.spinal_cord_lining = StatLimits.AgeScaledValue(years, 20, 45, 90, 70, 1.0f);
        s.torso.spine.fat_plaque = StatLimits.AgeScaledValue(years, 60, 40, 60, 5, -0.6f);
        s.torso.spine.cholesterol_plaque = StatLimits.AgeScaledValue(years, 70, 50, 70, 5, -0.7f);
        s.torso.spine.bruised = s.torso.spine.cut = s.torso.spine.discomfort = 0;
        s.torso.spine.fractured = false;

        s.torso.left_collarbone.bruised = s.torso.left_collarbone.cut = s.torso.left_collarbone.discomfort = 0;
        s.torso.left_collarbone.fractured = false;
        s.torso.right_collarbone.bruised = s.torso.right_collarbone.cut = s.torso.right_collarbone.discomfort = 0;
        s.torso.right_collarbone.fractured = false;

        /* 14) Arms details */
        foreach (var arm in new[] { s.arms.left, s.arms.right })
        {
            GenerateSkinStatsArms(arm.skin, years, s.profile.sex);

            // Shoulder
            arm.shoulder.bruised = arm.shoulder.cut = arm.shoulder.discomfort = 0;
            arm.shoulder.fractured = false;
            arm.shoulder.hair_amount = s.profile.sex == characterStats.SexType.Male ?
                Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 30, 60, 0, -0.5f) * (25f / 100f)) :
                UnityEngine.Random.Range(0, 6);
            arm.shoulder.soreness = StatLimits.AgeScaledValue(years, 35, 30, 50, 10, -0.6f);

            // Bicep
            arm.bicep.bruised = arm.bicep.cut = arm.bicep.discomfort = 0;
            arm.bicep.soreness = StatLimits.AgeScaledValue(years, 35, 30, 50, 10, -0.6f);

            // Elbow
            arm.elbow.bruised = arm.elbow.cut = arm.elbow.discomfort = 0;
            arm.elbow.fractured = false;

            // Forearm
            arm.forearm.bruised = arm.forearm.cut = arm.forearm.discomfort = 0;
            arm.forearm.fractured = false;
            arm.forearm.hair_amount = s.profile.sex == characterStats.SexType.Male ?
                Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 30, 70, 0, -0.6f) * (25f / 100f)) :
                UnityEngine.Random.Range(0, 13);
            arm.forearm.soreness = StatLimits.AgeScaledValue(years, 35, 30, 50, 10, -0.6f);

            // Wrist & hand
            arm.wrist.bruised = arm.wrist.cut = arm.wrist.discomfort = 0;
            arm.wrist.fractured = false;
            arm.hand.bruised = arm.hand.cut = arm.hand.discomfort = 0;
            arm.hand.fractured = false;

            // Fingers
            foreach (var finger in new[] { arm.fingers.thumb, arm.fingers.index, arm.fingers.middle,
                                         arm.fingers.ring, arm.fingers.pinky })
            {
                finger.bruised = finger.cut = finger.discomfort = 0;
                finger.fractured = false;
            }
        }

        /* 15) Legs details  */
        int footSize = GenerateFootSize(years, s.profile.sex);

        foreach (var leg in new[] { s.legs.left, s.legs.right })
        {
            GenerateSkinStatsLegs(leg.skin, years, s.profile.sex);

            // Thigh
            leg.thigh.bruised = leg.thigh.cut = leg.thigh.discomfort = 0;
            leg.thigh.hair_amount = s.profile.sex == characterStats.SexType.Male ?
                Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 30, 80, 0, -0.6f) * (25f / 100f)) :
                s.profile.sex == characterStats.SexType.Female ?
                UnityEngine.Random.Range(0, 13) : UnityEngine.Random.Range(0, 18);
            leg.thigh.soreness = StatLimits.AgeScaledValue(years, 35, 30, 50, 10, -0.6f);

            // Knee
            leg.knee.bruised = leg.knee.cut = leg.knee.discomfort = 0;
            leg.knee.fractured = false;

            // Shin
            leg.shin.bruised = leg.shin.cut = leg.shin.discomfort = 0;
            leg.shin.hair_amount = s.profile.sex == characterStats.SexType.Male ?
                Mathf.RoundToInt(StatLimits.AgeScaledValue(years, 16, 30, 75, 0, -0.6f) * (25f / 100f)) :
                s.profile.sex == characterStats.SexType.Female ?
                UnityEngine.Random.Range(0, 11) : UnityEngine.Random.Range(0, 16);
            leg.shin.soreness = StatLimits.AgeScaledValue(years, 35, 30, 50, 10, -0.6f);

            // Ankle
            leg.ankle.bruised = leg.ankle.cut = leg.ankle.discomfort = 0;
            leg.ankle.fractured = false;

            // Foot
            leg.foot.bruised = leg.foot.cut = leg.foot.discomfort = 0;
            leg.foot.fractured = false;
            leg.foot.soreness = StatLimits.AgeScaledValue(years, 40, 35, 60, 15, -0.7f);
            leg.foot.size = footSize;

            // Toes
            foreach (var toe in new[] { leg.toes.big, leg.toes.index, leg.toes.middle,
                                       leg.toes.ring, leg.toes.pinky })
            {
                toe.bruised = toe.cut = toe.discomfort = 0;
                toe.fractured = false;
            }
        }

        // --- Wardrobe seeding ---
        static void SeedWardrobePreferences(characterStats s)
        {
            // Ensure a CharacterWardrobe exists
            var cw = s.GetComponent<CharacterWardrobe>();
            if (cw == null) cw = s.gameObject.AddComponent<CharacterWardrobe>();

            // Try to find a ClothingCatalog so we can filter to valid IDs if present
            ClothingCatalog catalog = null;
            try { catalog = Resources.FindObjectsOfTypeAll<ClothingCatalog>().FirstOrDefault(); } catch { }

            bool isMan = s.profile.gender == characterStats.GenderType.Man;
            bool isWoman = s.profile.gender == characterStats.GenderType.Woman;

            // ---- ID pools (match your default catalog IDs) ----
            // Short sleeve
            string[] ss_m = { "crew_tee", "vneck_tee", "henley_ss", "polo_ss", "buttonup_ss", "work_shirt_ss", "jersey_athletic_ss", "rugby_ss" };
            string[] ss_f = { "crew_tee", "vneck_tee", "henley_ss", "polo_ss", "buttonup_ss", "graphic_tee", "mesh_tee", "crop_tee", "camp_collar" };
            string[] ss_n = { "crew_tee", "vneck_tee", "henley_ss", "ls_tee", "camp_collar", "graphic_tee", "work_shirt_ss" };

            // Long sleeve
            string[] ls_m = { "ls_tee", "henley_ls", "oxford_buttonup", "flannel", "denim_shirt", "rugby_ls", "turtleneck", "thermal_waffle", "work_shirt_ls" };
            string[] ls_f = { "ls_tee", "henley_ls", "oxford_buttonup", "flannel", "denim_shirt", "turtleneck", "thermal_waffle" };
            string[] ls_n = { "ls_tee", "henley_ls", "oxford_buttonup", "flannel", "denim_shirt", "turtleneck", "thermal_waffle" };

            // Bottoms
            string[] bt_m = { "jeans", "chinos", "trousers", "cargo_pants", "utility_work_pants", "joggers", "sweatpants", "track_pants", "denim_shorts", "athletic_shorts", "cargo_shorts" };
            string[] bt_f = { "jeans", "leggings", "yoga_pants", "denim_shorts", "athletic_shorts", "skirt_mini", "skirt_midi", "skirt_maxi" };
            string[] bt_n = { "jeans", "chinos", "joggers", "sweatpants", "track_pants", "denim_shorts", "athletic_shorts" };

            // Overgarments
            string[] og_m = { "hoodie_pullover","zip_hoodie","crew_sweatshirt","denim_jacket","leather_jacket","bomber","shacket","windbreaker","raincoat",
                            "overcoat","puffer","parka","blazer","suit_jacket","utility_vest","fleece" };
            string[] og_f = { "hoodie_pullover","zip_hoodie","crew_sweatshirt","cardigan","knit_sweater","denim_jacket","leather_jacket","bomber","trench",
                            "overcoat","puffer","poncho_cape","fleece","blazer" };
            string[] og_n = { "hoodie_pullover", "zip_hoodie", "crew_sweatshirt", "cardigan", "knit_sweater", "denim_jacket", "bomber", "windbreaker", "raincoat", "puffer", "fleece" };

            // Socks
            string[] so_m = { "crew_socks", "ankle_socks", "no_show_socks", "boot_socks", "athletic_socks" };
            string[] so_f = { "crew_socks", "ankle_socks", "no_show_socks", "boot_socks", "athletic_socks" };
            string[] so_n = { "crew_socks", "ankle_socks", "no_show_socks", "athletic_socks" };

            // Underwear
            string[] uw_m = { "briefs", "boxers", "boxer_briefs", "trunks", "long_johns", "undershirt_tee", "undershirt_tank" };
            string[] uw_f = { "camisole", "bralette", "sports_bra", "bikini_brief", "hipster", "thong", "boyshort", "slip", "slip_shorts", "tights_hosiery", "shapewear_top", "shapewear_bottom" };
            string[] uw_n = { "undershirt_tee", "undershirt_tank", "long_johns", "slip_shorts" };

            // Shoes
            string[] sh_m = { "sneakers","running_shoes","basketball_shoes","skate_shoes","casual_leather","oxford_dress","derby_dress","loafers","boat_shoes",
                            "work_boots","hiking_boots","chelsea_boots","chukka_boots","slides","sandals_sport","flip_flops" };
            string[] sh_f = { "sneakers","running_shoes","flats_ballet","heels_pumps","heels_block","wedges","sandals_dressy","sandals_sport",
                            "boots_knee_high","boots_ankle","slides","flip_flops" };
            string[] sh_n = { "sneakers", "running_shoes", "slides", "sandals_sport", "hiking_boots" };

            // Headwear
            string[] hw_m = { "baseball_cap", "dad_cap", "snapback_fitted", "trucker_cap", "beanie", "knit_cap", "bucket_hat" };
            string[] hw_f = { "baseball_cap", "dad_cap", "beanie", "bucket_hat", "sun_hat", "visor", "headband", "bandana" };
            string[] hw_n = { "baseball_cap", "beanie", "bucket_hat" };

            // ---- helpers ----
            string[] Blend(string[] male, string[] female, string[] neutral)
            {
                var bag = new List<string>();
                if (isMan) { if (UnityEngine.Random.value < 0.70f) bag.AddRange(male); if (UnityEngine.Random.value < 0.90f) bag.AddRange(neutral); if (UnityEngine.Random.value < 0.15f) bag.AddRange(female); }
                else if (isWoman) { if (UnityEngine.Random.value < 0.70f) bag.AddRange(female); if (UnityEngine.Random.value < 0.90f) bag.AddRange(neutral); if (UnityEngine.Random.value < 0.15f) bag.AddRange(male); }
                else { bag.AddRange(neutral); if (UnityEngine.Random.value < 0.5f) bag.AddRange(male); if (UnityEngine.Random.value < 0.5f) bag.AddRange(female); }
                return bag.Distinct().ToArray();
            }

            HashSet<string> PickSome(string[] pool, int min, int max)
            {
                int n = Mathf.Clamp(UnityEngine.Random.Range(min, max + 1), 0, Mathf.Max(0, pool.Length));
                return new HashSet<string>(pool.OrderBy(_ => UnityEngine.Random.value).Take(n), StringComparer.Ordinal);
            }

            void SafeSet(ClothingCategory cat, IEnumerable<string> ids)
            {
                IEnumerable<string> list = ids;
                if (catalog != null)
                {
                    // keep only IDs that exist in the catalog & match the category
                    list = ids.Where(id => catalog.TryGetById(id, out var t) && t.category == cat);
                }
                cw.SetPreferredTypes(cat, new HashSet<string>(list, StringComparer.Ordinal));
            }

            // ---- choose + set per category ----
            SafeSet(ClothingCategory.ShortSleeveShirt, PickSome(Blend(ss_m, ss_f, ss_n), 2, 5));
            SafeSet(ClothingCategory.LongSleeveShirt, PickSome(Blend(ls_m, ls_f, ls_n), 2, 5));
            SafeSet(ClothingCategory.Bottoms, PickSome(Blend(bt_m, bt_f, bt_n), 2, 4));
            SafeSet(ClothingCategory.Overgarments, PickSome(Blend(og_m, og_f, og_n), 1, 4));
            SafeSet(ClothingCategory.Socks, PickSome(Blend(so_m, so_f, so_n), 2, 3));
            SafeSet(ClothingCategory.Underwear, PickSome(Blend(uw_m, uw_f, uw_n), 3, 6));
            SafeSet(ClothingCategory.Shoes, PickSome(Blend(sh_m, sh_f, sh_n), 2, 4));
            SafeSet(ClothingCategory.Headwear, PickSome(Blend(hw_m, hw_f, hw_n), 0, 2));
        }


        /* 16) Clear momentary states (excluded as requested) */
        s.emotions = new characterStats.EmotionalStateStats();
        s.relationships = new characterStats.RelationshipsStats();
        s.opinions = new characterStats.OpinionStats();
        s.thoughtsAndFeelings = new characterStats.ThoughtsAndFeelingsStats();

        /* 17) Needs reset (excluded as requested) */
        int full = 100;
        s.needs.hunger = s.needs.thirst = s.needs.sleep = s.needs.urination = s.needs.defecation =
        s.needs.hygiene = s.needs.social_interaction = s.needs.mental_stimulation = full;

        /* 18) Wardrobe preferences */
        SeedWardrobePreferences(s);

        // 19) Opinions
        RandomizeOpinions(s);

        // 19a) Health conditions (rare)
        RandomizeConditions(s);

        /* 20) Habits & Hobbies */
        RandomizeHabitsAndHobbies(s);

        RandomizeLearnedSkills(s);
        RandomizeImprovableSkills(s);
    }
    
    public static characterStats Create(characterStats.CharacterType type)
    {
        var go = new GameObject($"{type} Character");
        var s = go.AddComponent<characterStats>();
        if (Game.Instance) go.transform.SetParent(Game.Instance.transform);

        StatLimits.ResetPreviousRanges();   // <— add this
        Randomize(s);                       // EnsureRefs already runs inside Randomize
        s.character_type = type;
        return s;
    }

}