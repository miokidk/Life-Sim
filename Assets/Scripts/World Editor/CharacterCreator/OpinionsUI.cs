using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[Serializable] public class OpinionConfirmedEvent : UnityEvent<string,string> {}

public class OpinionsUI : MonoBehaviour
{
    [Header("Model / Services")]
    [SerializeField] private StatsService statsService; // ⇐ assign in scene

    [Header("Top-level UI")]
    [SerializeField] private Button addButton;
    [SerializeField] private GameObject adder;                  // Opinions/Adder
    [SerializeField] private Button confirmButton;

    [Header("Selector parents (where selection buttons spawn)")]
    [SerializeField] private Transform opinionsSelectorContent; // .../Opinions Selector/.../Opinion Selections
    [SerializeField] private Transform conceptsSelectorContent; // .../Concepts  Selector/.../Concept Selections

    [Header("Prefabs (no custom components required)")]
    [SerializeField] private GameObject opinionSelectionPrefab; // prefab has Button + TMP inside
    [SerializeField] private GameObject conceptSelectionPrefab; // prefab has Button + TMP inside
    [SerializeField] private GameObject opinionListItemPrefab;  // row prefab shown in list

    [Header("List container")]
    [SerializeField] private Transform opinionListContent;      // .../Opinion List

    [Header("Events (optional)")]
    public OpinionConfirmedEvent OnOpinionAddedToCharacter;

    // ---- data ----
    private static readonly string[] OPINIONS = {
        "Positive","Curious","Neutral","Indifferent","Negative"
    };

