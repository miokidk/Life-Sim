using System;
using System.Collections.Generic;
using UnityEngine;

public class characterStats : MonoBehaviour
{
    // ─── Characteristics ──────────────────────────────────────
    public ProfileStats            profile            = new();
    public QualitiesStats          qualities          = new();
    public EmotionalStateStats     emotions           = new();
    public RelationshipsStats      relationships      = new();
    public SkillsStats skills                         = new();
    public TraitsStats traits                         = new();
    public NeedsStats              needs              = new();
    public SensesStats             senses             = new();
    public HabitsHobbiesStats habitsAndHobbies        = new();

    public ThoughtsAndFeelingsStats thoughtsAndFeelings = new();
    public OpinionStats            opinions           = new();

    // ─── Physicals ─────────────────────────────────────────────
    public HeadStats               head               = new();
    public NeckStats               neck               = new();
    public TorsoStats              torso              = new();
    public InternalOrgansStats     internalOrgans     = new();
    public ArmsStats               arms               = new();
    public PelvisReproductiveStats pelvisAndRepro     = new();
    public LegsStats               legs               = new();

    // ─── Meta ─────────────────────────────────────────────
    [SerializeField] public string id;
    public enum CharacterType { Main, Side, Extra, World }

    [Header("Meta")]
    [SerializeField] public CharacterType character_type = CharacterType.World;

    public bool IsMain  => character_type == CharacterType.Main;
    public bool IsSide  => character_type == CharacterType.Side;
    public bool IsExtra => character_type == CharacterType.Extra;
    public bool IsWorld => character_type == CharacterType.World;

// ─── Validation ──────────────────────────────────────────
#if UNITY_EDITOR
void OnValidate()
{
    if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)) return;
    StatLimits.RecomputeLimits(this);
    RefreshHierarchyName(); // keep the name in sync when editing in Inspector
}
#endif

void Awake()  { if (string.IsNullOrEmpty(id)) id = System.Guid.NewGuid().ToString(); }
void Reset()  { if (string.IsNullOrEmpty(id)) id = System.Guid.NewGuid().ToString(); }


// Public method for runtime validation
public void ValidateStats()
{
    StatLimits.RecomputeLimits(this);
}

    // Call this when age changes during gameplay
    public void OnAgeChanged()
    {
        StatLimits.ResetPreviousRanges();
        StatLimits.RecomputeLimits(this);
    }


    // Call this when creating/modifying character
    public void OnStatChanged()
    {
        StatLimits.RecomputeLimits(this);
    }
    
    public void RefreshHierarchyName()
    {
        string f = (profile.first_name ?? "").Trim();
        string l = (profile.last_name ?? "").Trim();
        string newName = (f + " " + l).Trim();
        if (string.IsNullOrEmpty(newName)) newName = "Character";
        gameObject.name = newName;
    }

    #region Profile ---------------------------------------------------------

