using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The bank lobby (its own scene): marble floor, wood-panelled walls, four
// columns, a long teller counter with glass partitions, the CUSTOMER QUEUE
// between rope stanchions, a waiting area, loan desks, ATMs, and the staff
// door to the vault stairs behind the counter.
public static partial class HeistBuilder
{
    const float BankW = 28f, BankD = 30f, BankH = 8f;

    // one desk from the downloaded Desk Set: the desk, its chair behind it (on
    // the -Z side before `yaw`), a table lamp, a framed photo and books on top
    static void Furnish(Transform parent, Vector3 at, float yaw, int seed)
    {
        var q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 W(Vector3 local) => at + q * local;
        P("DeskSet_02_Desk", parent, at, yaw);
        PFace("DeskSet_03_OfficeChair", parent, W(new Vector3(0f, 0f, 0.75f)), q * Vector3.back);   // behind the desk, facing it
        P("DeskSet_06_TableLamp", parent, W(new Vector3(0.55f, 0.82f, 0.1f)), yaw + 200f);
        P(seed % 2 == 0 ? "DeskSet_04_PhotographyType1" : "DeskSet_05_PhotographyType2", parent, W(new Vector3(-0.55f, 0.82f, 0.15f)), yaw + 160f);
        P(seed % 2 == 0 ? "DeskSet_00_BookType1" : "DeskSet_01_BookType2", parent, W(new Vector3(0.25f, 0.82f, 0.05f)), yaw + 10f * seed);
    }

