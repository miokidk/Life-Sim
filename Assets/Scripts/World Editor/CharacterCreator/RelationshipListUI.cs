using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class RelationshipsListUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private StatsService svc;
    [SerializeField] private RectTransform listParent; // The "Content" object of your Relationship List ScrollView
    [SerializeField] private GameObject relationshipPrefab; // Your prefab for a single relationship entry

    [Header("Overlay Panel")]
    [SerializeField] private RectTransform panelRoot; // The root "Relationships" panel
    [SerializeField] private Camera uiCamera;
    private RectTransform panelRect;

    void Awake()
    {
        panelRect = panelRoot ? panelRoot : transform as RectTransform;
        
        var root = GetComponentInParent<Canvas>();
        if (root && root.renderMode != RenderMode.ScreenSpaceOverlay) 
        {
            uiCamera = root.worldCamera;
        }
    }

    void OnEnable()
    {
        StatsService.StatsRecomputed += Rebuild;
        if (!svc) svc = FindObjectOfType<StatsService>();
        Rebuild();
    }

    void OnDisable()
    {
        StatsService.StatsRecomputed -= Rebuild;
    }

    void Rebuild()
    {
        if (!svc || svc.Model == null || !listParent || !relationshipPrefab) return;

        // Clear existing relationship UI elements
        for (int i = listParent.childCount - 1; i >= 0; i--)
        {
            Destroy(listParent.GetChild(i).gameObject);
        }

        var relationships = svc.Model.relationships?.people;
        if (relationships == null) return;

        for (int i = 0; i < relationships.Count; i++)
        {
            var entry = relationships[i];
            var row = Instantiate(relationshipPrefab, listParent);
            
            SetText(row.transform, "Relationship Name", entry.DisplayName);
            
            var dropdownTf = row.transform.Find("Relationship Titles");
            if (dropdownTf != null)
            {
                var dropdown = dropdownTf.GetComponent<TMP_Dropdown>();
                if (dropdown != null)
                {
                    dropdown.ClearOptions();
                    List<string> tagStrings = entry.tags.Select(t => t.ToString()).ToList();
                    dropdown.AddOptions(tagStrings);
                    dropdown.RefreshShownValue();
                }
            }
            
            string durationStr = entry.duration_is_days ? $"{entry.duration_days} days" : $"{entry.duration_ym.years}y {entry.duration_ym.months}m";
            SetText(row.transform, "Duration Length", durationStr);

            string basePath = $"relationships.people[{i}]";
            BindSliderWithUndo(row.transform, "Closeness Slider",        $"{basePath}.closeness");
            BindSliderWithUndo(row.transform, "Satisfaction Slider",     $"{basePath}.satisfaction");
            BindSliderWithUndo(row.transform, "Love Slider",             $"{basePath}.love");
            BindSliderWithUndo(row.transform, "Conflict Slider",         $"{basePath}.conflict");
            BindSliderWithUndo(row.transform, "Given Support Slider",    $"{basePath}.given_support");
            BindSliderWithUndo(row.transform, "Received Support Slider", $"{basePath}.received_support");

            // MODIFIED: This method now contains the logic for reciprocal removal
            AttachRemoveOnName(row.transform, entry);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(listParent);
    }

    void BindSliderWithUndo(Transform row, string sliderName, string path)
    {
        var sliderTf = row.Find(sliderName);
        if (!sliderTf)
        {
            if(sliderName == "Received Support Slider") sliderTf = row.Find("Received Suppport Slider"); // Typo fix
            if (!sliderTf)
            {
                Debug.LogWarning($"Could not find '{sliderName}' in relationship prefab instance.");
                return;
            }
        }

        var slider = sliderTf.GetComponentInChildren<Slider>(true);
        if (!slider) return;

        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.interactable = true;
        
        var initialValueObj = svc.GetValue(path);
        if (initialValueObj != null)
        {
            slider.SetValueWithoutNotify(System.Convert.ToSingle(initialValueObj));
        }

        object beforeValue = null;
        var et = slider.gameObject.GetComponent<EventTrigger>() ?? slider.gameObject.AddComponent<EventTrigger>();
        et.triggers.Clear();

        var pointerDownEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDownEntry.callback.AddListener(_ => {
            if (svc == null) return;
            beforeValue = svc.GetValue(path);
            ChangeBuffer.BeginEdit(svc, $"Adjust {sliderName.Replace(" Slider", "")}");
        });
        et.triggers.Add(pointerDownEntry);

        var pointerUpEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUpEntry.callback.AddListener(_ => {
            if (beforeValue == null)
            {
                ChangeBuffer.EndEdit();
                return;
            }
            object afterValue = Mathf.RoundToInt(slider.value);
            ChangeBuffer.Record(path, beforeValue, afterValue);
            ChangeBuffer.EndEdit();
            beforeValue = null;
        });
        et.triggers.Add(pointerUpEntry);
    }

    /// <summary>
    /// Attaches a click listener to the relationship name to remove it, now with reciprocal removal logic.
    /// </summary>
    void AttachRemoveOnName(Transform row, characterStats.RelationshipEntry entryToRemove)
    {
        var nameTf = row.Find("Relationship Name");
        if (!nameTf) return;
        
        var graphic = nameTf.GetComponent<Graphic>();
        if(graphic) graphic.raycastTarget = true;

        var et = nameTf.gameObject.GetComponent<EventTrigger>() ?? nameTf.gameObject.AddComponent<EventTrigger>();
        et.triggers.Clear();

        var clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener(_ => {
            if (svc?.Model?.relationships?.people == null) return;

            // --- NEW: Reciprocal Removal Logic ---
            var sourceCharacter = svc.Model;
            var targetCharacter = FindCharacterByName(entryToRemove.DisplayName);

            if (targetCharacter != null)
            {
                // Find the relationship entry on the *other* character that points back to this one.
                string sourceCharacterName = $"{sourceCharacter.profile.first_name} {sourceCharacter.profile.last_name}".Trim();
                var reciprocalEntry = targetCharacter.relationships.people.FirstOrDefault(r => r.DisplayName == sourceCharacterName);
                
                if (reciprocalEntry != null)
                {
                    // Remove the reciprocal entry from the other character's list.
                    targetCharacter.relationships.people.Remove(reciprocalEntry);
                }
            }
            // --- END NEW LOGIC ---

            // Remove the primary relationship entry.
            svc.Model.relationships.people.Remove(entryToRemove);
            
            // Commit changes for both characters and rebuild the UI.
            svc.CommitAndRecompute();
        });
        et.triggers.Add(clickEntry);
    }

    /// <summary>
    /// Searches all characters in the current world save for one with a matching name.
    /// </summary>
    /// <returns>The found characterStats object or null.</returns>
    private characterStats FindCharacterByName(string displayName)
    {
        if (Game.Instance?.CurrentSave == null || string.IsNullOrWhiteSpace(displayName)) return null;

        var currentSave = Game.Instance.CurrentSave;
        // Combine all character lists into a single searchable collection.
        IEnumerable<characterStats> allCharacters = Enumerable.Empty<characterStats>();
        if (currentSave.mains != null) allCharacters = allCharacters.Concat(currentSave.mains);
        if (currentSave.sides != null) allCharacters = allCharacters.Concat(currentSave.sides);
        if (currentSave.extras != null) allCharacters = allCharacters.Concat(currentSave.extras);

        // Find the first character whose profile name matches the display name of the relationship.
        return allCharacters.FirstOrDefault(c => 
            $"{c.profile.first_name} {c.profile.last_name}".Trim() == displayName
        );
    }
    
    static void SetText(Transform root, string childName, string value)
    {
        var tf = root.Find(childName);
        if (!tf) return;
        var textComponent = tf.GetComponentInChildren<TMP_Text>(true);
        if (textComponent) textComponent.text = value;
    }
}