using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public enum EditorView { CenterPark, WorldView }

public class EditorViewSwitcher : MonoBehaviour
{
    [Header("Controller")]
    [SerializeField] private EditorController controller; // assign; auto-found if null

    [Header("UI")]
    [SerializeField] private Button centerParkButton;
    [SerializeField] private Button worldViewButton;
    [Tooltip("Optional: knob that slides between the two tabs")]
    [SerializeField] private RectTransform knob;
    [SerializeField] private RectTransform knobPosCenterPark;
    [SerializeField] private RectTransform knobPosWorldView;
    [SerializeField] private float knobAnim = 0.12f;

    [Header("Roots (only used if controller is null)")]
    [SerializeField] private GameObject ccRoot;
    [SerializeField] private GameObject centerParkRoot;
    [SerializeField] private GameObject worldViewRoot;

    [Header("Camera (non-Cinemachine)")]
    [SerializeField] private Camera editorCamera;
    [SerializeField] private Transform camTargetCenterPark;
    [SerializeField] private Transform camTargetWorldView;
    [SerializeField] private float camMoveSeconds = 0.35f;
    [SerializeField] private AnimationCurve camEase = AnimationCurve.EaseInOut(0, 0, 1, 1);
    Coroutine camMoveCo;

    [Header("Center Park Tilt")]
    [SerializeField] private bool enableCameraTilt = true;
    [Tooltip("How responsive the camera tilt is to mouse movement.")]
    [SerializeField] private float tiltSpeed = 4f;
    [Tooltip("The maximum angle the camera can tilt in any direction.")]
    [SerializeField] private float maxTiltAngle = 5f;

    [Header("Optional: Cinemachine (leave null if unused)")]
    [SerializeField] private MonoBehaviour centerParkVCam; // CinemachineVirtualCamera
    [SerializeField] private MonoBehaviour worldViewVCam;  // CinemachineVirtualCamera

    public EditorView Current { get; private set; } = EditorView.CenterPark;

    // NEW: Flag to ignore the first selection event on load
    private bool _hasHandledInitialSelection = false;

    void Awake()
    {
        if (!controller) controller = FindObjectOfType<EditorController>(true);

        centerParkButton.onClick.AddListener(() =>
        {
            if (controller) controller.SetState(EditorController.EditorState.CenterPark);
            else SetView(EditorView.CenterPark, true);
        });

        worldViewButton.onClick.AddListener(() =>
        {
            if (controller) controller.SetState(EditorController.EditorState.WorldView);
            else SetView(EditorView.WorldView, true);
        });

        if (controller)
        {
            controller.OnStateChanged += HandleControllerStateChanged;
            controller.OnSelectedChanged += HandleSelectionChanged;
            var mapped = Map(controller.State);
            if (mapped.HasValue) Current = mapped.Value;
            
            UpdateKnobVisibility(controller.State);
            // MODIFIED: We no longer check the initial state here. Tilt is on by default.
            // _isCharacterSelected = controller.Selected != null; // This line was removed.
        }

        Apply(Current, animate: false);
    }

    void OnDestroy()
    {
        if (controller)
        {
            controller.OnStateChanged -= HandleControllerStateChanged;
            controller.OnSelectedChanged -= HandleSelectionChanged;
        }
    }

    void Update()
    {
        // Only in Center Park, not animating, and not inside CC
        if (Current != EditorView.CenterPark) return;
        if (camMoveCo != null) return;
        if (controller != null &&
            controller.State == EditorController.EditorState.CharacterCreator) return;

        // Keep updating the tilt so easing finishes even after the mouse stops.
        HandleCameraTilt();
    }
        
    private void HandleSelectionChanged(characterStats stats)
    {
        // MODIFIED: This logic now ignores the first event call.
        if (!_hasHandledInitialSelection)
        {
            _hasHandledInitialSelection = true;
            return; // Ignore the automatic selection on load
        }
    }

    void HandleControllerStateChanged(EditorController.EditorState s)
    {
        UpdateKnobVisibility(s);
        
        var mapped = Map(s);
        if (!mapped.HasValue) return;
        Current = mapped.Value;
        Apply(Current, animate: true);
    }

