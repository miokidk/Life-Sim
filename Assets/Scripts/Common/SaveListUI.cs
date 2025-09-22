using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveListUI : MonoBehaviour
{
    [Header("List")]
    [SerializeField] Transform content;      // VerticalLayoutGroup
    [SerializeField] Button itemPrefab;      // simple Button with a TMP_Text + Image

    [Header("Actions")]
    [SerializeField] Button loadButton;      // wire on the panel
    [SerializeField] Button deleteButton;    // wire on the panel

    [Header("Style")]
    [SerializeField] Color normalColor   = new Color(1, 1, 1, 0.05f);
    [SerializeField] Color selectedColor = new Color(1, 1, 1, 0.25f);

    [SerializeField] GameObject panelToHideOnLoad;  // assign this in the Inspector


    readonly List<Button> rowButtons = new();
    readonly List<string> rowIds     = new();

    string selectedId;
    float  confirmUntil; // for delete confirm

    void Awake()
    {
        if (loadButton)
        {
            loadButton.onClick.AddListener(LoadSelected);
            loadButton.interactable = false;
        }
        if (deleteButton)
        {
            deleteButton.onClick.AddListener(DeleteSelected);
            deleteButton.interactable = false;
        }
    }

    void OnEnable() { Refresh(); }
    void Update()
    {
        // auto-cancel delete confirm
        if (confirmUntil > 0f && Time.unscaledTime > confirmUntil) ResetDeleteLabel();
    }

    public void Refresh()
    {
        // clear rows
        foreach (Transform c in content) Destroy(c.gameObject);
        rowButtons.Clear();
        rowIds.Clear();
        selectedId = null;
        SetActionButtons(false);
        ResetDeleteLabel();

        var saves = SaveSystem.ListSaves(); // uses your SaveSystem
        foreach (var s in saves)
        {
            var btn = Instantiate(itemPrefab, content);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label)
                label.text = $"{s.displayName} • {SaveSystem.FormatLocal(s.updatedUtc)}";

            var img = btn.GetComponent<Image>();
            if (img) img.color = normalColor;

            string id = s.saveId;
            btn.onClick.AddListener(() => Select(id));

            rowButtons.Add(btn);
            rowIds.Add(id);
        }

        if (saves.Count == 0)
        {
            var btn = Instantiate(itemPrefab, content);
            btn.interactable = false;
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label) label.text = "No saves yet";
            var img = btn.GetComponent<Image>();
            if (img) img.color = normalColor;
        }
    }

    void Select(string id)
    {
        selectedId = id;
        for (int i = 0; i < rowButtons.Count; i++)
        {
            var img = rowButtons[i].GetComponent<Image>();
            if (!img) continue;
            img.color = (rowIds[i] == id) ? selectedColor : normalColor;
        }
        SetActionButtons(true);
        ResetDeleteLabel();
    }

    void SetActionButtons(bool on)
    {
        if (loadButton)   loadButton.interactable = on;
        if (deleteButton) deleteButton.interactable = on;
    }

    void LoadSelected()
    {
        if (string.IsNullOrEmpty(selectedId)) return;
        if (panelToHideOnLoad) panelToHideOnLoad.SetActive(false);
        Game.Instance.LoadWorld(selectedId, "autosave");
    }

    void DeleteSelected()
    {
        if (string.IsNullOrEmpty(selectedId)) return;

        // two-click confirm (3s window)
        if (Time.unscaledTime <= confirmUntil)
        {
            SaveSystem.DeleteWorld(selectedId);
            Refresh();              // rebuild list & disable buttons again
            return;
        }
        confirmUntil = Time.unscaledTime + 3f;
        if (deleteButton)
        {
            var t = deleteButton.GetComponentInChildren<TMP_Text>();
            if (t) t.text = "Confirm";
        }
    }

    void ResetDeleteLabel()
    {
        confirmUntil = 0f;
        if (deleteButton)
        {
            var t = deleteButton.GetComponentInChildren<TMP_Text>();
            if (t) t.text = "Delete";
        }
    }
}