public enum GenderType { Man, Woman, NonBinary }
    public enum SexType      { Male, Female, Intersex }

    [Serializable] public struct AgeYearsMonths
    {
        [Range(0,110)] public int years;   // 0‑110
        [Range(0,11)]  public int months;  // 0‑11
    }

    public enum HairType
    {
        Type1A, Type1B, Type1C,
        Type2A, Type2B, Type2C,
        Type3A, Type3B, Type3C,
        Type4A, Type4B, Type4C
    }

    public enum SkinColor
    {
        Ivory, Porcelean, PaleIvory, WarmIvory, Sand,
        RoseBlige, Limestone, Blige, Sienna, Honey,
        Band, Almond, Chestnut, Bronze, Umber,
        Golden, Espresso, Cacao
    }

    [Serializable] public struct HeightFtIn
    {
        [Range(0, 9)] public int feet;    // whole feet
        [Range(0,11)] public int inches; // 0-11 only
    }

    [Serializable] public class LanguageStats
    {
        public bool english, spanish, french, chinese, arabic;
        public string primary_language;
    }

    [Serializable] public class VoiceStats
    {
        [Range(1, 100)]public int pitch, tone, timbre;
    }

    [Serializable] public class WeightStats
    {
        [Range(1, 400)]public int pounds;
    }

    [Serializable] public class ProfileStats
    {
        public string first_name, last_name;
        public AgeYearsMonths age;
        public LanguageStats  language = new();
        public GenderType     gender;
        public SexType        sex;
        public VoiceStats     voice = new();
        public SkinColor      skin_color;
        public HeightFtIn     height;
        public WeightStats    weight = new();
        public HairType       hair_type;
    }
    #endregion

    
    #region Qualities -------------------------------------------------------
    [Serializable] public class IdentityStats
    {
        [Range(0, 100)] public int self_awareness, self_esteem, ego;
    }

    [Serializable] public class EmotionsQualityStats
    {
        [Range(0, 100)] public int emotional_intelligence, optimism, eccentricity, loneliness;
    }

    [Serializable] public class CognitiveAbilitiesStats
    {
        [Range(0, 100)] public int logic, creativity, focus, intelligence;
    }

    [Serializable] public class SocialSkillsStats
    {
        [Range(0, 100)] public int sociality, cooperation, politeness, charisma, humility, sense_of_humor;
    }

    [Serializable] public class RomanceStats
    {
        [Range(0, 100)] public int sensuality, passion, flirtation, seduction;
    }

    [Serializable] public class MoralCompassStats
    {
        [Range(0, 100)] public int honesty, responsibility, generosity, moral_strength, spirituality;
    }

    [Serializable] public class ResilienceStats
    {
        [Range(0, 100)] public int independence, perseverance, courage, patience, self_control, decisiveness;
    }

    [Serializable] public class QualitiesStats
    {
        public IdentityStats            identity            = new();
        public EmotionsQualityStats     emotions            = new();
        public CognitiveAbilitiesStats  cognitive_abilities = new();
        public SocialSkillsStats        social_skills       = new();
        public RomanceStats             romance             = new();
        public MoralCompassStats        moral_compass       = new();
        public ResilienceStats          resilience          = new();
    }
    #endregion

    #region Emotional State -------------------------------------------------
    [Serializable] public class EmotionalStateStats
    {
        [Range(0, 100)] public int amazement, love, joy, relief, excitement, confidence, surprise,
                pride, anger, sadness, grief, fear, anxiety, guilt, desire,
                affection, playfulness, boredom;
    }
    #endregion

    #region Relationships ---------------------------------------------------
    public enum RelationshipTag
    {
        // family
        Parent, Child, Sibling, Grandparent, Grandchild, Aunt, Uncle, Niece, Nephew,
        Cousin, StepParent, StepChild, StepSibling, InLaw,

        // friends
        BestFriend, CloseFriend, ChildhoodFriend, Friend, WorkFriend, FamilyFriend,

        // colleagues
        Manager, Subordinate, Peer, TeamLeader, Client,

        // mature relationships
        Boyfriend, Girlfriend, Partner, Spouse, Fiancé, CasualDating,
        PolyamorousPartner, PrimaryPartner, ExGirlfriend, ExBoyfriend,
        ExPartner, ExSpouse, FriendWithBenefits, CasualHookup,

        // mentorship
        Mentor, Mentee, Coach, Player, Trainer, Trainee, Tutor, Student, Teacher,

        // acquaintances
        SpecialAcquaintance, CasualAcquaintance, Familiar,

        // neighbors
        ImmediateNeighbor, StreetNeighbor, BuildingNeighbor, BlockNeighbor,

        // online
        GamingFriend, SocialMediaFriend, OnlineFoe,

        // adversarial
        Rival, Foe, Enemy
    }

    /// one entry per person
    [Serializable]
    public class RelationshipEntry
    {
        // Names
        public string first_name;
        public string last_name;

        public string personName;

        // Tags (already supported)
        public List<RelationshipTag> tags = new();   // multiple classifications

        // Duration: choose EITHER days OR years+months
        public bool duration_is_days;                // true = use days, false = use years+months
        [Range(0, 365000)] public int duration_days; // arbitrary high cap
        public AgeYearsMonths duration_ym;           // reuse struct (years 0–110, months 0–11)

        // Relationship metrics (0 = the left side of each pair, 100 = the right side)
        // Distance/Closeness, Dissatisfaction/Satisfaction, Hate/Love
        [Range(0,100)] public int closeness;
        [Range(0,100)] public int satisfaction;
        [Range(0,100)] public int love;

        // Independent axes
        [Range(0,100)] public int conflict;
        [Range(0,100)] public int given_support;
        [Range(0,100)] public int received_support;

        // --- Convenience (not serialized to Inspector) ---
        public string DisplayName => 
            string.IsNullOrWhiteSpace(first_name) && string.IsNullOrWhiteSpace(last_name)
            ? (personName ?? "").Trim()
            : $"{first_name} {last_name}".Trim();

        public void SetDurationDays(int days)
        {
            duration_is_days = true;
            duration_days = Mathf.Max(0, days);
            duration_ym.years = 0; duration_ym.months = 0;
        }

        public void SetDurationYM(int years, int months)
        {
            duration_is_days = false;
            duration_days = 0;
            duration_ym.years  = Mathf.Max(0, years);
            duration_ym.months = Mathf.Clamp(months, 0, 11);
        }
    }

    [Serializable]
    public class RelationshipsStats
    {
        public List<RelationshipEntry> people = new(); // starts empty

        // Helpers (optional)
        public RelationshipEntry AddOrGet(string first, string last)
        {
            var r = people.Find(p => p.first_name == first && p.last_name == last);
            if (r == null) { r = new RelationshipEntry { first_name = first, last_name = last }; people.Add(r); }
            return r;
        }
    }
    #endregion
    
    #region Skills --------------------------------------------------------------
    public enum SkillMode { LearnOnce, Improvable }

    [Serializable]
    public class SkillEntry
    {
        public string id;                 // e.g., "Writing", "Sprint", etc.
        public SkillMode mode;

        // --- Learn-once ---
        [Range(0,100)] public int progress;  // 0–100
        public bool learned;                 // true once mastered

        // --- Improvable ---
        [Range(0,2)]   public int awareness;     // 0–2
        [Range(0,100)] public int freshness;     // 0–100
        [Range(0,100)] public int confidence;    // 0–100
        [Range(0,100)] public int efficiency;    // 0–100
        [Range(0,100)] public int precision;     // 0–100

        // Convenience
        public bool IsLearnOnce => mode == SkillMode.LearnOnce;

        public void SetLearnOnceProgress(int value)
        {
            mode = SkillMode.LearnOnce;
            progress = Mathf.Clamp(value, 0, 100);
            learned = (progress >= 100);
        }

        public void SetImprovable(int awareness, int fresh, int conf, int eff, int prec)
        {
            mode = SkillMode.Improvable;
            this.awareness  = Mathf.Clamp(awareness, 0, 2);
            this.freshness  = Mathf.Clamp(fresh, 0, 100);
            this.confidence = Mathf.Clamp(conf, 0, 100);
            this.efficiency = Mathf.Clamp(eff, 0, 100);
            this.precision  = Mathf.Clamp(prec, 0, 100);
        }

    }

    [Serializable]
    public class SkillsStats
    {
        public List<SkillEntry> list = new();

        // Ensures one entry per skill id; updates mode if it changes
        public SkillEntry AddOrGet(string skillId, SkillMode modeIfNew = SkillMode.Improvable)
        {
            var e = list.Find(s => string.Equals(s.id, skillId, StringComparison.OrdinalIgnoreCase));
            if (e == null)
            {
                e = new SkillEntry { id = skillId, mode = modeIfNew };
                list.Add(e);
            }
            else
            {
                e.mode = modeIfNew;
            }
            return e;
        }
    }
    #endregion


    #region Traits ----------------------------------------------------------
    [Serializable] public class TraitsStats
    {
        public bool adventurous, ambitious, artistic, argumentative, carefree, cautious,
                    competitive, curious, decisive, dependable, determined, distant,
                    energetic, friendly, frugal, hygienic, indecisive, impulsive, jealous,
                    leader, loud, loyal, materialistic, mean, messy, perceptive, playful,
                    practical, punctual, quiet, rebellious, resourceful, sarcastic,
                    sentimental, serious, silly, spontaneous, stoic, stubborn, thoughtful,
                    tidy, visionary;
    }
    #endregion

    #region Needs -----------------------------------------------------------
    [Serializable] public class NeedsStats
    {
        [Range(0, 100)] public int hunger, thirst, sleep, urination, defecation, hygiene,
                social_interaction, mental_stimulation;
    }
    #endregion

    #region Senses ----------------------------------------------------------
    [Serializable] public class SightStats  { [Range(0, 100)] public int visual_acuity,  color_perception,  light_sensitivity; }
    [Serializable] public class HearingStats{ [Range(0, 100)] public int auditory_acuity, noise_sensitivity; }
    [Serializable] public class TasteStats  { [Range(0, 100)] public int flavor_discrimination; }
    [Serializable] public class TouchStats  { [Range(0, 100)] public int heat_sensitivity, cold_sensitivity, pain_tolerance; }

    [Serializable] public class SensesStats
    {
        public SightStats   sight   = new();
        public HearingStats hearing = new();
        public TasteStats   taste   = new();
        public TouchStats   touch   = new();
    }
    #endregion
    
    #region Habits & Hobbies ----------------------------------------------------------
    [Serializable] public class HabitsHobbiesStats
    {
        public enum Category { Habit, Hobby }
        public enum Adherence { Rigid, Consistent, Flexible, Loose, Sporadic } // UI shows "Rigid" now; expand as needed.

        public enum FrequencyMode { Every, TimesPer }
        public enum FrequencyUnit { Hour, Day, Week, Month, Year }

        [Serializable] public class Frequency
        {
            public FrequencyMode mode;            // Every | TimesPer

            // Every N <unit>  (e.g., Every 3 Days)
            [Range(1, 6)] public int every_amount = 1;
            public FrequencyUnit every_unit = FrequencyUnit.Day;

            // <times> Times per <unit>  (e.g., 2 Times per Day)
            [Range(1, 6)] public int times_per_count = 1;
            public FrequencyUnit times_per_unit = FrequencyUnit.Day;
        }

        [Serializable] public class Entry
        {
            public Category category;         // Habit or Hobby
            public string   type;             // e.g., "Daily self-care & hygiene", "Creative"
            public string   name;             // e.g., "Brush teeth", "Draw"

            // Session
            public string   session_length;   // one of your mapped options (e.g., "1–2", "Until complete")

            // Frequency + Adherence
            public Frequency frequency = new Frequency();
            public Adherence adherence = Adherence.Rigid;

            // Triggers chosen from the Trigger list
            public List<string> triggers = new();

            // Convenience flags
            public bool active = true;
            public bool pinned;
        }

        public List<Entry> list = new();

        // Helper: ensure one per (category+name); updates type if it changes
        public Entry AddOrGet(Category cat, string name, string typeIfNew = null)
        {
            var e = list.Find(x => x.category == cat && string.Equals(x.name, name, StringComparison.OrdinalIgnoreCase));
            if (e == null)
            {
                e = new Entry { category = cat, name = name, type = typeIfNew ?? "" };
                list.Add(e);
            }
            else if (!string.IsNullOrEmpty(typeIfNew))
            {
                e.type = typeIfNew;
            }
            return e;
        }
    }
    #endregion


    #region Thoughts and Feelings ------------------------------------------