    private static readonly string[] CONCEPTS = {
          "Baths",  "Morning showers",  "Night showers",  "Cold showers",  "Skipping a day of hygiene",  "Handwashing diligence",  "Hand sanitizer use",  "Air-drying hands",  "Paper towels for hands",  "Blow-dryers for hands",  "Oral care intensity",  "Flossing habit",  "Mouthwash strength",  "Whitening treatments",  "Tongue scraping",  "Skincare routines",  "Fragrance-free products",  "Scented products",  "Daily sunscreen",  "Reapplying sunscreen",  "Mineral sunscreen",  "Chemical sunscreen",  "Lip balm habit",  "Body exfoliation",  "Face exfoliation",  "Scalp care",  "Hair washing frequency",  "Protective hairstyles",  "Hair dye and bleach",  "Box dye at home",  "Heat styling tools",  "Air-drying hair",  "Facial hair grooming",  "Beard oils and balms",  "Body hair trimming",  "Waxing",  "Threading",  "Laser hair removal",  "Nail length preference",  "Nail polish",  "Press-on nails",  "Nail art detail",  "Cuticle care",  "Public grooming",  "Deodorant use",  "Natural deodorant",  "Perfume and cologne strength",  "Layering fragrances",  "Makeup for everyday",  "Minimal makeup look",  "Full glam makeup",  "Makeup on men",  "No-makeup pride",  "Hat hair acceptance",  "Sitting posture",  "Feet on furniture",  "Floor sitting",  "Kneeling in public",  "Crouching in public",  "Stretching in public",  "Warm-up before exercise",  "Power walking",  "Leisurely strolling",  "Running indoors",  "Running outdoors",  "Heavy footsteps indoors",  "Parkour in public",  "Escalator etiquette",  "Walking lanes etiquette",  "Push-up form focus",  "Pull-up form focus",  "Assisted reps",  "Ego lifting",  "Treadmill preference",  "Free weights preference",  "Machines preference",  "Grunting in the gym",  "Wiping gym equipment",  "Tracking PRs",  "Posting workout selfies",  "Gym fashion",  "Lifting belts",  "Wrist straps",  "Group fitness classes",  "Solo training",  "Spotting strangers",  "Stretching importance",  "Balance training",  "Mobility work",  "Breathwork practice",  "Humming at work",  "Whistling in public",  "Singing along in stores",  "Constant throat clearing",  "Sniffling vs blowing nose",  "Heavy breathing after stairs",  "Breath-holding games",  "Mouth breathing",  "Sighing loudly",  "Spitting in public",  "Chewing with mouth open",  "Burping etiquette",  "Farting etiquette",  "Toilet seat position norms",  "Courtesy flush habit",  "Phone use in restroom",  "Bidets",  "Toilet paper brands loyalty",  "Shower urination",  "TMI about digestion",  "Carrying tissues",  "Blowing nose at table",  "Eating speed",  "Biting food vs cutting",  "Double dipping",  "Sharing bites",  "Drinking from shared bottles",  "Alcohol at lunch",  "Daily energy drinks",  "Caffeine-free preference",  "Cooking for guests",  "Plating aesthetics",  "Washing produce",  "Handwashing before cooking",  "Meal prep Sundays",  "Leftovers creativity",  "Spice tolerance",  "Microwave meals",  "Cast iron care",  "Air fryer enthusiasm",  "Organic food premium",  "Plant-based eating",  "Mock meats",  "Supplements routine",  "Hydration tracking",  "Fasting for focus",  "Cheat days",  "Sugar-free substitutes",  "Artificial sweeteners",  "Dining alone",  "Tipping generosity",  "Splitting the bill",  "Coupons and deals",  "Loud restaurants",  "Open kitchen restaurants",  "Bar seating",  "Talking volume",  "Profanity tolerance",  "Think-out-loud style",  "Concise speaking",  "Sarcasm and roasting",  "Teasing boundaries",  "Pranks",  "Interrupting",  "Storytelling exaggeration",  "White lies",  "Impersonations",  "Gossip",  "Blunt feedback",  "Cushioned feedback",  "Public praise",  "Private praise",  "Steel-manning in debate",  "Rhetorical tricks",  "Handshake greetings",  "Hug greetings",  "Fist-bump greetings",  "Eye contact",  "Personal space",  "Small talk value",  "Hanging out frequency",  "Ghosting",  "Hosting style",  "Icebreaker games",  "Networking authenticity",  "Exchanging social handles",  "Exchanging phone numbers",  "Mentorship expectations",  "Teaching tone",  "Teamwork roles",  "Crying in public",  "Groaning during effort",  "Laughing volume",  "Laughing at solemn events",  "Blushing reactions",  "Smirking",  "Glaring",  "Winking",  "Eye-rolling",  "Lip biting",  "Brow raising",  "Consoling touch",  "Verbal-only consoling",  "Forgiving quickly",  "Holding boundaries",  "Empathy displays",  "Tough love approach",  "Flirting styles",  "Giving compliments",  "Receiving compliments",  "First-date kissing",  "PDA limits",  "Sexual teasing in public",  "Consent literacy",  "Aftercare as norm",  "Sex on first date",  "Talking about sex life",  "Masturbation talk",  "Privacy expectations in relationships",  "Bragging about conquests",  "Discretion about partners",  "Open relationships",  "Monogamy as default",  "Sharing passwords with partner",  "Location sharing with partner",  "Competitive trash talk",  "Sportsmanship",  "Playing to win",  "Playing for fun",  "Board games",  "Card games",  "Video games",  "Pay-to-win tolerance",  "Loot boxes tolerance",  "House rules",  "Cheating as a joke",  "Kids’ games as adults",  "Co-op patience for new players",  "Stream sniping ethics",  "Speedrunning culture",  "Modding games",  "Drawing skill flex",  "It’s just for me art",  "Tracing and references",  "AI-assisted art",  "Fanart etiquette",  "Watermarking art",  "Sharing WIPs",  "Daily practice",  "Only create when inspired",  "Public speaking nerves",  "Using notes on stage",  "Memorized talks",  "Comedic roast limits",  "Voice drills in shared spaces",  "Accent pride",  "Dialect pride",  "Mocking accents",  "Decisive style",  "Deliberative style",  "Speed over polish",  "Thoroughness over speed",  "Planning tools",  "Calendar blocking",  "Punctuality norms",  "Over-optimization",  "Analysis paralysis",  "Sleeping on decisions",  "Anchoring in negotiation",  "Strategic silence",  "Win–win mindset",  "Hardball tactics",  "De-escalation attempts",  "Apologizing styles",  "Accepting apologies",  "Conflict in public",  "Conflict in private",  "Mediation",  "Compromise lines",  "Rallying people",  "Hype leadership",  "Speeding tolerance",  "Lane discipline",  "Turn signal timing",  "Honking",  "Parking etiquette",  "Music volume in car",  "Back-seat driving",  "GPS reliance",  "Wayfinding by memory",  "Scenic detours",  "Efficiency routes",  "Texting while driving",  "Hands-free calls while driving",  "Road rage reactions",  "Dashcams",  "Cruise control",  "Zipper merging",  "Rideshare chat with drivers",  "Public transit small talk",  "Babysitting standards",  "Elder care patience",  "It’s not my job debates",  "Comforting physically",  "Comforting verbally",  "Bringing food to mourners",  "Attending ceremonies",  "Hair texture care at work",  "Protective styles at work",  "Hats indoors",  "Hoods indoors",  "Nail art in professional settings",  "Visible tattoos at work",  "Piercings at work",  "Lipstick on men",  "Clothing comfort vs formality",  "Shoes indoors",  "Office fragrances",  "Messy bun in public",  "Lip smacking habit",  "Jumping right in vs warming up",  "Cold exposure",  "Bundling up",  "Covering ears in crowds",  "Covering eyes in crowds",  "Covering mouth in crowds",  "Masks when sick",  "Home COVID tests",  "Thermometer checking",  "Step counting",  "Stretch breaks",  "Standing desks",  "Under-desk treadmills",  "Blue light glasses",  "Screen time limits",  "Read receipts on",  "Typing indicators",  "Voice notes",  "Long voice notes",  "Double texting",  "Group chat muting",  "Camera on in video calls",  "Virtual backgrounds",  "Emojis in work chats",  "GIFs in work chats",  "Meeting reaction buttons",  "Taking calls on speaker",  "Public AirPods wearing",  "Noise-canceling always on",  "Always-on display phones",  "Face unlock",  "Fingerprint unlock",  "Ad blockers",  "In-app tracking",  "Privacy-focused browsers",  "Password managers",  "Two-factor authentication",  "Auto-updates on devices",  "Beta software use",  "Smart home devices",  "Always listening assistants",  "Home security cameras",  "Doorbell cameras",  "Dash notifications",  "Fitness wearables",  "Sharing fitness rings",  "Posting streaks",  "Minimalist wallets",  "Cashless lifestyle",  "Credit card points gaming",  "Buy now, pay later",  "Budgeting apps",  "Spontaneous big purchases",  "Price matching",  "Haggling in stores",  "Returns without receipt",  "Thrifting",  "Fast fashion",  "Tailoring clothes",  "Signature outfit",  "Seasonal color palettes",  "Capsule wardrobe",  "Sneaker collecting",  "Heels at work",  "Sandals at work",  "Crocs in public",  "Socks with sandals",  "Barefoot at home",  "House slippers",  "Indoor-only clothes",  "Pajamas outside",  "Making the bed daily",  "Top sheet usage",  "Weighted blankets",  "Scented candles at home",  "Incense at home",  "Essential oil diffusers",  "Open windows for airflow",  "Thermostat temperature",  "Shoes-off households",  "Dishwasher loading style",  "Handwashing dishes",  "Soaking dishes overnight",  "Laundry frequency",  "Fabric softener use",  "Line drying clothes",  "Folding method",  "Closet organization",  "Junk drawer acceptance",  "Minimalist decor",  "Maximalist decor",  "Houseplants",  "Artificial plants",  "Pet ownership",  "Pets on furniture",  "Pet clothing",  "Leash training cats",  "Outdoor cats",  "Dog parks",  "Picking up after pets",  "Composting",  "Recycling diligence",  "Reusable bags",  "Reusable bottles",  "Single-use plastics",  "Long showers and water use",  "Short showers for conservation",  "Driving less for climate",  "Carbon offsets",  "Thrift over new",  "Secondhand electronics",  "eBike commuting",  "Public transit over driving",  "Carpooling",  "Working from home",  "Going to the office",  "Hybrid schedules",  "Focus music at work",  "Classical music for focus",  "Lo-fi beats for focus",  "White noise for focus",  "Open office plans",  "Noise-canceling at work",  "Heads-down time blocks",  "Calendar visibility",  "Meeting-free days",  "Status updates frequency",  "Daily standups",  "Async communication",  "Reply-all culture",  "Email after hours",  "Do Not Disturb",  "Weekend work",  "Side hustles",  "Sabbath or rest day",  "Vacation fully unplugged",  "Staycations",  "Solo travel",  "Group travel",  "Hostels",  "Luxury hotels",  "Early flights",  "Red-eye flights",  "Airport lounges",  "Travel credit cards",  "Seat reclining on planes",  "Armrest sharing",  "Carry-on only",  "Checking bags",  "Boarding early",  "Clapping when plane lands",  "Tipping flight attendants",  "Listening to podcasts",  "Audiobooks habit",  "Reading eBooks",  "Physical books",  "Library use",  "Book annotations",  "Spoilers tolerance",  "Binge-watching",  "Weekly releases",  "Watching with subtitles",  "Dubbing tolerance",  "Playback speed changes",  "Trailer watching",  "Review aggregators trust",  "Creator merch",  "Commenting online",  "Lurking online",  "Blocking liberally",  "Muting liberally",  "Quote-tweeting drama",  "Long-form posts",  "Short-form reels",  "Daily journaling",  "Gratitude lists",  "Meditation habit",  "Prayer habit",  "Yoga at home",  "Therapy",  "Self-help books",  "Mood tracking",  "Morning routines",  "Night routines",  "Naps",  "Caffeine naps",  "Cold plunges",  "Saunas",  "Massage therapy",  "Chiropractic care",  "Acupuncture",  "Energy healing",  "Horoscopes",  "Breathable bedding",  "Blackout curtains",  "White noise at night",  "Phone-free bedroom",  "Sleep masks",  "Weighted sleep masks"
    };



