using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The vault level (its own scene): stairs down from the bank, a concrete and
// steel corridor past a security gate, an antechamber, the huge round vault
// door (VaultDoor - burned open with thermite), and the vault itself: walls of
// safe-deposit boxes, tables of cash bundles and stacks of gold bars (Loot).
public static partial class HeistBuilder
{
    static void BuildVaultScene()
    {
        s_rng = new System.Random(99);
        var scene = NewScene("Vault");
        Atmosphere(true);
        Probe(new Vector3(0f, 1.8f, 10f), new Vector3(5f, 3.6f, 24f), true);
        Probe(new Vector3(0f, 2f, 33f), new Vector3(14f, 4.2f, 18f), true);
        var root = new GameObject("Basement").transform;
        var map = new GameObject("MinimapGeometry").transform;

        var concrete = "concrete.png";
        var steel = Tex("brushed_steel.png", new Vector2(2f, 1f), null, 0.6f, 0.8f);
        var steelDark = Col(new Color(0.3f, 0.31f, 0.33f), 0.85f, 0.55f);
        var lampM = Emissive(new Color(0.9f, 0.95f, 1f), new Color(1.4f, 1.5f, 1.6f) * 1.6f);

        // ---- stairwell + corridor (x +-2.5, z 0..22) ----
        BoxT("CorridorFloor", root, new Vector3(0f, -0.1f, 10f), new Vector3(5f, 0.2f, 24f), concrete, 3f, 3f, new Color(0.8f, 0.8f, 0.8f), true, 0.35f);
        BoxT("CorridorCeil", root, new Vector3(0f, 3.6f, 10f), new Vector3(5f, 0.2f, 24f), concrete, 3f, 3f, new Color(0.7f, 0.7f, 0.7f));
        foreach (float s in new[] { -1f, 1f })
        {
            BoxT("CorridorWall", root, new Vector3(s * 2.7f, 1.75f, 10f), new Vector3(0.4f, 3.5f, 24f), concrete, 3f, 3f);
            Box("SteelBand", root, new Vector3(s * 2.48f, 0.6f, 10f), new Vector3(0.05f, 1.2f, 24f), steel, false);
        }
        BoxT("StairWall", root, new Vector3(0f, 1.75f, -2.2f), new Vector3(5.4f, 3.5f, 0.4f), concrete, 3f, 3f);
        // the stairwell door back up to the bank
        Box("StairDoor", root, new Vector3(0f, 1.25f, -1.98f), new Vector3(1.6f, 2.5f, 0.1f), Col(new Color(0.35f, 0.36f, 0.38f), 0.7f, 0.5f), false);
        Box("ExitSign", root, new Vector3(0f, 2.8f, -1.95f), new Vector3(1f, 0.3f, 0.05f), Emissive(Color.black, new Color(0.2f, 1.6f, 0.4f)), false);
        for (float z = 2f; z < 22f; z += 4f)
        {
            Box("CeilLamp", root, new Vector3(0f, 3.47f, z), new Vector3(0.4f, 0.06f, 1.6f), lampM, false);
            PointLight("CorridorLight", root, new Vector3(0f, 3f, z), new Color(0.9f, 0.95f, 1f), 2.2f, 7f);
        }
        // open security gate (bars folded to the side) + a camera
        for (int i = 0; i < 6; i++)
            Cyl("GateBar", root, new Vector3(-2.3f + i * 0.12f, 1.6f, 9f), new Vector3(0.05f, 1.6f, 0.05f), steelDark, false);
        Box("GateTrack", root, new Vector3(0f, 3.3f, 9f), new Vector3(5f, 0.12f, 0.12f), steelDark, false);
        Box("CamBody", root, new Vector3(2.3f, 3.1f, 12f), new Vector3(0.25f, 0.18f, 0.45f), Col(new Color(0.85f, 0.85f, 0.85f)), false);
        Box("CamLed", root, new Vector3(2.3f, 3.1f, 11.76f), new Vector3(0.05f, 0.05f, 0.02f), Emissive(Color.red, new Color(3f, 0f, 0f)), false);

        // ---- antechamber (x +-7, z 22..28) ----
        BoxT("AnteFloor", root, new Vector3(0f, -0.1f, 25f), new Vector3(14f, 0.2f, 6f), "marble_tiles.png", 2.4f, 2.4f, new Color(0.85f, 0.85f, 0.85f), true, 0.5f);
        BoxT("AnteCeil", root, new Vector3(0f, 4.1f, 25f), new Vector3(14f, 0.2f, 6f), concrete, 3f, 3f);
        foreach (float s in new[] { -1f, 1f })
        {
            BoxT("AnteWallSide", root, new Vector3(s * 7.2f, 2f, 25f), new Vector3(0.4f, 4f, 6f), concrete, 3f, 3f);
            BoxT("AnteWallFront", root, new Vector3(s * 4.85f, 2f, 22.2f), new Vector3(4.3f, 4f, 0.4f), concrete, 3f, 3f);
        }
        PointLight("AnteLight", root, new Vector3(0f, 3.5f, 25f), new Color(1f, 0.95f, 0.85f), 3f, 10f, true);

        // ---- vault wall with the round door (opening 3.6 x 3.6 at z = 28) ----
        const float vz = 28f;
        var wallM = Col(new Color(0.42f, 0.43f, 0.45f), 0.7f, 0.5f);
        Box("VaultWallL", root, new Vector3(-4.6f, 2f, vz), new Vector3(5.2f, 4f, 1.6f), wallM);
        Box("VaultWallR", root, new Vector3(4.6f, 2f, vz), new Vector3(5.2f, 4f, 1.6f), wallM);
        Box("VaultWallTop", root, new Vector3(0f, 3.9f, vz), new Vector3(4f, 0.4f, 1.6f), wallM);
        // a steel ring (24 segments) framing the round opening
        for (int i = 0; i < 24; i++)
        {
            float a0 = i * 15f;
            var seg = Box("DoorRing", root, new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad) * 2.2f, 1.85f + Mathf.Sin(a0 * Mathf.Deg2Rad) * 2.2f, vz - 0.82f),
                          new Vector3(0.62f, 0.3f, 0.14f), steel, false);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, a0 + 90f);
        }
        var hinge = new GameObject("VaultHinge").transform; hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(2.05f, 0f, vz - 0.95f);
        var door = new GameObject("VaultDoorDisc").transform; door.SetParent(hinge, false);
        door.localPosition = new Vector3(-2.05f, 1.85f, 0f);
        // layer 2 keeps the door out of the NavMesh bake, so NPCs can path through
        // the doorway whenever the door is open
        Cyl("Disc", door, Vector3.zero, new Vector3(4.1f, 0.45f, 4.1f), steel, true, new Vector3(90f, 0f, 0f)).layer = 2;
        Cyl("DiscFace", door, new Vector3(0f, 0f, -0.46f), new Vector3(3.3f, 0.02f, 3.3f), steelDark, false, new Vector3(90f, 0f, 0f));
        Cyl("Hub", door, new Vector3(0f, 0f, -0.55f), new Vector3(0.7f, 0.1f, 0.7f), Col(new Color(0.75f, 0.6f, 0.3f), 1f, 0.8f), false, new Vector3(90f, 0f, 0f));
        for (int i = 0; i < 3; i++)
        {
            var spoke = Box("Spoke", door, new Vector3(0f, 0f, -0.62f), new Vector3(1.9f, 0.1f, 0.1f), Col(new Color(0.75f, 0.6f, 0.3f), 1f, 0.8f), false);
            spoke.transform.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
        }
        for (int i = 0; i < 12; i++)
        {
            float a = i * 30f * Mathf.Deg2Rad;
            Cyl("Bolt", door, new Vector3(Mathf.Cos(a) * 1.85f, Mathf.Sin(a) * 1.85f, -0.48f), new Vector3(0.18f, 0.05f, 0.18f), steelDark, false, new Vector3(90f, 0f, 0f));
        }
        // the real (downloaded) vault door model replaces the code-built disc's looks
        var vdModel = PropPrefab("VaultDoor");
        if (vdModel != null)
        {
            foreach (var r in door.GetComponentsInChildren<Renderer>()) r.enabled = false;
            float sc = 4.2f / 3.4f;
            var m = P("VaultDoor", door, new Vector3(0f, -3.26f * sc * 0.5f, 0f), 0f, sc);
            foreach (var c in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }
        var vd = new GameObject("VaultDoor").AddComponent<VaultDoor>();
        vd.transform.SetParent(root, false);
        vd.transform.position = new Vector3(0f, 0f, vz - 2.2f);
        vd.transform.rotation = Quaternion.identity;
        vd.hinge = hinge;
        vd.openAngle = -110f;
        vd.range = 2.6f;
        var spark = new GameObject("SparkPoint").transform; spark.SetParent(root, false); spark.position = new Vector3(-1.2f, 1.9f, vz - 1.2f);
        vd.sparkPoint = spark;

        // ---- the vault (x +-7, z 29..41) ----
        BoxT("VaultFloor", root, new Vector3(0f, -0.1f, 35f), new Vector3(14f, 0.2f, 12f), "brushed_steel.png", 3f, 3f, new Color(0.7f, 0.7f, 0.72f), true, 0.6f);
        // threshold under the round door: the antechamber floor stops at z 28 and the
        // vault floor starts at z 29 - without this there's a 1 m hole in the doorway
        BoxT("DoorThreshold", root, new Vector3(0f, -0.1f, 28.5f), new Vector3(14f, 0.2f, 1.2f), "brushed_steel.png", 3f, 3f, new Color(0.55f, 0.55f, 0.58f), true, 0.6f);
        Box("VaultCeil", root, new Vector3(0f, 4.1f, 35f), new Vector3(14f, 0.2f, 12f), steelDark);
        var boxes = Tex("deposit_boxes.png", new Vector2(2.2f, 0.9f), null, 0.6f, 0.6f);
        Box("BoxWallN", root, new Vector3(0f, 2f, 41.2f), new Vector3(14f, 4f, 0.4f), boxes);
        Box("BoxWallW", root, new Vector3(-7.2f, 2f, 35f), new Vector3(0.4f, 4f, 12f), Tex("deposit_boxes.png", new Vector2(1.9f, 0.9f), null, 0.6f, 0.6f));
        Box("BoxWallE", root, new Vector3(7.2f, 2f, 35f), new Vector3(0.4f, 4f, 12f), Tex("deposit_boxes.png", new Vector2(1.9f, 0.9f), null, 0.6f, 0.6f));
        for (float x = -5f; x <= 5f; x += 5f)
        {
            Box("VaultLamp", root, new Vector3(x, 3.97f, 35f), new Vector3(1.4f, 0.06f, 0.6f), lampM, false);
        }
        PointLight("VaultLight1", root, new Vector3(-3f, 3.4f, 35f), new Color(1f, 0.95f, 0.85f), 4f, 12f, true);
        PointLight("VaultLight2", root, new Vector3(3f, 3.4f, 35f), new Color(1f, 0.95f, 0.85f), 4f, 12f);
        var goldGlow = PointLight("GoldGlow", root, new Vector3(0f, 1.2f, 37f), new Color(1f, 0.78f, 0.35f), 2f, 6f);

        // loot: two tables of cash bundles + two pallets of gold bars
        var table = Col(new Color(0.3f, 0.31f, 0.33f), 0.8f, 0.5f);
        var cashM = Tex("cash.png", Vector2.one, null, 0.2f);
        var gold = Col(new Color(1f, 0.76f, 0.26f), 1f, 0.85f);
        var loot = new GameObject("Loot").transform; loot.SetParent(root, false);
        int n = 0;
        foreach (var tp in new[] { new Vector3(-3.5f, 0f, 33.5f), new Vector3(3.5f, 0f, 33.5f) })
        {
            Box("Table", root, tp + Vector3.up * 0.45f, new Vector3(2.6f, 0.9f, 1.3f), table);
            for (int k = 0; k < 2; k++)
            {
                var l = new GameObject($"Cash_{n++}").AddComponent<Loot>();
                l.transform.SetParent(loot, false);
                l.transform.position = tp + new Vector3(-0.6f + k * 1.2f, 0.9f, 0f);
                l.value = 250000; l.label = "grab the cash"; l.range = 1.9f;
                MoneyGrid(l.transform, new Vector3(0f, 0f, -0.25f), new Vector2(1.1f, 0.6f), 0.45f);
                if (P(k == 0 ? "MoneyStacks" : "CashPile", l.transform, Vector3.zero, k * 25f, k == 0 ? 1.9f : 1.3f) == null)
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 2; b++)
                        Box("Bundle", l.transform, new Vector3(-0.2f + b * 0.4f, 0.06f + a * 0.12f, 0f), new Vector3(0.36f, 0.11f, 0.18f), cashM, false);
            }
        }
        foreach (var gp in new[] { new Vector3(-3.5f, 0f, 38.6f), new Vector3(3.5f, 0f, 38.6f) })
        {
            Box("Pallet", root, gp + Vector3.up * 0.1f, new Vector3(1.8f, 0.2f, 1.2f), Col(new Color(0.35f, 0.25f, 0.15f)));
            var l = new GameObject($"Gold_{n++}").AddComponent<Loot>();
            l.transform.SetParent(loot, false);
            l.transform.position = gp + Vector3.up * 0.2f;
            l.value = 1000000; l.label = "load the gold bars"; l.range = 2f;
            for (int layer = 0; layer < 4; layer++)
                for (int i = 0; i < 4 - layer; i++)
                    for (int j = 0; j < 3; j++)
                        if (P("GoldBar", l.transform, new Vector3(-0.45f + i * 0.3f + layer * 0.15f, layer * 0.09f, -0.3f + j * 0.3f), 90f, 1.1f) == null)
                            Box("GoldBar", l.transform, new Vector3(-0.45f + i * 0.3f + layer * 0.15f, 0.06f + layer * 0.12f, -0.3f + j * 0.3f), new Vector3(0.26f, 0.11f, 0.12f), gold, false);
        }
        // a cart of cash briefcases
        var cart = new Vector3(0f, 0f, 39.6f);
        Box("Cart", root, cart + Vector3.up * 0.5f, new Vector3(1.4f, 0.1f, 0.8f), table);
        // open cash briefcases (downloaded model) on the cart
        for (int i = 0; i < 3; i++)
            P("CashBriefcase", root, cart + new Vector3(-0.42f + i * 0.42f, 0.55f, 0f), 90f + i * 8f, 0.9f);

        // gold bars stacked between the cash on both tables (decor)
        foreach (var tp in new[] { new Vector3(-3.5f, 0.9f, 33.5f), new Vector3(3.5f, 0.9f, 33.5f) })
            for (int i = 0; i < 3; i++)
                if (P("GoldBar", root, tp + new Vector3(-0.15f + i * 0.15f, 0.02f, 0.45f), 90f, 1.1f) == null)
                    Box("GoldBar", root, tp + new Vector3(-0.15f + i * 0.15f, 0.06f, 0.45f), new Vector3(0.12f, 0.11f, 0.26f), gold, false);

        // steel racks along both side walls, every shelf packed with cash - each rack is one grab
        var rackM = Col(new Color(0.42f, 0.43f, 0.45f), 0.85f, 0.5f);
        int rackN = 0;
        foreach (float side in new[] { -1f, 1f })
            foreach (float z in new[] { 31.1f, 33.7f, 36.3f, 38.9f })
            {
                Vector3 rc = new Vector3(side * 6.45f, 0f, z);
                for (int u = 0; u < 4; u++)
                    Box("RackPost", root, rc + new Vector3((u % 2 == 0 ? -0.4f : 0.4f), 1.1f, (u < 2 ? -1.05f : 1.05f)), new Vector3(0.06f, 2.2f, 0.06f), rackM, false);
                Box("RackBody", root, rc + Vector3.up * 1.1f, new Vector3(0.9f, 2.2f, 2.2f), rackM, true).GetComponent<Renderer>().enabled = false;   // one collider for the whole rack
                var l = new GameObject($"Rack_{rackN++}").AddComponent<Loot>();
                l.transform.SetParent(loot, false);
                l.transform.position = rc - new Vector3(side * 0.55f, 0f, 0f) + Vector3.up * 0.9f;
                l.value = 500000; l.label = "empty the cash rack"; l.range = 1.9f;
                foreach (float y in new[] { 0.45f, 1.15f, 1.85f })
                {
                    Box("Shelf", root, rc + Vector3.up * y, new Vector3(0.9f, 0.04f, 2.2f), rackM, false);
                    // bricks go under the Loot object so the shelves empty when it's taken
                    MoneyGrid(l.transform, l.transform.InverseTransformPoint(rc + new Vector3(0f, y + 0.02f, 0f)), new Vector2(0.8f, 2.1f), y > 1.5f ? 0.3f : 0.6f);
                }
            }

        // pallets of cash on the floor: along the back wall, between the tables and the
        // gold, and both sides of the doorway - every one of them can be grabbed
        var palletM = Col(new Color(0.42f, 0.3f, 0.18f));
        int pn = 0;
        foreach (var pp in new[] { new Vector3(-1.8f, 0f, 39.9f), new Vector3(1.8f, 0f, 39.9f),
                                   new Vector3(-3.5f, 0f, 35.9f), new Vector3(3.5f, 0f, 35.9f),
                                   new Vector3(-4.2f, 0f, 30.2f), new Vector3(4.2f, 0f, 30.2f) })
        {
            var l = new GameObject($"Pallet_{pn++}").AddComponent<Loot>();
            l.transform.SetParent(loot, false);
            l.transform.position = pp;
            l.value = 2000000; l.label = "load the pallet of cash"; l.range = 2.1f;
            Box("Pallet", l.transform, Vector3.up * 0.07f, new Vector3(1.3f, 0.14f, 1.0f), palletM);
            float top = MoneyGrid(l.transform, Vector3.up * 0.14f, new Vector2(1.3f, 1.0f), pp.z > 39f ? 1.0f : 0.7f);
            P("CashPile", l.transform, Vector3.up * (0.14f + top), 30f, 1.2f);
        }
        // and on the cart, next to the bags

        // the robbers' downloaded duffel bags, dropped by the loot ready to be filled
        foreach (var (bp, by) in new[] { (new Vector3(-2.2f, 0f, 31.3f), 20f), (new Vector3(2.4f, 0f, 31.0f), 160f),
                                          (new Vector3(-0.9f, 0f, 37.6f), 75f), (new Vector3(1.1f, 0f, 37.9f), 250f) })
            P("LootBag", root, bp, by, 1.4f);

        // ---- minimap ----
        MapQuad(map, new Vector3(0f, 0f, 10f), new Vector2(5f, 24f), new Color(0.32f, 0.33f, 0.36f));
        MapQuad(map, new Vector3(0f, 0f, 25f), new Vector2(14f, 6f), new Color(0.32f, 0.33f, 0.36f));
        MapQuad(map, new Vector3(0f, 0f, 35f), new Vector2(14f, 12f), new Color(0.38f, 0.36f, 0.3f));
        MapIcon(map, new Vector3(0f, 0f, 36f), new Color(0.35f, 0.8f, 0.35f), 2.5f);

        // ---- gameplay ----
        var d = Director(SceneKind.Vault);
        d.minimapCam = MinimapCamera(14f);
        var sp = new GameObject("SpawnPoints").transform;
        SpawnPt("fromBank", sp, new Vector3(0f, 0f, 1f), 0f);
        SpawnPt("start", sp, new Vector3(0f, 0f, 1f), 0f);
        SpawnPt("P2", sp, new Vector3(3.5f, 0f, 24.5f), 250f);       // guard in the antechamber
        SpawnPt("R1", sp, new Vector3(0f, 0f, 36f), 180f);           // leader inside the vault
        SpawnPt("R4", sp, new Vector3(-4f, 0f, 36.5f), 160f);
        Portal(root, new Vector3(0f, 0f, -0.6f), "Bank", "fromVault", "go back upstairs");

        BakeNavMesh(root.gameObject);
        Save(scene, "Vault");
    }
}
