using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum SceneKind { City, Bank, Vault }

// Runs one gameplay scene (City street / Bank lobby / Vault): spawns the player
// for the chosen side, their NPC crew (with HP carried in GameData), the enemy
// team and the civilians; drives the mission (objectives, alarm, vault, escape);
// and decides WASTED / MISSION PASSED / MISSION FAILED.
public class HeistDirector : MonoBehaviour
{
    public static HeistDirector Instance;

    public SceneKind kind;
    public Font font, bigFont;
    public Camera minimapCam;
    public GameObject[] robberLooks, copLooks, civilianLooks;
    public BankQueue queue;
    public Transform[] civilianSpots;       // tellers / pre-placed customers / pedestrians
    public Transform[] wanderPoints;        // street pedestrians roam between these
    public Transform safehouse;             // robber escape destination (City)
    public Transform policeLine;            // bystander safe zone (City)
    public Transform entrance;              // bank front door (Bank) - late arrivals come in here
    public GameObject alarmLight;           // red rotating lights, on when the alarm rings
    public GameObject policeScene;          // police cars + tape outside the bank (City)

    PlayerController _player;
    bool _ended, _alarm;
    float _t;
    AudioSource _alarmSrc;
    bool _bystanderRobbersIn, _bystanderPoliceIn;

    const float CopTimeLimit = 300f;

    void Awake()
    {
        Instance = this;
        if (!GameData.started)
        {
            // pressing Play in a gameplay scene directly (or the Tools > Bank
            // Heist > Test ... menu): start as the side the test menu picked
            GameData.NewGame((Side)PlayerPrefs.GetInt("heist_test_side", 0));
            GameData.spawnId = PlayerPrefs.GetString("heist_test_spawn", "start");
        }
        Squad.ClearAll();
        Time.timeScale = 1f;
        // heavy downloads (street trees, Volvo, chandeliers) carry generated mesh LODs:
        // switch to the lighter levels sooner so the street holds a playable frame rate
        QualitySettings.meshLodThreshold = 4f;
        Hud.Ensure(font, bigFont);
        if (minimapCam != null)
        {
            var rt = new RenderTexture(512, 512, 16);
            minimapCam.targetTexture = rt;
            minimapCam.enabled = true;
            Hud.I.minimapCam = minimapCam;
        }
    }

    void Start()
    {
        SpawnPlayer();
        SpawnCrew();
        SpawnEnemies();
        SpawnCivilians();
        SetupScene();
    }

    GameObject Look(Team t, int i)
    {
        var arr = t == Team.Robbers ? robberLooks : t == Team.Police ? copLooks : civilianLooks;
        if (arr == null || arr.Length == 0) return null;
        return arr[Mathf.Abs(i) % arr.Length];
    }

    // ------------------------------------------------------------- spawning

    void SpawnPlayer()
    {
        var sp = SpawnPoint.Find(GameData.spawnId) ?? SpawnPoint.Find("start");
        Vector3 pos = sp != null ? sp.transform.position : Vector3.zero;
        Quaternion rot = sp != null ? sp.transform.rotation : Quaternion.identity;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f; cam.farClipPlane = 600f;
        int mm = LayerMask.NameToLayer("Minimap");
        if (mm >= 0) cam.cullingMask &= ~(1 << mm);
        camGo.AddComponent<AudioListener>();
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        if (data != null) { data.renderPostProcessing = true; data.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing; }

        GameObject look = GameData.side == Side.Robber ? Look(Team.Robbers, 3)
                        : GameData.side == Side.Cop ? Look(Team.Police, 2) : Look(Team.Civilians, 1);
        _player = CharacterFactory.Player(look, pos, rot, cam, GameData.side != Side.Bystander);
        _player.Yaw = rot.eulerAngles.y;
        var sq = Squad.Of(GameData.PlayerTeam);
        sq.leader = _player.transform;
        sq.order = Order.Follow;
    }

