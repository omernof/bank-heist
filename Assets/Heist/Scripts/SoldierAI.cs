using UnityEngine;
using UnityEngine.AI;

// An armed NPC - a robber or a police officer - who obeys its squad's current
// order (Follow / Attack / Grenade / Retreat / Hold), finds enemies with line of
// sight, shoots in bursts, keeps its distance from squad mates and throws
// grenades when told to.
[RequireComponent(typeof(NavMeshAgent))]
public class SoldierAI : MonoBehaviour
{
    public Team team;
    public bool isLeader;          // an NPC leader walks to objectives instead of following
    public int grenades = 1;
    public float sightRange = 38f;
    public float preferredRange = 12f;
    public Vector3 home;           // spawn point - "Hold" position and fallback objective
    public Vector3 objective;      // where an NPC leader heads (set by the HeistDirector)
    public bool hasObjective;

    public Health Health { get; private set; }
    public Health Target { get; private set; }
    public Squad Squad => Squad.Of(team);

    NavMeshAgent _agent;
    Animator _anim;
    Weapon _weapon;
    float _nextThink, _burstLeft, _nextBurst, _reloadUntil;
    int _shotsSinceReload;
    Vector3 _grenadeAt; bool _grenadeQueued;
    Vector3 _strafe; float _nextStrafe;
    int _seed;

    void Awake()
    {
        Health = GetComponent<Health>();
        _agent = GetComponent<NavMeshAgent>();
        _anim = GetComponentInChildren<Animator>();
        _weapon = GetComponentInChildren<Weapon>();
        home = transform.position;
        _seed = Random.Range(0, 100000);
    }

    // join the squad only once `team` is set: CharacterFactory assigns it right
    // after AddComponent, and OnEnable already runs inside AddComponent - joining
    // there put every NPC (police included) into the default Robbers squad
    bool _started;
    void OnEnable() { if (_started) Squad.Register(this); }
    void OnDisable() { Squad.Unregister(this); }

    void Start()
    {
        _started = true;
        Squad.Register(this);
        _agent.radius = 0.4f;
        _agent.height = 1.8f;
        _agent.speed = 3.6f;
        _agent.acceleration = 14f;
        _agent.angularSpeed = 540f;
        _agent.stoppingDistance = 0.4f;
        _agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        _agent.avoidancePriority = 30 + _seed % 40;   // unique-ish so they give way to each other
        _agent.autoBraking = true;
    }

    public void QueueGrenade(Vector3 at) { _grenadeAt = at; _grenadeQueued = true; }

    static float W => GaitSettings.I.armedWalk;
    static float Run => GaitSettings.I.armedRun;

    void Update()
    {
        if (Health.Dead || !_agent.enabled || !_agent.isOnNavMesh) return;

        if (Time.time >= _nextThink)
        {
            _nextThink = Time.time + 0.25f + Random.value * 0.1f;
            var t = FindTarget();
            // a moment to react before opening fire on someone who just showed up
            if (t != null && t != Target) _nextBurst = Mathf.Max(_nextBurst, Time.time + Random.Range(0.5f, 0.9f));
            Target = t;
        }

        var sq = Squad;
        Order o = sq.order;
        bool running = false;

        if (_board != null) { Boarding(); return; }

        if (_grenadeQueued) { DoGrenade(); }
        else if (o == Order.Retreat)
        {
            // NPC-led squads never set a retreat point (it stays at the world
            // origin) - they fall back to where they started instead
            MoveTo(sq.leader == null || isLeader ? home : sq.retreatPoint, Run * 1.35f);
            running = true;
        }
        else if (o == Order.Hold)
        {
            MoveTo(isLeader || sq.leader == null ? home : sq.orderPoint + Spread(), Run * 0.9f, 0.8f);
        }
        else if (o == Order.Attack && Target != null)
        {
            Engage();
        }
        else if (o == Order.Follow && Target == null && Looting())
        {
            // busy filling bags in the vault
        }
        else if (isLeader || sq.leader == null)
        {
            // an NPC leader (or a squad without one) walks toward its objective
            MoveTo(hasObjective ? objective : home, W, 1f);
        }
        else
        {
            // Follow: take our formation slot; run to catch up if we fell behind
            Vector3 slot = sq.SlotFor(this);
            float far = (transform.position - slot).magnitude;
            MoveTo(slot, far > 5f ? Run * 1.35f : far > 2f ? Run : W, 0.6f);
            running = far > 5f;
        }

        // shoot at anything hostile in view (while moving too, like GTA crews)
        if (Target != null && !running && o != Order.Retreat) ShootAt(Target);
        else if (Target != null && o == Order.Retreat && Vector3.Distance(Target.transform.position, transform.position) < 6f) ShootAt(Target);

        Separate();

        float v = _agent.velocity.magnitude;
        HeistAnimInfo.Float(_anim, "Speed", v);
        HeistAnimInfo.Bool(_anim, "Aim", Target != null);
        if (_anim != null) _anim.speed = GaitSettings.PlaybackRate(v, Run);
    }