/* ---------- Thought categories ---------- */
    [Serializable] public class PositiveThoughts_SelfPerception
    { public bool confident, clear_headed, motivated, optimistic, mindful, proud, resilient, empowered; }

    [Serializable] public class PositiveThoughts_CognitiveState
    { public bool amazed, surprised, focused, distracted, contemplative, inspired, inquisitive; }

    [Serializable] public class PositiveThoughts_PlanningReflection
    { public bool planning, daydreaming, connected_to_nature, appreciative_of_surroundings,
                future_oriented, reflective; }

    [Serializable] public class PositiveThoughts_ActionProductivity
    { public bool productive, creative, generous, learning, developing_skills, self_improving,
                efficient, determined; }

    [Serializable] public class PositiveThoughts_Financial
    { public bool stable, budgeting, financially_planning, thriving, financially_independent; }

    [Serializable] public class PositiveThoughts
    {
        public PositiveThoughts_SelfPerception    self_perception         = new();
        public PositiveThoughts_CognitiveState    cognitive_state         = new();
        public PositiveThoughts_PlanningReflection planning_and_reflection = new();
        public PositiveThoughts_ActionProductivity action_and_productivity = new();
        public PositiveThoughts_Financial         financial_thoughts      = new();
    }

    /* ---- Negative thoughts ---- */
    [Serializable] public class NegativeThoughts_SelfPerception
    { public bool anxious, confused, doubtful, overwhelmed, pessimistic, stressed,
                distrustful, inadequate, ashamed; }

    [Serializable] public class NegativeThoughts_CognitiveState
    { public bool bored, shocked, unfocused, scattered, indifferent, uninspired, ignorant; }

    [Serializable] public class NegativeThoughts_PlanningReflection
    { public bool disorganized, lost_in_thought, disconnected_from_nature,
                discontented_with_surroundings, short_sighted, unreflective; }

    [Serializable] public class NegativeThoughts_ActionProductivity
    { public bool procrastinating, destructive, neglectful, burned_out, ineffective; }

    [Serializable] public class NegativeThoughts_Financial
    { public bool in_debt, financially_stressed, impoverished, undervalued; }

    [Serializable] public class NegativeThoughts
    {
        public NegativeThoughts_SelfPerception    self_perception         = new();
        public NegativeThoughts_CognitiveState    cognitive_state         = new();
        public NegativeThoughts_PlanningReflection planning_and_reflection = new();
        public NegativeThoughts_ActionProductivity action_and_productivity = new();
        public NegativeThoughts_Financial         financial_thoughts      = new();
    }

    /* ---- Neutral thoughts ---- */
    [Serializable] public class NeutralThoughts_Social
    { public bool acquainted, professional, formal; }

    [Serializable] public class NeutralThoughts_Environment
    { public bool neutral_perception_of_surroundings; }

    [Serializable] public class NeutralThoughts_Routine
    { public bool performing_routine_tasks, maintaining; }

    [Serializable] public class NeutralThoughts
    {
        public NeutralThoughts_Social      social_interactions      = new();
        public NeutralThoughts_Environment environmental_perception = new();
        public NeutralThoughts_Routine     routine_and_maintenance  = new();
    }

    /* ---- Feeling categories ---- */
    [Serializable] public class PositiveFeelings_Emotions
    { public bool happy, content, loving, grateful, excited, joyful,
                relieved, ecstatic, gratified; }

    [Serializable] public class PositiveFeelings_Physical
    { public bool energetic, relaxed, strong, vital, rejuvenated, supple; }

    [Serializable] public class PositiveFeelings_Social
    { public bool friendly, trusting, companionable, belonging, affectionate,
                included, respected; }

    [Serializable] public class PositiveFeelings_Aspirations
    { public bool ambitious, hopeful, aspiring, visionary, purposeful; }

    [Serializable] public class PositiveFeelings_BasicNeeds
    { public bool satisfied, fulfilled, comfortable, nourished, well_rested; }

    [Serializable] public class PositiveFeelings_Financial
    { public bool secure, abundant, wealthy; }

    [Serializable] public class PositiveFeelings
    {
        public PositiveFeelings_Emotions      emotions            = new();
        public PositiveFeelings_Physical      physical_state      = new();
        public PositiveFeelings_Social        social_interactions = new();
        public PositiveFeelings_Aspirations   aspirations         = new();
        public PositiveFeelings_BasicNeeds    basic_needs         = new();
        public PositiveFeelings_Financial     financial_feelings  = new();
    }

    /* ---- Negative feelings ---- */
    [Serializable] public class NegativeFeelings_Emotions
    { public bool sad, angry, fearful, disgusted, jealous, frustrated,
                guilty, grieving, disappointed, resentful, ashamed; }

    [Serializable] public class NegativeFeelings_Physical
    { public bool fatigued, in_pain, uncomfortable, stiff, sluggish; }

    [Serializable] public class NegativeFeelings_Health
    { public bool sick, nauseous, dizzy, headache, lightheaded, vertiginous,
                disoriented, blurred_vision, acne, tinnitus, gingivitis,
                erectile_dysfunction, ibs, anxious, depressed, bipolar,
                socially_anxious, insomniac, gender_dysphoric, narcissistic; }

    [Serializable] public class NegativeFeelings_Social
    { public bool lonely, betrayed, in_conflict, isolated, neglected, ridiculed; }

    [Serializable] public class NegativeFeelings_Aspirations
    { public bool unambitious, hopeless, unaspiring, unimaginative, purposeless; }

    [Serializable] public class NegativeFeelings_BasicNeeds
    { public bool hungry, thirsty, sleep_deprived, urgent, starving, parched; }

    [Serializable] public class NegativeFeelings_Financial
    { public bool insecure, scarce, poor; }

    [Serializable] public class NegativeFeelings
    {
        public NegativeFeelings_Emotions      emotions            = new();
        public NegativeFeelings_Physical      physical_state      = new();
        public NegativeFeelings_Health        health_conditions   = new();
        public NegativeFeelings_Social        social_interactions = new();
        public NegativeFeelings_Aspirations   aspirations         = new();
        public NegativeFeelings_BasicNeeds    basic_needs         = new();
        public NegativeFeelings_Financial     financial_feelings  = new();
    }

    /* ---- Neutral feelings ---- */
    [Serializable] public class NeutralFeelings_Emotions
    { public bool calm, indifferent, curious, nostalgic, bored; }

    [Serializable] public class NeutralFeelings
    {
        public NeutralFeelings_Emotions emotions = new();
    }

    [Serializable] public class ThoughtCategories
    {
        public PositiveThoughts positive_thoughts = new();
        public NegativeThoughts negative_thoughts = new();
        public NeutralThoughts  neutral_thoughts  = new();
    }

    [Serializable] public class FeelingCategories
    {
        public PositiveFeelings positive_feelings = new();
        public NegativeFeelings negative_feelings = new();
        public NeutralFeelings  neutral_feelings  = new();
    }

    [Serializable] public class ThoughtsAndFeelingsStats
    {
        public ThoughtCategories  thought_categories = new();
        public FeelingCategories  feeling_categories = new();
    }
    #endregion

    #region Opinions ---------------------------------------------------
    public enum OpinionValue
    {
        Positive,
        Curious,
        Neutral,
        Indifferent,
        Negative
    }

    [Serializable] public class Opinion
    {
        public string topic;          // e.g. "Pizza", "Politics", "John Doe"
        public OpinionValue value;    // how the character feels about it
    }

    [Serializable] public class OpinionStats
    {
        public List<Opinion> opinions = new();   // start empty; fill at runtime
    }
