using UnityEngine;
using UnityEngine.InputSystem;

// Arcade GTA-style car: raycast suspension on four corners, engine force,
// grip that kills sideways sliding, speed-sensitive steering, handbrake (SPACE)
// and a chase camera. F gets in / out (the player's model is hidden while driving).
[RequireComponent(typeof(Rigidbody))]
public class Vehicle : Interactable
{
    public static Vehicle Current;

    public Transform[] wheels = new Transform[4];   // FL, FR, RL, RR visuals (optional)
    public Vector3 halfExtents = new Vector3(0.95f, 0.4f, 2.1f);
    public float suspension = 0.55f, springK = 42000f, damper = 3800f, wheelRadius = 0.36f;
    public float rideHeight = 0.75f;          // height of the spring mounts above the car's origin
    public float engine = 16000f, maxSpeed = 34f, steerDeg = 34f, grip = 7.5f;
    public string displayName = "getaway car";
    public bool isGetaway = true;

    Rigidbody _rb;
    float _steer, _wheelSpin;
    bool _driving;
    Camera _cam;
    float _camYaw;
    AudioSource _engineSrc;

    public override string Prompt => _driving ? "" : $"Press F to get in the {displayName}";
    public override bool CanUse => !_driving && isGetaway && GameData.side == Side.Robber;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.mass = 1400f;
        _rb.centerOfMass = new Vector3(0f, -0.35f, 0.1f);
        _rb.linearDamping = 0.05f; _rb.angularDamping = 1.5f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        range = 3.4f;
    }

    public override void Use(PlayerController p)
    {
        if (_driving) return;
        _driving = true;
        Current = this;
        p.InVehicle = true;
        foreach (var r in p.GetComponentsInChildren<Renderer>()) r.enabled = false;
        var cc = p.GetComponent<CharacterController>(); if (cc != null) cc.enabled = false;
        _cam = p.cam;
        _camYaw = transform.eulerAngles.y;
        if (_engineSrc == null)
        {
            var clip = Sfx.Clip("engine");
            if (clip != null)
            {
                _engineSrc = gameObject.AddComponent<AudioSource>();
                _engineSrc.clip = clip; _engineSrc.loop = true; _engineSrc.spatialBlend = 0.6f; _engineSrc.volume = 0.5f;
                _engineSrc.Play();
            }
        }
        Sfx.Play("door_car", transform.position, 0.8f);
        HeistDirector.Instance?.OnEnteredVehicle(this);
        // the crew pile in after you
        foreach (var m in Squad.Of(GameData.PlayerTeam).members.ToArray())
            if (m != null && !m.Health.Dead && !m.isLeader) m.Board(this);
    }

    // crew riding along (inactive while aboard) and crew still running over to get in
    readonly System.Collections.Generic.List<SoldierAI> _passengers = new System.Collections.Generic.List<SoldierAI>();
    public bool Driving => _driving;

    public void TakeAboard(SoldierAI m)
    {
        if (_passengers.Contains(m)) return;
        _passengers.Add(m);
        Sfx.Play("door_car", transform.position, 0.6f);
        m.gameObject.SetActive(false);
    }

    void DropPassengers()
    {
        int i = 0;
        foreach (var m in _passengers)
        {
            if (m == null) continue;
            // out of the doors on both sides and the back
            Vector3 at = transform.position + (i == 0 ? transform.right * 1.9f : i == 1 ? transform.right * 1.9f - transform.forward * 1.6f : -transform.forward * (halfExtents.z + 1.4f)) + Vector3.up * 0.2f;
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            m.transform.position = at;
            m.transform.rotation = transform.rotation;
            m.gameObject.SetActive(true);
            m.LeftVehicle(at);
            i++;
        }
        _passengers.Clear();
    }

    void Exit()
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        _driving = false; Current = null;
        p.InVehicle = false;
        p.transform.position = transform.position - transform.right * 1.8f + Vector3.up * 0.2f;
        foreach (var r in p.GetComponentsInChildren<Renderer>()) r.enabled = true;
        var cc = p.GetComponent<CharacterController>(); if (cc != null) cc.enabled = true;
        if (_engineSrc != null) _engineSrc.Stop();
        Sfx.Play("door_car", transform.position, 0.8f);
        DropPassengers();
        foreach (var m in Squad.Of(GameData.PlayerTeam).members) if (m != null) m.Board(null);   // anyone still on the way: never mind
    }

    void Update()
    {
        if (!_driving) return;
        var kb = Keyboard.current;
        var p = PlayerController.Instance;
        if (p != null) p.transform.position = transform.position + Vector3.up * 0.5f;   // ride along
        if (kb != null && kb.fKey.wasPressedThisFrame && _rb.linearVelocity.magnitude < 3f) Exit();
        if (_engineSrc != null) _engineSrc.pitch = 0.7f + Mathf.Clamp01(_rb.linearVelocity.magnitude / maxSpeed) * 1.3f;
    }

    void FixedUpdate()
    {
        var kb = Keyboard.current;
        float throttle = 0f, steerIn = 0f; bool brake = false;
        if (_driving && kb != null && !Hud.Blocking)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steerIn += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steerIn -= 1f;
            brake = kb.spaceKey.isPressed;
        }

        // suspension: a spring at each corner
        int grounded = 0;
        Vector3[] corners = {
            new Vector3(-halfExtents.x, rideHeight,  halfExtents.z * 0.8f), new Vector3(halfExtents.x, rideHeight,  halfExtents.z * 0.8f),
            new Vector3(-halfExtents.x, rideHeight, -halfExtents.z * 0.8f), new Vector3(halfExtents.x, rideHeight, -halfExtents.z * 0.8f) };
        for (int i = 0; i < 4; i++)
        {
            Vector3 origin = transform.TransformPoint(corners[i]);
            float len = suspension + wheelRadius;
            if (Physics.Raycast(origin, -transform.up, out var hit, len, ~0, QueryTriggerInteraction.Ignore) && hit.rigidbody != _rb)
            {
                grounded++;
                float compression = 1f - (hit.distance - wheelRadius) / suspension;
                float vel = Vector3.Dot(_rb.GetPointVelocity(origin), transform.up);
                float force = Mathf.Max(0f, compression * springK - vel * damper) * 0.25f;   // per corner
                _rb.AddForceAtPosition(transform.up * force, origin);
                if (wheels.Length > i && wheels[i] != null)
                    wheels[i].position = hit.point + transform.up * wheelRadius;
            }
        }

        Vector3 v = _rb.linearVelocity;
        float fwd = Vector3.Dot(v, transform.forward);
        if (grounded >= 2)
        {
            // engine / reverse
            if (Mathf.Abs(fwd) < maxSpeed || Mathf.Sign(throttle) != Mathf.Sign(fwd))
                _rb.AddForce(transform.forward * throttle * engine * (throttle < 0f && fwd > 1f ? 1.6f : 1f));
            // grip: cancel sideways velocity (less when handbraking -> drifts)
            float side = Vector3.Dot(v, transform.right);
            _rb.AddForce(-transform.right * side * _rb.mass * (brake ? grip * 0.25f : grip) * 0.2f);
            if (brake) _rb.AddForce(-transform.forward * fwd * _rb.mass * 0.9f);
            if (throttle == 0f) _rb.AddForce(-transform.forward * fwd * _rb.mass * 0.25f);   // rolling resistance
            // steering: yaw rate depends on speed, reverses when backing up
            _steer = Mathf.MoveTowards(_steer, steerIn, 4f * Time.fixedDeltaTime);
            float yawRate = _steer * steerDeg * Mathf.Clamp01(Mathf.Abs(fwd) / 6f) * Mathf.Sign(fwd == 0f ? 1f : fwd);
            Vector3 av = _rb.angularVelocity;
            av.y = Mathf.Lerp(av.y, yawRate * Mathf.Deg2Rad * (brake ? 1.6f : 1f), 0.25f);
            _rb.angularVelocity = av;
        }

        _wheelSpin += fwd / wheelRadius * Mathf.Rad2Deg * Time.fixedDeltaTime;
        for (int i = 0; i < wheels.Length; i++)
            if (wheels[i] != null)
                wheels[i].localRotation = Quaternion.Euler(0f, i < 2 ? _steer * steerDeg : 0f, 0f) * Quaternion.Euler(_wheelSpin, 0f, 0f);
    }

    void LateUpdate()
    {
        if (!_driving || _cam == null) return;
        // chase camera: behind and above, lagging a little on turns
        _camYaw = Mathf.LerpAngle(_camYaw, transform.eulerAngles.y, 4f * Time.deltaTime);
        Quaternion rot = Quaternion.Euler(14f, _camYaw, 0f);
        Vector3 target = transform.position + Vector3.up * 1.4f;
        float speedPull = Mathf.Clamp01(_rb.linearVelocity.magnitude / maxSpeed) * 2f;
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, target - rot * Vector3.forward * (7.5f + speedPull) + Vector3.up * 1.2f, 10f * Time.deltaTime);
        _cam.transform.rotation = Quaternion.LookRotation(target + transform.forward * 3f - _cam.transform.position);
        _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, 62f + speedPull * 8f, 4f * Time.deltaTime);
    }
}

