using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Downtown: a 4-lane avenue (along X, z = 0) crossed by two streets (x = +-60)
// and two back streets (z = +-70). Blocks are raised sidewalk slabs lined with
// buildings (glass towers, brick and concrete walk-ups with shop fronts). The
// CENTRAL NATIONAL BANK sits mid-block on the north side of the avenue.
public static partial class HeistBuilder
{
    const float AveHalf = 8f, StreetHalf = 6f, Walk = 5f, Curb = 0.15f;
    static readonly float[] CrossX = { -60f, 60f };
    static readonly float[] BackZ = { -70f, 70f };
    const float CityX = 150f, CityZ = 110f;

    static readonly Vector3 VanPos = new Vector3(-34f, 0f, 5.2f);
    static readonly Vector3 SafehousePos = new Vector3(112f, 0f, -70f);

    static System.Random s_rng;
    static float R(float a, float b) => a + (float)s_rng.NextDouble() * (b - a);

    static void BuildCityScene(bool menu)
    {
        s_rng = new System.Random(1234);
        var scene = NewScene(menu ? "MainMenu" : "City");
        Atmosphere(false);
        Probe(new Vector3(0f, 6f, 0f), new Vector3(320f, 80f, 260f), false);

        var city = new GameObject("City").transform;
        var map = new GameObject("MinimapGeometry").transform;

        BuildRoads(city, map);
        BuildBlocks(city, map);
        BuildBank(city, map);
        if (HasStreetProps) BuildStreetProps(city); else BuildStreetFurniture(city);
        BuildParkedCars(city);
        BuildStreetExtras(city);
        BuildSafehouse(city, map);
        BuildSkyline(city);

        var police = BuildPoliceScene(city);
        var van = BuildGetawayVan(city);
        BuildPigeons(city);

        if (menu)
        {
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>(); cam.farClipPlane = 800f;
            camGo.AddComponent<AudioListener>();
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderPostProcessing = true;
            var mm = LayerMask.NameToLayer("Minimap"); cam.cullingMask &= ~(1 << mm);
            police.SetActive(true);
            var menuGo = new GameObject("MainMenu").AddComponent<MainMenu>();
            menuGo.font = AssetDatabase.LoadAssetAtPath<Font>(ROOT + "Fonts/BarlowCondensed-SemiBold.ttf");
            menuGo.bigFont = AssetDatabase.LoadAssetAtPath<Font>(ROOT + "Fonts/BebasNeue-Regular.ttf");
            var center = new GameObject("OrbitCenter").transform; center.position = new Vector3(0f, 0f, 20f);
            menuGo.orbitCenter = center;
            Object.DestroyImmediate(van.GetComponent<Vehicle>());
            Save(scene, "MainMenu");
            return;
        }

        // ---- gameplay ----
        var d = Director(SceneKind.City);
        d.minimapCam = MinimapCamera(42f);
        d.policeScene = police;
        d.safehouse = new GameObject("Safehouse").transform; d.safehouse.position = SafehousePos;
        d.policeLine = police.transform.Find("PoliceLine");
        d.entrance = null;

        var sp = new GameObject("SpawnPoints").transform;
        SpawnPt("start", sp, VanPos + new Vector3(2f, Curb, 5.5f), 90f);          // robbers, by the van
        SpawnPt("cops", sp, new Vector3(-6f, 0f, -12f), 0f);                      // police, behind the cars
        SpawnPt("fromBank", sp, new Vector3(0f, Curb, 9.2f), 180f);               // just outside the bank doors
        SpawnPt("P3", sp, new Vector3(9f, 0f, -5f), 0f);
        SpawnPt("P4", sp, new Vector3(-12f, 0f, -4f), 0f);
        SpawnPt("R9", sp, new Vector3(0f, Curb, 10f), 180f);

        Portal(city, new Vector3(0f, Curb + 1f, 15.8f), "Bank", "fromCity", "enter the bank");

        // pedestrians on the sidewalks + their wander points
        var peds = new GameObject("Pedestrians").transform;
        var spots = new List<Transform>();
        var wander = new List<Transform>();
        foreach (float x in new[] { -48f, -30f, -20f, 24f, 38f, 50f })
            foreach (float z in new[] { -10.5f, 10.5f })
            {
                var t = new GameObject("Ped").transform; t.SetParent(peds, false);
                t.position = new Vector3(x + R(-2f, 2f), Curb, z); t.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                if (spots.Count < 9) spots.Add(t);
                wander.Add(t);
            }
        d.civilianSpots = spots.ToArray();
        d.wanderPoints = wander.ToArray();

        BakeNavMesh(city.gameObject);
        Save(scene, "City");
    }

    // ------------------------------------------------------------- roads

    static void BuildRoads(Transform city, Transform map)
    {
        var roads = new GameObject("Roads").transform; roads.SetParent(city, false);
        // ground under everything
        BoxT("Ground", roads, new Vector3(0f, -0.25f, 0f), new Vector3(CityX * 2f + 400f, 0.5f, CityZ * 2f + 400f), "asphalt.png", 8f, 8f, new Color(0.8f, 0.8f, 0.8f));
        // avenue
        Road(roads, new Vector3(0f, 0f, 0f), CityX * 2f, AveHalf * 2f, true);
        foreach (float z in BackZ) Road(roads, new Vector3(0f, 0f, z), CityX * 2f, StreetHalf * 2f, true);
        foreach (float x in CrossX) Road(roads, new Vector3(x, 0f, 0f), CityZ * 2f, StreetHalf * 2f, false);
        // manhole covers + storm drains for street detail
        var iron = Col(new Color(0.16f, 0.15f, 0.14f), 0.8f, 0.45f);
        for (float x = -135f; x <= 135f; x += 27f)
        {
            if (P("CityProps_10_Manhole", roads, new Vector3(x + 4f, 0.01f, (x % 2 == 0 ? 1 : -1) * 3.5f), 0f) == null)
                Cyl("Manhole", roads, new Vector3(x + 4f, 0.012f, (x % 2 == 0 ? 1 : -1) * 3.5f), new Vector3(0.8f, 0.006f, 0.8f), iron, false);
            if (P("CityProps_19_StormDrain", roads, new Vector3(x - 6f, 0.01f, AveHalf - 0.6f), 0f) == null)
                Box("Drain", roads, new Vector3(x - 6f, 0.011f, AveHalf - 0.35f), new Vector3(0.9f, 0.006f, 0.4f), iron, false);
            if (P("CityProps_19_StormDrain", roads, new Vector3(x + 7f, 0.01f, -AveHalf + 0.6f), 180f) == null)
                Box("Drain", roads, new Vector3(x + 7f, 0.011f, -AveHalf + 0.35f), new Vector3(0.9f, 0.006f, 0.4f), iron, false);
        }
        // intersections (crosswalk texture)
        foreach (float x in CrossX)
            foreach (float z in new[] { 0f, BackZ[0], BackZ[1] })
            {
                float hz = z == 0f ? AveHalf : StreetHalf;
                var g = Box("Intersection", roads, new Vector3(x, 0.012f, z), new Vector3(StreetHalf * 2f, 0.02f, hz * 2f), Tex("intersection.png", Vector2.one, null, 0.25f), false);
            }
        // minimap
        MapQuad(map, Vector3.zero, new Vector2(CityX * 2f, AveHalf * 2f), new Color(0.62f, 0.64f, 0.66f));
        foreach (float z in BackZ) MapQuad(map, new Vector3(0f, 0f, z), new Vector2(CityX * 2f, StreetHalf * 2f), new Color(0.55f, 0.57f, 0.6f));
        foreach (float x in CrossX) MapQuad(map, new Vector3(x, 0f, 0f), new Vector2(StreetHalf * 2f, CityZ * 2f), new Color(0.55f, 0.57f, 0.6f));
    }

