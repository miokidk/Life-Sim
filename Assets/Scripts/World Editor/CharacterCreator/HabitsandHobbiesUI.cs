using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class HabitsAndHobbiesUI : MonoBehaviour
{
    [SerializeField] private StatsService stats;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Dropdown adherenceDropdown;

    [SerializeField] private TMP_Dropdown everyAmountDD;
    [SerializeField] private TMP_Dropdown everyUnitDD;
    [SerializeField] private TMP_Dropdown timesAmountDD;
    [SerializeField] private TMP_Dropdown timesUnitDD;

    [SerializeField] private Transform listRoot;      // "Habits and Hobbies List"
    [SerializeField] private GameObject hhRowPrefab;  // "H _ H" prefab

    [Header("Scene refs")]
    [SerializeField] private Button addButton;          // Add Button
    [SerializeField] private GameObject adderRoot;      // H & H Adder
    [SerializeField] private TMP_Dropdown nameDropdown; // H|H Dropdown
    [SerializeField] private TMP_Dropdown typeDropdown; // H|H Type Dropdown

    [Header("Triggers UI")]
    [SerializeField] private Button triggersButton;        // "Triggers Button"
    [SerializeField] private GameObject triggersScrollView; // "Triggers Scroll View"
    [SerializeField] private Button triggersCloseButton;   // "Close Button"
    [SerializeField] private RectTransform triggersList;
    [SerializeField] private Toggle triggerTogglePrefab;

    [Header("Frequency UI")]
    [SerializeField] private TMP_Dropdown frequencyTypeSelector;       // "Frequency Type Selector"
    [SerializeField] private GameObject everyAmountSelector;           // "Every Amount Selector"
    [SerializeField] private GameObject everyFrequencyUnitSelector;    // "Every Frequency Unit Selector"
    [SerializeField] private GameObject timesPerAmountSelector;        // "Times per Amount Selector"
    [SerializeField] private GameObject timesPerFrequencyUnitSelector; // "Times per Frequency Unit Selector"
    [Header("Session Length")]
    [SerializeField] private TMP_Dropdown sessionLengthDropdown;

    [SerializeField] private RectTransform adderPanel;

    Camera uiCam;


    // ---- Types (Habits + Hobbies) ----
    private static readonly string[] HabitTypes = {
        "Daily self-care & hygiene","Bodily routines","Breathing & voice care",
        "Movement & posture","Fitness routine","Manual/utility habits",
        "Reading/writing & thinking","Personal maintenance & appearance",
        "Tics & micro-expressions","Commuting & errands",
        "Sexual/private (adult)","Greetings & small talk","Manners & prosocial",
        "Coordination","Networking & community","Recurring social patterns"
    };

    private static readonly string[] HobbyTypes = {
        "Creative","Music & movement","Games","Sports & outdoor","Fitness as pastime",
        "Food & home","Mindful & reflective","Collecting/consumer","Travel & motoring",
        "Group play & performance","Celebration & leisure","Clubs & discourse",
        "Community & causes","Story & comedy","Traveling together"
    };

    // ---- Map: Type -> Names (both Habits + Hobbies) ----
    // HABITS
    private static readonly Dictionary<string, List<string>> TypeToNames =
        new Dictionary<string, List<string>> {
        { "Daily self-care & hygiene", new() {
            "Shower","Bathe","Wash up","Wash face","Wash hands","Wash hair",
            "Brush teeth","Floss","Rinse mouth","Cut nails","Paint nails","Apply nails","Apply makeup"
        }},
        { "Bodily routines", new() {
            "Sleep","Eat","Drink","Urinate","Defecate","Blow nose","Cough","Sneeze",
            "Yawn","Sniffle","Burp","Fart"
        }},
        { "Breathing & voice care", new() {
            "Inhale","Exhale","Hold breath (breathwork)","Clear throat","Vocal exercises"
        }},
        { "Movement & posture", new() {
            "Sit","Sit on","Stand","Adjust self in seat","Stretch","Stroll","Walk","Jog",
            "Run (light)","Tie shoe","Warm up","Wrap up"
        }},
        { "Fitness routine", new() {
            "Exercise","Pushups","Sit-ups","Pull-ups","Squats","Curls","Bench press",
            "Treadmill (walk)","Treadmill (jog)","Treadmill (run)"
        }},
        { "Manual/utility habits", new() {
            "Open","Close","Carry","Place","Move","Lift","Pull","Push","Throw","Drop","Touch",
            "Wash","Rinse","Wipe","Cut (food/tasks)","Pour","Serve","Use"
        }},
        { "Reading/writing & thinking", new() {
            "Read","Writing (journaling/notes)","Think out loud","Ponder","Decide","Deliberate",
            "Consider","Check in with emotions","Daydream","Imagine","Notice","Appreciate"
        }},
        { "Personal maintenance & appearance", new() {
            "Bite (eating)","Chew (eating)","Warm (thermoregulation)","Wrap (thermoregulation)","Freeze (thermoregulation)"
        }},
        { "Tics & micro-expressions (recurring)", new() {
            "Bite nails","Bite lip","Purse lips","Roll eyes","Squint","Raise eyebrows","Furrow eyebrows",
            "Scrunch nose","Smirk","Frown","Grin","Smile","Wink"
        }},
        { "Commuting & errands", new() {
            "Drive","Travel","Check mail","Buy (shopping routine)","Browse"
        }},
        { "Sexual/private (adult)", new() { "Masturbate" }},
        { "Greetings & small talk", new() {
            "Greet","Smile (social)","Nod","Wave","Introduce","Chat","Hang out","Joke around"
        }},
        { "Manners & prosocial", new() {
            "Compliment","Thank","Apologize","Share stories","Listen","Comfort","Console",
            "Encourage","Support","Congratulate"
        }},
        { "Coordination", new() { "Make plans","Discuss","Participate","Hold meeting" }},
        { "Networking & community", new() { "Bond","Connect","Network","Visit","Host","Volunteer" }},
        { "Recurring social patterns (good or bad)", new() {
            "Gossip","Argue","Debate","Tease","Praise","Criticize","Scold","Ignore",
            "Ghost","Lie","Interrupt","Start a rumor","Spread news"
        }},

        // HOBBIES
        { "Creative", new() { "Writing (creative)","Draw","Paint" }},
        { "Music & movement", new() {
            "Sing","Rap","Hum","Whistle","Vocal exercises","Dance"
        }},
        { "Games", new() {
            "PC game","Console game","Mobile game","Board game","Cards","Toss","Tag","Hide and seek"
        }},
        { "Sports & outdoor", new() {
            "Basketball","Soccer","Run/Sprint","Walk/Stroll (hiking)","Climb onto","Climb down",
            "Jump","Jump over","Aim/Shoot (target practice)"
        }},
        { "Fitness as pastime", new() {
            "Strength training","Pushups","Sit-ups","Pull-ups","Squats","Curls","Bench press",
            "Treadmill sessions","Stretching"
        }},
        { "Food & home", new() { "Cook","Pour/Serve (mixology/hosting)" }},
        { "Mindful & reflective", new() {
            "Meditate","Daydream","Imagine","Ponder","Check in with emotions","Read for pleasure"
        }},
        { "Collecting/consumer", new() { "Browse (collecting)","Buy (thrifting/collecting)" }},
        { "Travel & motoring", new() { "Travel (leisure)","Drive/Ride (road-tripping)" }},
        { "Group play & performance", new() {
            "Play games","Play sports","Dance together","Sing together","Perform"
        }},
        { "Celebration & leisure", new() {
            "Celebrate","Party","Eat together","Drink together","Toast","Host","Visit","Share interests/hobbies"
        }},
        { "Clubs & discourse", new() { "Debate (club)","Discuss (club)","Give presentation","Pitch ideas" }},
        { "Community & causes", new() { "Volunteer (cause)","Rally/Protest","Participate (events)","Cheer","Compete" }},
        { "Story & comedy", new() { "Share stories (group)","Reminisce","Roast","Prank","Serenade" }},
        { "Traveling together", new() { "Travel together","Reunite","Form group/alliances","Network (social)" }},
    };

    void Awake()
    {
        if (adderRoot) adderRoot.SetActive(false);
        if (addButton) addButton.onClick.AddListener(OpenAdder);

        var c = GetComponentInParent<Canvas>();
        if (c && c.renderMode != RenderMode.ScreenSpaceOverlay) uiCam = c.worldCamera;

        // Build type dropdown (Habits first, then Hobbies)
        var allTypes = HabitTypes.Concat(HobbyTypes).ToList();
        FillDropdown(typeDropdown, allTypes, "Select type");
        typeDropdown.onValueChanged.AddListener(OnTypeChanged);

        // Names disabled until type chosen
        FillDropdown(nameDropdown, new List<string>(), "Select habit or hobby");
        nameDropdown.interactable = false;

        if (triggersScrollView) triggersScrollView.SetActive(false);
        if (triggersButton) triggersButton.onClick.AddListener(OpenTriggers);
        if (triggersCloseButton) triggersCloseButton.onClick.AddListener(CloseTriggers);

        EnsureTriggersPopulated();

        if (frequencyTypeSelector)
            frequencyTypeSelector.onValueChanged.AddListener(OnFrequencyTypeChanged);

        ApplyFrequencyTypeUI();

        if (nameDropdown) nameDropdown.onValueChanged.AddListener(OnNameChanged);

        ResetSessionLengthDropdown();

        if (confirmButton) confirmButton.onClick.AddListener(OnConfirm);
        HookFormValidation();

    }

    void Update()
    {
        if (!adderRoot || !adderRoot.activeSelf) return;

        if (Input.GetMouseButtonDown(0) && ClickedOutside(Input.mousePosition))
            adderRoot.SetActive(false);

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            if (ClickedOutside(Input.GetTouch(0).position))
                adderRoot.SetActive(false);
    }

    void OnEnable()
    {
        if (!stats) stats = GetComponentInParent<StatsService>() ?? FindObjectOfType<StatsService>();
        StatsService.StatsRecomputed += RefreshFromModel;
        RefreshFromModel(); // initial populate
    }

    void OnDisable()
    {
        StatsService.StatsRecomputed -= RefreshFromModel;
    }

    void RefreshFromModel()
    {
        if (stats == null || listRoot == null || hhRowPrefab == null) return;
        var model = stats.Model;
        if (model == null || model.habitsAndHobbies == null) return;

        // clear current rows
        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        // rebuild from the model
        foreach (var e in model.habitsAndHobbies.list)
        {
            var row = Instantiate(hhRowPrefab, listRoot);
            PopulateRow(row, e);
            WireRowRemove(row, e);
        }
    }


    void OnDestroy()
    {
        if (addButton) addButton.onClick.RemoveListener(OpenAdder);
        if (typeDropdown) typeDropdown.onValueChanged.RemoveListener(OnTypeChanged);
        if (triggersButton) triggersButton.onClick.RemoveListener(OpenTriggers);
        if (triggersCloseButton) triggersCloseButton.onClick.RemoveListener(CloseTriggers);
        if (frequencyTypeSelector) frequencyTypeSelector.onValueChanged.RemoveListener(OnFrequencyTypeChanged);
        if (nameDropdown) nameDropdown.onValueChanged.RemoveListener(OnNameChanged);
    }

    void OpenAdder()
    {
        if (!adderRoot) return;
        adderRoot.transform.SetAsLastSibling();
        adderRoot.SetActive(true);
    }

    void OnTypeChanged(int idx)
    {
        if (idx <= 0)
        { // placeholder
            FillDropdown(nameDropdown, new List<string>(), "Select habit or hobby");
            nameDropdown.interactable = false;
            ResetSessionLengthDropdown();
            return;
        }

        string selectedType = typeDropdown.options[idx].text;
        if (!TypeToNames.TryGetValue(selectedType, out var names)) names = new List<string>();

        // Dedup + sort for cleanliness
        var clean = names.Where(s => !string.IsNullOrWhiteSpace(s))
                         .Select(s => s.Trim())
                         .Distinct()
                         .OrderBy(s => s)
                         .ToList();

        FillDropdown(nameDropdown, clean, "Select habit or hobby");
        nameDropdown.interactable = true;

        ResetSessionLengthDropdown();
        ValidateConfirm();


    }

    static void FillDropdown(TMP_Dropdown dd, List<string> options, string placeholder)
    {
        if (!dd) return;
        dd.ClearOptions();
        var list = options.Select(o => new TMP_Dropdown.OptionData(o)).ToList();
        list.Insert(0, new TMP_Dropdown.OptionData(placeholder));
        dd.AddOptions(list);
        dd.value = 0;
        dd.RefreshShownValue();
    }

    private bool _triggersBuilt;

    private static readonly string[] TriggerNames = new[]
    {
        "on wake","after a nap","before bed","at bedtime","before a meal","after a meal",
        "at breakfast time","at lunch time","at dinner time","on work start","on work end",
        "on class start","on class end","on break start","on break end","on daily reset",
        "on weekend start","on a holiday","at sunrise","at sunset","on arrival anywhere",
        "on leaving anywhere","on entering a vehicle","on exiting a vehicle","on drive start",
        "on drive end","on alarm","on timer done","on calendar reminder","on notification check",
        "on device unlock","when headphones go on","when headphones come off","when music starts",
        "when music stops","when charging starts","when charging ends","when it starts raining",
        "when it's hot out","when it's cold out","when it's windy","in bright sunlight",
        "when allergies are high","when it's dusty","when hungry","when thirsty","when sleepy",
        "when fatigued","when stressed","when anxious","when calm","when someone approaches",
        "on eye contact","on hearing a greeting","when your name is called","when a group forms",
        "on meeting start","when inspired","when bored","when in a low mood","when in a good mood",
        "when a decision is needed","after a conflict","after praise","after criticism","when idle",
        "after toilet","after shower","after a bath","when hands feel dirty",
        "after touching public surfaces","after handling trash","after handling money",
        "after handling an animal","after handling raw food","when face feels oily",
        "when scalp feels oily","when you notice a makeup smudge","when hair is in your face",
        "when breath smells bad","when food is stuck in teeth","when a nail chips","when you're sweaty",
        "when clothes are muddy or stained","when you look in a mirror","before a big event",
        "before a date","before a meeting or presentation","when bladder feels full",
        "when bowels feel full","when your nose is runny","when you feel congested",
        "when your throat has phlegm","when you need to cough","when you need to sneeze",
        "with morning grogginess","with morning voice","before a call","before a performance",
        "before a rehearsal","after long talking or singing","after sitting too long",
        "when your back feels tight","when muscles feel stiff","when a shoelace comes loose",
        "when you join a queue","when you take a seat","when a workout is scheduled",
        "on arriving at the gym","when your training partner arrives","on cardio day","on strength day",
        "when your step count is low","when a spill happens","when you notice a mess",
        "when the trash is full","when supplies run low","when starting to cook",
        "when meal prep is needed","when guests arrive (kitchen)","after handling raw ingredients",
        "when a story idea appears","when you see a useful reference","at a scenic spot",
        "after therapy","after a workout","after an event","when a deadline is near",
        "on a sale notification","on payday","on arriving at a store, market, or flea",
        "on arriving home","on arriving at work","on arriving at the gym","on arriving at a park or trail",
        "on arriving at a court or field","on arriving at a restroom","on arriving in the kitchen",
        "when a guest arrives","on arriving at an event","when you hear good news",
        "when you hear bad news","on hearing a sneeze","on hearing a cough","during an awkward silence",
        "when breaking news hits","when a club session starts","when friends come online",
        "when a reunion is scheduled","on a holiday gathering","on match start","on race start",
        "on scrim start","when the DJ set starts","when you need grounding",
        "when heart rate calms after a spike","after a tense conversation","when you have privacy",
        "when arousal cues appear"
    };

    // call from your existing OpenTriggers()
    void EnsureTriggersPopulated()
    {
        var list = GetAdderTriggersList();
        var closeBtn = GetAdderTriggersCloseButton();
        if (_triggersBuilt || !list || !triggerTogglePrefab) return;

        for (int i = list.childCount - 1; i >= 0; i--)
        {
            var child = list.GetChild(i);
            if (closeBtn && child == closeBtn.transform) continue;
            if (child.GetComponent<Toggle>() != null) Destroy(child.gameObject);
        }

        int insertIndex = closeBtn ? closeBtn.transform.GetSiblingIndex() + 1 : 0;

        foreach (var label in TriggerNames)
        {
            var t = Instantiate(triggerTogglePrefab, list);
            t.transform.SetSiblingIndex(insertIndex++);
            t.gameObject.name = $"Trigger - {label}";
            t.isOn = false;

            var tmp = t.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp) tmp.text = label;
            else
            {
                var legacy = t.GetComponentInChildren<Text>(true);
                if (legacy) legacy.text = label;
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        _triggersBuilt = true;

        var sv = GetAdderTriggersScrollView();
        if (sv) sv.SetActive(false);
    }

    void OpenTriggers()
    {
        EnsureTriggersPopulated();
        var sv = GetAdderTriggersScrollView();
        if (!sv) return;
        sv.transform.SetAsLastSibling();
        sv.SetActive(true);
    }

    void CloseTriggers()
    {
        var sv = GetAdderTriggersScrollView();
        if (sv) sv.SetActive(false);
    }

    private void OnFrequencyTypeChanged(int _)
    {
        ApplyFrequencyTypeUI();
    }

    private void ApplyFrequencyTypeUI()
    {
        if (!frequencyTypeSelector) return;

        // read current text ("Every" or "Time per")
        var label = frequencyTypeSelector.options[frequencyTypeSelector.value].text
            .Trim().ToLowerInvariant();

        bool isEvery = label.StartsWith("every");
        bool isTimesPer = label.StartsWith("time per") || label.StartsWith("times per");

        // default: hide all if unknown/placeholder
        bool showEveryGroup = isEvery;
        bool showTimesPerGroup = isTimesPer;

        if (everyAmountSelector) everyAmountSelector.SetActive(showEveryGroup);
        if (everyFrequencyUnitSelector) everyFrequencyUnitSelector.SetActive(showEveryGroup);

        if (timesPerAmountSelector) timesPerAmountSelector.SetActive(showTimesPerGroup);
        if (timesPerFrequencyUnitSelector) timesPerFrequencyUnitSelector.SetActive(showTimesPerGroup);
    }

    // Populates session lengths for the currently selected Name
    private void OnNameChanged(int idx)
    {
        if (idx <= 0) { ResetSessionLengthDropdown(); return; }

        string selected = nameDropdown.options[idx].text;
        if (NameToSessionLengths.TryGetValue(selected, out var lengths))
        {
            var opts = lengths.Distinct().ToList();
            FillDropdown(sessionLengthDropdown, opts, "Select session length");
            sessionLengthDropdown.interactable = true;

            if (opts.Count == 1)                      // <— auto-pick single choice
            {
                sessionLengthDropdown.value = 1;      // index 0 is the placeholder
                sessionLengthDropdown.RefreshShownValue();
            }

            ValidateConfirm();
        }
        else
        {
            ResetSessionLengthDropdown();
        }
    }
    private void ResetSessionLengthDropdown()
    {
        FillDropdown(sessionLengthDropdown, new System.Collections.Generic.List<string>(), "Select session length");
        if (sessionLengthDropdown) sessionLengthDropdown.interactable = false;
        ValidateConfirm();


    }

    #region SessionLength

    private static readonly System.Collections.Generic.Dictionary<string, string[]> NameToSessionLengths =
    new System.Collections.Generic.Dictionary<string, string[]>
    {
        // --- Daily self-care & hygiene ---
        ["Shower"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Bathe"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Wash up"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Wash face"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Wash hands"] = new[] { "1–2 minutes" },
        ["Wash hair"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Brush teeth"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Floss"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Rinse mouth"] = new[] { "1–2 minutes" },
        ["Cut nails"] = new[] { "5-10 minutes", "10–20 minutes" },
        ["Paint nails"] = new[] { "30–60 minutes", "60–90 minutes", "Until complete" },
        ["Apply nails"] = new[] { "30–60 minutes", "60–90 minutes", "Until complete" },
        ["Apply makeup"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },

        // --- Bodily routines ---
        ["Sleep"] = new[] { "Until complete" },
        ["Eat"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Drink"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Urinate"] = new[] { "Until complete" },
        ["Defecate"] = new[] { "Until complete" },
        ["Blow nose"] = new[] { "Until complete" },
        ["Cough"] = new[] { "Until complete" },
        ["Sneeze"] = new[] { "Until complete" },
        ["Yawn"] = new[] { "Until complete" },
        ["Sniffle"] = new[] { "Until complete" },
        ["Burp"] = new[] { "Until complete" },
        ["Fart"] = new[] { "Until complete" },

        // --- Breathing & voice care ---
        ["Inhale"] = new[] { "Until complete" },
        ["Exhale"] = new[] { "Until complete" },
        ["Hold breath (breathwork)"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Clear throat"] = new[] { "Until complete" },
        ["Vocal exercises"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Movement & posture ---
        ["Sit"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Sit on"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Stand"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Adjust self in seat"] = new[] { "Until complete" },
        ["Stretch"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Stroll"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Walk"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Jog"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Run (light)"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Tie shoe"] = new[] { "Until complete" },
        ["Warm up"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Wrap up"] = new[] { "5–10 minutes", "10–20 minutes" },

        // --- Fitness routine ---
        ["Exercise"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Pushups"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Sit-ups"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Pull-ups"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Squats"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Curls"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Bench press"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Treadmill (walk)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Treadmill (jog)"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Treadmill (run)"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Manual/utility habits ---
        ["Open"] = new[] { "Until complete" },
        ["Close"] = new[] { "Until complete" },
        ["Carry"] = new[] { "Until complete" },
        ["Place"] = new[] { "Until complete" },
        ["Move"] = new[] { "Until complete" },
        ["Lift"] = new[] { "Until complete" },
        ["Pull"] = new[] { "Until complete" },
        ["Push"] = new[] { "Until complete" },
        ["Throw"] = new[] { "Until complete" },
        ["Drop"] = new[] { "Until complete" },
        ["Touch"] = new[] { "Until complete" },
        ["Wash"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Rinse"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Wipe"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Cut (food/tasks)"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Pour"] = new[] { "Until complete" },
        ["Serve"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Use"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Reading/writing & thinking ---
        ["Read"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Writing (journaling/notes)"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Think out loud"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Ponder"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Decide"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Deliberate"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Consider"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Check in with emotions"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Daydream"] = new[] { "10–20 minutes", "20–40 minutes", "Indefinitely" },
        ["Imagine"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Notice"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Appreciate"] = new[] { "1–2 minutes", "5–10 minutes" },

        // --- Personal maintenance & appearance ---
        ["Bite (eating)"] = new[] { "Until complete" },
        ["Chew (eating)"] = new[] { "Until complete" },
        ["Warm (thermoregulation)"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Wrap (thermoregulation)"] = new[] { "Until complete" },
        ["Freeze (thermoregulation)"] = new[] { "Until complete" },

        // --- Tics & micro-expressions (recurring) ---
        ["Bite nails"] = new[] { "Until complete" },
        ["Bite lip"] = new[] { "Until complete" },
        ["Purse lips"] = new[] { "Until complete" },
        ["Roll eyes"] = new[] { "Until complete" },
        ["Squint"] = new[] { "Until complete" },
        ["Raise eyebrows"] = new[] { "Until complete" },
        ["Furrow eyebrows"] = new[] { "Until complete" },
        ["Scrunch nose"] = new[] { "Until complete" },
        ["Smirk"] = new[] { "Until complete" },
        ["Frown"] = new[] { "Until complete" },
        ["Grin"] = new[] { "Until complete" },
        ["Smile"] = new[] { "Until complete" },
        ["Wink"] = new[] { "Until complete" },

        // --- Commuting & errands ---
        ["Drive"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Travel"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Check mail"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Buy (shopping routine)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Browse"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes", "Indefinitely" },

        // --- Sexual/private (adult) ---
        ["Masturbate"] = new[] { "10–20 minutes", "20–40 minutes", "Until complete" },

        // --- Greetings & small talk ---
        ["Greet"] = new[] { "Until complete" },
        ["Smile (social)"] = new[] { "Until complete" },
        ["Nod"] = new[] { "Until complete" },
        ["Wave"] = new[] { "Until complete" },
        ["Introduce"] = new[] { "Until complete" },
        ["Chat"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes", "Indefinitely" },
        ["Hang out"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Joke around"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },

        // --- Manners & prosocial ---
        ["Compliment"] = new[] { "Until complete" },
        ["Thank"] = new[] { "Until complete" },
        ["Apologize"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Share stories"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Listen"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes", "Indefinitely" },
        ["Comfort"] = new[] { "5–10 minutes", "10–20 minutes", "20–40 minutes" },
        ["Console"] = new[] { "5–10 minutes", "10–20 minutes", "20–40 minutes" },
        ["Encourage"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Support"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Congratulate"] = new[] { "5–10 minutes", "10–20 minutes" },

        // --- Coordination ---
        ["Make plans"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Discuss"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Participate"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Hold meeting"] = new[] { "30–60 minutes", "60–90 minutes" },

        // --- Networking & community ---
        ["Bond"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Connect"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Network"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Visit"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Host"] = new[] { "60–90 minutes", "Indefinitely" },
        ["Volunteer"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },

        // --- Recurring social patterns (good or bad) ---
        ["Gossip"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Argue"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Debate"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Tease"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Praise"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Criticize"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Scold"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Ignore"] = new[] { "Until complete" },
        ["Ghost"] = new[] { "Until complete" },
        ["Lie"] = new[] { "Until complete" },
        ["Interrupt"] = new[] { "Until complete" },
        ["Start a rumor"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Spread news"] = new[] { "10–20 minutes", "20–40 minutes" },

        // ================= HOBBIES =================
        // --- Creative ---
        ["Writing (creative)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Draw"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Paint"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },

        // --- Music & movement ---
        ["Sing"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Rap"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Hum"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Whistle"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Vocal exercises"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Dance"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },

        // --- Games ---
        ["PC game"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Console game"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Mobile game"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Board game"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Cards"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Toss"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Tag"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Hide and seek"] = new[] { "20–40 minutes", "30–60 minutes" },

        // --- Sports & outdoor ---
        ["Basketball"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Soccer"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Run/Sprint"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Walk/Stroll (hiking)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Climb onto"] = new[] { "Until complete" },
        ["Climb down"] = new[] { "Until complete" },
        ["Jump"] = new[] { "Until complete" },
        ["Jump over"] = new[] { "Until complete" },
        ["Aim/Shoot (target practice)"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },

        // --- Fitness as pastime ---
        ["Strength training"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Treadmill sessions"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Stretching"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Pushups"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Sit-ups"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Pull-ups"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Squats"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Curls"] = new[] { "5–10 minutes", "10–20 minutes", "Until complete" },
        ["Bench press"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Food & home ---
        ["Cook"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Pour/Serve (mixology/hosting)"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Mindful & reflective ---
        ["Meditate"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Daydream"] = new[] { "10–20 minutes", "20–40 minutes", "Indefinitely" },
        ["Imagine"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Ponder"] = new[] { "5–10 minutes", "10–20 minutes", "20–40 minutes" },
        ["Check in with emotions"] = new[] { "5–10 minutes", "10–20 minutes" },
        ["Read for pleasure"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },

        // --- Collecting/consumer ---
        ["Browse (collecting)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Buy (thrifting/collecting)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },

        // --- Travel & motoring ---
        ["Travel (leisure)"] = new[] { "60–90 minutes", "Indefinitely" },
        ["Drive/Ride (road-tripping)"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },

        // --- Group play & performance ---
        ["Play games"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Play sports"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Dance together"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Sing together"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Perform"] = new[] { "20–40 minutes", "30–60 minutes" },

        // --- Celebration & leisure ---
        ["Celebrate"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Party"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Eat together"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Drink together"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Toast"] = new[] { "1–2 minutes", "5–10 minutes" },
        ["Host"] = new[] { "60–90 minutes", "Indefinitely" },
        ["Visit"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Share interests/hobbies"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },

        // --- Clubs & discourse ---
        ["Debate (club)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
        ["Discuss (club)"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Give presentation"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Pitch ideas"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Community & causes ---
        ["Volunteer (cause)"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Rally/Protest"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Participate (events)"] = new[] { "30–60 minutes", "60–90 minutes" },
        ["Cheer"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Compete"] = new[] { "30–60 minutes", "60–90 minutes" },

        // --- Story & comedy ---
        ["Share stories (group)"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Reminisce"] = new[] { "10–20 minutes", "20–40 minutes", "30–60 minutes" },
        ["Roast"] = new[] { "1-2 minutes", "5-10 minutes", "10-20 minutes" },
        ["Prank"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Serenade"] = new[] { "10–20 minutes", "20–40 minutes" },

        // --- Traveling together ---
        ["Travel together"] = new[] { "30–60 minutes", "60–90 minutes", "Indefinitely" },
        ["Reunite"] = new[] { "10–20 minutes", "20–40 minutes" },
        ["Form group/alliances"] = new[] { "20–40 minutes", "30–60 minutes" },
        ["Network (social)"] = new[] { "20–40 minutes", "30–60 minutes", "60–90 minutes" },
    };
    #endregion

    bool ClickedOutside(Vector2 pos)
    {
        // Inside the main panel / triggers / Add button?
        if (IsInsideRect(adderPanel, pos)) return false;
        if (triggersScrollView && triggersScrollView.activeSelf &&
            IsInsideRect(triggersScrollView.GetComponent<RectTransform>(), pos)) return false;
        if (addButton && IsInsideRect(addButton.GetComponent<RectTransform>(), pos)) return false;

        // Inside any of our dropdown widgets themselves?
        TMP_Dropdown[] dds = {
            typeDropdown, nameDropdown, sessionLengthDropdown, frequencyTypeSelector,
            everyAmountDD, everyUnitDD, timesAmountDD, timesUnitDD, adherenceDropdown
        };
        foreach (var dd in dds)
            if (dd && IsInsideRect(dd.GetComponent<RectTransform>(), pos)) return false;

        // Inside an expanded dropdown list (TMP spawns "TMP Dropdown List"/"Dropdown List")
        // or its full-screen "Blocker"? If so, don't close the adder.
        if (PointerOverDropdownListOrBlocker(pos)) return false;

        // Otherwise: it's outside.
        return true;
    }

    bool IsInsideRect(RectTransform rt, Vector2 pos)
    {
        return rt && RectTransformUtility.RectangleContainsScreenPoint(rt, pos, uiCam);
    }

    bool PointerOverDropdownListOrBlocker(Vector2 pos)
    {
        if (EventSystem.current == null) return false;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = pos }, results);

        foreach (var r in results)
        {
            if (!r.gameObject) continue;

            // “Blocker” is spawned by the dropdown while open
            if (r.gameObject.name == "Blocker") return true;

            // Any child of "... Dropdown List" counts as inside
            Transform t = r.gameObject.transform;
            while (t != null)
            {
                string n = t.name;
                if (n.Contains("Dropdown List") || n.Contains("TMP Dropdown List"))
                    return true;
                t = t.parent;
            }
        }
        return false;
    }

    void HookFormValidation()
    {
        void Hook(TMP_Dropdown d) { if (d) d.onValueChanged.AddListener(_ => ValidateConfirm()); }
        Hook(typeDropdown);
        Hook(nameDropdown);
        Hook(sessionLengthDropdown);
        Hook(frequencyTypeSelector);
        Hook(everyAmountDD); Hook(everyUnitDD);
        Hook(timesAmountDD); Hook(timesUnitDD);
        Hook(adherenceDropdown);
        ValidateConfirm();
    }

    // --- keep Confirm disabled until the form is complete ---
    void ValidateConfirm()
    {
        bool typeOk = typeDropdown && typeDropdown.value > 0;
        bool nameOk = nameDropdown && nameDropdown.value > 0;
        bool sessOk = sessionLengthDropdown &&
                    sessionLengthDropdown.interactable &&
                    sessionLengthDropdown.value > 0;

        if (confirmButton) confirmButton.interactable = typeOk && nameOk && sessOk;
    }


    // --- build a Frequency from UI ---
    characterStats.HabitsHobbiesStats.Frequency ReadFrequency()
    {
        var f = new characterStats.HabitsHobbiesStats.Frequency();
        if (frequencyTypeSelector.value == 0)
        {
            f.mode = characterStats.HabitsHobbiesStats.FrequencyMode.Every;
            f.every_amount = Mathf.Max(1, everyAmountDD.value); // assuming 1..N, with placeholder at 0
            f.every_unit = EnumParseUnit(everyUnitDD);
        }
        else
        {
            f.mode = characterStats.HabitsHobbiesStats.FrequencyMode.TimesPer;
            f.times_per_count = Mathf.Max(1, timesAmountDD.value);
            f.times_per_unit = EnumParseUnit(timesUnitDD);
        }
        return f;

        static characterStats.HabitsHobbiesStats.FrequencyUnit EnumParseUnit(TMP_Dropdown dd)
        {
            // option text must match enum names (Hour/Day/Week/Month/Year) or map here if you use display names
            var label = dd.options[dd.value].text.Trim();
            return (characterStats.HabitsHobbiesStats.FrequencyUnit)System.Enum.Parse(
                typeof(characterStats.HabitsHobbiesStats.FrequencyUnit), label, true);
        }
    }

    // --- Confirm click ---
    void OnConfirm()
    {
        if (!confirmButton || !confirmButton.interactable || stats == null) return;

        var cat = HabitTypes.Contains(typeDropdown.options[typeDropdown.value].text)
            ? characterStats.HabitsHobbiesStats.Category.Habit
            : characterStats.HabitsHobbiesStats.Category.Hobby;

        var entry = new characterStats.HabitsHobbiesStats.Entry
        {
            category = cat,
            type = typeDropdown.options[typeDropdown.value].text,
            name = nameDropdown.options[nameDropdown.value].text,
            session_length = sessionLengthDropdown.options[sessionLengthDropdown.value].text,
            frequency = ReadFrequency(),
            adherence = ParseAdherence(),
            triggers = ReadCheckedTriggers()
        };

        // push into stats model
        var model = stats.Model;
        var list = new List<characterStats.HabitsHobbiesStats.Entry>(model.habitsAndHobbies.list);
        list.Add(entry);
        stats.SetValue("habitsAndHobbies.list", list);
        
        // This will trigger RefreshFromModel(), which handles all UI updates correctly.
        stats.CommitAndRecompute(); 

        // The manual row addition block has been removed.

        // close adder + reset confirm
        if (adderRoot) adderRoot.SetActive(false);
        foreach (var t in triggersList.GetComponentsInChildren<Toggle>(true)) t.isOn = false;
        ValidateConfirm();

        var adderList = GetAdderTriggersList();
        if (adderList)
            foreach (var t in adderList.GetComponentsInChildren<Toggle>(true))
                t.isOn = false;
    }
    

    // --- parse adherence from dropdown text ---
    characterStats.HabitsHobbiesStats.Adherence ParseAdherence()
    {
        var txt = adherenceDropdown.options[adherenceDropdown.value].text.Trim();
        return (characterStats.HabitsHobbiesStats.Adherence)
            System.Enum.Parse(typeof(characterStats.HabitsHobbiesStats.Adherence), txt, true);
    }

    List<string> ReadCheckedTriggers()
    {
        EnsureTriggersPopulated();
        var list = GetAdderTriggersList();
        var picked = new List<string>();
        if (!list) return picked;

        foreach (var t in list.GetComponentsInChildren<Toggle>(true))
        {
            if (t && t.isOn)
            {
                string triggerText = null;
                
                var tmpLabel = t.GetComponentInChildren<TMP_Text>(true);
                if (tmpLabel != null)
                {
                    triggerText = tmpLabel.text;
                }
                else
                {
                    var legacyLabel = t.GetComponentInChildren<Text>(true);
                    if (legacyLabel != null)
                    {
                        triggerText = legacyLabel.text;
                    }
                }

                if (!string.IsNullOrWhiteSpace(triggerText))
                {
                    picked.Add(triggerText.Trim());
                }
            }
        }
        return picked;
    }

    // --- fill the row prefab’s labels ---
    void PopulateRow(GameObject row, characterStats.HabitsHobbiesStats.Entry e)
    {
        if (!row) return;

        void SetTxt(string namePart, string value)
        {
            var t = row.GetComponentsInChildren<TMP_Text>(true)
                    .FirstOrDefault(x => x.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0);
            if (t) t.text = value;
        }

        SetTxt("H|H Name", e.name);
        SetTxt("H|H Type Label", e.type);
        SetTxt("Session Length", e.session_length);

        string freqText = e.frequency.mode == characterStats.HabitsHobbiesStats.FrequencyMode.Every
            ? $"Every {e.frequency.every_amount} {e.frequency.every_unit}"
            : $"{e.frequency.times_per_count} Times per {e.frequency.times_per_unit}";
        SetTxt("Frequency", freqText);

        SetTxt("Adherence", e.adherence.ToString());

        var triggersText = row.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(x => x.name.IndexOf("Triggers List", StringComparison.OrdinalIgnoreCase) >= 0);
        if (triggersText) triggersText.text = (e.triggers.Count == 0) ? "—" : string.Join(", ", e.triggers);

        WireRowTriggersUI(row, e.triggers);
    }

    void WireRowTriggersUI(GameObject row, List<string> triggers)
    {
        var btn = row.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(x => x.name.IndexOf("Triggers Button", StringComparison.OrdinalIgnoreCase) >= 0);

        var panelRT = row.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(x => x.name.IndexOf("Triggers Scroll View", StringComparison.OrdinalIgnoreCase) >= 0
                            || x.name.IndexOf("Triggers Panel", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!panelRT) { if (btn) btn.interactable = false; return; }

        var list = panelRT.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(x => x.name.IndexOf("Triggers List", StringComparison.OrdinalIgnoreCase) >= 0);
        if (!list) { if (btn) btn.interactable = false; return; }

        // keep Close Button if present
        var closeBtn = list.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(b => b.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0);

        // clear everything EXCEPT Close Button
        for (int i = list.childCount - 1; i >= 0; i--)
        {
            var child = list.GetChild(i);
            if (closeBtn && child == closeBtn.transform) continue;
            Destroy(child.gameObject);
        }

        // insert triggers BEFORE Close Button so it stays last
        int insertIndex = closeBtn ? closeBtn.transform.GetSiblingIndex() : list.childCount;
        foreach (var s in triggers)
        {
            var go = new GameObject("Trigger", typeof(RectTransform));
            go.transform.SetParent(list, false);
            go.transform.SetSiblingIndex(insertIndex++);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = s;
            tmp.textWrappingMode = TextWrappingModes.Normal;
        }

        // wire close
        if (closeBtn)
        {
            var panelGO = panelRT.gameObject;
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(() => panelGO.SetActive(false));
        }

        panelRT.gameObject.SetActive(false);

        if (btn)
        {
            btn.interactable = triggers.Count > 0;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
                panelRT.gameObject.SetActive(!panelRT.gameObject.activeSelf));
        }

        // keep content sized inside viewport
        LayoutRebuilder.ForceRebuildLayoutImmediate(list);
    }


    // --- remove button on the row ---
    void WireRowRemove(GameObject row, characterStats.HabitsHobbiesStats.Entry e)
    {
        var btn = row.GetComponentsInChildren<Button>(true)
                    .FirstOrDefault(x => x.name.IndexOf("Remove Button", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (!btn) return;

        btn.onClick.AddListener(() =>
        {
            var model = stats.Model;
            var list = model.habitsAndHobbies.list;
            // remove by reference if still present, else fallback by identity
            var index = list.IndexOf(e);
            if (index < 0)
                index = list.FindIndex(x => x.category == e.category && x.type == e.type && x.name == e.name);

            if (index >= 0)
            {
                list.RemoveAt(index);
                stats.CommitAndRecompute();
            }
            if (row) Destroy(row);
        });
    }
    
    RectTransform GetAdderTriggersList()
    {
        if (triggersList) return triggersList;
        if (!adderRoot) return null;
        return adderRoot.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(x => x.name.IndexOf("Triggers List", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    GameObject GetAdderTriggersScrollView()
    {
        if (triggersScrollView) return triggersScrollView;
        if (!adderRoot) return null;
        return adderRoot.GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(x => x.name.IndexOf("Triggers Scroll View", StringComparison.OrdinalIgnoreCase) >= 0)
            ?.gameObject;
    }

    Button GetAdderTriggersCloseButton()
    {
        if (triggersCloseButton) return triggersCloseButton;
        if (!adderRoot) return null;
        return adderRoot.GetComponentsInChildren<Button>(true)
            .FirstOrDefault(x => x.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0);
    }


}