#endregion

#region Head ---------------------------------------------------
[Serializable]
public class HeadStats
{
    // ---------- skin ----------
    [Serializable]
    public class SkinStats
    {
        [Range(0, 100)] public int hydration, smoothness, elasticity, stretch_marks;
    }

    public SkinStats skin = new();

    // ---------- skull ----------
    [Serializable]
    public class SkullSectionHairColor
    {
        public string roots;
        public string mids;
        public string ends;
    }

    [Serializable]
    public class SkullSectionHair
    {
        [Range(0, 500)] public int amount;
        public string style;
        [Range(0, 100)] public int messiness;
        public SkullSectionHairColor color = new();
    }

    [Serializable]
    public class SkullSectionBase
    {
        [Range(0, 100)] public int bruised, cut, discomfort;
        public bool fractured;
    }

    [Serializable]
    public class SkullSectionWithHair : SkullSectionBase
    {
        public SkullSectionHair hair = new();
    }

    public SkullSectionBase forehead = new();
    public SkullSectionWithHair front = new();
    public SkullSectionWithHair top = new();
    public SkullSectionWithHair back = new();
    public SkullSectionWithHair left_side = new();
    public SkullSectionWithHair right_side = new();


    // ---------- brain ----------
    [Serializable]
    public class BrainStats
    {
        [Range(0, 100)] public int myelin;
        [Range(0, 100)] public int plaque;
        [Range(0, 100)] public int shape_regulation;
        [Range(0, 100)] public int inflammation;
        [Range(0, 100)] public int lining;
        [Range(0, 100)] public int blood_amount;
        [Range(0, 100)] public int cavity_pressure;
        [Range(0, 100)] public int spinal_fluid_leak;
        [Range(0, 100)] public int bruised;
    }

