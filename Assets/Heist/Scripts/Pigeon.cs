using System.Collections.Generic;
using UnityEngine;

// One street pigeon. On the ground it pecks, coos, looks around and waddles a
// little; when its flock is scared it bursts off the ground, climbs and flies
// away along the street, then circles back and lands near the flock once things
// are quiet. A "soaring" pigeon just circles high over the city.
// Animator states are named by role (Idle, Peck, Coo, Walk, TakeOff, Fly, Land)
// by the editor's AnimalLibrary.
public class Pigeon : MonoBehaviour
{
    public enum State { Ground, TakeOff, Flying, Landing, Soar }

    public PigeonFlock flock;
    public State state;
    // soaring
    public Vector3 soarCenter; public float soarRadius = 30f, soarHeight = 32f, soarSpeed = 0.18f;

    Animator _anim;
    string _cur;
    float _next, _t0, _phase;
    Vector3 _vel, _walkTo, _escape, _landAt;
    bool _walking;

    const float FlySpeed = 8f;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        _phase = Random.value * Mathf.PI * 2f;
    }

    void Start()
    {
        if (_anim != null) { _anim.speed = Random.Range(0.85f, 1.15f); _anim.applyRootMotion = false; }
        if (state == State.Soar) Play("Fly");
        else { Play(Random.value < 0.6f ? "Peck" : "Idle", 0f); _next = Time.time + Random.Range(0.5f, 3f); }
    }

    bool Has(string s) => _anim != null && _anim.HasState(0, Animator.StringToHash(s));

    void Play(string s, float fade = 0.2f)
    {
        if (_anim == null || _cur == s) return;
        if (!Has(s))
        {
            // fall back to whatever the model has
            s = s == "Coo" || s == "Peck" ? "Idle" : s == "TakeOff" || s == "Land" ? "Fly" : s;
            if (!Has(s) || _cur == s) return;
        }
        _anim.CrossFadeInFixedTime(s, fade, 0, s == "Idle" || s == "Peck" || s == "Fly" ? Random.value : 0f);
        _cur = s;
    }

    // ---- called by the flock ----
    public void Scare(Vector3 from, float delay)
    {
        if (state != State.Ground && state != State.Landing) return;
        Vector3 away = transform.position - from; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
        away.y = 0f; away.Normalize();
        // flee mostly along the street (the open sky corridor), away from the threat
        Vector3 along = Mathf.Abs(away.x) > 0.2f ? new Vector3(Mathf.Sign(away.x), 0f, 0f) : new Vector3(Random.value < 0.5f ? -1f : 1f, 0f, 0f);
        Vector3 dir = (along * 2f + away + Random.insideUnitSphere * 0.4f); dir.y = 0f; dir.Normalize();
        _escape = transform.position + dir * Random.Range(35f, 60f) + Vector3.up * Random.Range(14f, 22f);
        _t0 = Time.time + delay;
        _vel = (dir * 0.6f + Vector3.up).normalized * 1.5f;
        state = State.TakeOff;
        _walking = false;
    }

    public void ComeBack(Vector3 at)
    {
        if (state != State.Flying) return;
        _landAt = at;
        _escape = Vector3.zero;
    }

    void Update()
    {
        switch (state)
        {
            case State.Ground: GroundUpdate(); break;
            case State.TakeOff: TakeOffUpdate(); break;
            case State.Flying: FlyUpdate(); break;
            case State.Landing: LandUpdate(); break;
            case State.Soar: SoarUpdate(); break;
        }
    }

    void GroundUpdate()
    {
        if (_walking)
        {
            Vector3 d = _walkTo - transform.position; d.y = 0f;
            if (d.magnitude < 0.05f) { _walking = false; Play("Peck"); }
            else
            {
                Face(d, 360f);
                transform.position += d.normalized * Mathf.Min(d.magnitude, 0.32f * Time.deltaTime);
            }
        }
        if (Time.time < _next) return;
        _next = Time.time + Random.Range(1.5f, 4.5f);
        float r = Random.value;
        if (r < 0.45f) Play("Peck");
        else if (r < 0.65f) Play("Idle");
        else if (r < 0.75f) { Play("Coo"); if (flock != null) flock.MaybeCoo(); }
        else
        {
            // a short waddle to somewhere else inside the flock area
            Vector3 c = flock != null ? flock.transform.position : transform.position;
            float rad = flock != null ? flock.radius : 1.5f;
            Vector2 p = Random.insideUnitCircle * rad;
            _walkTo = Ground(new Vector3(c.x + p.x, transform.position.y, c.z + p.y));
            _walking = true;
            Play("Walk", 0.15f);
        }
    }

    void TakeOffUpdate()
    {
        if (Time.time < _t0) return;
        Play("TakeOff", 0.05f);
        // explosive vertical burst for the first half second, then away
        _vel = Vector3.Lerp(_vel, (_escape - transform.position).normalized * FlySpeed + Vector3.up * 3f, 2.5f * Time.deltaTime);
        Move();
        if (Time.time > _t0 + 0.6f) { state = State.Flying; Play("Fly", 0.25f); }
    }

    void FlyUpdate()
    {
        Vector3 goal;
        if (_escape != Vector3.zero)
        {
            goal = _escape;
            if ((goal - transform.position).sqrMagnitude < 16f)
            {
                // wander high up until the flock calls us back
                _escape = transform.position + new Vector3(Random.Range(-25f, 25f), Random.Range(-3f, 3f), Random.Range(-8f, 8f));
                _escape.y = Mathf.Clamp(_escape.y, 14f, 26f);
            }
        }
        else
        {
            // come back: approach from above, then glide in
            Vector3 over = _landAt + Vector3.up * 2.5f;
            goal = (transform.position - _landAt).sqrMagnitude > 12f * 12f ? over + Vector3.up * 6f : over;
            if ((transform.position - over).sqrMagnitude < 2f) { state = State.Landing; Play("Land", 0.2f); _t0 = Time.time; }
        }
        Steer(goal, FlySpeed);
        Move();
    }

    void LandUpdate()
    {
        float k = Mathf.Clamp01((Time.time - _t0) / 0.9f);
        transform.position = Vector3.Lerp(transform.position, _landAt, 4f * Time.deltaTime);
        Vector3 d = _vel; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) Face(d, 180f);
        if (k >= 1f || (transform.position - _landAt).sqrMagnitude < 0.0004f)
        {
            transform.position = _landAt;
            Vector3 e = transform.eulerAngles; transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            state = State.Ground; _next = Time.time + Random.Range(0.6f, 2f);
            Play("Idle", 0.3f);
        }
    }

    void SoarUpdate()
    {
        _phase += soarSpeed * Time.deltaTime;
        Vector3 p = soarCenter + new Vector3(Mathf.Cos(_phase) * soarRadius, soarHeight + Mathf.Sin(_phase * 2.3f) * 2f, Mathf.Sin(_phase) * soarRadius * 0.7f);
        Vector3 d = p - transform.position;
        transform.position = p;
        if (d.sqrMagnitude > 0.000001f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 18f * Mathf.Sign(soarSpeed));
    }

    // ---- helpers ----

    void Steer(Vector3 goal, float speed)
    {
        Vector3 want = (goal - transform.position).normalized * speed;
        // pull up if a wall is right ahead
        if (Physics.Raycast(transform.position, _vel.normalized, 5f, ~0, QueryTriggerInteraction.Ignore)) want += Vector3.up * speed * 1.5f;
        _vel = Vector3.Lerp(_vel, want, 1.8f * Time.deltaTime);
    }

    void Move()
    {
        transform.position += _vel * Time.deltaTime;
        if (_vel.sqrMagnitude > 0.01f)
        {
            Vector3 flat = _vel; flat.y = 0f;
            Vector3 v = _vel; v.y *= 0.4f;                       // keep the body fairly level
            Quaternion look = Quaternion.LookRotation(v.normalized, Vector3.up);
            if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, look, 6f * Time.deltaTime);
        }
    }

    void Face(Vector3 d, float degPerSec)
    {
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d.normalized, Vector3.up), degPerSec * Time.deltaTime);
    }

    public static Vector3 Ground(Vector3 p)
    {
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
        return p;
    }
}

