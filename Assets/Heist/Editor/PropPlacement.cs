using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Dresses the city with the downloaded Sketchfab props (Prefabs/Props):
// street lamps, traffic signals, street-name / stop signs, hydrants, trash
// cans, benches, mailboxes, parking meters, a bus shelter, utility boxes,
// manholes and storm drains; and the police cordon (metal barricades, cones,
// flashing road lights).
public static partial class HeistBuilder
{
    // the "Street Asset Pack" was authored about twice real size
    static float PropScale(string name) => name.StartsWith("StreetPack_") ? 0.47f : 1f;

    public static GameObject P(string prefab, Transform parent, Vector3 pos, float yaw, float scale = 1f)
    {
        var src = PropPrefab(prefab);
        if (src == null) return null;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.transform.localScale = Vector3.one * scale * PropScale(prefab);
        GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
        return g;
    }

    // seats (chairs, sofas): place so the seat faces `face`. The model's own front
    // is worked out from its backrest - the tall part - which sits at the back.
    static readonly Dictionary<string, Vector3> s_front = new Dictionary<string, Vector3>();
    public static GameObject PFace(string prefab, Transform parent, Vector3 pos, Vector3 face, float scale = 1f)
    {
        if (!s_front.TryGetValue(prefab, out var front))
        {
            front = Vector3.forward;
            var probe = P(prefab, null, Vector3.zero, 0f, 1f);
            if (probe != null)
            {
                var pts = new List<Vector3>();
                foreach (var mf in probe.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null)   // the editor can read any mesh
                        foreach (var v in mf.sharedMesh.vertices) pts.Add(mf.transform.TransformPoint(v));
                if (pts.Count > 0)
                {
                    float lo = float.MaxValue, hi = float.MinValue; Vector3 mid = Vector3.zero;
                    foreach (var p in pts) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); mid += p; }
                    mid /= pts.Count;
                    Vector3 top = Vector3.zero; int n = 0;
                    foreach (var p in pts) if (p.y > lo + (hi - lo) * 0.7f) { top += p; n++; }
                    Vector3 back = n > 0 ? top / n - mid : Vector3.zero; back.y = 0f;
                    // snap to the nearest axis: models are authored axis-aligned
                    if (back.sqrMagnitude > 1e-6f)
                        front = Mathf.Abs(back.x) > Mathf.Abs(back.z) ? new Vector3(-Mathf.Sign(back.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(back.z));
                }
                Object.DestroyImmediate(probe);
            }
            s_front[prefab] = front;
        }
        face.y = 0f;
        float yaw = Vector3.SignedAngle(front, face.normalized, Vector3.up);
        return P(prefab, parent, pos, yaw, scale);
    }

    // wall art (paintings, a clock): hung flat on a wall, centred on `wallPoint`,
    // face pointing along `outward`. The model's thinnest side is its face axis,
    // whichever way the download was authored (some lie flat, face up).
    public static GameObject PWall(string prefab, Transform parent, Vector3 wallPoint, Vector3 outward, float scale = 1f, bool flip = false)
    {
        var src = PropPrefab(prefab);
        if (src == null) return null;
        var size = src.GetComponent<BoxCollider>() != null ? src.GetComponent<BoxCollider>().size : Vector3.one;
        Quaternion toZ = size.y <= size.x && size.y <= size.z ? Quaternion.Euler(90f, 0f, 0f)     // face-up -> +Z
                       : size.x <= size.z ? Quaternion.Euler(0f, -90f, 0f)                       // faces +X -> +Z
                       : Quaternion.identity;
        Quaternion rot = Quaternion.LookRotation(outward.normalized, Vector3.up) * (flip ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity) * toZ;
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
        g.transform.SetParent(parent, false);
        g.transform.rotation = rot;
        g.transform.localScale = Vector3.one * scale;
        g.transform.position = Vector3.zero;
        var rs = g.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        float depth = Mathf.Abs(Vector3.Dot(b.size, new Vector3(Mathf.Abs(outward.normalized.x), Mathf.Abs(outward.normalized.y), Mathf.Abs(outward.normalized.z))));
        g.transform.position = wallPoint + outward.normalized * (depth * 0.5f + 0.01f) - b.center;
        foreach (var c in g.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // flat on the wall: nothing to bump into
        return g;
    }

    static bool HasStreetProps => PropPrefab("CityProps_09_Light") != null;

    static void BuildStreetProps(Transform city)
    {
        var f = new GameObject("StreetFurniture").transform; f.SetParent(city, false);
        string[] bins = { "CityProps_22_TrashCanLarge", "CityProps_23_TrashCanSmall", "CityProps_24_TrashCommercial", "StreetProps_01_metalstreetpropslowpoly0", "StreetProps_06_metalstreetpropslowpoly0" };

        bool nearCross(float x, float m) => Mathf.Abs(x - CrossX[0]) < m || Mathf.Abs(x - CrossX[1]) < m;

        // street lamps along both avenue sidewalks
        for (float x = -140f; x <= 140f; x += 24f)
            foreach (float s in new[] { -1f, 1f })
            {
                if (Mathf.Abs(x) < 8f && s > 0) continue;
                if (nearCross(x, 8f)) continue;
                P("CityProps_09_Light", f, new Vector3(x, Curb, s * (AveHalf + 0.6f)), s > 0 ? 180f : 0f, 1.45f);
            }

        // signals + street-name signs at the avenue intersections, stop signs on the back streets
        foreach (float x in CrossX)
        {
            foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1) })
            {
                Vector3 p = new Vector3(x + c.x * (StreetHalf + 0.7f), Curb, c.y * (AveHalf + 0.7f));
                P("StreetProps_08_metalstreetpropslowpoly0", f, p, c.y > 0 ? 180f : 0f, 1.25f);
                P("StreetProps_07_metalstreetpropslowpoly0", f, p + new Vector3(c.x * 1.2f, 0f, 0f), c.x > 0 ? 90f : -90f, 1f);
                P("CityProps_04_Crossing", f, p + new Vector3(-c.x * 2.6f, 0f, c.y * 0.6f), c.y > 0 ? 180f : 0f, 1f);
            }
            foreach (float z in BackZ)
                foreach (float sx in new[] { -1f, 1f })
                    P("StreetProps_04_metalstreetpropslowpoly0", f, new Vector3(x + sx * (StreetHalf + 0.6f), Curb, z - Mathf.Sign(z) * (StreetHalf + 0.6f)), sx > 0 ? 90f : -90f, 1f);
        }

        // hydrants, bins, benches, mailboxes, parking meters, planter trees
        var leaves = Col(new Color(0.2f, 0.36f, 0.15f), 0f, 0.2f);
        var bark = Col(new Color(0.28f, 0.2f, 0.13f));
        int i = 0;
        for (float x = -132f; x <= 132f; x += 9f, i++)
            foreach (float s in new[] { -1f, 1f })
            {
                if (Mathf.Abs(x) < 16f) continue;
                if (nearCross(x, 10f)) continue;
                float z = s * (AveHalf + 1.25f);
                float yaw = s > 0 ? 180f : 0f;
                int k = (i * 7 + (s > 0 ? 3 : 0)) % 9;
                Vector3 p = new Vector3(x + R(-1.5f, 1.5f), Curb, z);
                switch (k)
                {
                    case 0: P(R(0f, 1f) < 0.5f ? "CityProps_07_FireHydrant" : "StreetProps_02_metalstreetpropslowpoly0", f, p, yaw, 0.78f); break;
                    case 1: case 5: P(bins[s_rng.Next(bins.Length)], f, p, yaw, 1f); break;
                    case 2: P("CityProps_13_ParkBenchLong", f, p + new Vector3(0f, 0f, s * 0.5f), 90f + (s > 0 ? 180f : 0f), 1f); break;
                    case 3: P("CityProps_18_PostBox", f, p, yaw, 1f); break;
                    case 4: case 7:
                        P("CityProps_15_ParkingMeter", f, new Vector3(p.x, Curb, s * (AveHalf + 0.45f)), yaw, 1f);
                        break;
                    case 6: case 8:
                        // the Jacaranda is ~300k triangles: every other slot only, or the street drops to ~10 FPS
                        if (PropPrefab("Jacaranda") != null) { if (k == 6) StreetTree(f, new Vector3(p.x, Curb, s * (AveHalf + 1.3f))); break; }
                        if (k == 8) break;
                        Box("Planter", f, p + Vector3.up * 0.35f, new Vector3(1.4f, 0.7f, 1.4f), Tex("granite.png", Vector2.one, null, 0.3f));
                        Cyl("Trunk", f, p + Vector3.up * 2.3f, new Vector3(0.18f, 1.7f, 0.18f), bark, false);
                        for (int l = 0; l < 4; l++)
                        {
                            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(leaf.GetComponent<Collider>());
                            leaf.transform.SetParent(f, false);
                            leaf.transform.position = p + new Vector3(R(-0.5f, 0.5f), R(4.2f, 5f), R(-0.5f, 0.5f));
                            leaf.transform.localScale = Vector3.one * R(1.2f, 1.7f);
                            leaf.GetComponent<Renderer>().sharedMaterial = leaves;
                        }
                        break;
                }
            }
        P("CityProps_25_Utility", f, new Vector3(-40f, Curb, AveHalf + 3.2f), 180f, 1f);
        P("CityProps_25_Utility", f, new Vector3(72f, Curb, -(AveHalf + 3.2f)), 0f, 1f);
        P("CityProps_06_Dumpster", f, new Vector3(CrossX[0] + StreetHalf + 2.5f, Curb, 30f), 90f, 1f);
        P("CityProps_06_Dumpster", f, new Vector3(CrossX[1] - StreetHalf - 2.5f, Curb, -28f), -90f, 1f);
        P("TrashCan", f, new Vector3(CrossX[1] - StreetHalf - 2.3f, Curb, -24f), 0f, 1f);

        // a proper bus shelter across from the bank (open side to the road)
        P("BusStop", f, new Vector3(18f, Curb, -(AveHalf + 2.3f)), 180f, 1f);
    }

    // police cordon dressing: barricades across the avenue, cones and lamps
    static bool BuildPoliceCordonProps(Transform p)
    {
        if (PropPrefab("StreetPack_12_metalbarrier1") == null) return false;
        for (float x = -26f; x <= 26f; x += 2.15f)
            P(((int)(x * 10) % 3 == 0) ? "StreetPack_13_metalbarrier2" : "StreetPack_12_metalbarrier1", p, new Vector3(x, 0f, -8.8f), 0f, 1f);
        string[] cones = { "TrafficCones_14_TrafficCone21001", "TrafficCones_15_TrafficCone22002", "TrafficCones_16_TrafficCone22003", "TrafficCones_17_TrafficCone22004" };
        var conePos = new[] { new Vector3(-16f, 0f, -6.5f), new Vector3(-14f, 0f, -6f), new Vector3(-12f, 0f, -5.5f), new Vector3(14f, 0f, -6.5f), new Vector3(16f, 0f, -6f),
                              new Vector3(26f, 0f, -2f), new Vector3(27f, 0f, 0.5f), new Vector3(-20f, 0f, 3.5f), new Vector3(-21f, 0f, 1f) };
        for (int i = 0; i < conePos.Length; i++) P(cones[i % cones.Length], p, conePos[i], i * 37f, 1f);
        P("TrafficCones_01_Collar1001", p, new Vector3(-10f, 1.02f, -8.8f), 0f, 1f);
        P("TrafficCones_03_Collar2002", p, new Vector3(4f, 1.02f, -8.8f), 0f, 1f);
        P("TrafficCones_01_Collar1001", p, new Vector3(18f, 1.02f, -8.8f), 0f, 1f);
        P("StreetPack_05_concretebarrier", p, new Vector3(-18f, 0f, 6.2f), 0f, 1f);
        P("StreetPack_05_concretebarrier", p, new Vector3(24f, 0f, 6.2f), 0f, 1f);
        return true;
    }
}