    public BrainStats brain = new();

    // ---------- eyes ----------
    [Serializable]
    public class EyeStats
    {
        [Range(0, 100)] public int lens_flexibility;
        [Range(0, 100)] public int refraction;
        [Range(0, 100)] public int bruised;
        [Range(0, 100)] public int strain;
        [Range(0, 100)] public int dryness;
        [Range(0, 100)] public int discomfort;
    }

    public EyeStats left_eye = new();
    public EyeStats right_eye = new();

    // ---------- nose ----------
    [Serializable]
    public class NoseStats
    {
        [Range(0, 100)] public int inflammation;
        [Range(0, 100)] public int bruised;
        public bool fractured;
        [Range(0, 100)] public int cut;
        [Range(0, 10)] public int hair_amount;
        [Range(0, 100)] public int discomfort;
    }

    public NoseStats nose = new();

    // ---------- mouth ----------
    [Serializable]
    public class HairSimple
    {
        [Range(0, 150)] public int amount;
        public string style;
        [Range(0, 100)] public int messiness;
    }

    [Serializable]
    public class LipStats
    {
        [Range(0, 100)] public int inflammation;
        [Range(0, 100)] public int bruised;
        [Range(0, 100)] public int cut;
        public HairSimple hair = new();
        [Range(0, 100)] public int discomfort;
    }

