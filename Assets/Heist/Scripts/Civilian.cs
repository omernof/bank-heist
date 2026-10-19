using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// Bank customers and street pedestrians. Customers stand in the BankQueue and
// shuffle forward as the teller serves them; pedestrians stroll between
// waypoints. When the robbery starts (or shots ring out nearby) everyone gets
// DOWN ON THE FLOOR - the brief's requirement - and stays there.
[RequireComponent(typeof(NavMeshAgent))]
public class Civilian : MonoBehaviour
{
    public enum Mode { Queue, Leaving, Wander, Fleeing, Down }
    public Mode mode = Mode.Wander;
    public BankQueue queue;
    public Transform[] wanderPoints;

    NavMeshAgent _agent;
    Animator _anim;
    Health _health;
    float _served;
    int _slot = -1;
    Vector3 _exit;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();
        _health = GetComponent<Health>();
        _agent.speed = GaitSettings.I.civWalk; _agent.radius = 0.35f; _agent.acceleration = 8f;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        _agent.avoidancePriority = 60 + Random.Range(0, 30);
    }

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }
    public static readonly System.Collections.Generic.List<Civilian> All = new System.Collections.Generic.List<Civilian>();

    public void JoinQueue(BankQueue q, Vector3 exit)
    {
        queue = q; _exit = exit; mode = Mode.Queue;
        _slot = q.Join(this);
    }

    public void SetSlot(int s) { _slot = s; _served = 0f; }

    // NavMesh area the city builder paints over every road (see HeistBuilder.RoadArea)
    public const int RoadArea = 3;
    bool _sidewalk;

    // street pedestrians stay on the pavement on their own side of the street
    public void KeepToSidewalk()
    {
        _sidewalk = true;
        _agent.areaMask = NavMesh.AllAreas & ~(1 << RoadArea);
    }

    Vector3 NextWanderPoint()
    {
        float side = Mathf.Sign(transform.position.z);
        for (int tries = 0; tries < 12; tries++)
        {
            var p = wanderPoints[Random.Range(0, wanderPoints.Length)].position;
            if (_sidewalk && Mathf.Sign(p.z) != side) continue;
            if ((p - transform.position).sqrMagnitude < 9f) continue;
            return p + new Vector3(Random.Range(-1.5f, 1.5f), 0f, Random.Range(-0.6f, 0.6f));
        }
        return transform.position + new Vector3(Random.Range(-6f, 6f), 0f, 0f);
    }

    void Update()
    {
        if ((_health != null && _health.Dead) || mode == Mode.Down || !_agent.enabled || !_agent.isOnNavMesh) return;

        switch (mode)
        {
            case Mode.Queue:
                if (queue == null || _slot < 0) break;
                Vector3 p = queue.SlotPosition(_slot);
                _agent.stoppingDistance = 0.1f;
                if ((_agent.destination - p).sqrMagnitude > 0.04f) _agent.SetDestination(p);
                if (!_agent.pathPending && _agent.remainingDistance < 0.25f)
                {
                    // face the counter while waiting
                    Vector3 f = queue.SlotFacing(_slot);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(f), 240f * Time.deltaTime);
                    if (_slot == 0)
                    {
                        _served += Time.deltaTime;
                        if (_served > queue.serveTime) { queue.Leave(this); mode = Mode.Leaving; _agent.SetDestination(_exit); }
                    }
                }
                break;

            case Mode.Leaving:
                if (!_agent.pathPending && _agent.remainingDistance < 0.6f)
                {
                    if (queue != null) queue.Respawn(this);
                    else gameObject.SetActive(false);
                }
                break;

            case Mode.Wander:
                if (wanderPoints != null && wanderPoints.Length > 0 && !_agent.pathPending && _agent.remainingDistance < 0.8f)
                    _agent.SetDestination(NextWanderPoint());
                break;

            case Mode.Fleeing:
                if (!_agent.pathPending && _agent.remainingDistance < 1f) GetDown();
                break;
        }

        // personal space: step aside from anyone closer than ~0.9 m (the player has
        // no NavMesh agent, so plain avoidance lets pedestrians walk into you)
        if (mode != Mode.Queue)
        {
            Vector3 push = Vector3.zero;
            foreach (var h in Health.All)
            {
                if (h == null || h == _health || h.Dead) continue;
                Vector3 d = transform.position - h.transform.position; d.y = 0f;
                float dist = d.magnitude;
                if (dist < 0.9f && dist > 0.01f) push += d / dist * (0.9f - dist);
            }
            if (push.sqrMagnitude > 0.0001f) _agent.Move(push * 3f * Time.deltaTime);
        }

        float v = _agent.velocity.magnitude;
        HeistAnimInfo.Float(_anim, "Speed", v);
        if (_anim != null && mode != Mode.Down) _anim.speed = GaitSettings.PlaybackRate(v, GaitSettings.I.civRun);
    }

    // shots nearby in the street: run away from the danger first, then get down
    public void Panic(Vector3 danger)
    {
        if (mode == Mode.Down || mode == Mode.Fleeing || (_health != null && _health.Dead)) return;
        if (queue != null) { queue.Leave(this); queue = null; }
        Vector3 away = (transform.position - danger); away.y = 0f;
        Vector3 to = transform.position + away.normalized * Random.Range(8f, 14f);
        _agent.areaMask = NavMesh.AllAreas;          // in a panic people run across the road
        if (NavMesh.SamplePosition(to, out var hit, 6f, NavMesh.AllAreas))
        {
            mode = Mode.Fleeing;
            _agent.speed = GaitSettings.I.civRun;
            _agent.SetDestination(hit.position);
            Sfx.Play(Random.value < 0.5f ? "scream1" : "scream2", transform.position, 0.6f, 0.15f);
        }
        else GetDown();
    }

    // "Everybody on the floor!"
    public void GetDown()
    {
        if (mode == Mode.Down || (_health != null && _health.Dead)) return;
        FaceFreeSpace();
        if (queue != null) { queue.Leave(this); queue = null; }
        mode = Mode.Down;
        if (_agent.enabled && _agent.isOnNavMesh) { _agent.ResetPath(); _agent.velocity = Vector3.zero; }
        HeistAnimInfo.Float(_anim, "Speed", 0f, 0f);
        if (HeistAnimInfo.HasParam(_anim, "Down")) _anim.SetBool("Down", true);
        else StartCoroutine(LieDown());
    }

    // Lying down takes ~1.8 m of floor: turn toward the direction with the most
    // room, so people don't end up lying on top of each other or inside the
    // furniture. In the queue (1 m between customers) that means across the aisle.
    void FaceFreeSpace()
    {
        Vector3 p = transform.position + Vector3.up * 0.35f;
        float bestScore = -1f; Vector3 best = transform.forward;
        for (int i = 0; i < 8; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 45f + (queue != null ? 90f : 0f), 0f) * Vector3.forward;
            float score = 0f;
            foreach (float sgn in new[] { 1f, -1f })   // the body ends up on one side of the feet or the other
            {
                float free = 1.9f;
                if (Physics.SphereCast(p, 0.25f, dir * sgn, out var hit, 1.9f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
                    free = hit.distance;
                score += free;
            }
            foreach (var c in All)   // keep clear of anyone already on the floor
                if (c != null && c != this && c.mode == Mode.Down)
                {
                    Vector3 o = c.transform.position - transform.position; o.y = 0f;
                    if (o.magnitude < 2.2f && Mathf.Abs(Vector3.Dot(o.normalized, dir)) > 0.7f) score -= 2f;
                }
            if (score > bestScore) { bestScore = score; best = dir; }
        }
        transform.rotation = Quaternion.LookRotation(best);
    }

    // Fallback without a lie-down clip: lower the body flat onto the floor,
    // face down with the hands by the head (idle pose rotated), with a little
    // random turn so a room full of customers doesn't look copy-pasted.
    IEnumerator LieDown()
    {
        yield return new WaitForSeconds(Random.Range(0.05f, 0.6f));
        if (_agent != null) _agent.enabled = false;
        Quaternion from = transform.rotation;
        Quaternion yaw = Quaternion.Euler(0f, from.eulerAngles.y + Random.Range(-40f, 40f), 0f);
        Quaternion to = yaw * Quaternion.Euler(90f, 0f, 0f);
        Vector3 start = transform.position;
        Vector3 end = start + yaw * Vector3.forward * 0.9f + Vector3.up * 0.18f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            transform.rotation = Quaternion.Slerp(from, to, e);
            transform.position = Vector3.Lerp(start, end, e);
            yield return null;
        }
        transform.rotation = to; transform.position = end;
        if (_anim != null) _anim.speed = 0.25f;   // barely breathing, still alive
    }
}