    EditorView? Map(EditorController.EditorState s)
    {
        switch (s)
        {
            case EditorController.EditorState.CenterPark: return EditorView.CenterPark;
            case EditorController.EditorState.WorldView: return EditorView.WorldView;
            default: return null; // CharacterCreator is handled elsewhere
        }
    }
    
    // ... (rest of the script is unchanged) ...

    public void SetView(EditorView view, bool animate = true)
    {
        if (view == Current) return;
        Current = view;
        Apply(view, animate);
    }

    void Apply(EditorView view, bool animate)
    {
        bool center = view == EditorView.CenterPark;

        if (!controller)
        {
            if (ccRoot) ccRoot.SetActive(false);
            if (centerParkRoot) centerParkRoot.SetActive(center);
            if (worldViewRoot) worldViewRoot.SetActive(!center);
        }

        if (knob && knobPosCenterPark && knobPosWorldView)
        {
            Vector2 target = (center ? knobPosCenterPark : knobPosWorldView).anchoredPosition;
            if (animate) StartCoroutine(AnimateKnob(target));
            else knob.anchoredPosition = target;
        }

        if (centerParkVCam != null && worldViewVCam != null)
        {
            SetPriority(centerParkVCam, center ? 20 : 10);
            SetPriority(worldViewVCam, center ? 10 : 20);
        }
        else if (editorCamera && camTargetCenterPark && camTargetWorldView)
        {
            Transform t = center ? camTargetCenterPark : camTargetWorldView;
            StartCameraMove(editorCamera.transform, t, animate ? camMoveSeconds : 0f);
        }
    }

    IEnumerator AnimateKnob(Vector2 target)
    {
        Vector2 start = knob.anchoredPosition;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / knobAnim;
            knob.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            yield return null;
        }
        knob.anchoredPosition = target;
    }

    void StartCameraMove(Transform cam, Transform target, float dur)
    {
        if (camMoveCo != null) StopCoroutine(camMoveCo);
        camMoveCo = StartCoroutine(MoveCamRoutine(cam, target, dur));
    }

    IEnumerator MoveCamRoutine(Transform cam, Transform target, float dur)
    {
        if (dur <= 0f)
        {
            cam.SetPositionAndRotation(target.position, target.rotation);
            camMoveCo = null;
            yield break;
        }
        Vector3 p0 = cam.position; Quaternion r0 = cam.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float e = camEase.Evaluate(t);
            cam.SetPositionAndRotation(Vector3.LerpUnclamped(p0, target.position, e),
                                       Quaternion.SlerpUnclamped(r0, target.rotation, e));
            yield return null;
        }
        cam.SetPositionAndRotation(target.position, target.rotation);
        camMoveCo = null;
    }

    void HandleCameraTilt()
    {
        if (!enableCameraTilt || camTargetCenterPark == null || editorCamera == null)
            return;

        float mouseX = (Input.mousePosition.x / Screen.width) * 2 - 1;
        float mouseY = (Input.mousePosition.y / Screen.height) * 2 - 1;

        float yaw = mouseX * maxTiltAngle;
        float pitch = -mouseY * maxTiltAngle; 

        Quaternion baseRotation = camTargetCenterPark.rotation;
        Quaternion tiltOffset = Quaternion.Euler(pitch, yaw, 0);
        Quaternion targetRotation = baseRotation * tiltOffset;

        editorCamera.transform.rotation = Quaternion.Slerp(
            editorCamera.transform.rotation,
            targetRotation,
            Time.deltaTime * tiltSpeed
        );
    }

    void SetPriority(MonoBehaviour vcam, int priority)
    {
        var prop = vcam.GetType().GetProperty("Priority");
        if (prop != null) prop.SetValue(vcam, priority, null);
    }
    
    void UpdateKnobVisibility(EditorController.EditorState s)
    {
        if (!knob) return;
        knob.gameObject.SetActive(s != EditorController.EditorState.CharacterCreator);
    }
}