    // ---- behaviours ----

    // getting into the player's car: sprint to its side door and climb in. If the
    // car pulls away or it takes too long, they're "already in" (no one gets left behind)
    Vehicle _board; float _boardBy;
    public void Board(Vehicle v) { _board = v; _boardBy = Time.time + 7f; if (v == null && _agent.isOnNavMesh) _agent.ResetPath(); }

    void Boarding()
    {
        Vector3 door = _board.transform.position + _board.transform.right * (_seed % 2 == 0 ? 1.6f : -1.6f);
        Vector3 flat = door - transform.position; flat.y = 0f;
        if (!_board.Driving) { _board = null; return; }
        if (flat.magnitude < 1.3f || Time.time > _boardBy || flat.magnitude > 35f)
        {
            var v = _board; _board = null; Target = null;
            v.TakeAboard(this);
            return;
        }
        Target = null;
        MoveTo(door, Run * 1.35f, 0.5f);
        float sp = _agent.velocity.magnitude;
        HeistAnimInfo.Float(_anim, "Speed", sp);
        HeistAnimInfo.Bool(_anim, "Aim", false);
        if (_anim != null) _anim.speed = GaitSettings.PlaybackRate(sp, Run);
    }

    public void LeftVehicle(Vector3 at)
    {
        if (_agent.enabled) _agent.Warp(at);
        _nextThink = 0f;
    }

    // In the open vault the robber crew help themselves: each claims a different
    // pile, walks over, takes a moment to bag it, then goes for the next one.
    Loot _loot; float _grabUntil;
    bool Looting()
    {
        if (team != Team.Robbers || GameData.side != Side.Robber || !VaultDoor.IsOpen) { DropLoot(); return false; }
        var p = PlayerController.Instance;
        if (p != null && (p.transform.position - transform.position).sqrMagnitude > 30f * 30f) { DropLoot(); return false; }
        if (_loot == null || !_loot.Available) { DropLoot(); _loot = PickLoot(); }
        if (_loot == null) return false;

        Vector3 at = _loot.transform.position;
        Vector3 flat = at - transform.position; flat.y = 0f;
        if (flat.magnitude > _loot.range - 0.2f)
        {
            _grabUntil = 0f;
            MoveTo(at, Run, _loot.range - 0.5f);
            return true;
        }
        // in reach: stop, face it, bag it
        _agent.ResetPath();
        FaceTowards(at, 360f);
        if (_grabUntil == 0f) _grabUntil = Time.time + 1.1f;
        else if (Time.time >= _grabUntil) { var l = _loot; DropLoot(); l.Take(Health); }
        return true;
    }

    Loot PickLoot()
    {
        Loot best = null; float bd = float.MaxValue;
        foreach (var i in Interactable.All)
        {
            if (!(i is Loot l) || !l.Available || (l.claimedBy != null && l.claimedBy != this)) continue;
            if (!NavMesh.SamplePosition(l.transform.position, out var hit, l.range, NavMesh.AllAreas)) continue;
            float d = (l.transform.position - transform.position).sqrMagnitude;
            if (d < bd) { bd = d; best = l; }
        }
        if (best != null) best.claimedBy = this;
        return best;
    }

    void DropLoot()
    {
        if (_loot != null && _loot.claimedBy == this) _loot.claimedBy = null;
        _loot = null; _grabUntil = 0f;
    }

    void Engage()
    {
        float d = Vector3.Distance(transform.position, Target.transform.position);
        bool los = HasLineOfSight(Target);
        if (!los || d > preferredRange + 4f)
        {
            MoveTo(Target.transform.position, Run, preferredRange * 0.6f);
        }
        else
        {
            // in range: small side-steps so they don't stand like statues
            if (Time.time > _nextStrafe)
            {
                _nextStrafe = Time.time + Random.Range(1.2f, 2.6f);
                Vector3 side = Vector3.Cross(Vector3.up, (Target.transform.position - transform.position).normalized);
                _strafe = transform.position + side * Random.Range(-2.5f, 2.5f);
            }
            MoveTo(_strafe, W, 0.3f);
        }
    }