    static void Road(Transform p, Vector3 c, float length, float width, bool alongX)
    {
        // the road texture has its lanes along V, so rotate the box for X roads
        var m = Tex("road.png", new Vector2(1f, length / (width * 2f)), null, 0.25f);
        var g = Box("Road", p, c + Vector3.up * 0.005f, alongX ? new Vector3(width, 0.01f, length) : new Vector3(width, 0.01f, length), m, false, alongX ? 90f : 0f);
    }

    // ------------------------------------------------------------- blocks

    struct Block { public float x0, x1, z0, z1; public Block(float a, float b, float c, float d) { x0 = a; x1 = b; z0 = c; z1 = d; } }

    static List<Block> Blocks()
    {
        var xs = new List<(float, float)> { (-CityX, CrossX[0] - StreetHalf), (CrossX[0] + StreetHalf, CrossX[1] - StreetHalf), (CrossX[1] + StreetHalf, CityX) };
        var zs = new List<(float, float)> { (-CityZ, BackZ[0] - StreetHalf), (BackZ[0] + StreetHalf, -AveHalf), (AveHalf, BackZ[1] - StreetHalf), (BackZ[1] + StreetHalf, CityZ) };
        var list = new List<Block>();
        foreach (var x in xs) foreach (var z in zs) list.Add(new Block(x.Item1, x.Item2, z.Item1, z.Item2));
        return list;
    }

    static readonly string[] Facades = { "facade_glass.png", "facade_brick.png", "facade_concrete.png", "facade_cream.png" };
    static readonly string[] Shops = { "shop_red.png", "shop_green.png", "shop_blue.png", "shop_yellow.png" };
    static readonly string[] ShopSigns = { "sign_diner.png", "sign_pharmacy.png", "sign_electronics.png", "sign_liquor.png" };

    static bool s_storePlaced;
    static void BuildBlocks(Transform city, Transform map)
    {
        s_storePlaced = false;
        var blocks = new GameObject("Blocks").transform; blocks.SetParent(city, false);
        foreach (var b in Blocks())
        {
            float cx = (b.x0 + b.x1) * 0.5f, cz = (b.z0 + b.z1) * 0.5f, w = b.x1 - b.x0, dd = b.z1 - b.z0;
            // raised sidewalk slab with a curb
            BoxT("Sidewalk", blocks, new Vector3(cx, Curb * 0.5f, cz), new Vector3(w, Curb, dd), "sidewalk.png", 2.5f, 2.5f);
            MapQuad(map, new Vector3(cx, 0f, cz), new Vector2(w, dd), new Color(0.2f, 0.22f, 0.25f), 0.06f);
            // concrete curb stones along every sidewalk edge
            var curb = Col(new Color(0.62f, 0.61f, 0.58f), 0f, 0.25f);
            Box("CurbN", blocks, new Vector3(cx, Curb * 0.5f + 0.005f, b.z1 - 0.15f), new Vector3(w, Curb + 0.01f, 0.3f), curb, false);
            Box("CurbS", blocks, new Vector3(cx, Curb * 0.5f + 0.005f, b.z0 + 0.15f), new Vector3(w, Curb + 0.01f, 0.3f), curb, false);
            Box("CurbW", blocks, new Vector3(b.x0 + 0.15f, Curb * 0.5f + 0.005f, cz), new Vector3(0.3f, Curb + 0.01f, dd), curb, false);
            Box("CurbE", blocks, new Vector3(b.x1 - 0.15f, Curb * 0.5f + 0.005f, cz), new Vector3(0.3f, Curb + 0.01f, dd), curb, false);

            float ix0 = b.x0 + Walk, ix1 = b.x1 - Walk, iz0 = b.z0 + Walk, iz1 = b.z1 - Walk;
            bool bankBlock = cx == 0f && cz > 0f && cz < BackZ[1];
            bool faceAve = Mathf.Abs(cz) < BackZ[1];           // blocks touching the avenue get shop fronts
            // lots along X
            float x = ix0;
            while (x < ix1 - 6f)
            {
                float lw = Mathf.Min(R(14f, 24f), ix1 - x);
                if (ix1 - (x + lw) < 8f) lw = ix1 - x;
                float lcx = x + lw * 0.5f;
                if (bankBlock && Mathf.Abs(lcx) < 26f) { x += lw; continue; }   // leave room for the bank
                if (!s_storePlaced && cz < BackZ[0] && Mathf.Abs(lcx - SafehousePos.x) < 12f && lw > 13f && PropPrefab("OldStore") != null)
                {
                    // the downloaded Old Street Store, next to the safehouse garage
                    s_storePlaced = true;
                    P("OldStore", blocks, new Vector3(lcx, Curb, iz1 - 7.2f), 0f, 1f);
                    MapQuad(map, new Vector3(lcx, 0f, iz1 - 7.2f), new Vector2(12.3f, 14f), new Color(0.42f, 0.4f, 0.38f), 0.07f);
                    x += lw; continue;
                }
                float h = R(12f, 26f);
                if (s_rng.NextDouble() < 0.25) h = R(36f, 70f);                    // the odd tower
                string facade = h > 34f ? "facade_glass.png" : Facades[s_rng.Next(1, Facades.Length)];
                int side = cz > 0f ? -1 : 1;                                         // which face looks at the avenue
                bool first = x <= ix0 + 0.01f, last = x + lw >= ix1 - 0.01f;
                Building(blocks, map, new Vector3(lcx, Curb, (iz0 + iz1) * 0.5f), new Vector2(lw - 0.6f, iz1 - iz0), h, facade,
                         faceAve ? side : 0, s_rng.Next(0, Shops.Length), faceAve, first || bankBlock, last || bankBlock);
                x += lw;
            }
            if (bankBlock)
            {
                // fill behind the bank
                Building(blocks, map, new Vector3(0f, Curb, (47f + iz1) * 0.5f + 1f), new Vector2(52f, iz1 - 47f), 30f, "facade_concrete.png", 0, 0);
            }
        }
    }