    [Serializable]
    public class TongueTonsilStats
    {
        [Range(0, 100)] public int inflammation;
        [Range(0, 100)] public int bruised;
        [Range(0, 100)] public int cut;
    }

    public LipStats lips_top = new();
    public LipStats lips_bottom = new();
    public TongueTonsilStats tongue = new();
    public TongueTonsilStats tonsils = new();

    // ---------- teeth ----------
    [Serializable]
    public class Tooth
    {
        [Range(0, 100)] public int alignment;
        [Range(0, 100)] public int whiteness;
        public bool chipped;
        public bool missing;
    }

    [Serializable]
    public class TeethQuadrant
    {
        // 2 × incisors (central, lateral)
        public Tooth[] incisors = new Tooth[2] { new Tooth(), new Tooth() };

        // 1 × canine
        public Tooth canine = new();

        // 2 × premolars (first, second)
        public Tooth[] premolars = new Tooth[2] { new Tooth(), new Tooth() };

        // 2 × molars (first, second) ‑‑ add a third slot if you want wisdom teeth
        public Tooth[] molars = new Tooth[2] { new Tooth(), new Tooth() };

        [Range(0, 100)] public int discomfort;
    }

    [Serializable]
    public class TeethStats
    {
        public TeethQuadrant upper_right = new();
        public TeethQuadrant upper_left = new();
        public TeethQuadrant lower_right = new();
        public TeethQuadrant lower_left = new();


        [Range(0, 100)] public int overall_whiteness;
        [Range(0, 100)] public int overall_alignment;


        [Range(0, 100)] public int breath_freshness;
        [Range(0, 100)] public int moisture;
    }

    public TeethStats teeth = new();

    // ---------- cheekbones ----------
    [Serializable]
    public class BoneSection
    {
        [Range(0, 100)] public int bruised;
        public bool fractured;
        [Range(0, 100)] public int cut;
        public HairSimple hair = new();
        [Range(0, 100)] public int discomfort;
    }

    public BoneSection cheekbone_left = new();
    public BoneSection cheekbone_right = new();

    // ---------- jaw ----------
    [Serializable]
    public class JawStats
    {
        public BoneSection left = new();
        public BoneSection right = new();
        [Range(0, 100)] public int strength;
    }

    public JawStats jaw = new();


    // ---------- chin ----------
    [Serializable]
    public class ChinStats
    {
        public BoneSection chin = new();
        public HairSimple hair = new();
    }
    
    public ChinStats Chin = new();
}

#endregion

#region Neck ------------------------------------------------------------
[Serializable]
public class NeckStats
{
    [Serializable] public class SkinStats
    {
        [Range(0, 100)] public int hydration, smoothness, elasticity, stretch_marks;
    }

    public SkinStats skin = new();

    [Range(0, 100)] public int inflammation;
    [Range(0, 100)] public int bruised;
    public bool fractured;
    [Range(0, 100)] public int cut;
    [Range(0, 100)] public int discomfort;
    [Range(0, 100)] public int soreness;
    [Range(0, 100)] public int muscle_mass;
    [Range(0, 100)] public int fat;
}
#endregion

