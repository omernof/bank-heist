using System.Collections.Generic;
using UnityEngine;

public enum Order { Follow, Attack, Grenade, Retreat, Hold }

// A team's squad: its leader (the player or an NPC), its members and the
// current order. The player commands their own squad (keys 1-5 / the TAB
// wheel); NPC-led squads are commanded by an AICommander.
public class Squad
{
    static readonly Dictionary<Team, Squad> s_all = new Dictionary<Team, Squad>();

    public Team team;
    public Order order = Order.Follow;
    public Transform leader;                // who "Follow" formations form up on
    public Vector3 orderPoint;              // Hold / Grenade / Attack focus point
    public Vector3 retreatPoint;            // where "Retreat" runs to
    public float orderTime;
    public readonly List<SoldierAI> members = new List<SoldierAI>();

    public static Squad Of(Team t)
    {
        if (!s_all.TryGetValue(t, out var s)) { s = new Squad { team = t }; s_all[t] = s; }
        return s;
    }

    // squads are per scene - forget the old members when a new scene loads
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => s_all.Clear();
    public static void ClearAll() => s_all.Clear();

    public void Register(SoldierAI m) { if (!members.Contains(m)) members.Add(m); }
    public void Unregister(SoldierAI m) => members.Remove(m);

    public int AliveCount()
    {
        int n = 0;
        foreach (var m in members) if (m != null && !m.Health.Dead) n++;
        return n;
    }

    // V formation behind the leader, ~1.8 m between members so they never bunch
    // up or bump into each other
    public Vector3 SlotFor(SoldierAI m)
    {
        if (leader == null) return m.transform.position;
        int i = 0;
        foreach (var o in members) { if (o == m) break; if (o != null && !o.Health.Dead && !o.isLeader) i++; }
        int row = i / 2 + 1;
        float side = (i % 2 == 0) ? -1f : 1f;
        Vector3 f = leader.forward; f.y = 0f; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        return leader.position - f * (1.7f * row) + r * side * (1.5f + 0.4f * row);
    }

    public void Command(Order o, Vector3 point, bool announce = true)
    {
        order = o;
        orderPoint = point;
        orderTime = Time.time;
        if (o == Order.Grenade)
        {
            // the closest member who still has a grenade throws it
            SoldierAI best = null; float bd = float.MaxValue;
            foreach (var m in members)
                if (m != null && !m.Health.Dead && !m.isLeader && m.grenades > 0)
                {
                    float d = (m.transform.position - point).sqrMagnitude;
                    if (d < bd) { bd = d; best = m; }
                }
            if (best != null) best.QueueGrenade(point);
            order = Order.Attack;   // everyone else keeps fighting
        }
        if (announce && team == GameData.PlayerTeam && GameData.side != Side.Bystander)
            Hud.Notify("Crew: " + Label(o), Hud.Info);
    }

    public static string Label(Order o)
    {
        switch (o)
        {
            case Order.Follow:  return "Follow me";
            case Order.Attack:  return "Open fire!";
            case Order.Grenade: return "Grenade out!";
            case Order.Retreat: return "Fall back!";
            default:            return "Hold position";
        }
    }
}