    // ---- state ----
    private Button _selectedOpinionBtn;
    private string _selectedOpinion;
    private Button _selectedConceptBtn;
    private string _selectedConcept;
    private readonly HashSet<string> _existingPairs = new();
    private bool _isAdderOpen = false;

    void Awake()
    {
        if (adder) adder.SetActive(false);
        if (confirmButton) confirmButton.interactable = false;

        if (addButton) addButton.onClick.AddListener(OpenAdder);
        if (confirmButton) confirmButton.onClick.AddListener(ConfirmSelection);
    }

    void Update()
    {
        // Check for click outside adder when it's open
        if (_isAdderOpen && Input.GetMouseButtonDown(0))
        {
            // Check if we clicked on a UI element
            if (EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
            {
                // Clicked outside all UI
                CloseAdder();
            }
            else if (adder && adder.activeSelf)
            {
                // Check if click was outside the adder panel
                PointerEventData pointerData = new PointerEventData(EventSystem.current)
                {
                    position = Input.mousePosition
                };

                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);

                bool clickedOnAdder = false;
                bool clickedOnAddButton = false;

                foreach (var result in results)
                {
                    if (result.gameObject == adder || result.gameObject.transform.IsChildOf(adder.transform))
                    {
                        clickedOnAdder = true;
                        break;
                    }
                    if (addButton && (result.gameObject == addButton.gameObject ||
                        result.gameObject.transform.IsChildOf(addButton.transform)))
                    {
                        clickedOnAddButton = true;
                        break;
                    }
                }

                // Close if clicked outside adder and not on the add button
                if (!clickedOnAdder && !clickedOnAddButton)
                {
                    CloseAdder();
                }
            }
        }
    }

