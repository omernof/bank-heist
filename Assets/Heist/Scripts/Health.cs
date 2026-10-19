using System.Collections;
using UnityEngine;

// HP + armor for anyone who can be shot: the player, squad members, enemies and
// civilians. Posts the "X was hit" / "X is down" messages, plays the hit / death
// reaction and writes the result back to GameData so it survives scene changes.
public class Health : MonoBehaviour
{
    public Team team;
    public string displayName = "Someone";
    public string memberId;            // GameData.MemberState id (empty for extras)
    public bool isPlayer;
    public float maxHp = 100f;
    public float hp = 100f;
    public float armor;

    public static readonly System.Collections.Generic.List<Health> All = new System.Collections.Generic.List<Health>();
    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool Dead { get; private set; }
    public System.Action<Health, float, Health> OnDamaged;   // victim, amount, attacker
    public System.Action<Health> OnDied;

    Animator _anim;
    float _lastHitMsg;

    void Awake() { _anim = GetComponentInChildren<Animator>(); }

    void Start()
    {
        if (isPlayer) { hp = GameData.hp; armor = GameData.armor; maxHp = GameData.maxHp; }
        else if (!string.IsNullOrEmpty(memberId))
        {
            var m = GameData.Get(memberId);
            if (m != null) { hp = m.hp; armor = m.armor; if (!m.alive) { Dead = true; gameObject.SetActive(false); } }
        }
    }

    public Vector3 AimPoint => transform.position + Vector3.up * (Dead ? 0.3f : 1.3f);

    public void TakeDamage(float amount, Health attacker, Vector3 hitDir)
    {
        if (Dead || amount <= 0f) return;
        // armor soaks 70% of the damage until it is gone (GTA-style blue bar)
        if (armor > 0f)
        {
            float soak = Mathf.Min(armor, amount * 0.7f);
            armor -= soak; amount -= soak;
        }
        hp = Mathf.Max(0f, hp - amount);
        Sync();
        OnDamaged?.Invoke(this, amount, attacker);

        if (hp <= 0f) { Die(attacker); return; }

        HeistAnimInfo.Trigger(_anim, "Hit");
        if (!isPlayer && team != Team.Civilians && Time.time - _lastHitMsg > 2.5f)
        {
            _lastHitMsg = Time.time;
            bool mine = team == GameData.PlayerTeam;
            Hud.Notify($"{displayName} was hit ({Mathf.CeilToInt(hp)} HP)", mine ? Hud.Warn : Hud.Info);
        }
        if (isPlayer)
        {
            Hud.DamageFlash();
            // the brief wants a message whenever a player is hit - you included
            if (Time.time - _lastHitMsg > 2.5f)
            {
                _lastHitMsg = Time.time;
                string by = attacker != null && !string.IsNullOrEmpty(attacker.displayName) ? $" by {attacker.displayName}" : "";
                Hud.Notify($"You were hit{by}! ({Mathf.CeilToInt(hp)} HP)", Hud.Bad, 2.5f);
            }
        }
    }

    public void Heal(float amount) { if (!Dead) { hp = Mathf.Min(maxHp, hp + amount); Sync(); } }

    void Sync()
    {
        if (isPlayer) { GameData.hp = hp; GameData.armor = armor; }
        else if (!string.IsNullOrEmpty(memberId))
        {
            var m = GameData.Get(memberId);
            if (m != null) { m.hp = hp; m.armor = armor; m.alive = hp > 0f; }
        }
    }

    void Die(Health attacker)
    {
        Dead = true;
        Sync();
        OnDied?.Invoke(this);

        if (!isPlayer)
        {
            bool mine = team == GameData.PlayerTeam;
            string msg = team == Team.Civilians ? "A civilian was killed"
                       : team == Team.Police ? $"{displayName} is down!" : $"{displayName} has been killed!";
            Hud.Notify(msg, mine ? Hud.Bad : Hud.Good);
            if (team == Team.Civilians && attacker != null && attacker.isPlayer)
                GameData.wanted = Mathf.Min(5, GameData.wanted + 1);
        }

        foreach (var c in GetComponents<Collider>()) if (!(c is CharacterController)) c.enabled = false;
        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        StartCoroutine(FallOver());
        HeistDirector.Instance?.OnDeath(this);
    }

    // Death reaction: use the "Dead" animation when the controller has one;
    // otherwise topple the body over like a ragdoll going limp.
    IEnumerator FallOver()
    {
        // the builder only adds a "Dead" parameter when a real death clip exists
        if (HeistAnimInfo.HasParam(_anim, "Dead")) { _anim.SetBool("Dead", true); yield break; }

        if (_anim != null) _anim.speed = 0.15f;
        Quaternion from = transform.rotation;
        Quaternion to = Quaternion.AngleAxis(-88f, transform.right) * from;
        Vector3 start = transform.position;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.45f)
        {
            float e = t * t;
            transform.rotation = Quaternion.Slerp(from, to, e);
            transform.position = start + Vector3.up * 0.15f * Mathf.Sin(e * Mathf.PI);
            yield return null;
        }
        transform.rotation = to;
        transform.position = start + Vector3.up * 0.12f;
        if (_anim != null) _anim.enabled = false;
    }
}

// The generated animator only has a parameter when the matching Mixamo clip was
// found, so runtime code can fall back gracefully (e.g. topple instead of a
// death animation, or rotate the body flat instead of a lie-down clip).
public static class HeistAnimInfo
{
    public static bool HasParam(Animator a, string name)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters) if (p.name == name) return true;
        return false;
    }

    public static void Trigger(Animator a, string name) { if (HasParam(a, name)) a.SetTrigger(name); }
    public static void Bool(Animator a, string name, bool v) { if (HasParam(a, name)) a.SetBool(name, v); }
    public static void Float(Animator a, string name, float v, float damp = 0.1f)
    { if (HasParam(a, name)) a.SetFloat(name, v, damp, Time.deltaTime); }
}
