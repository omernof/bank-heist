using UnityEngine;
using UnityEngine.InputSystem;

// GTA-style third-person player:
//  * WASD moves relative to the camera, the character turns to face where it
//    walks; SHIFT sprints; C gets down (bystanders) / crouch-walks.
//  * Right mouse = aim: camera tightens over the right shoulder, the character
//    faces the crosshair and strafes. Left mouse fires, R reloads, G throws a
//    grenade at the crosshair point.
//  * E uses the nearest Interactable (hold for timed ones), F enters vehicles.
//  * 1-5 or the TAB command wheel give orders to the NPC crew.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance;

    public float walkSpeed = 3.0f, runSpeed = 6.2f, aimSpeed = 2.3f;
    public float mouseSens = 0.11f;
    public Camera cam;

    CharacterController _cc;
    Animator _anim;
    Health _health;
    Weapon _weapon;
    float _yaw, _pitch = 10f, _vy;
    float _camDist = 3.6f, _shoulder = 0.3f, _fov = 60f;
    float _reloadUntil, _holdT;
    bool _reloadPending;
    Vector3 _lastGround;
    Vector3 _moveVel;
    float _animSpeed, _pivotY, _colDist = 3.6f;
    Interactable _focus;
    public bool Aiming { get; private set; }
    public bool Down { get; private set; }
    public bool InVehicle { get; set; }
    public Vector3 AimPoint { get; private set; }

    void Awake()
    {
        Instance = this;
        _cc = GetComponent<CharacterController>();
        _anim = GetComponentInChildren<Animator>();
        _health = GetComponent<Health>();
        _weapon = GetComponentInChildren<Weapon>();
        _yaw = transform.eulerAngles.y;
        _lastGround = transform.position;
        _pivotY = transform.position.y;
    }

    void OnEnable() => Lock(true);
    void OnDisable() => Lock(false);

    static void Lock(bool on)
    {
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }

    bool Armed => _weapon != null && GameData.side != Side.Bystander;

    void Update()
    {
        if (_health != null && _health.Dead) return;
        if (Hud.Blocking) return;                  // mission-end banner / menu up
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.escapeKey.wasPressedThisFrame) Lock(Cursor.lockState != CursorLockMode.Locked);

        // ---- command wheel (hold TAB) freezes look while choosing ----
        bool wheel = kb.tabKey.isPressed && GameData.side != Side.Bystander;
        if (wheel)
        {
            Hud.ShowWheel(mouse.delta.ReadValue());
            if (InVehicle) return;
        }
        else if (Hud.WheelOpen)
        {
            int pick = Hud.CloseWheel();
            if (pick >= 0) GiveOrder((Order)pick);
        }

        if (InVehicle) return;

        // ---- look ----
        if (!wheel && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 d = mouse.delta.ReadValue();
            _yaw += d.x * mouseSens;
            _pitch = Mathf.Clamp(_pitch - d.y * mouseSens, -35f, 65f);
        }

        Aiming = Armed && mouse.rightButton.isPressed && !Down;
        HeistAnimInfo.Bool(_anim, "Aim", Aiming);

        // ---- move ----
        Vector2 input = Vector2.zero;
        if (kb.wKey.isPressed) input.y += 1; if (kb.sKey.isPressed) input.y -= 1;
        if (kb.dKey.isPressed) input.x += 1; if (kb.aKey.isPressed) input.x -= 1;
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (kb.cKey.wasPressedThisFrame) SetDown(!Down);
        if (Down && input.sqrMagnitude > 0.1f && GameData.side == Side.Bystander) SetDown(false);

        Quaternion camYaw = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 wish = camYaw * new Vector3(input.x, 0f, input.y);
        bool sprint = kb.leftShiftKey.isPressed && !Aiming && input.y > 0.1f;
        // speeds come from the measured clip paces (GaitSettings) so the feet
        // match the ground: W = jog (GTA default), SHIFT = sprint, aiming = walk
        var gait = GaitSettings.I;
        bool armedGait = GameData.side != Side.Bystander;
        float gWalk = armedGait ? gait.armedWalk : gait.civWalk, gRun = armedGait ? gait.armedRun : gait.civRun;
        float jog = armedGait ? gRun : gWalk * 1.1f;
        float speed = Down ? 0f : Aiming ? gWalk : sprint ? gRun * (armedGait ? 1.4f : 1f) : jog;
        // ease into / out of the target speed instead of snapping 0 -> run in one frame
        Vector3 target = wish * speed;
        _moveVel = Vector3.MoveTowards(_moveVel, target, (target.sqrMagnitude >= _moveVel.sqrMagnitude ? 14f : 22f) * Time.deltaTime);
        Vector3 vel = _moveVel;

        if (Aiming) transform.rotation = Quaternion.Slerp(transform.rotation, camYaw, 20f * Time.deltaTime);
        else if (wish.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), 10f * Time.deltaTime);

        if (_cc.isGrounded && _vy < 0f) _vy = -2f;
        // safety net: fell out of the level -> back to the last ground we stood on
        if (_cc.isGrounded) _lastGround = transform.position;
        else if (transform.position.y < _lastGround.y - 25f)
        {
            _cc.enabled = false; transform.position = _lastGround + Vector3.up * 0.2f; _cc.enabled = true; _vy = 0f;
        }
        _vy += -20f * Time.deltaTime;
        if (_cc.enabled) _cc.Move((vel + Vector3.up * _vy) * Time.deltaTime);

        // the controller's velocity jitters on curbs / props: feed the animator a smoothed speed
        float planar = new Vector3(_cc.velocity.x, 0f, _cc.velocity.z).magnitude;
        _animSpeed = Mathf.Lerp(_animSpeed, planar, 12f * Time.deltaTime);
        planar = _animSpeed;
        HeistAnimInfo.Float(_anim, "Speed", planar);
        if (_anim != null) _anim.speed = GaitSettings.PlaybackRate(planar, gRun);

        // ---- aim point = what the crosshair covers ----
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        AimPoint = ray.origin + ray.direction * 100f;
        foreach (var h in Physics.RaycastAll(ray, 150f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if ((h.point - ray.origin).sqrMagnitude < (AimPoint - ray.origin).sqrMagnitude) AimPoint = h.point;
        }

        // ---- fire / reload / grenade ----
        if (Armed)
        {
            // refill once the reload time is up (a flag, not a time window, so a
            // slow frame can't skip past it and lose the reload)
            if (_reloadPending && Time.time >= _reloadUntil)
            {
                _reloadPending = false;
                int got = Mathf.Min(GameData.clipSize - GameData.ammoInClip, GameData.ammoReserve);
                GameData.ammoInClip += got; GameData.ammoReserve -= got;
            }
            bool reloading = _reloadPending;
            if (!reloading && (kb.rKey.wasPressedThisFrame || GameData.ammoInClip == 0) && GameData.ammoReserve > 0 && GameData.ammoInClip < GameData.clipSize)
            {
                _reloadUntil = Time.time + 1.6f;
                _reloadPending = reloading = true;
                Sfx.Play("reload", transform.position, 0.8f);
                Hud.Notify("Reloading...", Hud.Info, 1.2f);
            }
            if (mouse.leftButton.isPressed && !reloading && GameData.ammoInClip > 0 && !wheel)
            {
                Vector3 origin = cam.transform.position + cam.transform.forward * 1.2f;
                float spread = Aiming ? 0f : 2.2f;
                if (sprint) spread += 2f;
                if (_weapon.Fire(origin, AimPoint - origin, _health, spread))
                {
                    GameData.ammoInClip--;
                    HeistAnimInfo.Trigger(_anim, "Fire");
                    _pitch -= 0.35f;                          // recoil
                    if (!Aiming) transform.rotation = camYaw;  // hip fire turns you to the target
                }
            }
            if (kb.gKey.wasPressedThisFrame && GameData.grenades > 0)
            {
                GameData.grenades--;
                HeistAnimInfo.Trigger(_anim, "Throw");
                Grenade.Throw(transform.position + Vector3.up * 1.7f + transform.forward * 0.5f, AimPoint, _health);
            }
        }

        // ---- orders to the crew: 1..5 ----
        if (GameData.side != Side.Bystander)
        {
            if (kb.digit1Key.wasPressedThisFrame) GiveOrder(Order.Follow);
            if (kb.digit2Key.wasPressedThisFrame) GiveOrder(Order.Attack);
            if (kb.digit3Key.wasPressedThisFrame) GiveOrder(Order.Grenade);
            if (kb.digit4Key.wasPressedThisFrame) GiveOrder(Order.Retreat);
            if (kb.digit5Key.wasPressedThisFrame) GiveOrder(Order.Hold);
        }

        // ---- interact ----
        _focus = NearestInteractable();
        if (_focus != null)
        {
            Hud.Prompt(_focus.Prompt, _focus.holdSeconds > 0f ? _holdT / _focus.holdSeconds : -1f);
            bool press = kb.eKey.wasPressedThisFrame || (kb.fKey.wasPressedThisFrame && _focus is Vehicle);
            if (_focus.holdSeconds > 0f)
            {
                if (kb.eKey.isPressed) { _holdT += Time.deltaTime; if (_holdT >= _focus.holdSeconds) { _holdT = 0f; _focus.Use(this); } }
                else _holdT = 0f;
            }
            else if (press) _focus.Use(this);
        }
        else _holdT = 0f;
    }

    void LateUpdate()
    {
        if (cam == null || InVehicle) return;
        float wantDist = Aiming ? 1.7f : Down ? 2.6f : 3.6f;
        float wantShoulder = Aiming ? 0.55f : 0.3f;
        _camDist = Mathf.Lerp(_camDist, wantDist, 12f * Time.deltaTime);
        _shoulder = Mathf.Lerp(_shoulder, wantShoulder, 12f * Time.deltaTime);
        _fov = Mathf.Lerp(_fov, Aiming ? 48f : 62f, 10f * Time.deltaTime);
        cam.fieldOfView = _fov;

        Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
        // follow height smoothly so stepping up a curb doesn't jolt the view
        _pivotY = Mathf.Abs(transform.position.y - _pivotY) > 2f ? transform.position.y : Mathf.Lerp(_pivotY, transform.position.y, 14f * Time.deltaTime);
        Vector3 pivot = new Vector3(transform.position.x, _pivotY, transform.position.z) + Vector3.up * (Down ? 0.6f : 1.6f) + rot * Vector3.right * _shoulder;
        Vector3 back = rot * Vector3.back;
        float dist = _camDist;
        // pull in for walls/props only - never for people (crew, NPCs, yourself)
        foreach (var hit in Physics.SphereCastAll(pivot, 0.2f, back, _camDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Health>() != null || hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
            if (hit.distance > 0f) dist = Mathf.Min(dist, Mathf.Clamp(hit.distance - 0.1f, 0.4f, _camDist));
        }
        // pull in at once (never through a wall) but ease back out, so passing a tree or
        // lamp post doesn't make the camera pop in and out
        _colDist = dist < _colDist ? dist : Mathf.Lerp(_colDist, dist, 5f * Time.deltaTime);
        cam.transform.position = pivot + back * _colDist + rot * CameraShake.Offset;
        cam.transform.rotation = rot;
    }

    public void GiveOrder(Order o)
    {
        var sq = Squad.Of(GameData.PlayerTeam);
        Vector3 pt = o == Order.Hold ? transform.position : AimPoint;
        sq.leader = transform;
        sq.Command(o, pt);
        Sfx.Play("radio", transform.position, 0.5f, 0f);
    }

    public void SetDown(bool down)
    {
        if (down == Down) return;
        Down = down;
        HeistAnimInfo.Bool(_anim, "Down", down);
        _cc.height = down ? 0.8f : 1.8f;
        _cc.center = new Vector3(0f, down ? 0.4f : 0.9f, 0f);
        // without a lie-down clip, tip the model flat (it's a child "Model")
        var model = _anim != null ? _anim.transform : null;
        if (model != null && model != transform && !HeistAnimInfo.HasParam(_anim, "Down"))
        {
            model.localRotation = down ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            model.localPosition = down ? new Vector3(0f, 0.2f, -0.8f) : Vector3.zero;
        }
        if (GameData.side == Side.Bystander) HeistDirector.Instance?.OnPlayerDown(down);
    }

    Interactable NearestInteractable()
    {
        Interactable best = null; float bd = float.MaxValue;
        foreach (var i in Interactable.All)
        {
            if (i == null || !i.CanUse) continue;
            float d = Vector3.Distance(transform.position, i.transform.position);
            if (d < i.range && d < bd) { bd = d; best = i; }
        }
        return best;
    }

    public float Yaw { get => _yaw; set => _yaw = value; }
}