    void OnEnable()
    {
        if (!statsService) statsService = FindObjectOfType<StatsService>();
        StatsService.StatsRecomputed += RefreshFromModel;
        RefreshFromModel();
    }

    void OnDisable()
    {
        StatsService.StatsRecomputed -= RefreshFromModel;
    }

    public void RefreshFromModel()
    {
        // reset UI state
        _selectedOpinionBtn = null;
        _selectedConceptBtn = null;
        _selectedOpinion = null;
        _selectedConcept = null;
        if (confirmButton) confirmButton.interactable = false;

        _existingPairs.Clear();
        ClearChildren(opinionListContent);

        if (!statsService) return;

        var list = (List<characterStats.Opinion>)statsService.GetValue("opinions.opinions");
        if (list == null) return;

        foreach (var o in list)
            AddRow(o.value.ToString(), o.topic);
    }

    // Extract row creation so both RefreshFromModel and ConfirmSelection use it.
    void AddRow(string opinionLabel, string conceptLabel)
    {
        string key = $"{opinionLabel}::{conceptLabel}";
        if (_existingPairs.Contains(key)) return;

        var row = Instantiate(opinionListItemPrefab, opinionListContent).transform;

        var opText = FindTMPByName(row, new[] { "Opinion", "Opinion Button", "Title" });
        var coText = FindTMPByName(row, new[] { "Concept", "Concepts" });
        var remove = FindButtonByName(row, new[] { "Remove", "Delete", "X" });

        if (opText) opText.text = opinionLabel;
        if (coText) coText.text = conceptLabel;

        if (remove)
        {
            remove.onClick.RemoveAllListeners();
            remove.onClick.AddListener(() =>
            {
                RemoveFromModel(opinionLabel, conceptLabel);
                _existingPairs.Remove(key);
                Destroy(row.gameObject);
            });
        }

        _existingPairs.Add(key);
    }

