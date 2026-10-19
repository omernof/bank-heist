using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Downloaded Sketchfab props (Assets/Heist/Props/<Name>/) -> clean prefabs in
// Assets/Heist/Prefabs/Props. Single items are scaled to a real-world size;
// "collection" files (many props laid out side by side) are split into one
// prefab per top-level object, keeping the pack's own scale (cm -> m fixed).
public static partial class HeistBuilder
{
    const string PROPS = ROOT + "Props/";
    const string PROPPREF = ROOT + "Prefabs/Props/";

    // name -> target size in metres along its largest axis (0 = collection)
    static readonly Dictionary<string, float> PropSize = new Dictionary<string, float>
    {
        { "ATM", 1.9f }, { "FireHydrant", 0.85f }, { "VaultDoor", 3.4f }, { "CashBriefcase", 0.5f },
        { "CashPile", 0.7f }, { "ConcreteBin", 1.1f }, { "Briefcase", 0.48f }, { "MoneyStacks", 0.6f },
        { "OfficeChair", 1.05f }, { "OfficeDesk", 1.6f }, { "BusStop", 4.2f }, { "GoldBar", 0.26f },
        { "TrafficCones", 0 }, { "TrafficLight", 5.2f }, { "TrashCan", 1.05f },
        { "CityProps", 0 }, { "TrafficSigns", 0 }, { "Park", 0 }, { "StreetPack", 0 }, { "StreetProps", 0 },
        // bank interior (downloaded 2026-10-06): plant pack + desk set are collections
        { "PottedPlants", 0 }, { "DeskSet", 0 }, { "OfficeSofa", 2.1f },
        { "ChandelierGrand", 1.3f }, { "Chandelier", 0.9f }, { "PaintingVictorian", 1.5f }, { "PaintingFrame", 1.2f },
        { "PaintingArt", 1.2f }, { "WallClock", 0.55f }, { "WaterDispenser", 1.15f },
        // street extras (downloaded, imported 2026-10-06): packs split into one prefab per object
        { "ParkingLot", 0 }, { "JapaneseTraffic", 0 }, { "OldStore", 14f }, { "CityNight", 0 },
        // glTF downloads (via glTFast). -1 = one prefab at the file's own (metre) scale
        { "LootBag", 0.55f }, { "OfficePack", 0 }, { "DeskFlags", 0 }, { "OfficeWorkerA", -1f }, { "OfficeWorkerB", -1f },
    };

    // per-pack scale fixes for downloads authored at odd units
    static readonly Dictionary<string, float> PackUnit = new Dictionary<string, float>
    {
        { "ParkingLot", 8.5f },     // a miniature: x8.5 makes the jeeps real size (~4.3 m)
        { "DeskFlags", 0.004f },    // ~35 cm desk flags
        { "CityNight", 0.01f },
    };
    static readonly Dictionary<string, (string part, float metres)> PackRef = new Dictionary<string, (string, float)>
    {
        { "JapaneseTraffic", ("jerseybarrierend", 1.2f) },   // the end piece of a jersey barrier is ~1.2 m long
    };
    static readonly Dictionary<string, string[]> PackDeep = new Dictionary<string, string[]>
    {
        { "ParkingLot", new[] { "Cars", "Lamps", "Signs", "Vegetation" } },
    };

    static readonly string[] StreetExtraProps = { "ParkingLot", "JapaneseTraffic", "OldStore", "CityNight", "LootBag", "OfficePack", "DeskFlags", "OfficeWorkerA", "OfficeWorkerB" };