    static void Building(Transform p, Transform map, Vector3 baseCenter, Vector2 size, float h, string facade, int shopSide, int shopIdx,
                         bool detailed = false, bool exposeW = true, bool exposeE = true)
    {
        var b = new GameObject("Building").transform; b.SetParent(p, false);
        b.position = baseCenter;
        // solid core (collisions + roof)
        Box("Core", b, new Vector3(0f, h * 0.5f, 0f), new Vector3(size.x, h, size.y), Col(new Color(0.3f, 0.3f, 0.31f), 0f, 0.1f));
        if (detailed)
        {
            // real 3D windows, cornices, fire escapes, shop fronts (CityDetail.cs)
            DetailedBuilding(b, size, h, facade, shopSide == -1 ? Vector3.back : shopSide == 1 ? Vector3.forward : Vector3.zero, shopIdx, exposeW, exposeE);
            RoofClutter(b, size, h);
            MapQuad(map, baseCenter, size, new Color(0.3f, 0.32f, 0.35f), 0.07f);
            return;
        }
        // facade quads on the four sides, tiled at real window size
        float tileW = facade == "facade_glass.png" ? 12f : 9f, tileH = facade == "facade_glass.png" ? 12f : 7f;
        Facade(b, new Vector3(0f, h * 0.5f, -size.y * 0.5f - 0.02f), Vector3.back, size.x, h, facade, tileW, tileH);
        Facade(b, new Vector3(0f, h * 0.5f, size.y * 0.5f + 0.02f), Vector3.forward, size.x, h, facade, tileW, tileH);
        Facade(b, new Vector3(-size.x * 0.5f - 0.02f, h * 0.5f, 0f), Vector3.left, size.y, h, facade, tileW, tileH);
        Facade(b, new Vector3(size.x * 0.5f + 0.02f, h * 0.5f, 0f), Vector3.right, size.y, h, facade, tileW, tileH);
        // parapet + rooftop clutter
        Box("Parapet", b, new Vector3(0f, h + 0.5f, 0f), new Vector3(size.x + 0.4f, 1f, size.y + 0.4f), Col(new Color(0.42f, 0.41f, 0.4f)), false);
        Box("Roof", b, new Vector3(0f, h + 0.55f, 0f), new Vector3(size.x - 0.6f, 1f, size.y - 0.6f), Col(new Color(0.2f, 0.2f, 0.21f), 0f, 0.1f), false);
        for (int i = 0; i < 3; i++)
            Box("AC", b, new Vector3(R(-size.x * 0.3f, size.x * 0.3f), h + 1.5f, R(-size.y * 0.3f, size.y * 0.3f)), new Vector3(R(2f, 4f), R(1f, 2f), R(2f, 3f)), Col(new Color(0.6f, 0.6f, 0.62f), 0.3f, 0.4f), false);
        if (h > 30f) Box("Antenna", b, new Vector3(0f, h + 6f, 0f), new Vector3(0.3f, 10f, 0.3f), Col(new Color(0.5f, 0.1f, 0.1f)), false);

        // ground-floor shop front facing the avenue
        if (shopSide != 0)
        {
            float z = shopSide * -(size.y * 0.5f + 0.05f);
            Vector3 n = shopSide > 0 ? Vector3.back : Vector3.forward;
            if (shopSide < 0) { z = size.y * 0.5f + 0.05f; } else { z = -size.y * 0.5f - 0.05f; }
            Vector3 face = shopSide < 0 ? Vector3.forward : Vector3.back;
            // the side facing the avenue: north-side buildings face -Z
            face = shopSide == -1 ? Vector3.back : Vector3.forward;
            z = shopSide == -1 ? -size.y * 0.5f - 0.05f : size.y * 0.5f + 0.05f;
            Facade(b, new Vector3(0f, 2.2f, z), face, size.x - 1f, 4.4f, Shops[shopIdx], size.x - 1f, 4.4f);
            var sign = Facade(b, new Vector3(0f, 5.1f, z + face.z * 0.25f), face, Mathf.Min(10f, size.x - 2f), 1.3f, ShopSigns[shopIdx], Mathf.Min(10f, size.x - 2f), 1.3f);
            sign.GetComponent<Renderer>().sharedMaterial.EnableKeyword("_EMISSION");
            var awningCol = new[] { new Color(0.6f, 0.12f, 0.1f), new Color(0.1f, 0.4f, 0.2f), new Color(0.12f, 0.2f, 0.45f), new Color(0.75f, 0.55f, 0.1f) }[shopIdx];
            var aw = Box("Awning", b, new Vector3(0f, 4.3f, z + face.z * 1.1f), new Vector3(size.x - 2f, 0.12f, 2.2f), Col(awningCol, 0f, 0.4f), false);
            aw.transform.localRotation = Quaternion.Euler(face.z * -12f, 0f, 0f);
        }

        MapQuad(map, baseCenter, size, new Color(0.3f, 0.32f, 0.35f), 0.07f);
    }

    static void RoofClutter(Transform b, Vector2 size, float h)
    {
        Box("Parapet", b, new Vector3(0f, h + 0.5f, 0f), new Vector3(size.x + 0.4f, 1f, size.y + 0.4f), Col(new Color(0.42f, 0.41f, 0.4f)), false);
        Box("Roof", b, new Vector3(0f, h + 0.55f, 0f), new Vector3(size.x - 0.6f, 1f, size.y - 0.6f), Col(new Color(0.2f, 0.2f, 0.21f), 0f, 0.1f), false);
        for (int i = 0; i < 3; i++)
            Box("AC", b, new Vector3(R(-size.x * 0.3f, size.x * 0.3f), h + 1.5f, R(-size.y * 0.3f, size.y * 0.3f)), new Vector3(R(2f, 4f), R(1f, 2f), R(2f, 3f)), Col(new Color(0.6f, 0.6f, 0.62f), 0.3f, 0.4f), false);
        if (h < 30f && s_rng.NextDouble() < 0.5)
        {
            // classic wooden rooftop water tank on legs
            var wood = Col(new Color(0.35f, 0.25f, 0.17f), 0f, 0.2f);
            Vector3 c = new Vector3(R(-size.x * 0.25f, size.x * 0.25f), h + 1.05f, R(-size.y * 0.25f, size.y * 0.25f));
            for (int k = 0; k < 4; k++) Box("TankLeg", b, c + new Vector3(k % 2 == 0 ? -0.9f : 0.9f, 1.2f, k < 2 ? -0.9f : 0.9f), new Vector3(0.15f, 2.4f, 0.15f), MetalBlack, false);
            Cyl("WaterTank", b, c + new Vector3(0f, 3.6f, 0f), new Vector3(2.6f, 1.3f, 2.6f), wood, false);
            Cyl("TankRoof", b, c + new Vector3(0f, 5.1f, 0f), new Vector3(2.8f, 0.2f, 2.8f), Col(new Color(0.2f, 0.2f, 0.2f)), false);
        }
        if (h > 30f) Box("Antenna", b, new Vector3(0f, h + 6f, 0f), new Vector3(0.3f, 10f, 0.3f), Col(new Color(0.5f, 0.1f, 0.1f)), false);
    }