    // ================= Adder =================
    void OpenAdder()
    {
        if (!Application.isPlaying) return;   // <— important

        ClearChildren(opinionsSelectorContent);
        ClearChildren(conceptsSelectorContent);

        foreach (var op in OPINIONS)
        {
            var go = Instantiate(opinionSelectionPrefab, opinionsSelectorContent); // ← no PrefabUtility
            var btn = go.GetComponent<Button>();
            SetAnyLabel(go.transform, op);
            btn.onClick.AddListener(() => SelectOpinion(btn, op));
            SetPressedVisual(btn, false);
        }

        foreach (var co in CONCEPTS)
        {
            var go = Instantiate(conceptSelectionPrefab, conceptsSelectorContent); // ← no PrefabUtility
            var btn = go.GetComponent<Button>();
            SetAnyLabel(go.transform, co);
            btn.onClick.AddListener(() => SelectConcept(btn, co));
            SetPressedVisual(btn, false);
        }

        adder.SetActive(true);
        _isAdderOpen = true;
    }

    void CloseAdder()
    {
        if (adder)
        {
            adder.SetActive(false);
            _isAdderOpen = false;
        }
    }

    // ================= Selection logic =================
    void SelectOpinion(Button btn, string value)
    {
        if (_selectedOpinionBtn && _selectedOpinionBtn != btn)
            SetPressedVisual(_selectedOpinionBtn, false);

        _selectedOpinionBtn = btn;
        _selectedOpinion = value;
        SetPressedVisual(btn, true);
        UpdateConfirmState();
    }

    void SelectConcept(Button btn, string value)
    {
        if (_selectedConceptBtn && _selectedConceptBtn != btn)
            SetPressedVisual(_selectedConceptBtn, false);

        _selectedConceptBtn = btn;
        _selectedConcept = value;
        SetPressedVisual(btn, true);
        UpdateConfirmState();
    }

