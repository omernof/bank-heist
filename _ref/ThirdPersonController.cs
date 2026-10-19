using UnityEngine;
using UnityEngine.InputSystem;

// HW3 - a small third-person player: WASD movement, mouse look, an orbiting
// camera on a child "CameraRig" that pulls in when a wall is behind the player
// (so it works indoors too), and it drives the Mixamo Animator's "Speed"
// parameter so the Idle/Walk clips switch. Uses the new Input System's low-level
// Keyboard/Mouse API so no action-asset wiring is needed.
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonController : MonoBehaviour
{
    public static ThirdPersonController Instance;

    [Header("Move / look")]
    public float moveSpeed = 3.2f;
    public float walkAnimSpeed = 1.45f;   // metres/sec the Mixamo walk clip covers at 1x
    public float mouseYaw = 0.12f;
    public float mousePitch = 0.10f;
    public float minPitch = -25f;
    public float maxPitch = 60f;
    public float gravity = -20f;

    [Header("Camera")]
    public float camDistance = 3.4f;
    public float camHeight = 0.35f;

    [Header("Refs (set by the builder)")]
    public Transform cameraRig;
    public Camera cam;

    CharacterController _cc;
    Animator _anim;
    float _pitch = 8f;
    float _vy;

    bool _duplicate;

    void Awake()
    {
        // The house interior is its own scene with its own player (so it can be
        // played on its own). When it is loaded INTO the village, the village
        // player already exists - switch this copy off before it grabs the
        // cursor or the camera.
        if (Instance != null && Instance != this)
        {
            _duplicate = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _cc = GetComponent<CharacterController>();
        _anim = GetComponent<Animator>();
    }

    void OnEnable()  { if (!_duplicate) LockCursor(true); }
    void OnDisable() { if (!_duplicate) LockCursor(false); }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        // ---- look ----
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue();
            transform.Rotate(0f, d.x * mouseYaw, 0f, Space.World);
            _pitch = Mathf.Clamp(_pitch - d.y * mousePitch, minPitch, maxPitch);
            if (cameraRig != null)
                cameraRig.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // ---- move (relative to where the player faces) ----
        float ix = 0f, iz = 0f;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) iz += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) iz -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ix += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) ix -= 1f;
        }
        Vector3 wish = transform.forward * iz + transform.right * ix;
        if (wish.sqrMagnitude > 1f) wish.Normalize();
        Vector3 horizontal = wish * moveSpeed;

        if (_cc.isGrounded && _vy < 0f) _vy = -2f;
        _vy += gravity * Time.deltaTime;

        _cc.Move((horizontal + Vector3.up * _vy) * Time.deltaTime);

        if (_anim != null)
        {
            _anim.SetFloat("Speed", horizontal.magnitude, 0.1f, Time.deltaTime);
            // play the walk cycle faster when moving faster, so the legs keep up
            // with the ground instead of sliding (the clip's natural pace is
            // ~walkAnimSpeed m/s); normal speed when standing still
            float want = horizontal.sqrMagnitude > 0.01f
                ? Mathf.Clamp(horizontal.magnitude / walkAnimSpeed, 1f, 2.5f)
                : 1f;
            _anim.speed = Mathf.MoveTowards(_anim.speed, want, 6f * Time.deltaTime);
        }

        if (kb != null && kb.escapeKey.wasPressedThisFrame)
            LockCursor(Cursor.lockState != CursorLockMode.Locked);
    }

    void LateUpdate()
    {
        if (cameraRig == null || cam == null) return;

        Vector3 pivot = cameraRig.TransformPoint(new Vector3(0f, camHeight, 0f));
        Vector3 wantLocal = new Vector3(0f, camHeight, -camDistance);
        Vector3 wantWorld = cameraRig.TransformPoint(wantLocal);

        float dist = camDistance;
        Vector3 dir = (wantWorld - pivot).normalized;
        if (Physics.SphereCast(pivot, 0.2f, dir, out RaycastHit hit, camDistance,
                               ~0, QueryTriggerInteraction.Ignore))
        {
            // don't let the player's own collider pull the camera to zero
            if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                dist = Mathf.Clamp(hit.distance - 0.15f, 0.6f, camDistance);
        }
        cam.transform.position = pivot + dir * dist;
        cam.transform.rotation = cameraRig.rotation;
    }

    // True on the frame SPACE is pressed - the interactables poll this.
    public bool InteractPressed()
    {
        var kb = Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