    // a classic four-sided lantern: base, corner posts, glowing glass, stepped
    // cap and finial (bronze bits batched, glass is its own emissive box)
    static void Lantern(Transform root, Batch b, Vector3 c, float s, Material metal)
    {
        float h = s * 1.4f, w = s;
        b.Add(metal, Matrix4x4.TRS(c + Vector3.down * (h * 0.5f + 0.03f), Quaternion.identity, new Vector3(w * 0.8f, 0.06f, w * 0.8f)));
        b.Add(metal, Matrix4x4.TRS(c + Vector3.down * (h * 0.5f + 0.1f), Quaternion.identity, new Vector3(w * 0.35f, 0.1f, w * 0.35f)));
        foreach (var k in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            b.Add(metal, Matrix4x4.TRS(c + new Vector3(k.x * w * 0.4f, 0f, k.y * w * 0.4f), Quaternion.identity, new Vector3(0.035f, h, 0.035f)));
        b.Add(metal, Matrix4x4.TRS(c + Vector3.up * (h * 0.5f + 0.03f), Quaternion.identity, new Vector3(w * 0.95f, 0.06f, w * 0.95f)));
        b.Add(metal, Matrix4x4.TRS(c + Vector3.up * (h * 0.5f + 0.1f), Quaternion.Euler(0f, 45f, 0f), new Vector3(w * 0.55f, 0.1f, w * 0.55f)));
        b.Add(metal, Matrix4x4.TRS(c + Vector3.up * (h * 0.5f + 0.19f), Quaternion.Euler(0f, 45f, 0f), new Vector3(w * 0.28f, 0.08f, w * 0.28f)));
        b.Add(metal, Matrix4x4.TRS(c + Vector3.up * (h * 0.5f + 0.3f), Quaternion.identity, new Vector3(0.04f, 0.16f, 0.04f)));
        Glow("LanternGlass", root, c, new Vector3(w * 0.76f, h * 0.96f, w * 0.76f), new Color(1f, 0.8f, 0.5f), new Color(1.1f, 0.7f, 0.3f));
    }

    // a glowing pane (lantern glass, light fixtures) that casts no shadow
    static GameObject Glow(string n, Transform p, Vector3 pos, Vector3 size, Color c, Color emis)
    {
        var g = Box(n, p, pos, size, Emissive(c, emis), false);
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return g;
    }

    static GameObject Facade(Transform b, Vector3 localPos, Vector3 outward, float width, float height, string tex, float tileW, float tileH)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "Facade"; q.transform.SetParent(b, false);
        q.transform.localPosition = localPos;
        q.transform.localRotation = Quaternion.LookRotation(-outward);
        q.transform.localScale = new Vector3(width, height, 1f);
        q.GetComponent<Renderer>().sharedMaterial = Tex(tex, new Vector2(Mathf.Max(1f, Mathf.Round(width / tileW)), Mathf.Max(1f, Mathf.Round(height / tileH))), null, tex.Contains("glass") ? 0.8f : 0.15f, tex.Contains("glass") ? 0.3f : 0f);
        return q;
    }

    // ------------------------------------------------------------- the bank

