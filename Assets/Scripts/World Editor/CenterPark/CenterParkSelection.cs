using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class CenterParkSelection : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private EditorController controller;     // drag EditorRoot (EditorController)
    [SerializeField] private Camera editorCamera;             // your Editor Camera
    [SerializeField] private LayerMask clickMask = ~0;        // optional: set to Character layer

    [Header("UI Panel")]
    [SerializeField] private RectTransform characterInfoPanel; // MODIFIED: Changed GameObject to RectTransform
    [SerializeField] private RectTransform panelPositionLeft;  // NEW: An empty RectTransform defining the left-side position/anchor
    [SerializeField] private RectTransform panelPositionRight; // NEW: An empty RectTransform defining the right-side position/anchor
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text ageText;
    [SerializeField] private TMP_Text typeText;
    [SerializeField] private TMP_Text genderSexText;
    [SerializeField] private Button addButton;
    [SerializeField] private float ccToParkAddLockSeconds = 0.5f;
    [SerializeField] private Button addButtonCC;
    [SerializeField] private Button closeButton;              // wire OnClick in inspector or via Awake
    [SerializeField] private Button deleteButton;

    [Header("Selection Visual")]
    [SerializeField] private GameObject selectedIndicatorPrefab;
    [SerializeField] private float indicatorYOffset = 0.15f;  // small lift above head
    [SerializeField] private float rayMaxDistance = 500f;

    [SerializeField] private Button editButton;

    [Header("Camera Move Tuning")]
    [SerializeField] private float editCamDistance = 3f;
    [SerializeField] private float editCamHeightOffset = 1.6f;
    [SerializeField] private float editCamSeconds = 0.5f;
    [SerializeField] private AnimationCurve editCamEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [SerializeField, Range(0f, 1f)] private float editCamFocusHeightBias = 0.68f; // where to look: feet..head
    [SerializeField] private float editCamVerticalOffset = 0f;                    // extra offset from focus

    [SerializeField] private CharacterSpawner characterSpawner;   // drag the one under Character Creator
    [SerializeField] private CenterParkSpawner parkSpawner;       // drag your CenterPark spawner
    [SerializeField] private characterStats.CharacterType addType = characterStats.CharacterType.Side;

    [Header("CC Add Swap")]
    [SerializeField] private float ccSwapSeconds = 0.6f;
    [SerializeField] private float ccSwapSideDistance = 3.0f;     // how far left/right to move
    [SerializeField] private AnimationCurve ccSwapEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    private enum SwapDirection { LeftToRight, RightToLeft }
    [SerializeField] private SwapDirection ccSwapDirection = SwapDirection.LeftToRight;

    private EditorController.EditorState lastState = EditorController.EditorState.CenterPark;

    [SerializeField] private StatsService ccStats;



    private bool addLocked;


    CharacterHandle current;
    GameObject currentIndicator;
    CharacterHandle pausedForEdit;

    GameObject cachedTypeIndicator;
    bool cachedTypeIndicatorPrevActive;

    void Awake()
    {
        if (!controller) controller = FindFirstObjectByType<EditorController>(FindObjectsInactive.Include);
        if (!editorCamera) editorCamera = Camera.main;
        if (controller) lastState = controller.State;
#if UNITY_2023_1_OR_NEWER
        if (!ccStats) ccStats = FindFirstObjectByType<StatsService>(FindObjectsInactive.Include);
#else
        if (!ccStats) ccStats = FindObjectOfType<StatsService>(true);
#endif
        if (closeButton) closeButton.onClick.AddListener(Deselect);
        if (deleteButton) deleteButton.onClick.AddListener(DeleteSelected);
        if (editButton) editButton.onClick.AddListener(EditSelected);
        HidePanel();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // ignore clicks through UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = editorCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, rayMaxDistance, clickMask, QueryTriggerInteraction.Ignore))
            {
                var h = hit.collider.GetComponentInParent<CharacterHandle>();
                if (h != null)
                {
                    // If we are in the Character Creator, clicking another character
                    // should switch to editing them. Otherwise, do a normal selection.
                    if (controller != null && controller.State == EditorController.EditorState.CharacterCreator)
                    {
                        SwitchToCharacterInCC(h);
                    }
                    else
                    {
                        Select(h);
                    }
                }
            }
        }

        // keep indicator sitting above the head
        if (current && currentIndicator)
        {
            Vector3 p = current.GetHeadWorldPosition() + Vector3.up * indicatorYOffset;
            currentIndicator.transform.position = p;
        }
    }

    void Select(CharacterHandle handle)
    {
        if (current == handle) return;

        // clear previous
        CleanupIndicator();

        current = handle;

        // --- NEW: Reposition the panel based on the character's screen position ---
        if (characterInfoPanel && panelPositionLeft && panelPositionRight && editorCamera)
        {
            // 1. Convert character's world position to screen coordinates
            Vector3 screenPos = editorCamera.WorldToScreenPoint(handle.transform.position);

            // 2. Determine if the character is on the right side of the screen
            bool isOnRightSide = screenPos.x > (Screen.width / 2f);

            // 3. Select the target RectTransform preset (left or right)
            RectTransform targetPosition = isOnRightSide ? panelPositionLeft : panelPositionRight;

            // 4. Apply the preset's properties to the info panel
            characterInfoPanel.anchorMin = targetPosition.anchorMin;
            characterInfoPanel.anchorMax = targetPosition.anchorMax;
            characterInfoPanel.pivot = targetPosition.pivot;
            characterInfoPanel.anchoredPosition = targetPosition.anchoredPosition;
            characterInfoPanel.sizeDelta = targetPosition.sizeDelta; // Also copy size if desired
        }
        // --- END NEW CODE ---

        // notify controller (set Selected); safe if controller lacks Deselect()
        if (controller != null && handle.Stats != null)
            controller.Select(handle.Stats);

        // UI
        FillPanel(handle.Stats);
        ShowPanel();

        // visual
        if (selectedIndicatorPrefab)
        {
            currentIndicator = Instantiate(selectedIndicatorPrefab);
            currentIndicator.name = "SelectedIndicator";
            currentIndicator.transform.position = handle.GetHeadWorldPosition() + Vector3.up * indicatorYOffset;
            currentIndicator.transform.SetParent(handle.transform, worldPositionStays: true);
        }
    }

    public void Deselect(bool clearController = true)
    {
        if (clearController && controller != null)
        {
            controller.Deselect();
        }

        current = null;
        CleanupIndicator();
        HidePanel();
    }

    // NEW: parameterless overload so Button.onClick can use it
    public void Deselect() => Deselect(true);


    void CleanupIndicator()
    {
        if (currentIndicator) Destroy(currentIndicator);
        currentIndicator = null;
    }

    // -------- UI helpers --------
    void ShowPanel()
    {
        if (characterInfoPanel) characterInfoPanel.gameObject.SetActive(true); // MODIFIED
        if (addButton) addButton.interactable = false;
    }

    void HidePanel()
    {
        if (characterInfoPanel) characterInfoPanel.gameObject.SetActive(false); // MODIFIED
        if (addButton) addButton.interactable = true;
    }

    void FillPanel(characterStats s)
    {
        if (s == null)
        {
            nameText?.SetText("");
            ageText?.SetText("");
            typeText?.SetText("");
            genderSexText?.SetText("");
            return;
        }

        var p = s.profile;
        string full = ((p.first_name ?? "").Trim() + " " + (p.last_name ?? "").Trim()).Trim();
        if (string.IsNullOrEmpty(full)) full = "Character";

        nameText?.SetText(full);
        ageText?.SetText(p.age.years.ToString());
        typeText?.SetText(s.character_type.ToString());

        string gender = p.gender.ToString();
        string sex = p.sex.ToString();
        genderSexText?.SetText($"{gender} - {sex}");
    }

    public void DeleteSelected()
    {
        if (current == null) return;

        // capture before we clear selection
        var stats = current.Stats;
        var toDestroy = current.gameObject;

        // remove from save
        if (Game.Instance != null && stats != null)
            Game.Instance.UnregisterCharacter(stats);   // updates mains/sides/extras in CurrentSave

        // clear UI/selection (also kills the floating indicator)
        Deselect();

        // finally, destroy the character GO
        if (toDestroy) Destroy(toDestroy);
    }

    public void EditSelected()
    {
        if (current == null || editorCamera == null) return;

        var wander = current.GetComponent<WanderWithinArea>();
        if (wander) { wander.PauseMovement(true); pausedForEdit = current; }

        // NEW: hide this character's type indicator (child named "Indicator")
        var t = current.transform.Find("Indicator");
        if (t)
        {
            cachedTypeIndicator = t.gameObject;
            cachedTypeIndicatorPrevActive = cachedTypeIndicator.activeSelf;
            cachedTypeIndicator.SetActive(false);
        }

        // --- NEW: look at chest/face, not the head/indicator ---
        Vector3 focus = current.GetLookPoint(editCamFocusHeightBias);

        // Use horizontal forward so the camera isn’t tilted up/down by any vertical component.
        Vector3 flatFwd = Vector3.ProjectOnPlane(current.transform.forward, Vector3.up).normalized;
        if (flatFwd.sqrMagnitude < 1e-6f) flatFwd = (editorCamera.transform.position - current.transform.position).normalized;

        Vector3 lookFrom = focus - flatFwd * editCamDistance + Vector3.up * editCamVerticalOffset;
        Quaternion lookRot = Quaternion.LookRotation((focus - lookFrom).normalized, Vector3.up);

        StartCoroutine(MoveCamThenEnterCC(lookFrom, lookRot, current.Stats));

        CleanupIndicator();
    }


    System.Collections.IEnumerator MoveCamThenEnterCC(Vector3 pos, Quaternion rot, characterStats stats)
    {
        Transform cam = editorCamera.transform;
        Vector3 p0 = cam.position; Quaternion r0 = cam.rotation;
        float t = 0f, dur = Mathf.Max(0.01f, editCamSeconds);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = editCamEase.Evaluate(t);
            cam.SetPositionAndRotation(Vector3.LerpUnclamped(p0, pos, e),
                                    Quaternion.SlerpUnclamped(r0, rot, e));
            yield return null;
        }
        cam.SetPositionAndRotation(pos, rot);

        // 4) tell the editor to use this character & open CC
        if (controller != null)
        {
            if (stats != null) controller.Select(stats);               // sets Selected & fires event
            controller.SetState(EditorController.EditorState.CharacterCreator);
        }
    }


    void HandleStateChanged(EditorController.EditorState s)
    {
        var from = lastState;
        lastState = s; // update first so early-returns don't skip it

        if (s == EditorController.EditorState.CharacterCreator)
        {
            Deselect(clearController: false);
            return;
        }

        if (s == EditorController.EditorState.CenterPark)
        {
            // NEW: if we just came from CC, lock the Center Park Add button briefly
            if (from == EditorController.EditorState.CharacterCreator)
                StartCoroutine(TemporarilyDisableCenterParkAdd(ccToParkAddLockSeconds));

            // resume walking if we paused for edit
            if (pausedForEdit)
            {
                var w = pausedForEdit.GetComponent<WanderWithinArea>();
                if (w) w.PauseMovement(false);
                pausedForEdit = null;
            }

            // restore the character type indicator we hid for CC
            if (cachedTypeIndicator)
            {
                cachedTypeIndicator.SetActive(cachedTypeIndicatorPrevActive);
                cachedTypeIndicator = null;
            }

#if UNITY_2023_1_OR_NEWER
            if (!parkSpawner) parkSpawner = FindFirstObjectByType<CenterParkSpawner>(FindObjectsInactive.Include);
#else
            if (!parkSpawner) parkSpawner = FindObjectOfType<CenterParkSpawner>(true);
#endif
            parkSpawner?.RefreshAllIndicators();
            return;
        }

        // World View: clear UI + controller selection
        bool clearController = (s == EditorController.EditorState.WorldView);
        Deselect(clearController);
    }




    System.Collections.IEnumerator RefreshPanelNextFrame()
    {
        yield return null;                 // wait a frame so Center Park UI is active
        ShowPanel();                       // keeps Add disabled
        FillPanel(current.Stats);          // pulls fresh values from the edited stats
    }

    void OnEnable()
    {
        if (!controller) controller = FindFirstObjectByType<EditorController>(FindObjectsInactive.Include);
        if (controller) controller.OnStateChanged += HandleStateChanged;
        if (controller) lastState = controller.State;
    }

    void OnDisable()
    {
        // unsubscribe
        if (controller) controller.OnStateChanged -= HandleStateChanged;

        addLocked = false; if (addButton) addButton.interactable = true;

        // HARD CLOSE UI when leaving Center Park
        current = null;          // drop local selection
        CleanupIndicator();      // destroy floating indicator if any
        HidePanel();             // hide the character info panel
    }

    void ToggleSelectedIndicator(bool show)
    {
        if (currentIndicator) currentIndicator.SetActive(show);
    }

    public void AddNewCharacter()
    {
        if (addLocked) return;

        if (!characterSpawner)
#if UNITY_2023_1_OR_NEWER
            characterSpawner = FindFirstObjectByType<CharacterSpawner>(FindObjectsInactive.Include);
#else
            characterSpawner = FindObjectOfType<CharacterSpawner>(true);
#endif

        var stats = characterSpawner.CreateRandomCharacter(addType);

        System.Collections.IEnumerator routine =
            (controller && controller.State == EditorController.EditorState.CharacterCreator)
            ? SwapInNewCharacterWhileInCC(stats)
            : AddThenEditRoutine(stats);

        StartCoroutine(WithAddLock(routine));
    }


    System.Collections.IEnumerator AddThenEditRoutine(characterStats stats)
    {
        // get/create the park avatar (same path you use elsewhere)
        if (!parkSpawner) parkSpawner = FindFirstObjectByType<CenterParkSpawner>(FindObjectsInactive.Include);
        var handle = parkSpawner ? parkSpawner.GetOrSpawnHandle(stats) : null;

        if (!handle)
        {
            controller.Select(stats);
            controller.SetState(EditorController.EditorState.CharacterCreator);
            yield break;
        }

        // pause wander + clear any floating bits
        current = handle;
        var wander = current.GetComponent<WanderWithinArea>();
        if (wander) { wander.PauseMovement(true); pausedForEdit = current; }
        CleanupIndicator();
        var typeInd = current.transform.Find("Indicator");
        if (typeInd) typeInd.gameObject.SetActive(false);

        // IMPORTANT: wait one frame so CC, bounds, and anchors are initialized
        yield return null;

        // aim camera exactly like Edit
        Vector3 focus = current.GetLookPoint(editCamFocusHeightBias);
        Vector3 flatFwd = Vector3.ProjectOnPlane(current.transform.forward, Vector3.up).normalized;
        if (flatFwd.sqrMagnitude < 1e-6f)
            flatFwd = (editorCamera.transform.position - current.transform.position).normalized;

        Vector3 lookFrom = focus - flatFwd * editCamDistance + Vector3.up * editCamVerticalOffset;
        Quaternion lookRot = Quaternion.LookRotation((focus - lookFrom).normalized, Vector3.up);

        controller.Select(stats); // ensure CC edits this one
        yield return MoveCamThenEnterCC(lookFrom, lookRot, stats);
    }

    System.Collections.IEnumerator SwapInNewCharacterWhileInCC(characterStats stats)
    {
        // who is currently on stage? (the one we paused when entering CC)
        var prev = pausedForEdit;
#if UNITY_2023_1_OR_NEWER
        if (!parkSpawner) parkSpawner = FindFirstObjectByType<CenterParkSpawner>(FindObjectsInactive.Include);
#else
        if (!parkSpawner) parkSpawner = FindObjectOfType<CenterParkSpawner>(true);
#endif
        if (!prev || !parkSpawner)
        {
            yield return AddThenEditRoutine(stats);
            yield break;
        }

        var incoming = parkSpawner.GetOrSpawnHandle(stats);
        if (!incoming)
        {
            yield return AddThenEditRoutine(stats);
            yield break;
        }
        var inWander = incoming.GetComponent<WanderWithinArea>(); if (inWander) inWander.PauseMovement(true);

        // Hide type indicators in CC
        var pInd = prev.transform.Find("Indicator"); if (pInd) pInd.gameObject.SetActive(false);
        var nInd = incoming.transform.Find("Indicator"); if (nInd) nInd.gameObject.SetActive(false);

        // Stage positions (use camera right/left)
        Transform cam = editorCamera.transform;
        Vector3 right = Vector3.Cross(Vector3.up, cam.forward).normalized;
        Vector3 target = prev.transform.position;

        float dir = (ccSwapDirection == SwapDirection.LeftToRight) ? 1f : -1f;

        // If LeftToRight: prev exits right (+), new enters from left (-)
        // If RightToLeft: prev exits left (-), new enters from right (+)
        Vector3 prevEnd = target + right * ccSwapSideDistance * dir;
        Vector3 newStart = target - right * ccSwapSideDistance * dir;

        // Place the newcomer at the start
        incoming.transform.position = newStart;
        incoming.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, Vector3.up);

        // Animate both
        float t = 0f; float dur = Mathf.Max(0.01f, ccSwapSeconds);
        Vector3 prevStart = target;
        Vector3 newEnd = target;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = ccSwapEase.Evaluate(t);

            // move
            prev.transform.position = Vector3.LerpUnclamped(prevStart, prevEnd, e);
            incoming.transform.position = Vector3.LerpUnclamped(newStart, newEnd, e);

            // face the camera softly
            Quaternion face = Quaternion.LookRotation(Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized, Vector3.up);
            prev.transform.rotation = Quaternion.Slerp(prev.transform.rotation, face, Time.deltaTime * 6f);
            incoming.transform.rotation = Quaternion.Slerp(incoming.transform.rotation, face, Time.deltaTime * 6f);

            yield return null;
        }
        prev.transform.position = prevEnd;
        incoming.transform.position = newEnd;

        // Old one resumes wander; new one stays paused for editing
        var prevWander = prev.GetComponent<WanderWithinArea>();
        if (prevWander) prevWander.PauseMovement(false);

        // Make the new one the staged/paused target
        pausedForEdit = incoming;
        current = null;                // no selection panel in CC
        CleanupIndicator();            // ensure no floating selection ring

        // Swap the selected stats to the new character (CC UI will update via your binder)
        controller.Select(stats);
        ccStats?.SetTarget(stats);            // <- make CC edits affect the new model immediately
        ccStats?.RefreshUIBindings();
    }

    private System.Collections.IEnumerator WithAddLock(System.Collections.IEnumerator routine)
    {
        if (addLocked) yield break;                 // guard against re-entry
        addLocked = true;
        if (addButton) addButton.interactable = false;
        if (addButtonCC) addButtonCC.interactable = false;

        yield return StartCoroutine(routine);       // wait until the routine truly finishes

        addLocked = false;
        if (addButton) addButton.interactable = true;
        if (addButtonCC) addButtonCC.interactable = true;
    }

    private System.Collections.IEnumerator TemporarilyDisableCenterParkAdd(float seconds)
    {
        if (addButton) addButton.interactable = false;
        if (addButtonCC) addButtonCC.interactable = false;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime; // unscaled so it still works during pause/timeScale changes
            yield return null;
        }

        // Only re-enable if we're actually in Center Park and not mid "add" lock
        if (controller && controller.State == EditorController.EditorState.CenterPark && !addLocked && addButton)
            addButton.interactable = true;
        addButtonCC.interactable = true;
    }
    
    private void SwitchToCharacterInCC(CharacterHandle newCharacter)
    {
        // Don't do anything if we clicked the character already being edited.
        if (newCharacter == pausedForEdit) return;

        StartCoroutine(SwitchCharacterRoutine(newCharacter));
    }

    private System.Collections.IEnumerator SwitchCharacterRoutine(CharacterHandle newCharacter)
    {
        // 1. Resume the previously edited character's movement.
        if (pausedForEdit != null)
        {
            var oldWanderer = pausedForEdit.GetComponent<WanderWithinArea>();
            if (oldWanderer) oldWanderer.PauseMovement(false);

            // Restore its type indicator if it was hidden.
            if (cachedTypeIndicator)
            {
                cachedTypeIndicator.SetActive(cachedTypeIndicatorPrevActive);
                cachedTypeIndicator = null;
            }
        }

        // 2. Pause the new character and hide its type indicator for a clean view.
        var newWanderer = newCharacter.GetComponent<WanderWithinArea>();
        if (newWanderer) newWanderer.PauseMovement(true);

        var newIndicatorTransform = newCharacter.transform.Find("Indicator");
        if (newIndicatorTransform)
        {
            cachedTypeIndicator = newIndicatorTransform.gameObject;
            cachedTypeIndicatorPrevActive = cachedTypeIndicator.activeSelf;
            cachedTypeIndicator.SetActive(false);
        }

        // 3. Animate the camera to focus on the new character.
        // This logic is mirrored from the EditSelected() method.
        Vector3 focus = newCharacter.GetLookPoint(editCamFocusHeightBias);
        Vector3 flatFwd = Vector3.ProjectOnPlane(newCharacter.transform.forward, Vector3.up).normalized;
        if (flatFwd.sqrMagnitude < 1e-6f) 
            flatFwd = (editorCamera.transform.position - newCharacter.transform.position).normalized;

        Vector3 lookFrom = focus - flatFwd * editCamDistance + Vector3.up * editCamVerticalOffset;
        Quaternion lookRot = Quaternion.LookRotation((focus - lookFrom).normalized, Vector3.up);

        Transform cam = editorCamera.transform;
        Vector3 p0 = cam.position; 
        Quaternion r0 = cam.rotation;
        float t = 0f, dur = Mathf.Max(0.01f, editCamSeconds);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = editCamEase.Evaluate(t);
            cam.SetPositionAndRotation(Vector3.LerpUnclamped(p0, lookFrom, e), 
                                       Quaternion.SlerpUnclamped(r0, lookRot, e));
            yield return null;
        }
        cam.SetPositionAndRotation(lookFrom, lookRot);

        // 4. Update the controller and our internal 'pausedForEdit' reference.
        pausedForEdit = newCharacter;
        if (controller != null)
        {
            controller.Select(newCharacter.Stats);
        }
    }


}
