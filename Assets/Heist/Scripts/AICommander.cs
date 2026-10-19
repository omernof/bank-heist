using UnityEngine;

// Commands an NPC-led squad (the robbers when the player is a cop, the police
// when the player is a robber, and both in Bystander mode). Every couple of
// seconds it reads the fight and issues the same orders a human commander can:
// attack when enemies show up, lob a grenade at a group, fall back when the
// squad is getting cut down, otherwise hold / follow the leader.
public class AICommander : MonoBehaviour
{
    public Team team;
    public string leaderName = "Leader";
    public float retreatBelow = 0.34f;     // fraction of the squad left alive
    public float grenadeCooldown = 12f;

    int _startCount = -1;
    float _nextThink, _nextGrenade;
    Order _last = Order.Hold;

    void Update()
    {
        if (Time.time < _nextThink) return;
        _nextThink = Time.time + 1.5f;

        var sq = Squad.Of(team);
        int alive = sq.AliveCount();
        if (_startCount < 0 && alive > 0) _startCount = alive;
        if (alive == 0) return;

        // who can we see?
        int seen = 0; Vector3 centroid = Vector3.zero; Health anyTarget = null;
        foreach (var m in sq.members)
            if (m != null && !m.Health.Dead && m.Target != null)
            { seen++; centroid += m.Target.transform.position; anyTarget = m.Target; }
        if (seen > 0) centroid /= seen;

        Order next = sq.order;
        if (_startCount > 1 && alive <= Mathf.Max(1, Mathf.FloorToInt(_startCount * retreatBelow)) && seen > 0)
            next = Order.Retreat;
        else if (seen > 0)
        {
            next = Order.Attack;
            // two or more enemies bunched together -> grenade
            if (Time.time > _nextGrenade && anyTarget != null && EnemiesNear(anyTarget.transform.position, 4f) >= 2)
            {
                _nextGrenade = Time.time + grenadeCooldown;
                sq.Command(Order.Grenade, anyTarget.transform.position, false);
                Hud.Notify($"{leaderName}: Grenade on them!", team == GameData.PlayerTeam ? Hud.Info : Hud.Warn);
                return;
            }
        }
        else if (sq.order == Order.Attack && Time.time - sq.orderTime > 8f)
            next = sq.leader != null ? Order.Follow : Order.Hold;

        if (next != sq.order)
        {
            sq.Command(next, next == Order.Hold ? sq.orderPoint : centroid, false);
            if (next != _last && next != Order.Hold && next != Order.Follow)
                Hud.Notify($"{leaderName}: {Squad.Label(next)}", team == GameData.PlayerTeam ? Hud.Info : Hud.Warn);
            _last = next;
        }
    }

    int EnemiesNear(Vector3 p, float r)
    {
        int n = 0;
        Team enemy = GameData.EnemyOf(team);
        foreach (var h in Health.All)
            if (h != null && !h.Dead && h.team == enemy && (h.transform.position - p).sqrMagnitude < r * r) n++;
        return n;
    }
}