    static void BuildBank(Transform city, Transform map)
    {
        var bank = new GameObject("CentralNationalBank").transform; bank.SetParent(city, false);
        bank.position = new Vector3(0f, Curb, 0f);
        float front = AveHalf + Walk;          // z of the facade line (13)
        float wallZ = front + 4f;              // the building's front wall
        var lime = "pbr_bankstone.png";
        var stoneCol = new Color(0.743f, 0.688f, 0.595f);
        var bronze = Col(new Color(0.36f, 0.25f, 0.13f), 0.9f, 0.62f);
        var brass = Col(new Color(0.78f, 0.6f, 0.3f), 1f, 0.8f);
        var b = new Batch();
        var limeM = Col(stoneCol, 0f, 0.25f);               // small mouldings: plain dressed stone
        var pedM = Tex(lime, Vector2.one, null, 0.25f);

        // body: dressed limestone cladding with real windows
        BoxT("Body", bank, new Vector3(0f, 11f, wallZ + 16f), new Vector3(46f, 22f, 32f), lime, 3f, 3f);
        var wb = new Batch();
        WindowWall(bank, wb, new Vector3(-16f, 0f, wallZ), Vector3.back, 14f, 22f, lime);
        WindowWall(bank, wb, new Vector3(16f, 0f, wallZ), Vector3.back, 14f, 22f, lime);
        WindowWall(bank, wb, new Vector3(-23f, 0f, wallZ + 16f), Vector3.left, 32f, 22f, lime);
        WindowWall(bank, wb, new Vector3(23f, 0f, wallZ + 16f), Vector3.right, 32f, 22f, lime);
        wb.Flush(bank, "BankWindows");
        Facade(bank, new Vector3(0f, 11f, wallZ - 0.02f), Vector3.back, 18f, 22f, lime, 3f, 3f);

        // podium + five proper steps (0.2 m rise, 0.45 m tread) with cheek blocks
        BoxT("Podium", bank, new Vector3(0f, 0.5f, front + 2.8f), new Vector3(23f, 1f, 3.6f), lime, 3f, 3f);
        for (int i = 0; i < 5; i++)
        {
            float zFront = front + 1f - (5 - i) * 0.45f;
            float depth = front + 1f - zFront;
            BoxT("Step", bank, new Vector3(0f, (i + 1) * 0.1f, zFront + depth * 0.5f), new Vector3(21f, (i + 1) * 0.2f, depth), lime, 3f, 3f);
            b.Add(Col(new Color(0.5f, 0.47f, 0.42f), 0f, 0.3f), Matrix4x4.TRS(new Vector3(0f, (i + 1) * 0.2f - 0.015f, zFront + 0.03f), Quaternion.identity, new Vector3(21f, 0.03f, 0.06f)));
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            BoxT("Cheek", bank, new Vector3(sx * 11.1f, 0.75f, front - 0.1f), new Vector3(1.2f, 1.5f, 2.6f), lime, 3f, 3f);
            // bronze lantern on a post on each cheek block
            Cyl("LampPost", bank, new Vector3(sx * 11.1f, 2.6f, front - 0.6f), new Vector3(0.14f, 1.1f, 0.14f), bronze);
            Lantern(bank, b, new Vector3(sx * 11.1f, 4.05f, front - 0.6f), 0.6f, bronze);
            PointLight("LampLight", bank, new Vector3(sx * 11.1f, 4.0f, front - 1.1f), new Color(1f, 0.8f, 0.5f), 2.5f, 8f);
        }

        // six fluted columns with Attic bases and capitals
        var shaft = Tex("column_flutes.png", Vector2.one, stoneCol, 0.3f);
        for (int i = 0; i < 6; i++)
        {
            float x = -8.75f + i * 3.5f, z = front + 2.2f;
            Cyl("Shaft", bank, new Vector3(x, 1.6f + 5.5f, z), new Vector3(1.2f, 5.5f, 1.2f), shaft);
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 1.125f, z), Quaternion.identity, new Vector3(1.6f, 0.25f, 1.6f)));                 // plinth
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 1.36f, z), Quaternion.identity, new Vector3(1.45f, 0.11f, 1.45f)), Batch.Cylinder);                // lower torus
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 1.52f, z), Quaternion.identity, new Vector3(1.3f, 0.05f, 1.3f)), Batch.Cylinder);                   // upper torus
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 12.72f, z), Quaternion.identity, new Vector3(1.32f, 0.08f, 1.32f)), Batch.Cylinder);               // necking
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 12.9f, z), Quaternion.identity, new Vector3(1.5f, 0.1f, 1.5f)), Batch.Cylinder);                   // echinus
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 13.15f, z), Quaternion.identity, new Vector3(1.7f, 0.3f, 1.7f)));                  // abacus
        }

        // entablature: three-band architrave, frieze with the name, dentil cornice
        float ez = front + 2.7f;
        BoxT("Architrave", bank, new Vector3(0f, 13.75f, ez), new Vector3(23f, 0.9f, 3.6f), lime, 3f, 3f);
        for (int k = 0; k < 3; k++)
            b.Add(limeM, Matrix4x4.TRS(new Vector3(0f, 13.42f + k * 0.3f, ez - 1.8f - 0.02f - k * 0.025f), Quaternion.identity, new Vector3(23f, 0.26f, 0.06f)));
        BoxT("Frieze", bank, new Vector3(0f, 14.75f, ez + 0.1f), new Vector3(23f, 1.1f, 3.4f), lime, 3f, 3f);
        Facade(bank, new Vector3(0f, 14.75f, ez + 0.1f - 1.72f), Vector3.back, 8.8f, 1.1f, "bank_sign.png", 8.8f, 1.1f);
        BoxT("Cornice", bank, new Vector3(0f, 15.5f, ez - 0.1f), new Vector3(24.2f, 0.45f, 4.2f), lime, 3f, 3f);
        for (float x = -11.8f; x <= 11.8f; x += 0.42f)
            b.Add(limeM, Matrix4x4.TRS(new Vector3(x, 15.2f, ez - 1.75f), Quaternion.identity, new Vector3(0.2f, 0.2f, 0.22f)));

        // the pediment: a real triangular gable with raking cornices and a crest
        var ped = new GameObject("Pediment"); ped.transform.SetParent(bank, false);
        ped.transform.localPosition = new Vector3(0f, 15.72f, ez);
        var pm = new Mesh();
        float hw2 = 11.8f, ph = 3.2f, d2 = 1.8f;
        pm.vertices = new[] {
            new Vector3(-hw2, 0, -d2), new Vector3(hw2, 0, -d2), new Vector3(0, ph, -d2),
            new Vector3(-hw2, 0, d2), new Vector3(hw2, 0, d2), new Vector3(0, ph, d2) };
        pm.triangles = new[] { 0, 2, 1, 3, 4, 5, 0, 3, 5, 0, 5, 2, 1, 2, 5, 1, 5, 4, 0, 1, 4, 0, 4, 3 };
        pm.uv = new[] { new Vector2(0, 0), new Vector2(6, 0), new Vector2(3, 1.6f), new Vector2(0, 0), new Vector2(6, 0), new Vector2(3, 1.6f) };
        pm.RecalculateNormals(); pm.RecalculateBounds();
        ped.AddComponent<MeshFilter>().sharedMesh = pm;
        ped.AddComponent<MeshRenderer>().sharedMaterial = pedM;
        float slope = Mathf.Atan2(ph, hw2) * Mathf.Rad2Deg, len = Mathf.Sqrt(hw2 * hw2 + ph * ph) + 0.6f;
        foreach (float sx in new[] { -1f, 1f })
            b.Add(limeM, Matrix4x4.TRS(new Vector3(sx * hw2 * 0.5f, 15.72f + ph * 0.5f + 0.18f, ez - 0.1f), Quaternion.Euler(0f, 0f, -sx * slope), new Vector3(len, 0.4f, 4.2f)));
        Cyl("Crest", bank, new Vector3(0f, 16.9f, ez - d2 - 0.06f), new Vector3(1.3f, 0.06f, 1.3f), brass, false, new Vector3(90f, 0f, 0f));
        Cyl("CrestInner", bank, new Vector3(0f, 16.9f, ez - d2 - 0.1f), new Vector3(0.9f, 0.04f, 0.9f), Col(new Color(0.1f, 0.2f, 0.12f), 0.3f, 0.6f), false, new Vector3(90f, 0f, 0f));

        // bronze double doors in a stone surround with keystone + transom
        float dz = wallZ;
        b.Add(limeM, Matrix4x4.TRS(new Vector3(-2.6f, 3.6f, dz - 0.2f), Quaternion.identity, new Vector3(0.8f, 6.0f, 0.4f)));
        b.Add(limeM, Matrix4x4.TRS(new Vector3(2.6f, 3.6f, dz - 0.2f), Quaternion.identity, new Vector3(0.8f, 6.0f, 0.4f)));
        b.Add(limeM, Matrix4x4.TRS(new Vector3(0f, 6.9f, dz - 0.22f), Quaternion.identity, new Vector3(6.0f, 0.7f, 0.44f)));
        b.Add(limeM, Matrix4x4.TRS(new Vector3(0f, 6.95f, dz - 0.46f), Quaternion.identity, new Vector3(0.7f, 0.9f, 0.1f)));   // keystone
        b.Add(bronze, Matrix4x4.TRS(new Vector3(0f, 6.05f, dz - 0.08f), Quaternion.identity, new Vector3(4.4f, 1.0f, 0.1f)));
        b.Add(GlassDark, Matrix4x4.TRS(new Vector3(0f, 6.05f, dz - 0.14f), Quaternion.identity, new Vector3(4.1f, 0.8f, 0.03f)));
        for (int g = -3; g <= 3; g++) b.Add(bronze, Matrix4x4.TRS(new Vector3(g * 0.55f, 6.05f, dz - 0.17f), Quaternion.identity, new Vector3(0.05f, 0.8f, 0.04f)));
        foreach (float sx in new[] { -1f, 1f })
        {
            float lx = sx * 1.1f;
            b.Add(bronze, Matrix4x4.TRS(new Vector3(lx, 2.85f, dz - 0.1f), Quaternion.identity, new Vector3(2.15f, 5.3f, 0.12f)));
            for (int pnl = 0; pnl < 3; pnl++)
            {
                float py = 1.1f + pnl * 1.65f;
                b.Add(Col(new Color(0.28f, 0.19f, 0.1f), 0.9f, 0.55f), Matrix4x4.TRS(new Vector3(lx, py, dz - 0.18f), Quaternion.identity, new Vector3(1.6f, 1.35f, 0.04f)));
                b.Add(brass, Matrix4x4.TRS(new Vector3(lx, py, dz - 0.21f), Quaternion.identity, new Vector3(1.66f, 0.04f, 0.02f)));
            }
            b.Add(brass, Matrix4x4.TRS(new Vector3(sx * 0.25f, 3.0f, dz - 0.26f), Quaternion.identity, new Vector3(0.06f, 1.2f, 0.06f)));      // pull handle
            // wall lanterns beside the door
            b.Add(bronze, Matrix4x4.TRS(new Vector3(sx * 3.6f, 4.75f, dz - 0.3f), Quaternion.identity, new Vector3(0.06f, 0.06f, 0.6f)));     // wall arm
            b.Add(bronze, Matrix4x4.TRS(new Vector3(sx * 3.6f, 4.75f, dz - 0.02f), Quaternion.identity, new Vector3(0.22f, 0.3f, 0.04f)));    // wall plate
            Lantern(bank, b, new Vector3(sx * 3.6f, 4.35f, dz - 0.6f), 0.42f, bronze);
        }
        PointLight("PorticoLight", bank, new Vector3(0f, 7.5f, front + 1.5f), new Color(1f, 0.82f, 0.55f), 2.4f, 14f);
        // flag poles
        foreach (float sx in new[] { -1f, 1f })
        {
            // brushed-steel pole on a stone base with a gilded ball on top, flying a cloth flag
            var steel = Col(new Color(0.78f, 0.79f, 0.8f), 1f, 0.75f);
            Vector3 pole = new Vector3(sx * 13.3f, 0f, front - 1f);
            Cyl("FlagBase", bank, pole + Vector3.up * 0.2f, new Vector3(0.7f, 0.2f, 0.7f), limeM);
            Cyl("FlagPole", bank, pole + Vector3.up * 6.4f, new Vector3(0.14f, 6f, 0.14f), steel);
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.name = "FlagFinial"; ball.transform.SetParent(bank, false);
            ball.transform.localPosition = pole + Vector3.up * 12.55f; ball.transform.localScale = Vector3.one * 0.26f;
            ball.GetComponent<Renderer>().sharedMaterial = brass;
            bool usa = sx < 0f;
            var flag = new GameObject(usa ? "Flag_USA" : "Flag_State");
            flag.transform.SetParent(bank, false);
            flag.transform.localPosition = pole + new Vector3(0.08f, 12.25f, 0f);
            flag.AddComponent<MeshFilter>();
            flag.AddComponent<MeshRenderer>().sharedMaterial = Tex(usa ? "flag_usa.png" : "flag_state.png", Vector2.one, null, 0.12f);
            var fw = flag.AddComponent<FlagWave>();
            fw.width = usa ? 2.85f : 2.4f; fw.height = 1.5f;
            fw.Build();
        }
        b.Flush(bank, "BankDetail");

        MapQuad(map, new Vector3(0f, 0f, front + 18f), new Vector2(46f, 36f), new Color(0.36f, 0.38f, 0.42f), 0.08f);
        MapIcon(map, new Vector3(0f, 0f, front + 12f), new Color(0.35f, 0.8f, 0.35f), 7f);
    }

    // ------------------------------------------------------------- props

    static void BuildStreetFurniture(Transform city)
    {
        var f = new GameObject("StreetFurniture").transform; f.SetParent(city, false);
        var pole = Col(new Color(0.18f, 0.19f, 0.2f), 0.6f, 0.4f);
        var lampM = Emissive(new Color(1f, 0.92f, 0.75f), new Color(1f, 0.85f, 0.6f) * 2.5f);
        // streetlights on both avenue sidewalks
        for (float x = -140f; x <= 140f; x += 24f)
            foreach (float s in new[] { -1f, 1f })
            {
                if (Mathf.Abs(x) < 8f && s > 0) continue;   // not in front of the bank doors
                float z = s * (AveHalf + 0.6f);
                if (Mathf.Abs(x - CrossX[0]) < 8f || Mathf.Abs(x - CrossX[1]) < 8f) continue;
                Cyl("LightPole", f, new Vector3(x, Curb + 3.5f, z), new Vector3(0.18f, 3.5f, 0.18f), pole);
                Box("Arm", f, new Vector3(x, Curb + 7f, z - s * 1.2f), new Vector3(0.12f, 0.12f, 2.4f), pole, false);
                Box("Lamp", f, new Vector3(x, Curb + 6.85f, z - s * 2.3f), new Vector3(0.5f, 0.15f, 0.8f), lampM, false);
            }
        // traffic lights at the avenue intersections
        foreach (float x in CrossX)
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1) })
            {
                Vector3 p = new Vector3(x + c.x * (StreetHalf + 0.8f), Curb, c.y * (AveHalf + 0.8f));
                Cyl("SignalPole", f, p + Vector3.up * 2.6f, new Vector3(0.16f, 2.6f, 0.16f), pole);
                Box("SignalBox", f, p + Vector3.up * 5.5f, new Vector3(0.45f, 1.3f, 0.45f), Col(new Color(0.12f, 0.12f, 0.1f)), false);
                Cyl("Red", f, p + new Vector3(0f, 5.9f, -c.y * 0.24f), new Vector3(0.26f, 0.02f, 0.26f), Emissive(Color.red, new Color(3f, 0.1f, 0.1f)), false, new Vector3(90f, 0f, 0f));
                Cyl("Amber", f, p + new Vector3(0f, 5.5f, -c.y * 0.24f), new Vector3(0.26f, 0.02f, 0.26f), Col(new Color(0.35f, 0.25f, 0.05f)), false, new Vector3(90f, 0f, 0f));
                Cyl("Green", f, p + new Vector3(0f, 5.1f, -c.y * 0.24f), new Vector3(0.26f, 0.02f, 0.26f), Col(new Color(0.05f, 0.25f, 0.1f)), false, new Vector3(90f, 0f, 0f));
            }
        // hydrants, bins, benches, planters
        var red = Col(new Color(0.7f, 0.1f, 0.08f), 0.2f, 0.5f);
        var green = Col(new Color(0.12f, 0.25f, 0.14f), 0.2f, 0.4f);
        var leaves = Col(new Color(0.2f, 0.36f, 0.15f), 0f, 0.2f);
        var bark = Col(new Color(0.28f, 0.2f, 0.13f));
        for (float x = -130f; x <= 130f; x += 18f)
            foreach (float s in new[] { -1f, 1f })
            {
                if (Mathf.Abs(x) < 16f) continue;
                if (Mathf.Abs(x - CrossX[0]) < 10f || Mathf.Abs(x - CrossX[1]) < 10f) continue;
                float z = s * (AveHalf + 1.2f);
                int k = s_rng.Next(0, 4);
                Vector3 p = new Vector3(x + R(-3f, 3f) + 9f, Curb, z);
                if (k == 0) { Cyl("Hydrant", f, p + Vector3.up * 0.4f, new Vector3(0.3f, 0.4f, 0.3f), red); Cyl("Cap", f, p + Vector3.up * 0.85f, new Vector3(0.24f, 0.08f, 0.24f), red, false); }
                else if (k == 1) { Cyl("Bin", f, p + Vector3.up * 0.5f, new Vector3(0.6f, 0.5f, 0.6f), green); }
                else if (k == 2)
                {
                    Box("BenchSeat", f, p + new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.1f, 0.5f), Col(new Color(0.4f, 0.28f, 0.16f)));
                    Box("BenchBack", f, p + new Vector3(0f, 0.8f, s * 0.22f), new Vector3(2f, 0.5f, 0.08f), Col(new Color(0.4f, 0.28f, 0.16f)), false);
                    Box("BenchLeg", f, p + new Vector3(0f, 0.22f, 0f), new Vector3(1.8f, 0.44f, 0.4f), pole, false);
                }
                else
                {
                    Box("Planter", f, p + Vector3.up * 0.35f, new Vector3(1.4f, 0.7f, 1.4f), Col(new Color(0.55f, 0.53f, 0.5f)));
                    Cyl("Trunk", f, p + Vector3.up * 2.3f, new Vector3(0.18f, 1.7f, 0.18f), bark, false);
                    for (int i = 0; i < 4; i++)
                    {
                        var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(leaf.GetComponent<Collider>());
                        leaf.transform.SetParent(f, false);
                        leaf.transform.position = p + new Vector3(R(-0.5f, 0.5f), R(4.2f, 5f), R(-0.5f, 0.5f));
                        leaf.transform.localScale = Vector3.one * R(1.2f, 1.7f);
                        leaf.GetComponent<Renderer>().sharedMaterial = leaves;
                    }
                }
            }
        // a bus stop across from the bank
        var bs = new Vector3(18f, Curb, -(AveHalf + 2.2f));
        Box("BusRoof", f, bs + new Vector3(0f, 2.6f, 0f), new Vector3(4.5f, 0.12f, 1.8f), Col(new Color(0.2f, 0.2f, 0.22f), 0.5f, 0.5f), false);
        Box("BusBack", f, bs + new Vector3(0f, 1.3f, -0.8f), new Vector3(4.5f, 2.5f, 0.06f), Glass);
        Box("BusAd", f, bs + new Vector3(2.1f, 1.3f, 0f), new Vector3(0.1f, 2.4f, 1.6f), Emissive(new Color(0.9f, 0.5f, 0.2f), new Color(1.2f, 0.6f, 0.2f)));
        Box("BusBench", f, bs + new Vector3(0f, 0.45f, -0.4f), new Vector3(3.6f, 0.1f, 0.5f), Col(new Color(0.5f, 0.5f, 0.52f), 0.6f, 0.5f));
    }

    // Kenney car model with glossy paint, a box collider, and optional colour
    static GameObject Car(Transform p, string model, Vector3 pos, float yaw, Color? paint = null, float scale = 2.1f)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(CARS + model + ".fbx");
        if (src == null) return null;
        var car = (GameObject)PrefabUtility.InstantiatePrefab(src);
        car.transform.SetParent(p, false);
        car.transform.localPosition = pos; car.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        car.transform.localScale = Vector3.one * scale;
        foreach (var r in car.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = new Material(mats[i]);
                m.SetFloat("_Smoothness", 0.78f); m.SetFloat("_Metallic", 0.25f);
                if (paint.HasValue) m.SetColor("_BaseColor", Color.Lerp(Color.white, paint.Value, 0.55f));
                mats[i] = m;
            }
            r.sharedMaterials = mats;
        }
        var b = new Bounds(car.transform.position, Vector3.zero);
        foreach (var r in car.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
        var col = car.AddComponent<BoxCollider>();
        col.center = car.transform.InverseTransformPoint(b.center);
        var sz = car.transform.InverseTransformVector(b.size);
        col.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
        return car;
    }

    static GameObject RealCar(Transform p, string prefab, string fallback, Vector3 pos, float yaw)
    {
        var src = VehiclePrefab(prefab) ?? PropPrefab(prefab);   // the parking-lot jeeps are props
        if (src == null) { Debug.LogWarning("Missing vehicle prefab: " + prefab); return null; }   // only the downloaded vehicles are used
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return g;
    }

    // extra downloaded street furniture: vending machines against the shop fronts
    // and street bins along the curb (Japanese Traffic Assets pack)
    static void BuildStreetExtras(Transform city)
    {
        var f = new GameObject("StreetExtras").transform; f.SetParent(city, false);
        string[] bins = { "JapaneseTraffic_36_streetbinoutside", "JapaneseTraffic_37_streetbinoutside1" };
        int k = 0;
        foreach (float x in new[] { -118f, -88f, -44f, 34f, 82f, 128f })
            foreach (float sd in new[] { -1f, 1f })
            {
                if (Mathf.Abs(x - VanPos.x) < 8f && sd > 0) continue;
                P("JapaneseTraffic_41_vendingmachine", f, new Vector3(x + 3f * sd, Curb, sd * (AveHalf + Walk - 0.85f)), sd > 0 ? 180f : 0f, 1f);
                P(bins[k++ % bins.Length], f, new Vector3(x - 4f, Curb, sd * (AveHalf + 0.9f)), 0f, 1f);
            }
    }

    static void BuildParkedCars(Transform city)
    {
        var cars = new GameObject("ParkedCars").transform; cars.SetParent(city, false);
        // real Sketchfab vehicles (see Assets/Heist/Vehicles/CREDITS.txt)
        string[] models = { "Volvo", "Taxi", "BMW", "Pickup", "Volvo", "Van", "Taxi", "Truck", "ParkingLot_01_Carjeep", "ParkingLot_02_Carjeep1" };
        int bmws = 0, trucks = 0, volvos = 0;
        for (float x = -140f; x <= 140f; x += 12f)
            foreach (float s in new[] { -1f, 1f })
            {
                if (s_rng.NextDouble() < 0.45) continue;
                if (Mathf.Abs(x) < 26f) continue;                                   // bank frontage stays clear
                if (Mathf.Abs(x - CrossX[0]) < 12f || Mathf.Abs(x - CrossX[1]) < 12f) continue;
                if (Mathf.Abs(x - VanPos.x) < 9f && s > 0) continue;                  // the getaway van's spot
                if (Mathf.Abs(x - 18f) < 10f && s < 0) continue;                      // bus stop
                string m = models[s_rng.Next(models.Length)];
                if (m == "BMW" && ++bmws > 2) m = "Taxi";                            // the BMW is a heavy model
                if (m == "Volvo" && ++volvos > 3) m = "Pickup";                      // so is the Volvo (~2M triangles)
                if (m == "Truck" && ++trucks > 3) m = "Pickup";
                RealCar(cars, m, "sedan", new Vector3(x + R(-1f, 1f), 0f, s * (AveHalf - 1.5f)), s > 0 ? 90f : -90f);
            }
        // a city bus at the stop across from the bank, a motorbike by the diner
        RealCar(cars, "Bus", "truck", new Vector3(18f, 0f, -(AveHalf - 1.7f)), -90f);
        RealCar(cars, "NakedBike", "box", new Vector3(-50f, 0f, AveHalf - 1.2f), 70f);
        RealCar(cars, "Honda", "box", new Vector3(44f, 0f, -(AveHalf - 1.2f)), -110f);
        RealCar(cars, "MotoDemo", "box", new Vector3(-74f, 0f, -(AveHalf - 1.2f)), -70f);
        RealCar(cars, "Honda", "box", new Vector3(86f, 0f, AveHalf - 1.2f), 110f);

        // moving traffic in the two middle lanes
        var traffic = new GameObject("Traffic").transform; traffic.SetParent(city, false);
        string[] movers = { "Taxi", "Pickup", "Van", "Taxi", "Truck", "Volvo" };
        for (int i = 0; i < 6; i++)
        {
            bool east = i % 2 == 0;
            float x = -130f + i * 47f;
            var c = RealCar(traffic, movers[i], "sedan", new Vector3(x, 0f, east ? -2.2f : 2.2f), east ? 90f : -90f);
            if (c == null) continue;
            var tc = c.AddComponent<TrafficCar>();
            tc.dir = east ? Vector3.right : Vector3.left;
            tc.cruise = R(7.5f, 10f);
            var rb = c.AddComponent<Rigidbody>(); rb.isKinematic = true;
        }
    }

    static GameObject BuildPoliceScene(Transform city)
    {
        var p = new GameObject("PoliceScene").transform; p.SetParent(city, false);
        var cars = new[] { (new Vector3(-9f, 0f, -2.5f), 60f), (new Vector3(-1f, 0f, -4.5f), 95f), (new Vector3(8f, 0f, -2f), 125f), (new Vector3(20f, 0f, 1f), 80f) };
        foreach (var (pos, yaw) in cars)
        {
            var car = RealCar(p, "PoliceE7", "police", pos, yaw);
            if (car == null) continue;
            var pl = car.AddComponent<PoliceLights>();
            var bar = new GameObject("LightBar").transform; bar.SetParent(car.transform, false); bar.localPosition = new Vector3(0f, 0.95f, 0f);
            pl.red = PointLight("Red", car.transform, new Vector3(-0.2f, 1.1f, 0f), new Color(1f, 0.1f, 0.1f), 6f, 12f);
            pl.blue = PointLight("Blue", car.transform, new Vector3(0.2f, 1.1f, 0f), new Color(0.15f, 0.3f, 1f), 6f, 12f);
            pl.siren = pos.x < 0f;   // two of them have sirens going
        }
        // SWAT armored truck, an ambulance on standby, and the helicopter overhead
        RealCar(p, "Armored", "truck", new Vector3(-24f, 0f, -3.5f), 75f);
        RealCar(p, "Ambulance", "ambulance", new Vector3(30f, 0f, -4f), -100f);
        // jersey barriers closing the cordon ends
        foreach (var jb in new[] { new Vector3(-28.5f, 0f, -8.8f), new Vector3(-30f, 0f, -8.8f), new Vector3(28.5f, 0f, -8.8f), new Vector3(30f, 0f, -8.8f) })
            P(jb.x < 0 ? "JapaneseTraffic_13_JerseyBarrierEnd" : "JapaneseTraffic_12_jerseyshape1", p, jb, 90f, 1.6f);
        // police motorcycles at both ends of the cordon
        RealCar(p, "HarleyPolice", "box", new Vector3(13.5f, 0f, -5.5f), 150f);
        RealCar(p, "HarleyPolice", "box", new Vector3(-14.5f, 0f, -5f), 30f);
        var heliSrc = VehiclePrefab("Helicopter");
        if (heliSrc != null)
        {
            var heli = (GameObject)PrefabUtility.InstantiatePrefab(heliSrc);
            heli.transform.SetParent(p, false);
            heli.transform.position = new Vector3(0f, 32f, 18f);
            Object.DestroyImmediate(heli.GetComponent<BoxCollider>());
            var ph = heli.AddComponent<PoliceHelicopter>();
            var sl = new GameObject("Searchlight").AddComponent<Light>();
            sl.transform.SetParent(heli.transform, false); sl.transform.localPosition = new Vector3(0f, -0.6f, 3f);
            sl.type = LightType.Spot; sl.spotAngle = 18f; sl.range = 80f; sl.intensity = 60f; sl.color = new Color(1f, 0.97f, 0.9f);
            sl.shadows = LightShadows.None;
            ph.searchlight = sl;
        }
        // police tape line with barriers across the avenue
        var line = new GameObject("PoliceLine").transform; line.SetParent(p, false); line.position = new Vector3(-6f, 0f, -12f);
        var tape = Col(new Color(0.95f, 0.8f, 0.1f), 0f, 0.5f);
        if (!BuildPoliceCordonProps(p))
        for (float x = -26f; x <= 26f; x += 4f)
        {
            Box("Barrier", p, new Vector3(x, 0.55f, -8.8f), new Vector3(2.2f, 0.3f, 0.2f), tape);
            Box("BarrierLeg", p, new Vector3(x - 0.9f, 0.3f, -8.8f), new Vector3(0.1f, 0.6f, 0.5f), Col(new Color(0.2f, 0.2f, 0.2f)), false);
            Box("BarrierLeg", p, new Vector3(x + 0.9f, 0.3f, -8.8f), new Vector3(0.1f, 0.6f, 0.5f), Col(new Color(0.2f, 0.2f, 0.2f)), false);
        }
        p.gameObject.SetActive(false);
        return p.gameObject;
    }

    static GameObject BuildGetawayVan(Transform city)
    {
        var holder = new GameObject("GetawayVan"); holder.transform.SetParent(city, false);
        holder.transform.position = VanPos; holder.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        // the downloaded 2003 Chevrolet Express (falls back to the old van)
        bool chevy = VehiclePrefab("ChevyVan") != null;
        var car = RealCar(holder.transform, chevy ? "ChevyVan" : "Van", "van", Vector3.zero, 0f);
        if (car != null) Object.DestroyImmediate(car.GetComponent<BoxCollider>());
        var rb = holder.AddComponent<Rigidbody>();
        var col = holder.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 1.15f, 0f); col.size = chevy ? new Vector3(2.05f, 1.8f, 5.6f) : new Vector3(2f, 1.6f, 4.6f);
        var v = holder.AddComponent<Vehicle>();
        v.displayName = "getaway van";
        v.halfExtents = chevy ? new Vector3(0.95f, 0.4f, 2.5f) : new Vector3(0.9f, 0.4f, 2f);
        var wheels = new List<Transform>();
        if (car != null)
            foreach (Transform t in car.GetComponentsInChildren<Transform>())
                if (t.name.ToLower().Contains("wheel")) wheels.Add(t);
        wheels.Sort((a, b) => (b.localPosition.z > 0 ? 0 : 2).CompareTo(a.localPosition.z > 0 ? 0 : 2));
        v.wheels = new Transform[0];   // keep the model's own wheel placement (visual only)
        var mark = new GameObject("VanBlip").AddComponent<MinimapBlip>(); mark.transform.SetParent(holder.transform, false);
        mark.color = new Color(0.3f, 0.6f, 1f); mark.size = 5f;
        return holder;
    }

    static void BuildSafehouse(Transform city, Transform map)
    {
        var s = new GameObject("Safehouse").transform; s.SetParent(city, false);
        s.position = SafehousePos;
        // an auto-repair garage with its roller door open, on the back street
        var garage = new Vector3(0f, 0f, -StreetHalf - 9f);
        var mark = Cyl("Marker", s, new Vector3(0f, 0.6f, 0f), new Vector3(7f, 0.6f, 7f), Transparent(new Color(1f, 0.85f, 0.2f, 0.25f)), false);
        mark.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        MapIcon(map, SafehousePos, new Color(1f, 0.85f, 0.2f), 8f);
    }

    // distant city all around the edge so it feels big: the downloaded "City at
    // Night" blocks (13 variants) blown up to full size, plus the Innovate Hub office
    static void BuildSkyline(Transform city)
    {
        var sk = new GameObject("Skyline").transform; sk.SetParent(city, false);
        int variants = 0;
        while (PropPrefab($"CityNight_{variants:00}_instance{variants}") != null) variants++;
        const int count = 24;
        for (int i = 0; i < count && variants > 0; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f + R(-0.04f, 0.04f);
            float r = R(255f, 300f);
            var pos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            int v = i % variants;
            var g = P($"CityNight_{v:00}_instance{v}", sk, pos, R(0f, 360f), R(24f, 30f));
            if (g == null) continue;
            foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            foreach (var rr in g.GetComponentsInChildren<Renderer>()) rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // the parking-lot diorama's office block, as a closer landmark at the far end of the avenue
        var hub = P("ParkingLot_01_InnovateHubBuilding", sk, new Vector3(205f, 0f, 0f), 270f, 1f);
        if (hub != null) foreach (var c in hub.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
    }
}