    void DoGrenade()
    {
        float d = Vector3.Distance(transform.position, _grenadeAt);
        if (d > 22f) { MoveTo(_grenadeAt, Run, 18f); return; }
        FaceTowards(_grenadeAt, 1000f);
        HeistAnimInfo.Trigger(_anim, "Throw");
        Grenade.Throw(transform.position + Vector3.up * 1.7f + transform.forward * 0.4f, _grenadeAt, Health);
        grenades--;
        _grenadeQueued = false;
        Hud.Notify($"{Health.displayName}: Grenade!", team == GameData.PlayerTeam ? Hud.Info : Hud.Warn);
    }

    void ShootAt(Health t)
    {
        if (_weapon == null || !HasLineOfSight(t)) return;
        FaceTowards(t.transform.position, 720f);
        if (Time.time < _reloadUntil) return;
        if (_burstLeft <= 0)
        {
            if (Time.time < _nextBurst) return;
            _burstLeft = Random.Range(3, 7);
        }
        Vector3 eye = transform.position + Vector3.up * 1.45f;
        Vector3 aim = t.AimPoint + Random.insideUnitSphere * 0.25f;
        float dist = Vector3.Distance(eye, aim);
        // worse aim at range and when the target is moving
        float extra = 1.2f + dist * 0.08f;
        if (t.isPlayer && GameData.side == Side.Bystander) extra += 1.5f;
        if (_weapon.Fire(eye, aim - eye, Health, extra, 0.8f))
        {
            HeistAnimInfo.Trigger(_anim, "Fire");
            _burstLeft--;
            if (++_shotsSinceReload >= 30) { _shotsSinceReload = 0; _reloadUntil = Time.time + 2.2f; }
            if (_burstLeft <= 0) _nextBurst = Time.time + Random.Range(0.5f, 1.3f);
        }
    }

    // ---- helpers ----

    Health FindTarget()
    {
        Health best = null; float bd = sightRange;
        Team enemy = GameData.EnemyOf(team);
        foreach (var h in Health.All)
        {
            if (h == null || h.Dead || h == Health) continue;
            bool hostile = h.team == enemy;
            // robbers also shoot a bystander PLAYER who refuses to get down
            if (!hostile && team == Team.Robbers && h.isPlayer && h.team == Team.Civilians)
                hostile = CivilianDefiance.IsDefiant;
            if (!hostile) continue;
            float d = Vector3.Distance(transform.position, h.transform.position);
            if (d < bd && HasLineOfSight(h)) { bd = d; best = h; }
        }
        return best;
    }

    public bool HasLineOfSight(Health t)
    {
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        if (Physics.Linecast(eye, t.AimPoint, out var hit, ~0, QueryTriggerInteraction.Ignore))
        {
            // blocked by a wall - or by a squad mate, so nobody shoots through friends
            return hit.collider.GetComponentInParent<Health>() == t;
        }
        return true;
    }

    void MoveTo(Vector3 p, float speed, float stop = 0.4f)
    {
        _agent.speed = speed;
        _agent.stoppingDistance = stop;
        if ((_agent.destination - p).sqrMagnitude > 0.25f) _agent.SetDestination(p);
        if (Target == null && _agent.velocity.sqrMagnitude > 0.1f)
            FaceTowards(transform.position + _agent.velocity, 540f);
        _agent.updateRotation = Target == null;
    }

    void FaceTowards(Vector3 p, float degPerSec)
    {
        Vector3 d = p - transform.position; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degPerSec * Time.deltaTime);
    }

    Vector3 Spread() => new Vector3(Mathf.Sin(_seed) * 2f, 0f, Mathf.Cos(_seed) * 2f);

    // personal space: ~1.4 m from squad mates and ~1.1 m from everyone else
    // (the player, the other team, civilians) on top of the agents' own avoidance,
    // so nobody ends up standing inside somebody else
    void Separate()
    {
        Vector3 push = Vector3.zero;
        foreach (var h in Health.All)
        {
            if (h == null || h == Health || h.Dead) continue;
            Vector3 d = transform.position - h.transform.position; d.y = 0f;
            float dist = d.magnitude;
            float want = h.team == team && !h.isPlayer ? 1.4f : 1.1f;
            if (dist < want && dist > 0.01f) push += d / dist * (want - dist);
        }
        if (push.sqrMagnitude > 0.0001f) _agent.Move(push * 2.5f * Time.deltaTime);
    }
}

// Bystander mode: robbers order everyone down. A bystander player who stays on
// their feet too long (or runs around) after that becomes a target.
public static class CivilianDefiance
{
    public static bool IsDefiant;
}