    [MenuItem("Tools/Bank Heist/Build Street Extra Props")]
    public static void BuildStreetExtraProps()
    {
        foreach (var n in StreetExtraProps)
        {
            foreach (var old in AssetDatabase.FindAssets(n + "_", new[] { PROPPREF.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(old);
                if (Path.GetFileName(path).StartsWith(n + "_")) AssetDatabase.DeleteAsset(path);
            }
            try { BuildProp(n, PropSize[n]); }
            catch (System.Exception e) { Debug.LogWarning($"Prop {n} failed: {e}"); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        foreach (var n in StreetExtraProps)
            foreach (var g in AssetDatabase.FindAssets("t:Prefab " + n, new[] { PROPPREF.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var b = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<BoxCollider>();
                Debug.Log($"STREETPROP {Path.GetFileNameWithoutExtension(path)} size {(b != null ? b.size.ToString("F1") : "?")}");
            }
        Debug.Log("STREET EXTRA PROPS built");
    }

    static readonly string[] BankInteriorProps = { "PottedPlants", "DeskSet", "OfficeSofa", "OfficeChair",
        "ChandelierGrand", "Chandelier", "PaintingVictorian", "PaintingFrame", "PaintingArt", "WallClock", "WaterDispenser" };

    // rebuilds just the bank-interior props (the full pass re-imports every street prop)
    [MenuItem("Tools/Bank Heist/Build Bank Interior Props")]
    public static void BuildBankInteriorProps()
    {
        foreach (var n in BankInteriorProps)
        {
            foreach (var old in AssetDatabase.FindAssets(n + "_", new[] { PROPPREF.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(old);
                if (Path.GetFileName(path).StartsWith(n + "_")) AssetDatabase.DeleteAsset(path);
            }
            try { BuildProp(n, PropSize[n]); }
            catch (System.Exception e) { Debug.LogWarning($"Prop {n} failed: {e}"); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (var n in BankInteriorProps)
            foreach (var g in AssetDatabase.FindAssets("t:Prefab " + n, new[] { PROPPREF.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var b = go.GetComponent<BoxCollider>();
                Debug.Log($"BANKPROP {Path.GetFileNameWithoutExtension(path)} size {(b != null ? b.size.ToString("F2") : "?")}");
            }
        Debug.Log("BANK INTERIOR PROPS built");
    }

    public static GameObject PropPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PROPPREF + name + ".prefab");

    [MenuItem("Tools/Bank Heist/Build Prop Prefabs")]
    public static void BuildPropPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(ROOT + "Prefabs/Props")) AssetDatabase.CreateFolder(ROOT + "Prefabs", "Props");
        foreach (var kv in PropSize)
        {
            try { BuildProp(kv.Key, kv.Value); }
            catch (System.Exception e) { Debug.LogWarning($"Prop {kv.Key} failed: {e.Message}"); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("PROPS built");
    }

    static string PropModel(string name)
    {
        if (!Directory.Exists(PROPS + name)) return null;
        foreach (var f in Directory.GetFiles(PROPS + name))
        {
            string l = f.ToLower();
            if (l.EndsWith(".fbx") || l.EndsWith(".obj") || l.EndsWith(".dae") || l.EndsWith(".glb") || l.EndsWith(".gltf")) return f.Replace('\\', '/');
        }
        return null;
    }

    static void BuildProp(string name, float size)
    {
        string file = PropModel(name);
        var src = file != null ? AssetDatabase.LoadAssetAtPath<GameObject>(file) : null;
        if (src == null) { Debug.LogWarning("Prop missing: " + name); return; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        // some downloads ship their own scene lights / cameras (Painting 3 had a
        // 1000-intensity point light that washed out the bank wall): props never keep them
        StripLightsAndCameras(inst);
        // a painting's canvas is a big flat plane - exactly what the ground-plane
        // cleanup removes - so wall art keeps all its parts
        if (!name.StartsWith("Painting") && name != "WallClock") StripGroundPlanes(inst);
        FixMaterials(inst, name);
        DecorMaterials(inst, name);

        if (size > 0f)
        {
            SavePropPrefab(inst, name, size);
            return;
        }
        if (size < 0f)
        {
            SavePropPrefab(inst, name, 0f, 1f);
            return;
        }
        // collection: one prefab per top-level part that has geometry
        var whole = Bounds(inst);
        float unit = Mathf.Max(whole.size.x, whole.size.z) > 300f ? 0.01f : 1f;   // pack authored in cm
        if (PackUnit.TryGetValue(name, out var fixedUnit)) unit = fixedUnit;
        if (PackRef.TryGetValue(name, out var rf))
        {
            // scale the pack so one known item comes out at its real size
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLower().Contains(rf.part) && t.GetComponentInChildren<Renderer>() != null)
                {
                    var rb = Bounds(t.gameObject);
                    unit = rf.metres / Mathf.Max(0.0001f, Mathf.Max(rb.size.x, rb.size.y, rb.size.z));
                    break;
                }
        }
        var parts = new List<Transform>();
        foreach (Transform c in inst.transform) if (c.GetComponentInChildren<Renderer>() != null) parts.Add(c);
        // walk down wrapper nodes (scene -> root -> GLTF_SceneRootNode -> items)
        while (parts.Count == 1 && parts[0].GetComponent<Renderer>() == null)
        {
            var kids = new List<Transform>();
            foreach (Transform c in parts[0]) if (c.GetComponentInChildren<Renderer>() != null) kids.Add(c);
            if (kids.Count == 0) break;
            parts = kids;
        }
        // packs whose groups ("Cars", "Lamps") should become one prefab per item
        if (PackDeep.TryGetValue(name, out var deep))
        {
            var split = new List<Transform>();
            foreach (var part in parts)
            {
                if (System.Array.IndexOf(deep, part.name) >= 0)
                { foreach (Transform c in part) if (c.GetComponentInChildren<Renderer>() != null) split.Add(c); }
                else split.Add(part);
            }
            parts = split;
        }
        int i = 0;
        foreach (var part in parts)
        {
            var copy = Object.Instantiate(part.gameObject);
            copy.transform.position = part.position; copy.transform.rotation = part.rotation;
            copy.transform.localScale = part.lossyScale;
            var b = Bounds(copy);
            if (b.size.magnitude * unit < 0.05f || b.size.magnitude * unit > 400f
                || (part.name.StartsWith("Sphere") && b.size.magnitude * unit > 5f)) { Object.DestroyImmediate(copy); continue; }   // specks / sky spheres
            SavePropPrefab(copy, $"{name}_{i++:00}_{Clean(part.name)}", 0f, unit);
        }
        Object.DestroyImmediate(inst);
    }

    // fix-ups for downloads that ship without (some) colour textures
    static void DecorMaterials(GameObject g, string name)
    {
        string dir = PROPS + name + "/Textures/";
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (name.StartsWith("Chandelier"))
                {
                    // no textures in the download: polished brass
                    m.SetTexture("_BaseMap", null);
                    m.SetColor("_BaseColor", new Color(0.83f, 0.66f, 0.34f));
                    m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 0.78f);
                }
                else if (name == "WallClock")
                {
                    // only normal + specular maps: cream face, the normal map carries the numerals
                    m.SetColor("_BaseColor", new Color(0.93f, 0.9f, 0.82f));
                    var n = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "WallClock_LP_Clock_Normal.png");
                    if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); m.SetFloat("_BumpScale", 1.6f); }
                    m.SetFloat("_Smoothness", 0.55f);
                }
                else if (name == "PaintingFrame")
                {
                    string part = (m.name + " " + r.name).ToLower();
                    string stem = part.Contains("image") || part.Contains("canvas") || part.Contains("picture") || part.Contains("plane") ? "Image" : "Frame";
                    var b = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + stem + "_BaseColor.png");
                    if (b != null) { m.SetTexture("_BaseMap", b); m.SetColor("_BaseColor", Color.white); }
                    var n = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + stem + "_Normal.png");
                    if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
                }
                else if (name.StartsWith("Painting")) m.SetFloat("_Smoothness", 0.25f);
                EditorUtility.SetDirty(m);
            }
    }

    // URP's additional-data components must go first, or Unity refuses to remove
    // the Light / Camera they depend on
    public static void StripLightsAndCameras(GameObject g)
    {
        foreach (var d in g.GetComponentsInChildren<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>(true)) Object.DestroyImmediate(d);
        foreach (var d in g.GetComponentsInChildren<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(true)) Object.DestroyImmediate(d);
        foreach (var l in g.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l);
        foreach (var c in g.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c);
    }

    static string Clean(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(c);
        return sb.Length > 24 ? sb.ToString(0, 24) : sb.ToString();
    }

    static void SavePropPrefab(GameObject model, string prefabName, float size, float unit = 1f)
    {
        var root = new GameObject(prefabName);
        model.transform.SetParent(root.transform, true);
        var b = Bounds(root);
        float scale = size > 0f ? size / Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y, b.size.z)) : unit;
        model.transform.localScale *= scale;
        model.transform.position *= scale;
        b = Bounds(root);
        model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
        b = Bounds(root);
        var col = root.AddComponent<BoxCollider>(); col.center = b.center; col.size = b.size;
        PrefabUtility.SaveAsPrefabAsset(root, PROPPREF + prefabName + ".prefab");
        Object.DestroyImmediate(root);
    }

    static void StripGroundPlanes(GameObject g)
    {
        var drop = new List<GameObject>();
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var b = r.bounds;
            float big = Mathf.Max(b.size.x, b.size.z);
            string n = (r.name + " " + (r.sharedMaterial != null ? r.sharedMaterial.name : "")).ToLower();
            bool flat = big > 0.0001f && b.size.y < big * 0.01f && big > Bounds(g).size.magnitude * 0.6f;
            if (flat || n.Contains("ground_ao") || n.Contains("shadowplane") || n.Contains("shadow_plane"))
            {
                if (r.transform.childCount > 0) { var mf = r.GetComponent<MeshFilter>(); Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf); }
                else drop.Add(r.gameObject);
            }
        }
        foreach (var d in drop) if (d != null) Object.DestroyImmediate(d);
    }

