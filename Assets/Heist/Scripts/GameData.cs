using System.Collections.Generic;
using UnityEngine;

public enum Side { Robber, Cop, Bystander }
public enum Team { Robbers, Police, Civilians }
public enum Phase { Approach, Breached, VaultOpen, Escape, Done }

// One NPC squad member (or an enemy) whose state must survive scene changes.
[System.Serializable]
public class MemberState
{
    public string id;          // stable key, e.g. "R1", "P3"
    public string name;        // shown in messages: "Rico was hit!"
    public Team team;
    public float hp = 100f;
    public float armor;
    public bool alive = true;
    public int look;           // which character model to use
    public string scene;       // for enemies that are NOT with the player: where they are

    public MemberState(string id, string name, Team team, int look, string scene, float armor = 0f)
    { this.id = id; this.name = name; this.team = team; this.look = look; this.scene = scene; this.armor = armor; }
}

// Everything that persists between the City / Bank / Vault scenes: a plain
// static class survives SceneManager.LoadScene, so HP, money, ammo and every
// squad member's HP carry over.
public static class GameData
{
    public static Side side = Side.Robber;
    public static Phase phase = Phase.Approach;

    // player
    public static float hp = 100f, maxHp = 100f;
    public static float armor = 50f, maxArmor = 100f;
    public static int money;            // "GOLD" in the brief - dollars, GTA style
    public static int ammoInClip = 30, clipSize = 30, ammoReserve = 150;
    public static int grenades = 3;
    public static int wanted;           // 0..5 stars

    // squads (the player's own crew + the opposing team + civilians' outcome)
    public static readonly List<MemberState> members = new List<MemberState>();
    // loot already taken ("Scene/ObjectName") so it doesn't respawn on a return visit
    public static readonly HashSet<string> looted = new HashSet<string>();

    // where the player should appear in the next scene (a ScenePortal id)
    public static string spawnId = "start";
    public static bool started;
    public static float missionTime;

    public static Team PlayerTeam =>
        side == Side.Robber ? Team.Robbers : side == Side.Cop ? Team.Police : Team.Civilians;

    public static Team EnemyOf(Team t) =>
        t == Team.Robbers ? Team.Police : t == Team.Police ? Team.Robbers : Team.Robbers;

    public static void NewGame(Side s)
    {
        side = s;
        // a cop is called to a robbery that is already underway
        phase = s == Side.Cop ? Phase.Breached : Phase.Approach;
        hp = maxHp; armor = s == Side.Bystander ? 0f : 50f;
        money = 0; wanted = 0;
        ammoInClip = clipSize; ammoReserve = s == Side.Bystander ? 0 : 150;
        grenades = s == Side.Bystander ? 0 : 3;
        missionTime = 0f;
        members.Clear();
        looted.Clear();

        // the crew of robbers: with the player when they ARE a robber, otherwise
        // an NPC gang led by "Vince" already inside the bank / vault
        bool robberPlayer = s == Side.Robber;
        members.Add(new MemberState("R1", robberPlayer ? "Rico" : "Vince (leader)", Team.Robbers, 0, robberPlayer ? "" : "Vault", 30f));
        members.Add(new MemberState("R2", "Tony", Team.Robbers, 1, robberPlayer ? "" : "Bank", 20f));
        members.Add(new MemberState("R3", "Lamar", Team.Robbers, 2, robberPlayer ? "" : "Bank", 20f));
        if (!robberPlayer) members.Add(new MemberState("R4", "Dex", Team.Robbers, 1, "Vault", 20f));

        // police / security: the player's squad when they are a cop; otherwise
        // guards in the bank + a SWAT response that arrives after the alarm
        bool copPlayer = s == Side.Cop;
        members.Add(new MemberState("P1", copPlayer ? "Officer Diaz" : "Guard Miller", Team.Police, 0, copPlayer ? "" : "Bank", 20f));
        members.Add(new MemberState("P2", copPlayer ? "Officer Chen" : "Guard Brooks", Team.Police, 1, copPlayer ? "" : "Vault", 20f));
        members.Add(new MemberState("P3", copPlayer ? "Officer Walker" : "SWAT Hayes", Team.Police, 0, copPlayer ? "" : "City", 60f));
        if (!copPlayer) members.Add(new MemberState("P4", "SWAT Ortiz", Team.Police, 1, "City", 60f));

        spawnId = "start";
        started = true;
    }

    public static MemberState Get(string id) => members.Find(m => m.id == id);

    // members travelling with the player (their scene is "")
    public static IEnumerable<MemberState> Crew()
    {
        foreach (var m in members)
            if (m.team == PlayerTeam && m.alive && string.IsNullOrEmpty(m.scene)) yield return m;
    }

    public static int AliveCount(Team t)
    {
        int n = 0;
        foreach (var m in members) if (m.team == t && m.alive) n++;
        return n;
    }
}