    void SpawnCrew()
    {
        if (GameData.side == Side.Bystander) return;
        int i = 0;
        foreach (var m in GameData.Crew())
        {
            Vector3 f = _player.transform.forward, r = _player.transform.right;
            int row = i / 2 + 1; float side = i % 2 == 0 ? -1f : 1f;
            Vector3 p = _player.transform.position - f * 1.8f * row + r * side * 1.6f;
            if (UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) p = hit.position;
            CharacterFactory.Soldier(Look(m.team, m.look), m, p, _player.transform.rotation, false);
            i++;
        }
        var sq = Squad.Of(GameData.PlayerTeam);
        sq.retreatPoint = _player.transform.position;
    }

    string SceneName => kind.ToString();

    void SpawnEnemies()
    {
        foreach (var m in GameData.members)
        {
            if (!m.alive || m.team == GameData.PlayerTeam && GameData.side != Side.Bystander) continue;
            if (m.scene != SceneName) continue;
            if (!EnemyPresent(m)) continue;
            SpawnMember(m, null);
        }
        foreach (Team t in new[] { Team.Robbers, Team.Police })
        {
            if (t == GameData.PlayerTeam) continue;
            var sq = Squad.Of(t);
            sq.order = Order.Hold;
            var cmd = gameObject.AddComponent<AICommander>();
            cmd.team = t;
            cmd.leaderName = t == Team.Robbers ? "Vince" : "Sgt. Hayes";
        }
    }

    // who is actually here right now, given the side and how far the heist is
    bool EnemyPresent(MemberState m)
    {
        bool swat = m.id == "P3" || m.id == "P4";
        if (GameData.side == Side.Robber && swat) return GameData.phase >= Phase.VaultOpen;
        if (GameData.side == Side.Bystander && kind == SceneKind.Bank)
        {
            if (m.team == Team.Robbers) return _bystanderRobbersIn;
            if (swat) return _bystanderPoliceIn;
        }
        return true;
    }