    // copy materials to editable assets and hook up textures by name
    static void FixMaterials(GameObject g, string name)
    {
        var texByName = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(PROPS + name, "*.*", SearchOption.AllDirectories))
        {
            string l = f.ToLower();
            if (l.EndsWith(".png") || l.EndsWith(".jpg") || l.EndsWith(".jpeg") || l.EndsWith(".tga"))
                texByName[Path.GetFileNameWithoutExtension(f).ToLower()] = f.Replace('\\', '/');
        }
        string matDir = PROPS + name + "/Mats";
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(PROPS.TrimEnd('/') + "/" + name, "Mats");
        var done = new Dictionary<Material, Material>();
        int idx = 0;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (!done.TryGetValue(m, out var copy))
                {
                    copy = new Material(m) { name = m.name };
                    if (copy.shader == null || !(copy.shader.name.Contains("Universal") || copy.shader.name.Contains("glTF")))   // glTFast shaders render in URP as they are
                    {
                        var c = m.HasProperty("_Color") ? m.color : Color.white; var t = m.mainTexture;
                        copy.shader = Lit(); copy.SetColor("_BaseColor", c); if (t != null) copy.SetTexture("_BaseMap", t);
                    }
                    if (copy.GetTexture("_BaseMap") == null) LinkByName(copy, texByName);
                    AssetDatabase.CreateAsset(copy, $"{matDir}/{name}_{idx++}.mat");
                    done[m] = copy;
                }
                mats[i] = copy;
            }
            r.sharedMaterials = mats;
        }
    }

    static void LinkByName(Material m, Dictionary<string, string> tex)
    {
        string key = m.name.ToLower().Replace(" (instance)", "").Replace("mat_", "").Replace("_mat", "").Replace("material", "").Trim('_', ' ', '.');
        string diff = null, norm = null; int best = -1;
        foreach (var kv in tex)
        {
            string n = kv.Key;
            bool isDiff = n.Contains("basecolor") || n.Contains("base_color") || n.Contains("albedo") || n.Contains("diffuse") || n.EndsWith("_d") || n.EndsWith("_col") || n.Contains("color");
            if (!isDiff) continue;
            int score = key.Length > 2 && n.Contains(key) ? 2 : (key.Length > 2 && key.Contains(n.Split('_')[0]) ? 1 : 0);
            if (score > best) { best = score; diff = kv.Value; }
        }
        if (diff == null) return;
        if (best == 0 && tex.Count > 8) return;   // a big pack with no name match: don't guess
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff)); m.SetColor("_BaseColor", Color.white);
        string stem = Path.GetFileNameWithoutExtension(diff).ToLower();
        foreach (var w in new[] { "basecolor", "base_color", "albedo", "diffuse", "color" })
            if (stem.Contains(w))
                foreach (var nw in new[] { "normal", "normalgl", "norm", "nrm" })
                    if (tex.TryGetValue(stem.Replace(w, nw), out var np)) norm = np;
        if (norm != null) { m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(norm)); m.EnableKeyword("_NORMALMAP"); }
    }
}