    // the chandeliers are dense meshes: keep them out of the shadow maps
    static void NoShadows(GameObject g)
    {
        if (g == null) return;
        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // a downloaded office worker (glTF, typing at its own desk) with an animator
    // that plays the clip baked into the file
    static void Worker(Transform parent, string prefab, Vector3 at, float yaw, float scale)
    {
        var g = P(prefab, parent, at, yaw, scale);
        if (g == null) return;
        var anim = g.GetComponentInChildren<Animator>();
        string src = ROOT + "Props/" + prefab + "/" + (prefab == "OfficeWorkerA" ? "scene.gltf" : prefab + ".glb");
        var clip = AssetDatabase.LoadAllAssetsAtPath(src).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (anim != null && clip != null)
        {
            string path = ROOT + "Anim/" + prefab + ".controller";
            AssetDatabase.DeleteAsset(path);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.layers[0].stateMachine.defaultState = ctrl.layers[0].stateMachine.AddState("Typing");
            ctrl.layers[0].stateMachine.defaultState.motion = clip;
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
        }
        g.AddComponent<DeskWorker>();
    }

    static void BuildBankScene()
    {
        s_rng = new System.Random(77);
        var scene = NewScene("Bank");
        Atmosphere(true);
        Probe(new Vector3(0f, 4f, 15f), new Vector3(28.5f, 8.5f, 30.5f), true);
        var room = new GameObject("Lobby").transform;
        var map = new GameObject("MinimapGeometry").transform;
        float hw = BankW * 0.5f;

        // ---- shell ----
        BoxT("Floor", room, new Vector3(0f, -0.1f, BankD * 0.5f), new Vector3(BankW, 0.2f, BankD), "marble_tiles.png", 2.4f, 2.4f, null, true, 0.55f);
        var plaster = Col(new Color(0.9f, 0.86f, 0.78f), 0f, 0.2f);
        Box("WallN", room, new Vector3(0f, BankH * 0.5f, BankD + 0.2f), new Vector3(BankW + 0.8f, BankH, 0.4f), plaster);
        Box("WallW", room, new Vector3(-hw - 0.2f, BankH * 0.5f, BankD * 0.5f), new Vector3(0.4f, BankH, BankD), plaster);
        Box("WallE", room, new Vector3(hw + 0.2f, BankH * 0.5f, BankD * 0.5f), new Vector3(0.4f, BankH, BankD), plaster);
        Box("WallS_L", room, new Vector3(-8.5f, BankH * 0.5f, -0.2f), new Vector3(11f, BankH, 0.4f), plaster);
        Box("WallS_R", room, new Vector3(8.5f, BankH * 0.5f, -0.2f), new Vector3(11f, BankH, 0.4f), plaster);
        Box("WallS_Top", room, new Vector3(0f, 6.25f, -0.2f), new Vector3(6f, 3.5f, 0.4f), plaster);
        Box("Ceiling", room, new Vector3(0f, BankH + 0.15f, BankD * 0.5f), new Vector3(BankW + 0.8f, 0.3f, BankD + 0.8f), Col(new Color(0.82f, 0.78f, 0.7f)));
        // coffered ceiling beams
        var beam = Tex("wood_panel.png", new Vector2(4f, 1f), new Color(0.75f, 0.7f, 0.65f));
        for (float x = -hw + 3.5f; x < hw; x += 7f) Box("BeamZ", room, new Vector3(x, BankH - 0.25f, BankD * 0.5f), new Vector3(0.5f, 0.5f, BankD), beam, false);
        for (float z = 5f; z < BankD; z += 6f) Box("BeamX", room, new Vector3(0f, BankH - 0.25f, z), new Vector3(BankW, 0.5f, 0.5f), beam, false);
        // wood wainscoting + brass rail round the walls
        var wood = Tex("wood_panel.png", new Vector2(6f, 1f));
        Box("WainN", room, new Vector3(0f, 0.7f, BankD - 0.02f), new Vector3(BankW, 1.4f, 0.1f), wood, false);
        Box("WainW", room, new Vector3(-hw + 0.02f, 0.7f, BankD * 0.5f), new Vector3(0.1f, 1.4f, BankD), wood, false);
        Box("WainE", room, new Vector3(hw - 0.02f, 0.7f, BankD * 0.5f), new Vector3(0.1f, 1.4f, BankD), wood, false);
        // entrance: glass doors + frame
        var brassFrame = Col(new Color(0.55f, 0.42f, 0.2f), 1f, 0.7f);
        Box("DoorFrameL", room, new Vector3(-2.9f, 2.3f, -0.05f), new Vector3(0.2f, 4.6f, 0.3f), brassFrame);
        Box("DoorFrameR", room, new Vector3(2.9f, 2.3f, -0.05f), new Vector3(0.2f, 4.6f, 0.3f), brassFrame);
        Box("DoorFrameTop", room, new Vector3(0f, 4.5f, -0.05f), new Vector3(6f, 0.2f, 0.3f), brassFrame);
        Box("DoorGlassL", room, new Vector3(-1.4f, 2.1f, -0.1f), new Vector3(2.6f, 4.1f, 0.06f), Glass, false);
        Box("DoorGlassR", room, new Vector3(1.4f, 2.1f, -0.1f), new Vector3(2.6f, 4.1f, 0.06f), Glass, false);
        Box("DoorBlock", room, new Vector3(0f, 2.3f, -0.6f), new Vector3(6f, 4.6f, 0.2f), Col(new Color(0.9f, 0.85f, 0.7f)));   // daylight outside
        Box("Daylight", room, new Vector3(0f, 2.3f, -0.45f), new Vector3(5.8f, 4.4f, 0.05f), Emissive(new Color(1f, 0.95f, 0.85f), new Color(1.6f, 1.5f, 1.3f)), false);

        // ---- four marble columns ----
        var marbleCol = Tex("marble.png", new Vector2(1f, 3f), null, 0.6f);
        foreach (float x in new[] { -hw + 1.5f, hw - 1.5f })
            foreach (float z in new[] { 7f, 14.5f })
            {
                Cyl("Column", room, new Vector3(x, BankH * 0.5f, z), new Vector3(1.1f, BankH * 0.5f, 1.1f), marbleCol);
                Box("ColBase", room, new Vector3(x, 0.25f, z), new Vector3(1.5f, 0.5f, 1.5f), Col(new Color(0.55f, 0.5f, 0.45f), 0f, 0.5f));
            }

        // ---- teller counter + glass ----
        const float counterZ = 18f;
        var counterWood = Tex("wood_panel.png", new Vector2(8f, 1f));
        var marbleTop = Tex("marble.png", new Vector2(8f, 1f), null, 0.7f);
        Box("Counter", room, new Vector3(-2f, 0.58f, counterZ), new Vector3(20f, 1.16f, 0.9f), counterWood);
        Box("CounterTop", room, new Vector3(-2f, 1.2f, counterZ - 0.1f), new Vector3(20.3f, 0.08f, 1.2f), marbleTop);
        Box("CounterReturn", room, new Vector3(-hw + 1f, 0.58f, counterZ + 3f), new Vector3(0.9f, 1.16f, 6f), counterWood);
        for (int i = 0; i < 5; i++)
        {
            float x = -12f + i * 4f;
            Box("GlassPost", room, new Vector3(x, 1.9f, counterZ), new Vector3(0.1f, 1.3f, 0.1f), Col(new Color(0.6f, 0.46f, 0.22f), 1f, 0.7f), false);
        }
        Box("Glass", room, new Vector3(-2f, 1.95f, counterZ + 0.05f), new Vector3(20f, 1.3f, 0.04f), Glass, false);
        Box("GlassTopRail", room, new Vector3(-2f, 2.62f, counterZ), new Vector3(20.2f, 0.06f, 0.12f), Col(new Color(0.6f, 0.46f, 0.22f), 1f, 0.7f), false);
        // teller windows, lamps, cash drawers
        var tellers = new List<Transform>();
        for (int i = 0; i < 4; i++)
        {
            float x = -10f + i * 4f;
            Box("WindowSign", room, new Vector3(x, 2.85f, counterZ), new Vector3(1.4f, 0.35f, 0.06f), Emissive(new Color(0.1f, 0.1f, 0.1f), new Color(0.2f, 0.6f, 0.25f) * (i == 0 ? 2f : 0.6f)), false);
            Box("Screen", room, new Vector3(x + 0.5f, 1.45f, counterZ + 0.6f), new Vector3(0.5f, 0.35f, 0.05f), Emissive(Color.black, new Color(0.3f, 0.5f, 0.8f)), false);
            var t = new GameObject("TellerSpot").transform; t.SetParent(room, false);
            t.position = new Vector3(x, 0f, counterZ + 1.4f); t.rotation = Quaternion.Euler(0f, 180f, 0f);
            if (i < 3) tellers.Add(t);
        }
        // staff gate at the east end of the counter
        Box("GateRail", room, new Vector3(9.9f, 0.9f, counterZ), new Vector3(1.8f, 0.08f, 0.1f), Col(new Color(0.6f, 0.46f, 0.22f), 1f, 0.7f), false);
        // back office: desks + safes + staff door to the vault stairs
        // back office: Desk Set desks, a copier, shelving and big plants
        for (int i = 0; i < 3; i++) Furnish(room, new Vector3(-9f + i * 6f, 0f, 25f), 0f, i);
        P("OfficePack_111_Node42", room, new Vector3(-6.3f, 0f, 29.45f), 180f, 1.15f);   // copier
        P("OfficePack_110_Node41", room, new Vector3(0f, 0f, 29.6f), 180f, 1.6f);        // shelving
        P("OfficePack_07_Pot002", room, new Vector3(8.9f, 0f, 29.3f), 0f, 2.1f);
        P("OfficePack_07_Pot002", room, new Vector3(-10.9f, 0f, 29.3f), 70f, 2.1f);
        Box("Safe", room, new Vector3(-12.5f, 0.9f, 28.5f), new Vector3(1.4f, 1.8f, 1.2f), Col(new Color(0.25f, 0.26f, 0.28f), 0.8f, 0.5f));

        // (no loose cash in the lobby - all the money is down in the vault)
        Box("StaffDoor", room, new Vector3(11f, 1.3f, BankD - 0.05f), new Vector3(2f, 2.6f, 0.15f), Col(new Color(0.3f, 0.3f, 0.32f), 0.7f, 0.5f), false);
        Box("StaffSign", room, new Vector3(11f, 2.9f, BankD - 0.1f), new Vector3(1.6f, 0.3f, 0.04f), Emissive(Color.black, new Color(1.5f, 0.2f, 0.2f)), false);
        Facade(room, new Vector3(-2f, 5f, BankD - 0.03f), Vector3.back, 12f, 1.5f, "bank_sign.png", 12f, 1.5f);

        // ---- the customer queue: slots + rope stanchions ----
        var q = new GameObject("CustomerQueue").AddComponent<BankQueue>();
        var slots = new List<Transform>();
        for (int i = 0; i < 8; i++)
        {
            var s = new GameObject("Slot" + i).transform; s.SetParent(q.transform, false);
            s.position = new Vector3(-10f, 0f, counterZ - 1.1f - i * 1.05f);
            s.rotation = Quaternion.identity;
            slots.Add(s);
        }
        q.slots = slots.ToArray();
        q.entrance = new GameObject("QueueEntrance").transform; q.entrance.SetParent(q.transform, false); q.entrance.position = new Vector3(-2f, 0f, 1.5f);
        q.exit = new GameObject("QueueExit").transform; q.exit.SetParent(q.transform, false); q.exit.position = new Vector3(2f, 0f, 1.2f);
        var brass = Col(new Color(0.8f, 0.62f, 0.3f), 1f, 0.8f);
        var rope = Col(new Color(0.55f, 0.06f, 0.1f), 0f, 0.4f);
        for (int i = 0; i < 6; i++)
            foreach (float x in new[] { -11.1f, -8.9f })
            {
                float z = counterZ - 1.6f - i * 1.6f;
                Cyl("Stanchion", room, new Vector3(x, 0.5f, z), new Vector3(0.1f, 0.5f, 0.1f), brass);
                Cyl("StanchionBase", room, new Vector3(x, 0.02f, z), new Vector3(0.36f, 0.02f, 0.36f), brass, false);
                if (i < 5) Box("Rope", room, new Vector3(x, 0.85f, z - 0.8f), new Vector3(0.05f, 0.05f, 1.55f), rope, false);
            }

        // ---- waiting area, loan desks, ATMs, plants (all downloaded models) ----
        // sofas: one against the east wall, one under the front windows, coffee table between
        PFace("OfficeSofa", room, new Vector3(hw - 0.5f, 0f, 4.6f), Vector3.left);
        PFace("OfficeSofa", room, new Vector3(9.3f, 0f, 0.5f), Vector3.forward);
        Box("CoffeeTable", room, new Vector3(11.2f, 0.22f, 3.0f), new Vector3(1.4f, 0.44f, 0.9f), marbleTop);
        P("PottedPlants_00_BonsaiHandDrawLow", room, new Vector3(11.2f, 0.44f, 3.0f), 20f, 0.7f);
        P("DeskSet_00_BookType1", room, new Vector3(10.75f, 0.44f, 2.8f), 15f);
        // loan officers: desk along the east side, officer facing the lobby, a guest chair
        for (int i = 0; i < 2; i++)
        {
            float z = 9.5f + i * 2.8f;
            // yaw 90: the officer's chair ends up on the wall side, facing the lobby
            Furnish(room, new Vector3(11.3f, 0f, z), 90f, i + 3);
            PFace("DeskSet_03_OfficeChair", room, new Vector3(10.35f, 0f, z), Vector3.right);   // customer's chair
        }
        // ATMs along the west wall by the entrance
        for (int i = 0; i < 3; i++)
            P("ATM", room, new Vector3(-hw + 0.45f, 0f, 1.8f + i * 1.4f), 90f);
        // bonsai on marble plinths: both sides of the door, both ends of the counter
        var plinth = Tex("marble.png", Vector2.one, null, 0.6f);
        string[] plants = { "PottedPlants_00_BonsaiHandDrawLow", "PottedPlants_01_BonsaiHandDrawLow2", "PottedPlants_02_BonsaiHandDrawLow3", "PottedPlants_03_TreePack" };
        int pi = 0;
        foreach (var p in new[] { new Vector3(-4f, 0f, 1f), new Vector3(4f, 0f, 1f), new Vector3(hw - 1f, 0f, 16.4f), new Vector3(-hw + 1f, 0f, 17.4f) })
        {
            Box("Plinth", room, p + Vector3.up * 0.45f, new Vector3(0.75f, 0.9f, 0.75f), plinth);
            P(plants[pi++ % plants.Length], room, p + Vector3.up * 0.9f, pi * 70f, 1.05f);
        }
        foreach (var p in new[] { new Vector3(hw - 0.6f, 0f, 8.2f), new Vector3(hw - 0.6f, 0f, 11f), new Vector3(-hw + 0.6f, 0f, 5.55f), new Vector3(-6.5f, 0f, 16.8f) })
            P("OfficePack_07_Pot002", room, p, p.z * 40f, 2.1f);
        // desk flags on the loan desks, briefcases by the sofa and on a desk
        P("DeskFlags_00_model20010", room, new Vector3(11.45f, 0.82f, 8.95f), 270f);
        P("DeskFlags_03_model20113", room, new Vector3(11.45f, 0.82f, 11.75f), 270f);
        P("Briefcase", room, new Vector3(hw - 1.3f, 0f, 3.3f), 70f);
        P("Briefcase", room, new Vector3(11.3f, 0.82f, 12.7f), 0f);

        // ---- lighting: downloaded chandeliers (brass) + warm fill ----
        // grand chandeliers down the middle of the lobby, ring chandeliers over the back office
        int ci = 0;
        foreach (float x in new[] { -7f, 0f, 7f })
            foreach (float z in new[] { 6f, 14f })
            {
                const float sc = 1.45f;   // ~1.9 m across, 1.8 m tall, hung just under the beams
                NoShadows(P("ChandelierGrand", room, new Vector3(x, BankH - 0.5f - 1.22f * sc, z), ci * 30f, sc));
                if (ci++ % 2 == 0) PointLight("ChandelierGlow", room, new Vector3(x, BankH - 2.2f, z), new Color(1f, 0.82f, 0.55f), 3.5f, 11f);
            }
        foreach (float x in new[] { -7f, 0f, 7f })
        {
            Cyl("Cord", room, new Vector3(x, BankH - 1.1f, 24f), new Vector3(0.03f, 1.1f, 0.03f), Col(Color.black), false);
            NoShadows(P("Chandelier", room, new Vector3(x, BankH - 2.35f, 24f), 0f, 1.3f));
        }

        // ---- wall art, clock, water cooler (downloaded) ----
        float art = 3.4f;   // above the wood wainscoting
        PWall("PaintingVictorian", room, new Vector3(-hw, art, 10.75f), Vector3.right, 2f);
        PWall("PaintingArt", room, new Vector3(hw, art, 10.9f), Vector3.left, 2.4f);
        PWall("PaintingFrame", room, new Vector3(-hw, art, 22.5f), Vector3.right, 2.2f);
        PWall("PaintingArt", room, new Vector3(hw, art, 22.5f), Vector3.left, 2.4f);
        PWall("PaintingVictorian", room, new Vector3(-9f, 3.6f, 0f), Vector3.forward, 2f);
        PWall("PaintingFrame", room, new Vector3(9.3f, 3.6f, 0f), Vector3.forward, 2.2f);
        PWall("WallClock", room, new Vector3(7f, 4.6f, BankD), Vector3.back, 2f);
        P("WaterDispenser", room, new Vector3(hw - 0.4f, 0f, 1.3f), 225f);
        P("WaterDispenser", room, new Vector3(-hw + 0.4f, 0f, 21.5f), 90f);

        PointLight("Fill1", room, new Vector3(-6f, 6f, 8f), new Color(1f, 0.88f, 0.7f), 6f, 22f, true);
        PointLight("Fill2", room, new Vector3(6f, 6f, 8f), new Color(1f, 0.88f, 0.7f), 6f, 22f);
        PointLight("Fill3", room, new Vector3(0f, 6f, 22f), new Color(1f, 0.88f, 0.7f), 6f, 22f);

        // red alarm beacons (switched on by the director)
        var alarm = new GameObject("AlarmLights"); alarm.transform.SetParent(room, false);
        foreach (var p in new[] { new Vector3(-12f, 6.5f, 10f), new Vector3(12f, 6.5f, 10f), new Vector3(0f, 6.5f, 29f) })
        {
            var beacon = Box("Beacon", alarm.transform, p, new Vector3(0.4f, 0.3f, 0.4f), Emissive(new Color(0.4f, 0f, 0f), new Color(3f, 0.1f, 0.1f)), false);
            var pl = beacon.AddComponent<PoliceLights>();
            pl.red = PointLight("Red", beacon.transform, Vector3.zero, new Color(1f, 0.05f, 0.05f), 5f, 14f);
            pl.siren = false;
        }
        alarm.SetActive(false);

        // ---- minimap ----
        MapQuad(map, new Vector3(0f, 0f, BankD * 0.5f), new Vector2(BankW, BankD), new Color(0.32f, 0.33f, 0.36f));
        MapQuad(map, new Vector3(-2f, 0f, counterZ), new Vector2(20f, 1f), new Color(0.6f, 0.5f, 0.3f), 0.08f);
        MapIcon(map, new Vector3(11f, 0f, BankD - 1f), new Color(1f, 0.85f, 0.2f), 2.5f);
        MapIcon(map, new Vector3(0f, 0f, 1f), new Color(1f, 0.85f, 0.2f), 2.5f);

        // ---- gameplay wiring ----
        var d = Director(SceneKind.Bank);
        d.minimapCam = MinimapCamera(16f);
        d.queue = q;
        d.civilianSpots = tellers.ToArray();
        d.alarmLight = alarm;
        d.entrance = new GameObject("Entrance").transform; d.entrance.position = new Vector3(0f, 0f, 1.2f);

        var sp = new GameObject("SpawnPoints").transform;
        SpawnPt("fromCity", sp, new Vector3(0f, 0f, 2.4f), 0f);
        SpawnPt("fromVault", sp, new Vector3(10.5f, 0f, 27.5f), 180f);
        SpawnPt("queue", sp, slots[1].position, 0f);
        SpawnPt("start", sp, new Vector3(0f, 0f, 2.4f), 0f);
        SpawnPt("P1", sp, new Vector3(5.5f, 0f, 2.5f), 0f);          // guard by the door
        SpawnPt("P3", sp, new Vector3(-2.5f, 0f, 2.2f), 0f);         // SWAT coming in (robber escape)
        SpawnPt("P4", sp, new Vector3(2.5f, 0f, 2.2f), 0f);
        SpawnPt("R2", sp, new Vector3(-4f, 0f, 9f), 180f);           // robbers watching the hostages
        SpawnPt("R3", sp, new Vector3(4.5f, 0f, 12f), 200f);

        Portal(room, new Vector3(0f, 0f, 0.9f), "City", "fromBank", "go out to the street");
        Portal(room, new Vector3(11f, 0f, BankD - 1.3f), "Vault", "fromBank", "take the stairs down to the vault",
               Phase.Breached, "Staff only - the vault stairs are locked");

        BakeNavMesh(room.gameObject);
        Save(scene, "Bank");
    }
}