#region Torso -----------------------------------------------------------
[Serializable] public class TorsoStats
{
    // ----- simple sections -----
    [Serializable] public class SkinStats      { [Range(0, 100)] public int hydration, smoothness, elasticity, stretch_marks; }
    [Serializable] public class SectionHair   { [Range(0, 100)] public int bruised, cut, discomfort, soreness, strength, muscle_mass, fat; [Range(0, 25)] public int hair_amount; }
    [Serializable] public class SectionNoHair { [Range(0, 100)] public int bruised, cut, discomfort, soreness, muscle_mass, fat; }

    [Serializable] public class RibsStats
    {
        [Range(0, 100)] public int bruised, cut, discomfort;
        public bool fractured;
    }

    [Serializable] public class SpineStats
    {
        [Range(0, 100)] public int spinal_cord_lining, fat_plaque, cholesterol_plaque, bruised, cut, discomfort;
        public bool fractured;
    }

    [Serializable] public class CollarboneStats
    {
        [Range(0, 100)] public int bruised, cut, discomfort;
        public bool fractured;
    }

    // actual torso data
    public SkinStats     skin             = new();
    public SectionHair   chest            = new();
    public SectionHair   upper_back       = new();
    public SectionNoHair lower_back       = new();
    public SectionHair   abdomen          = new();
    public RibsStats     ribs             = new();
    public SpineStats    spine            = new();
    public CollarboneStats left_collarbone  = new(), right_collarbone = new();

    [Range(0, 100)] public int flexibility, stiffness, fatigue, strength, muscle_mass, fat;
}
#endregion

#region Internal Organs -------------------------------------------------
[Serializable] public class InternalOrgansStats
{
    // ----- heart -----
    [Serializable] public class HeartConnections { [Range(0, 100)] public int brain, lungs, kidneys, liver, spine, spleen; }

    [Serializable] public class HeartStats
    {
        [Range(0, 100)] public int blood_vessel, blood_vessel_size, blood_vessel_swelling;
        [Range(0, 100)] public int heartbeat_regulation, shape_regulation, inflammation, coronary_arteries_plaque;
        public HeartConnections connections = new();
        [Range(0, 100)] public int leaky_valves, hole_in_heart, bruised, discomfort, strength;
    }

    // ----- paired templates -----
    [Serializable] public class LungStats
    {
        [Range(0, 100)] public int mucus_amount, airway_size, inflammation, air_sacks, bruised, hole, discomfort;
    }

    [Serializable] public class KidneyStats
    {
        [Range(0, 100)] public int nephron_inflammation, small_blood_vessels, inflammation, bruised, discomfort;
    }

    // ----- singles -----
    [Serializable] public class LiverStats    { [Range(0, 100)] public int fat_amount, inflammation, bruised, discomfort; }
    [Serializable] public class SpleenStats   { [Range(0, 100)] public int size, inflammation, bruised, discomfort; }
    [Serializable] public class PancreasStats { [Range(0, 100)] public int insulin_production_rate, insulin_amount, insulin_resistance, inflammation, bruised, discomfort; }
    [Serializable] public class StomachStats  { [Range(0, 100)] public int metabolism, lactose_intolerance, inflammation, bruised, discomfort; }

    // actual organ data
    public HeartStats   heart        = new();
    public LungStats    left_lung    = new(), right_lung    = new();
    public LiverStats   liver        = new();
    public KidneyStats  left_kidney  = new(), right_kidney  = new();
    public SpleenStats  spleen       = new();
    public PancreasStats pancreas     = new();
    public StomachStats stomach      = new();
}
#endregion

#region Arms ------------------------------------------------------------
[Serializable] public class ArmsStats
{
    // ----- section templates -----
    [Serializable] public class SkinStats  { [Range(0, 100)] public int hydration, smoothness, elasticity, stretch_marks; }

    [Serializable]
    public class SegmentHair
    {
        [Range(0, 100)] public int bruised, cut, discomfort;
        public bool fractured;
        [Range(0, 100)] public int soreness;
        [Range(0, 25)] public int hair_amount;
    }

    [Serializable] public class SegmentBasic
    {
        [Range(0, 100)] public int bruised, cut, discomfort;
        public bool fractured;
    }

    [Serializable] public class SegmentMuscle
    {
        [Range(0, 100)] public int bruised, cut, discomfort, soreness;
    }

    // ---------- fingers ----------
    [Serializable] public class FingersStats
    {
        public SegmentBasic thumb  = new();
        public SegmentBasic index  = new();
        public SegmentBasic middle = new();
        public SegmentBasic ring   = new();
        public SegmentBasic pinky  = new();
    }

    [Serializable] public class ArmSide
    {
        public SkinStats    skin      = new();
        public SegmentHair  shoulder  = new();
        public SegmentMuscle bicep     = new();
        public SegmentBasic elbow     = new();
        public SegmentHair  forearm   = new();
        public SegmentBasic wrist     = new(), hand = new();
        public FingersStats fingers   = new();

        [Range(0, 100)] public int flexibility, stiffness, fatigue, strength, muscle_mass, fat;
    }

    // actual arm data
    public ArmSide left = new(), right = new();
}
#endregion