    void UpdateConfirmState()
    {
        if (confirmButton)
            confirmButton.interactable = !string.IsNullOrEmpty(_selectedOpinion)
                                      && !string.IsNullOrEmpty(_selectedConcept);
    }

    // ================= Confirm -> add to UI + MODEL =================
    void ConfirmSelection()
    {
        if (!Application.isPlaying) return;

        if (string.IsNullOrEmpty(_selectedOpinion) || string.IsNullOrEmpty(_selectedConcept)) return;

        var key = $"{_selectedOpinion}::{_selectedConcept}";
        if (_existingPairs.Contains(key))
        {
            CloseAdder();
            return;
        }

        // The event fired by EndEdit() will handle all UI updates.
        if (statsService)
        {
            var before = (List<characterStats.Opinion>)statsService.GetValue("opinions.opinions");
            var beforeCopy = before != null ? new List<characterStats.Opinion>(before) : new List<characterStats.Opinion>();

            var afterCopy = new List<characterStats.Opinion>(beforeCopy);
            afterCopy.Add(new characterStats.Opinion
            {
                topic = _selectedConcept,
                value = ParseOpinionEnum(_selectedOpinion)
            });

            ChangeBuffer.BeginEdit(statsService, $"Add Opinion: {_selectedOpinion} about {_selectedConcept}");
            ChangeBuffer.Record("opinions.opinions", beforeCopy, afterCopy);
            ChangeBuffer.EndEdit();
        }
        
        // All manual UI creation has been removed.

        OnOpinionAddedToCharacter?.Invoke(_selectedOpinion, _selectedConcept);
        CloseAdder();
    }

    void RemoveFromModel(string opinionStr, string conceptStr)
    {
        if (!statsService) return;

        var before = (List<characterStats.Opinion>)statsService.GetValue("opinions.opinions");
        var beforeCopy = before != null ? new List<characterStats.Opinion>(before) : new List<characterStats.Opinion>();
        var afterCopy = new List<characterStats.Opinion>(beforeCopy);

        var val = ParseOpinionEnum(opinionStr);
        int idx = afterCopy.FindIndex(o =>
            string.Equals(o.topic, conceptStr, StringComparison.OrdinalIgnoreCase) &&
            o.value == val);

        if (idx >= 0)
        {
            afterCopy.RemoveAt(idx);
            ChangeBuffer.BeginEdit(statsService, $"Remove Opinion: {opinionStr} about {conceptStr}");
            ChangeBuffer.Record("opinions.opinions", beforeCopy, afterCopy);
            ChangeBuffer.EndEdit();
        }
    }

    static characterStats.OpinionValue ParseOpinionEnum(string s) =>
        (characterStats.OpinionValue)Enum.Parse(typeof(characterStats.OpinionValue), s, true);

    // ================= Visual helpers =================
    static void SetPressedVisual(Button btn, bool pressed)
    {
        btn.interactable = !pressed; // simple selected-state
        var hi = btn.transform.Find("Selected");
        if (hi) hi.gameObject.SetActive(pressed);
    }

    // ================= Utils =================
    static void ClearChildren(Transform t){
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    static T Require<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (!c) throw new Exception($"Required component {typeof(T).Name} missing on {go.name}");
        return c;
    }

    static void SetAnyLabel(Transform root, string value)
    {
        var txt = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
        if (txt) txt.text = value;
    }

    static TMP_Text FindTMPByName(Transform root, string[] nameHints)
    {
        var all = root.GetComponentsInChildren<TMP_Text>(true);
        foreach (var hint in nameHints)
        {
            var m = all.FirstOrDefault(t => t.transform.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
            if (m) return m;
        }
        return all.FirstOrDefault();
    }

    static Button FindButtonByName(Transform root, string[] nameHints)
    {
        var all = root.GetComponentsInChildren<Button>(true);
        foreach (var hint in nameHints)
        {
            var m = all.FirstOrDefault(t => t.transform.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
            if (m) return m;
        }
        return all.FirstOrDefault();
    }
}