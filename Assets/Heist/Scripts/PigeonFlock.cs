using System.Collections.Generic;
using UnityEngine;

// A group of pigeons on one patch of sidewalk. Scares (people walking right up,
// someone running, a car close by, gunfire anywhere near) send the whole flock
// up at once; after a quiet spell they come back down a few at a time.
public class PigeonFlock : MonoBehaviour
{
    public static readonly List<PigeonFlock> All = new List<PigeonFlock>();

    public float radius = 2.2f;
    public float calmSeconds = 18f;

    readonly List<Pigeon> _birds = new List<Pigeon>();
    static Rigidbody[] s_bodies = new Rigidbody[0];
    static float s_bodiesAt;
    float _calmAt = -1f, _nextCheck, _nextCoo;
    bool _away;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Start()
    {
        foreach (var b in GetComponentsInChildren<Pigeon>())
        {
            b.flock = this;
            b.transform.position = Pigeon.Ground(b.transform.position);
            _birds.Add(b);
        }
    }

    // gunfire / explosions: every flock in range takes off
    public static void Alarm(Vector3 at, float range)
    {
        foreach (var f in All.ToArray())
            if (f != null && (f.transform.position - at).sqrMagnitude < range * range) f.ScareAll(at);
    }

    public void ScareAll(Vector3 from)
    {
        _calmAt = Time.time + calmSeconds * Random.Range(0.8f, 1.3f);
        if (_away) return;
        _away = true;
        foreach (var b in _birds) if (b != null) b.Scare(from, Random.Range(0f, 0.35f));
        Sfx.Play("wings", transform.position, 0.9f, 0.1f, 45f);
    }

    public void MaybeCoo()
    {
        if (Time.time < _nextCoo) return;
        _nextCoo = Time.time + Random.Range(5f, 12f);
        Sfx.Play("coo", transform.position, 0.35f, 0.15f, 18f);
    }

    void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.2f;

        Vector3 threat;
        if (Threat(out threat)) ScareAll(threat);
        else if (_away && Time.time > _calmAt)
        {
            // quiet again: call the birds back a few at a time
            _away = false;
            foreach (var b in _birds)
            {
                if (b == null) continue;
                Vector2 p = Random.insideUnitCircle * radius;
                b.ComeBack(Pigeon.Ground(transform.position + new Vector3(p.x, 0f, p.y)));
            }
        }
    }

    bool Threat(out Vector3 at)
    {
        at = Vector3.zero;
        Vector3 c = transform.position;
        foreach (var h in Health.All)
        {
            if (h == null || h.Dead) continue;
            Vector3 d = h.transform.position - c; d.y = 0f;
            float dist = d.magnitude - radius;
            if (dist > 6f) continue;
            float speed = 0f;
            var cc = h.GetComponent<CharacterController>(); if (cc != null) speed = cc.velocity.magnitude;
            var ag = h.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (ag != null && ag.enabled) speed = Mathf.Max(speed, ag.velocity.magnitude);
            // walking right into them, or running anywhere close
            if (dist < 0.8f || (dist < 4.5f && speed > 2.6f)) { at = h.transform.position; return true; }
        }
        if (Time.time > s_bodiesAt) { s_bodiesAt = Time.time + 1f; s_bodies = Object.FindObjectsByType<Rigidbody>(); }
        foreach (var v in s_bodies)
        {
            if (v == null) continue;
            if (v.isKinematic || v.linearVelocity.sqrMagnitude < 9f) continue;
            if ((v.position - c).sqrMagnitude < 8f * 8f) { at = v.position; return true; }
        }
        return false;
    }
}
