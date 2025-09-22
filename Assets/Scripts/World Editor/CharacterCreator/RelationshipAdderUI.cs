using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using System;

public class RelationshipsAdderUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private StatsService svc;
    [SerializeField] private Button addRelationshipButton;
    [SerializeField] private GameObject relationshipsListRoot;

    [Header("Adder Panels")]
    [SerializeField] private GameObject characterAdderPanel;
    [SerializeField] private GameObject relationshipTypePanel;

    [Header("Content Parents & Prefabs")]
    [SerializeField] private Transform characterListContent;
    [SerializeField] private GameObject characterButtonPrefab;
    [SerializeField] private Transform tagListContent;
    [SerializeField] private GameObject tagButtonPrefab;
    [SerializeField] private Button confirmTagsButton;

    [Header("Button Selection Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.7f, 0.85f, 1.0f);
    [SerializeField] private Color disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

    // State for the two-step process
    private characterStats selectedCharacter;
    private readonly List<characterStats.RelationshipTag> selectedTags = new();
    private characterStats.RelationshipEntry _existingEntry = null; // Holds the relationship if we're updating

    // For relationship restrictions
    private readonly Dictionary<characterStats.RelationshipTag, Button> _tagButtons = new();
    private readonly Dictionary<characterStats.RelationshipTag, Image> _tagButtonImages = new();

    // --- MODIFIED: More comprehensive exclusion groups ---
    private static readonly List<List<characterStats.RelationshipTag>> ExclusiveGroups = new()
    {
        // A person's primary familial or romantic role is singular. This prevents illogical combinations
        // like being a Parent AND a Sibling, or a Cousin AND a Child, or a Spouse AND an Ex-Spouse.
        new List<characterStats.RelationshipTag> {
            // Core Family
            characterStats.RelationshipTag.Parent, characterStats.RelationshipTag.Child,
            characterStats.RelationshipTag.Sibling, characterStats.RelationshipTag.Grandparent,
            characterStats.RelationshipTag.Grandchild, characterStats.RelationshipTag.Aunt,
            characterStats.RelationshipTag.Uncle, characterStats.RelationshipTag.Niece,
            characterStats.RelationshipTag.Nephew, characterStats.RelationshipTag.Cousin,
            // Step Family
            characterStats.RelationshipTag.StepParent, characterStats.RelationshipTag.StepChild,
            characterStats.RelationshipTag.StepSibling,
            // In-Laws
            characterStats.RelationshipTag.InLaw,
            // Romantic (Current & Past)
            characterStats.RelationshipTag.Boyfriend, characterStats.RelationshipTag.Girlfriend,
            characterStats.RelationshipTag.Partner, characterStats.RelationshipTag.Spouse,
            characterStats.RelationshipTag.Fiancé, characterStats.RelationshipTag.CasualDating,
            characterStats.RelationshipTag.PolyamorousPartner, characterStats.RelationshipTag.PrimaryPartner,
            characterStats.RelationshipTag.FriendWithBenefits, characterStats.RelationshipTag.CasualHookup,
            characterStats.RelationshipTag.ExGirlfriend, characterStats.RelationshipTag.ExBoyfriend,
            characterStats.RelationshipTag.ExPartner, characterStats.RelationshipTag.ExSpouse
        },
        // Professional hierarchical roles remain separately exclusive.
        new List<characterStats.RelationshipTag> {
            characterStats.RelationshipTag.Manager, characterStats.RelationshipTag.Subordinate
        },
        new List<characterStats.RelationshipTag> {
            characterStats.RelationshipTag.Mentor, characterStats.RelationshipTag.Mentee
        },
        new List<characterStats.RelationshipTag> {
            characterStats.RelationshipTag.Coach, characterStats.RelationshipTag.Player
        },
        new List<characterStats.RelationshipTag> {
            characterStats.RelationshipTag.Teacher, characterStats.RelationshipTag.Student
        },
        new List<characterStats.RelationshipTag> {
            characterStats.RelationshipTag.Trainer, characterStats.RelationshipTag.Trainee
        },
        new List<characterStats.RelationshipTag> {
            characterStats.RelationshipTag.Tutor, characterStats.RelationshipTag.Student
        }
    };

    void Awake()
    {
        if (!svc) svc = FindObjectOfType<StatsService>();
        if (addRelationshipButton) addRelationshipButton.onClick.AddListener(OpenCharacterAdder);
        if (confirmTagsButton) confirmTagsButton.onClick.AddListener(OnConfirmTags);
        
        if(characterAdderPanel) characterAdderPanel.SetActive(false);
        if(relationshipTypePanel) relationshipTypePanel.SetActive(false);
    }

    private void PopulateTagList()
    {
        selectedTags.Clear();
        _tagButtons.Clear();
        _tagButtonImages.Clear();

        for (int i = tagListContent.childCount - 1; i >= 0; i--)
        {
            GameObject child = tagListContent.GetChild(i).gameObject;
            if (child != confirmTagsButton.gameObject)
            {
                Destroy(child);
            }
        }

        foreach (characterStats.RelationshipTag tag in Enum.GetValues(typeof(characterStats.RelationshipTag)))
        {
            var buttonGO = Instantiate(tagButtonPrefab, tagListContent);
            var button = buttonGO.GetComponent<Button>();
            var buttonImage = buttonGO.GetComponent<Image>();
            var label = buttonGO.GetComponentInChildren<TMP_Text>();
            
            label.text = tag.ToString();
            
            _tagButtons[tag] = button;
            _tagButtonImages[tag] = buttonImage;
            button.onClick.AddListener(() => OnTagButtonClicked(tag));
        }
        
        UpdateTagButtonStates();
    }

    private void OnTagButtonClicked(characterStats.RelationshipTag tag)
    {
        if (selectedTags.Contains(tag))
        {
            selectedTags.Remove(tag);
        }
        else
        {
            selectedTags.Add(tag);
        }
        UpdateTagButtonStates();
    }

    private void UpdateTagButtonStates()
    {
        var preExistingTags = new HashSet<characterStats.RelationshipTag>();
        if (_existingEntry != null)
        {
            foreach (var tag in _existingEntry.tags)
            {
                preExistingTags.Add(tag);
            }
        }

        var incompatibleTags = new HashSet<characterStats.RelationshipTag>();
        var allTagsForExclusionCheck = selectedTags.Concat(preExistingTags);

        foreach (var tagToCheck in allTagsForExclusionCheck)
        {
            foreach (var group in ExclusiveGroups)
            {
                if (group.Contains(tagToCheck))
                {
                    foreach (var tagInGroup in group)
                    {
                        if (tagInGroup != tagToCheck)
                        {
                            incompatibleTags.Add(tagInGroup);
                        }
                    }
                }
            }
        }

        foreach (var pair in _tagButtons)
        {
            var tag = pair.Key;
            var button = pair.Value;
            var image = _tagButtonImages[tag];

            bool isSelected = selectedTags.Contains(tag);
            bool isPreExisting = preExistingTags.Contains(tag);
            bool isCompatible = !incompatibleTags.Contains(tag);

            button.interactable = !isPreExisting && (isCompatible || isSelected);

            if (isSelected) {
                image.color = selectedColor;
            } else if (isPreExisting || !button.interactable) {
                image.color = disabledColor;
            } else {
                image.color = normalColor;
            }
        }
    }

    private void OnConfirmTags()
    {
        if (selectedCharacter == null) return;
        
        if (selectedTags.Count == 0)
        {
            CloseAllPanels();
            return;
        }

        var sourceCharacter = svc.Model;
        var targetCharacter = selectedCharacter;
        
        var newReciprocalTags = selectedTags
            .Select(tag => GetReciprocalTag(tag, sourceCharacter.profile.gender))
            .Distinct()
            .ToList();

        if (_existingEntry != null)
        {
            _existingEntry.tags.AddRange(selectedTags.Except(_existingEntry.tags));
            
            string sourceCharacterName = $"{sourceCharacter.profile.first_name} {sourceCharacter.profile.last_name}".Trim();
            var reciprocalEntry = targetCharacter.relationships.people.FirstOrDefault(r => r.DisplayName == sourceCharacterName);
            if (reciprocalEntry != null)
            {
                reciprocalEntry.tags.AddRange(newReciprocalTags.Except(reciprocalEntry.tags));
            }
        }
        else
        {
            var newEntry = new characterStats.RelationshipEntry
            {
                first_name = targetCharacter.profile.first_name,
                last_name = targetCharacter.profile.last_name,
                tags = new List<characterStats.RelationshipTag>(selectedTags),
                closeness = 50, satisfaction = 50, love = 50,
                conflict = 0, given_support = 50, received_support = 50
            };
            sourceCharacter.relationships.people.Add(newEntry);

            var newReciprocalEntry = new characterStats.RelationshipEntry
            {
                first_name = sourceCharacter.profile.first_name,
                last_name = sourceCharacter.profile.last_name,
                tags = newReciprocalTags,
                closeness = 50, satisfaction = 50, love = 50,
                conflict = 0, given_support = 50, received_support = 50
            };
            targetCharacter.relationships.people.Add(newReciprocalEntry);
        }

        svc.CommitAndRecompute();
        CloseAllPanels();
    }
    
    #region Setup and Navigation
    private void OpenCharacterAdder()
    {
        if (!characterAdderPanel || !Game.Instance || !svc.Model) return;
        _existingEntry = null;
        PopulateCharacterList();
        characterAdderPanel.SetActive(true);
        relationshipTypePanel.SetActive(false);
        relationshipsListRoot.SetActive(false);
    }

    private void OnCharacterSelected(characterStats character)
    {
        selectedCharacter = character;
        _existingEntry = svc.Model.relationships.people.FirstOrDefault(r => r.DisplayName == $"{character.profile.first_name} {character.profile.last_name}".Trim());

        characterAdderPanel.SetActive(false);
        PopulateTagList();
        relationshipTypePanel.SetActive(true);
    }

    private void PopulateCharacterList()
    {
        for (int i = characterListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(characterListContent.GetChild(i).gameObject);
        }

        var allCharacters = new List<characterStats>();
        if (Game.Instance.CurrentSave != null)
        {
            allCharacters.AddRange(Game.Instance.CurrentSave.mains);
            allCharacters.AddRange(Game.Instance.CurrentSave.sides);
            allCharacters.AddRange(Game.Instance.CurrentSave.extras);
        }

        foreach (var character in allCharacters)
        {
            if (character == svc.Model) continue;

            var buttonGO = Instantiate(characterButtonPrefab, characterListContent);
            var button = buttonGO.GetComponent<Button>();
            var text = buttonGO.GetComponentInChildren<TMP_Text>();

            text.text = $"{character.profile.first_name} {character.profile.last_name}".Trim();
            
            button.interactable = true;
            button.onClick.AddListener(() => OnCharacterSelected(character));
        }
    }

    public void CloseAllPanels()
    {
        if (characterAdderPanel) characterAdderPanel.SetActive(false);
        if (relationshipTypePanel) relationshipTypePanel.SetActive(false);
        if (relationshipsListRoot) relationshipsListRoot.SetActive(true);
    }
    #endregion
    
    #region Reciprocal Logic
    private static characterStats.RelationshipTag GetReciprocalTag(characterStats.RelationshipTag originalTag, characterStats.GenderType subjectGender)
    {
        switch (originalTag)
        {
            case characterStats.RelationshipTag.Parent: return characterStats.RelationshipTag.Child;
            case characterStats.RelationshipTag.Child: return characterStats.RelationshipTag.Parent;
            case characterStats.RelationshipTag.Grandparent: return characterStats.RelationshipTag.Grandchild;
            case characterStats.RelationshipTag.Grandchild: return characterStats.RelationshipTag.Grandparent;
            case characterStats.RelationshipTag.StepParent: return characterStats.RelationshipTag.StepChild;
            case characterStats.RelationshipTag.StepChild: return characterStats.RelationshipTag.StepParent;
            case characterStats.RelationshipTag.Manager: return characterStats.RelationshipTag.Subordinate;
            case characterStats.RelationshipTag.Subordinate: return characterStats.RelationshipTag.Manager;
            case characterStats.RelationshipTag.Mentor: return characterStats.RelationshipTag.Mentee;
            case characterStats.RelationshipTag.Mentee: return characterStats.RelationshipTag.Mentor;
            case characterStats.RelationshipTag.Coach: return characterStats.RelationshipTag.Player;
            case characterStats.RelationshipTag.Player: return characterStats.RelationshipTag.Coach;
            case characterStats.RelationshipTag.Trainer: return characterStats.RelationshipTag.Trainee;
            case characterStats.RelationshipTag.Trainee: return characterStats.RelationshipTag.Trainer;
            case characterStats.RelationshipTag.Tutor: return characterStats.RelationshipTag.Student;
            case characterStats.RelationshipTag.Student: return characterStats.RelationshipTag.Teacher;
            case characterStats.RelationshipTag.Teacher: return characterStats.RelationshipTag.Student;
            case characterStats.RelationshipTag.Aunt:
            case characterStats.RelationshipTag.Uncle:
                return (subjectGender == characterStats.GenderType.Woman) ? characterStats.RelationshipTag.Niece : characterStats.RelationshipTag.Nephew;
            case characterStats.RelationshipTag.Niece:
            case characterStats.RelationshipTag.Nephew:
                 return (subjectGender == characterStats.GenderType.Woman) ? characterStats.RelationshipTag.Aunt : characterStats.RelationshipTag.Uncle;
            case characterStats.RelationshipTag.Boyfriend:
                 if (subjectGender == characterStats.GenderType.Woman) return characterStats.RelationshipTag.Girlfriend;
                 if (subjectGender == characterStats.GenderType.Man) return characterStats.RelationshipTag.Boyfriend;
                 return characterStats.RelationshipTag.Partner;
            case characterStats.RelationshipTag.Girlfriend:
                 if (subjectGender == characterStats.GenderType.Woman) return characterStats.RelationshipTag.Girlfriend;
                 if (subjectGender == characterStats.GenderType.Man) return characterStats.RelationshipTag.Boyfriend;
                 return characterStats.RelationshipTag.Partner;
            case characterStats.RelationshipTag.ExBoyfriend:
                 if (subjectGender == characterStats.GenderType.Woman) return characterStats.RelationshipTag.ExGirlfriend;
                 if (subjectGender == characterStats.GenderType.Man) return characterStats.RelationshipTag.ExBoyfriend;
                 return characterStats.RelationshipTag.ExPartner;
            case characterStats.RelationshipTag.ExGirlfriend:
                 if (subjectGender == characterStats.GenderType.Woman) return characterStats.RelationshipTag.ExGirlfriend;
                 if (subjectGender == characterStats.GenderType.Man) return characterStats.RelationshipTag.ExBoyfriend;
                 return characterStats.RelationshipTag.ExPartner;
            default:
                return originalTag;
        }
    }
    #endregion
}