    SoldierAI SpawnMember(MemberState m, Transform at)
    {
        var sp = at != null ? at : SpawnPoint.Find(m.id)?.transform;
        Vector3 pos = sp != null ? sp.position : transform.position;
        Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 4f, UnityEngine.AI.NavMesh.AllAreas)) pos = hit.position;
        bool leader = m.id == "R1" && GameData.side != Side.Robber;
        var ai = CharacterFactory.Soldier(Look(m.team, m.look), m, pos, rot, leader);
        ai.home = pos;
        return ai;
    }

    void SpawnCivilians()
    {
        if (civilianLooks == null || civilianLooks.Length == 0) return;
        bool calm = GameData.phase == Phase.Approach || (GameData.side == Side.Bystander && !_bystanderRobbersIn);
        int n = 0;
        if (kind == SceneKind.Bank && queue != null)
        {
            if (calm)
            {
                int customers = GameData.side == Side.Bystander ? queue.slots.Length - 2 : queue.slots.Length - 1;
                for (int i = 0; i < customers; i++)
                {
                    // the bystander player takes the 2nd place in line
                    int slot = GameData.side == Side.Bystander && i >= 1 ? i + 1 : i;
                    var c = CharacterFactory.Civilian(Look(Team.Civilians, n++), queue.slots[slot].position, queue.slots[slot].rotation, "Customer");
                    c.JoinQueue(queue, queue.exit.position);
                }
            }
        }
        if (civilianSpots != null)
            foreach (var s in civilianSpots)
            {
                var c = CharacterFactory.Civilian(Look(Team.Civilians, n++), s.position, s.rotation, kind == SceneKind.Bank ? "Teller" : "Pedestrian");
                if (kind == SceneKind.City && wanderPoints != null && wanderPoints.Length > 0) { c.mode = Civilian.Mode.Wander; c.wanderPoints = wanderPoints; c.KeepToSidewalk(); }
                else c.mode = Civilian.Mode.Wander;
                if (!calm) StartCoroutine(DownSoon(c));
            }
        if (!calm && kind == SceneKind.Bank && queue != null)
        {
            // the robbery already happened here: customers are on the floor
            for (int i = 0; i < queue.slots.Length - 1; i++)
            {
                var c = CharacterFactory.Civilian(Look(Team.Civilians, n++), queue.slots[i].position + Random.insideUnitSphere.WithY(0f) * 1.2f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), "Customer");
                StartCoroutine(DownSoon(c));
            }
            queue.running = false;
        }
    }

    IEnumerator DownSoon(Civilian c) { yield return null; c.GetDown(); }

    // --------------------------------------------------------------- mission

    void SetupScene()
    {
        var s = GameData.side;
        if (kind == SceneKind.City)
        {
            if (s == Side.Robber && GameData.phase >= Phase.VaultOpen) StartEscape();
            if (policeScene != null)
                policeScene.SetActive(s == Side.Cop || (s == Side.Bystander && GameData.phase >= Phase.Breached)
                                      || (s == Side.Robber && GameData.phase >= Phase.VaultOpen));
            if (s == Side.Robber)
                Hud.Objective(GameData.phase < Phase.VaultOpen ? "Go into the <color=#e6c34a>bank</color> with your crew." : "Get to the <color=#e6c34a>getaway car</color> and lose the cops!");
            else if (s == Side.Cop)
                Hud.Objective(GameData.AliveCount(Team.Robbers) > 0 ? "Robbery in progress! Enter the <color=#e6c34a>bank</color> and stop the crew." : "The bank is secure.");
            else
                Hud.Objective(GameData.phase >= Phase.Breached ? "Get behind the <color=#e6c34a>police line</color>!" : "Go into the <color=#e6c34a>bank</color> to make a deposit.");
            if (GameData.phase >= Phase.Breached) { StartAlarm(); PanicStreet(); }
        }
        else if (kind == SceneKind.Bank)
        {
            if (s == Side.Robber && GameData.phase == Phase.Approach) StartCoroutine(RobbersBurstIn());
            else if (s == Side.Robber) Hud.Objective(GameData.phase >= Phase.VaultOpen ? "Get out to the <color=#e6c34a>getaway car</color>!" : "Go down to the <color=#e6c34a>vault</color>.");
            else if (s == Side.Cop) { Hud.Objective("Take out the robbers, then check the <color=#e6c34a>vault</color>."); StartAlarm(); }
            else Hud.Objective("Wait in line for the teller.");
            if (s != Side.Robber && GameData.phase >= Phase.Breached) StartAlarm();
        }
        else // vault
        {
            if (s == Side.Robber)
                Hud.Objective(VaultDoor.IsOpen || GameData.phase >= Phase.VaultOpen ? "Grab the <color=#6fbf5a>cash</color>, then get out!" : "Burn open the <color=#e6c34a>vault door</color> (hold E).");
            else if (s == Side.Cop) Hud.Objective("Stop the robbers in the vault!");
            else Hud.Objective("You shouldn't be down here... get back upstairs!");
            if (GameData.phase >= Phase.Breached) StartAlarm();
        }
    }

    // ---- the escape: van waiting at the doors, the street full of police noise ----

    static readonly Vector3 VanAtDoors = new Vector3(3f, 0.05f, 4.1f);   // in the lane, clear of the kerb

    void StartEscape()
    {
        // the driver brought the van round: it's at the kerb right outside the bank
        foreach (var v in FindObjectsByType<Vehicle>(FindObjectsSortMode.None))
            if (v.isGetaway)
            {
                var rb = v.GetComponent<Rigidbody>();
                v.transform.SetPositionAndRotation(VanAtDoors, Quaternion.Euler(0f, 90f, 0f));
                if (rb != null) { rb.position = VanAtDoors; rb.rotation = v.transform.rotation; rb.linearVelocity = Vector3.zero; }
                break;
            }
        ClearEscapeRoute();
        if (policeScene != null)
        {
            // every cruiser's siren going, loud, and the helicopter right overhead
            foreach (var pl in policeScene.GetComponentsInChildren<PoliceLights>(true)) { pl.siren = true; pl.sirenVolume = 0.9f; pl.sirenRange = 140f; }
            foreach (var h in policeScene.GetComponentsInChildren<PoliceHelicopter>(true)) { h.volume = 1f; h.radius = 22f; h.height = 24f; }
        }
        // more units on the way: a far-off siren wail under everything
        var far = Sfx.Clip("siren");
        if (far != null)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = far; a.loop = true; a.spatialBlend = 0f; a.volume = 0.22f; a.pitch = 0.93f; a.Play();
        }
        StartCoroutine(PoliceFire());
        SpawnCordonCops();
        GameData.wanted = 5;
        Hud.Notify("Every cop in the city is outside!", Hud.Bad, 4f);
    }

    // a wall of officers behind the cruisers, plus two flanking the doors - real
    // shooters, but these extras hit lightly so the dash to the van is survivable
    void SpawnCordonCops()
    {
        var spots = new System.Collections.Generic.List<Vector3>();
        for (float x = -22f; x <= 22f; x += 4.4f) spots.Add(new Vector3(x + Random.Range(-0.8f, 0.8f), 0f, -6.6f + Random.Range(-0.6f, 0.6f)));
        spots.Add(new Vector3(-24f, 0.15f, 10.5f)); spots.Add(new Vector3(25f, 0.15f, 10.5f));
        int n = 0;
        foreach (var spot in spots)
        {
            if (!UnityEngine.AI.NavMesh.SamplePosition(spot, out var hit, 2.5f, UnityEngine.AI.NavMesh.AllAreas)) continue;
            var m = new MemberState("X" + n, "Officer", Team.Police, n, "City", 15f) { hp = 60f };
            var face = Quaternion.LookRotation(new Vector3(0f, 0f, 9f) - hit.position.WithY(0f));
            var ai = CharacterFactory.Soldier(Look(Team.Police, n), m, hit.position, face, false);
            ai.preferredRange = 30f;
            var w = ai.GetComponentInChildren<Weapon>();
            if (w != null) { w.damage = 2.2f; w.spreadDeg = 4f; }
            n++;
        }
    }

    // The police have shut the roads: no moving traffic, and nothing parked on the
    // way to the safehouse (avenue east -> first right -> back street east).
    void ClearEscapeRoute()
    {
        foreach (var t in FindObjectsByType<TrafficCar>(FindObjectsSortMode.None)) t.gameObject.SetActive(false);
        bool OnRoute(Vector3 p) =>
            (p.x > -4f && p.x < 68f && p.z > -1f && p.z < 8.5f)                                   // the avenue, bank -> cross street
            || (p.x > 50f && p.x < 70f && p.z > -80f && p.z < 8.5f)                               // the cross street, south
            || (p.x > 50f && p.x < safehouse.position.x + 12f && p.z > -80f && p.z < -60f);       // the back street, east
        var city = GameObject.Find("City");
        if (city == null || safehouse == null) return;
        foreach (var name in new[] { "ParkedCars", "StreetExtras" })
        {
            var root = city.transform.Find(name);
            if (root == null) continue;
            foreach (Transform c in root) if (OnRoute(c.position)) c.gameObject.SetActive(false);
        }
    }

    // the cordon opens up on the robbers: bursts of fire from behind the cruisers,
    // tracers and impacts kicking up around you (noise and spectacle - the hits miss)
    IEnumerator PoliceFire()
    {
        yield return new WaitForSeconds(1.5f);
        var guns = policeScene != null ? policeScene.GetComponentsInChildren<PoliceLights>(true) : new PoliceLights[0];
        while (!_ended && guns.Length > 0)
        {
            var p = PlayerController.Instance;
            if (p == null) break;
            Vector3 target = Vehicle.Current != null ? Vehicle.Current.transform.position : p.transform.position;
            // far from the cordon (driving away) - just the odd distant shot
            var g = guns[Random.Range(0, guns.Length)];
            if (g == null) { yield return null; continue; }
            Vector3 from = g.transform.position + Vector3.up * 1.4f - g.transform.right * 1.2f;
            float dist = Vector3.Distance(from, target);
            int shots = Random.Range(3, 8);
            for (int i = 0; i < shots && !_ended; i++)
            {
                Vector3 aim = target + new Vector3(Random.Range(-4f, 4f), Random.Range(0.3f, 2.2f), Random.Range(-4f, 4f));
                Vector3 dir = (aim - from).normalized;
                Sfx.Play("gunshot", from, dist < 60f ? 0.85f : 0.5f, 0.08f, 220f);
                if (dist < 70f)
                {
                    Fx.MuzzleFlash(from, dir);
                    if (Physics.Raycast(from, dir, out var hit, 90f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<Health>() == null)
                    {
                        Fx.Tracer(from, hit.point);
                        Fx.Impact(hit.point, hit.normal, false);
                    }
                    else Fx.Tracer(from, from + dir * 40f);
                }
                yield return new WaitForSeconds(Random.Range(0.09f, 0.16f));
            }
            yield return new WaitForSeconds(dist < 70f ? Random.Range(0.8f, 2.2f) : Random.Range(3f, 6f));
        }
    }

    IEnumerator RobbersBurstIn()
    {
        Hud.Objective("");
        yield return new WaitForSeconds(0.8f);
        Sfx.Play("shout_down", _player.transform.position, 1f, 0f);
        Hud.Notify("\"EVERYBODY ON THE FLOOR! THIS IS A ROBBERY!\"", Hud.Warn, 5f);
        Alarm();
        Hud.Objective("Deal with the guard, then go down to the <color=#e6c34a>vault</color>.");
    }

    void Update()
    {
        if (_ended) return;
        GameData.missionTime += Time.deltaTime;
        _t += Time.deltaTime;
        var s = GameData.side;

        if (s == Side.Cop)
        {
            float left = CopTimeLimit - GameData.missionTime;
            if (left <= 0f) { Fail("The robbers escaped with the money."); return; }
            if (GameData.AliveCount(Team.Robbers) > 0 && (int)(left) % 1 == 0)
                Hud.Objective($"Stop the robbers   <color=#e6c34a>{(int)left / 60}:{(int)left % 60:00}</color>");
        }

        if (s == Side.Bystander && kind == SceneKind.Bank)
        {
            if (!_bystanderRobbersIn && _t > 9f) StartCoroutine(BystanderRobbery());
            if (_bystanderRobbersIn && !_bystanderPoliceIn && _t > 42f) BystanderPolice();
        }

        if (s == Side.Bystander && kind == SceneKind.City && GameData.phase >= Phase.Breached && policeLine != null
            && Vector3.Distance(_player.transform.position, policeLine.position) < 5f)
            Pass("YOU SURVIVED", "You made it out of the bank alive.");

        // only counts as a getaway once there's loot - driving off at the start isn't a heist
        if (s == Side.Robber && kind == SceneKind.City && GameData.money > 0 && Vehicle.Current != null && safehouse != null
            && Vector3.Distance(Vehicle.Current.transform.position, safehouse.position) < 9f)
            Pass("BANK HEIST", $"You got away with ${GameData.money:N0}.");

        // wanted level creeps up while the heist is hot
        if (s == Side.Robber && GameData.phase >= Phase.Breached && GameData.phase < Phase.Done)
        {
            int want = kind == SceneKind.City && GameData.phase >= Phase.VaultOpen ? 5 : GameData.phase >= Phase.Escape ? 4 : GameData.phase >= Phase.VaultOpen ? 3 : 2;
            if (GameData.wanted < want) GameData.wanted = want;
        }
    }

    // ---- bystander timeline: robbers burst in, later SWAT storms the lobby ----
    IEnumerator BystanderRobbery()
    {
        _bystanderRobbersIn = true;
        foreach (var m in GameData.members)
            if (m.team == Team.Robbers && m.alive && m.scene == "Bank")
            {
                var ai = SpawnMember(m, entrance);
                ai.home = SpawnPoint.Find(m.id) != null ? SpawnPoint.Find(m.id).transform.position : ai.home;
                yield return new WaitForSeconds(0.4f);
            }
        Squad.Of(Team.Robbers).order = Order.Hold;
        Sfx.Play("shout_down", entrance != null ? entrance.position : transform.position, 1f, 0f);
        Hud.Notify("\"EVERYBODY ON THE FLOOR! NOW!\"", Hud.Bad, 5f);
        Alarm();
        Hud.Objective("GET DOWN! Press <color=#e6c34a>C</color> to lie on the floor.");
        yield return new WaitForSeconds(4.5f);
        if (!_player.Down) { CivilianDefiance.IsDefiant = true; Hud.Notify("The robbers noticed you standing!", Hud.Bad); }
    }

    public void OnPlayerDown(bool down)
    {
        if (GameData.side != Side.Bystander) return;
        if (down) { CivilianDefiance.IsDefiant = false; if (_bystanderRobbersIn) Hud.Objective("Stay down until the police arrive..."); }
        else if (_bystanderRobbersIn && GameData.AliveCount(Team.Robbers) > 0 && kind == SceneKind.Bank)
        { CivilianDefiance.IsDefiant = true; Hud.Objective("Get back down - or run for the <color=#e6c34a>exit</color>!"); }
    }

    void BystanderPolice()
    {
        _bystanderPoliceIn = true;
        foreach (var m in GameData.members)
            if ((m.id == "P3" || m.id == "P4") && m.alive) { m.scene = "Bank"; SpawnMember(m, entrance); }
        var sq = Squad.Of(Team.Police); sq.order = Order.Attack;
        Sfx.Play("police_shout", entrance != null ? entrance.position : transform.position, 1f, 0f);
        Hud.Notify("\"POLICE! DROP YOUR WEAPONS!\"", Hud.Info, 5f);
        Hud.Objective("SWAT is here. Stay down - or make a run for the <color=#e6c34a>exit</color>.");
    }

    // ------------------------------------------------------------ events

    public void Alarm()
    {
        if (_alarm) return;
        if (GameData.phase < Phase.Breached) GameData.phase = Phase.Breached;
        StartAlarm();
        if (queue != null) queue.Stop();
        foreach (var c in Civilian.All.ToArray()) if (c != null) c.GetDown();
        if (GameData.side == Side.Robber) Hud.Notify("The silent alarm was tripped - cops are on the way!", Hud.Warn, 5f);
    }

    void StartAlarm()
    {
        _alarm = true;
        if (alarmLight != null) alarmLight.SetActive(true);
        if (_alarmSrc == null && kind != SceneKind.City)
        {
            var clip = Sfx.Clip("alarm");
            if (clip != null)
            {
                _alarmSrc = gameObject.AddComponent<AudioSource>();
                _alarmSrc.clip = clip; _alarmSrc.loop = true; _alarmSrc.volume = 0.25f; _alarmSrc.spatialBlend = 0f;
                _alarmSrc.Play();
            }
        }
    }

    void PanicStreet()
    {
        foreach (var c in Civilian.All.ToArray()) if (c != null && c.mode == Civilian.Mode.Wander) c.Panic(_player.transform.position);
    }

    public void OnGunfire(Health shooter, Vector3 at)
    {
        PigeonFlock.Alarm(at, 45f);
        if (_ended) return;
        if (kind == SceneKind.Bank && !_alarm && shooter != null && shooter.team != Team.Civilians) Alarm();
        foreach (var c in Civilian.All.ToArray())
            if (c != null && c.mode != Civilian.Mode.Down && (c.transform.position - at).sqrMagnitude < 30f * 30f)
            {
                if (kind == SceneKind.City) c.Panic(at); else c.GetDown();
            }
    }

    public void OnVaultOpened()
    {
        GameData.phase = Phase.VaultOpen;
        // the SWAT response is now on scene: one team in the lobby, one outside
        var p3 = GameData.Get("P3"); if (p3 != null && GameData.side == Side.Robber) p3.scene = "Bank";
        Hud.Objective("Grab the <color=#6fbf5a>cash</color>, then get out!");
        Hud.Notify("Police scanner: SWAT units en route to the bank!", Hud.Warn, 5f);
    }

    public void OnLooted()
    {
        if (GameData.phase < Phase.Escape && GameData.money >= 1000000)
        {
            GameData.phase = Phase.Escape;
            Hud.Objective("That's enough! Get to the <color=#e6c34a>getaway car</color>!");
        }
    }

    public void OnEnteredVehicle(Vehicle v)
    {
        if (GameData.side == Side.Robber)
            Hud.Objective("Drive to the <color=#e6c34a>safehouse</color> (yellow marker on the map)!");
    }

    public void OnDeath(Health h)
    {
        if (_ended) return;
        if (h.isPlayer) { StartCoroutine(Wasted()); return; }
        if (GameData.side == Side.Cop && h.team == Team.Robbers && GameData.AliveCount(Team.Robbers) == 0)
        {
            GameData.money += 5000;
            Pass("ROBBERY FOILED", "Every robber has been taken down.");
            return;
        }
        if (GameData.side == Side.Cop && h.team == Team.Robbers) { GameData.money += 1500; Hud.Notify("+$1,500 bounty", Hud.Good, 2f); }
        if (GameData.side == Side.Bystander && h.team == Team.Robbers && kind == SceneKind.Bank && _bystanderRobbersIn
            && Squad.Of(Team.Robbers).AliveCount() == 0)
            Pass("YOU SURVIVED", "The police took down the robbers.");
        if (h.team == GameData.PlayerTeam && GameData.side != Side.Bystander && Squad.Of(GameData.PlayerTeam).AliveCount() == 0)
            Hud.Notify("Your whole crew is down - you're on your own!", Hud.Bad, 5f);
    }

    // ------------------------------------------------------ end of mission

    IEnumerator Wasted()
    {
        _ended = true;
        GameData.phase = Phase.Done;
        Time.timeScale = 0.35f;
        Sfx.Play2D("wasted", 1f);
        yield return new WaitForSecondsRealtime(1.2f);
        Hud.Banner("wasted", "WASTED", "", "");
        Debug.Log("RESULT: WASTED");
        yield return new WaitForSecondsRealtime(3f);
        Time.timeScale = 1f;
        Hud.Banner("failed", "MISSION FAILED", "You died.", "ENTER - retry      ESC - main menu");
        yield return WaitRetry();
    }

    void Fail(string why)
    {
        if (_ended) return;
        _ended = true;
        GameData.phase = Phase.Done;
        Sfx.Play2D("failed", 1f);
        Hud.Banner("failed", "MISSION FAILED", why, "ENTER - retry      ESC - main menu");
        Debug.Log("RESULT: MISSION FAILED - " + why);
        StartCoroutine(WaitRetry());
    }

    void Pass(string title, string detail)
    {
        if (_ended) return;
        _ended = true;
        GameData.phase = Phase.Done;
        GameData.wanted = 0;
        Sfx.Play2D("passed", 1f);
        Debug.Log($"RESULT: MISSION PASSED - {title} - {detail}");
        int mins = (int)GameData.missionTime / 60, secs = (int)GameData.missionTime % 60;
        Hud.Banner("passed", "MISSION PASSED", title + " - " + detail,
                   $"CASH ${GameData.money:N0}     TIME {mins}:{secs:00}     ENTER - play again      ESC - main menu");
        StartCoroutine(WaitRetry());
    }

    IEnumerator WaitRetry()
    {
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        while (true)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            {
                GameData.NewGame(GameData.side);
                LoadScene(GameData.side == Side.Bystander ? "Bank" : "City", GameData.side == Side.Bystander ? "queue" : "start");
                yield break;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { LoadScene("MainMenu", "start"); yield break; }
            yield return null;
        }
    }

    // -------------------------------------------------------- scene changes

    public void GoTo(string scene, string spawn)
    {
        if (_ended) return;
        StartCoroutine(Transition(scene, spawn));
    }

    IEnumerator Transition(string scene, string spawn)
    {
        _ended = true;   // freeze mission logic during the fade
        if (Hud.I != null) yield return Hud.I.FadeOut(0.5f);
        LoadScene(scene, spawn);
    }

    public static void LoadScene(string scene, string spawn)
    {
        GameData.spawnId = spawn;
        Time.timeScale = 1f;
        CivilianDefiance.IsDefiant = false;
        SceneManager.LoadScene(scene);
    }
}

public static class VecExt
{
    public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
}