#region Pelvis & Reproductive Organs -----------------------------------
    [Serializable] public class PelvisReproductiveStats
{
    [Serializable] public class HipsStats
    {
        [Range(0, 100)] public int  bruised, cut, discomfort, soreness, flexibility;
        public bool fractured;
    }

    [Serializable]
    public class PenisStats
    {
        [Range(0, 100)] public int bruised, cut, discomfort, soreness, length, girth;
        [Range(0, 30)] public int hair_amount;
    }

    [Serializable] public class TesticleSide
    {
        [Range(0, 100)] public int bruised, cut, discomfort;
    }

    [Serializable] public class TesticlesStats
    {
        // 👉 instantiate both sides immediately
        public TesticleSide left  = new();
        public TesticleSide right = new();

        [Range(0, 10)] public int hair_amount;
    }

    [Serializable]
    public class VulvaStats
    {
        [Range(0, 100)] public int bruised, cut, discomfort, soreness, lip_size, vaginal_opening_size;
        [Range(0, 30)] public int hair_amount;
    }

    [Serializable] public class UterusStats
    {
        /* overall cycle state */
        public bool      cycles_active;                 // true ⇢ between menarche & menopause
        [Range(0,28)]    public int   cycle_day;        // 0 = first day of bleeding
        public bool      bleeding;                      // true while shedding lining
        public bool      menopausal;                    // frozen cycles after ~55 yrs

        /* fertility & pregnancy */
        [Range(0,100)]   public int   fertility_factor; // declines with age
        [Range(0,100)]   public int   lining_thickness; // 0‑100 plushness
        public bool      pregnant;
        [Range(0,42)]    public int   gestation_weeks;

        /* misc health */
        [Range(0,100)]   public int   contraction_strength, inflammation, discomfort;
    }



    [Serializable] public class ButtSide { [Range(0, 100)] public int bruised, cut, discomfort, soreness; }

    [Serializable] public class ButtocksStats
    {
        public ButtSide left = new(), right = new();
    }
    
    /* ── polymorphic wrapper ── */
    [Serializable] public abstract class GenitalsBase { }

    [Serializable] public class MaleGenitals : GenitalsBase
    {
        public PenisStats     penis     = new();
        public TesticlesStats testicles = new();
    }

    [Serializable] public class FemaleGenitals : GenitalsBase
    {
        public VulvaStats  vulva  = new();
        public UterusStats uterus = new();
    }

    [Serializable] public class IntersexGenitals : GenitalsBase
    {
        public PenisStats     penis     = new();
        public TesticlesStats testicles = new();
        public VulvaStats     vulva     = new();
        public UterusStats    uterus    = new();
    }


    /* ── actual pelvis data ── */
    public HipsStats hips = new();
    [SerializeReference] public GenitalsBase genitals;   // 👈 one slot, polymorphic
    public ButtocksStats buttocks = new();
}
#endregion

#region Legs ------------------------------------------------------------
[Serializable] public class LegsStats
{
    // ----- reusable bits -----
    [Serializable] public class SkinStretchStats { [Range(0, 100)] public int hydration, smoothness, elasticity, stretch_marks; }
    [Serializable] public class SegmentHair      { [Range(0, 100)] public int bruised, cut, discomfort, soreness; [Range(0, 25)] public int hair_amount;}
    [Serializable] public class JointStats       { [Range(0, 100)] public int bruised, cut, discomfort; public bool fractured; }

    [Serializable] public class FootStats
    {
        [Range(0, 100)] public int bruised, cut, discomfort, soreness;
        public bool fractured;
        [Range(0, 57)] public int size;
    }

    // ---------- toes ----------
    [Serializable] public class ToeStats
    {
        [Range(0,100)] public int bruised, cut, discomfort;
        public bool fractured;
    }

    [Serializable] public class ToesStats
    {
        public ToeStats big    = new();
        public ToeStats index  = new();
        public ToeStats middle = new();
        public ToeStats ring   = new();
        public ToeStats pinky  = new();
    }

    [Serializable] public class LegSide
    {
        public SkinStretchStats skin  = new();
        public SegmentHair      thigh = new();
        public JointStats       knee  = new();
        public SegmentHair      shin  = new();
        public JointStats       ankle = new();
        public FootStats        foot  = new();
        public ToesStats        toes  = new();

        [Range(0, 100)] public int flexibility, stiffness, fatigue, strength, muscle_mass, fat;
    }

    // actual leg data
    public LegSide left = new(), right = new();
}
#endregion

    void Start()
    {
        RefreshHierarchyName();
    }

    void Update()
    {
        
    }
}