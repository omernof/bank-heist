using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Turns the downloaded Sketchfab vehicles (Assets/Heist/Vehicles/<Name>/) into
// clean, game-ready prefabs (Assets/Heist/Prefabs/Vehicles/<Name>.prefab):
//  * removes the baked "ground shadow" planes Sketchfab exports add,
//  * links textures the FBX didn't (diffuse _d / normal _n by name),
//  * scales each one to its real-world length and lays its long axis along +Z
//    (front = +Z), sitting on y = 0, with a box collider.
// Credits for every model are in Assets/Heist/Vehicles/CREDITS.txt.
public static partial class HeistBuilder
{
    const string VEH = ROOT + "Vehicles/";
    const string VEHPREF = ROOT + "Prefabs/Vehicles/";

    struct VSpec
    {
        public string name; public float length; public float yaw; public Color? tint; public bool keepPlanes;
        public string keepPrefix;   // keep only parts whose name starts with this (files with several versions inside)
        public string texMap;       // "Material=texture[|normal];..." for downloads whose textures were renamed
    }

    // yaw: extra turn so the front faces +Z (checked on the preview renders)
    static readonly VSpec[] Specs =
    {
        new VSpec { name = "Van",          length = 5.4f },
        // the getaway van: 2003 Chevrolet Express cargo (see CREDITS.txt)
        new VSpec { name = "ChevyVan",     length = 5.7f },
        new VSpec { name = "Truck",        length = 7.2f },
        new VSpec { name = "Armored",      length = 6.8f },
        new VSpec { name = "Pickup",       length = 5.5f },
        new VSpec { name = "Bus",          length = 12f },
        // CopCruiser ('80 sedan) removed on request - every patrol car is the modern E7
        new VSpec { name = "PoliceE7",     length = 5.1f },
        new VSpec { name = "Taxi",         length = 5.1f },
        new VSpec { name = "Ambulance",    length = 6.3f },
        new VSpec { name = "BMW",          length = 4.8f, tint = new Color(0.12f, 0.13f, 0.16f) },
        new VSpec { name = "Helicopter",   length = 13.7f },
        new VSpec { name = "NakedBike",    length = 2.1f },
        new VSpec { name = "Volvo",        length = 4.97f },
        // the police Harley: its .mtl was missing from the download and was rebuilt
        // from the .obj's material names (see EXTRA_5_SHERIFF.mtl)
        new VSpec { name = "HarleyPolice", length = 2.55f },
        // Sketchfab renamed the Honda's textures to 75_$albedo_N: matched to its parts by eye
        new VSpec { name = "Honda",        length = 2.05f, texMap =
            "Bike=75_$albedo|75_$normal_0;Exhaust=75_$albedo_1|75_$normal_1;Glass=75_$albedo_2;Grips=75_$albedo_3|75_$normal_3;" +
            "Mirrors=75_$albedo_4|75_$normal_4;Chain=75_$albedo_5|75_$normal_5;brake=75_$albedo_5|75_$normal_5;" +
            "Tyre=75_$albedo_6|75_$normal_6;Rim=75_$albedo_7|75_$normal_7" },
        // the file holds a detailed (HD) and a light (LD) copy: keep the LD one, it carries the colour texture
        new VSpec { name = "MotoDemo",     length = 2.1f, keepPrefix = "SZH2I2", texMap = "SZH2I2=SZh2I2Col2K|SZH2I2Nor2K" },
    };

    static readonly string[] NewVehicles = { "ChevyVan" };

    [MenuItem("Tools/Bank Heist/Build New Vehicle Prefabs")]
    public static void BuildNewVehiclePrefabs()
    {
        // the Harley's rebuilt .mtl only takes effect on a fresh import of the .obj
        AssetDatabase.ImportAsset(VEH + "HarleyPolice/EXTRA 5 SHERIFF.obj", ImportAssetOptions.ForceUpdate);
        foreach (var spec in Specs)
            if (System.Array.IndexOf(NewVehicles, spec.name) >= 0)
            {
                try { BuildVehicle(spec); }
                catch (System.Exception e) { Debug.LogWarning($"Vehicle {spec.name} failed: {e}"); }
            }
        AssetDatabase.SaveAssets();
        Debug.Log("NEW VEHICLES built");
    }

    public static GameObject VehiclePrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(VEHPREF + name + ".prefab");

    [MenuItem("Tools/Bank Heist/Build Vehicle Prefabs")]
    public static void BuildVehiclePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(ROOT + "Prefabs/Vehicles")) AssetDatabase.CreateFolder(ROOT + "Prefabs", "Vehicles");
        foreach (var spec in Specs) BuildVehicle(spec);
        AssetDatabase.SaveAssets();
    }

    static string ModelFile(string name)
    {
        string dir = VEH + name;
        if (!Directory.Exists(dir)) return null;
        foreach (var f in Directory.GetFiles(dir))
        {
            string l = f.ToLower();
            if (l.EndsWith(".fbx") || l.EndsWith(".obj")) return f.Replace('\\', '/');
        }
        return null;
    }

    static void BuildVehicle(VSpec spec)
    {
        string file = ModelFile(spec.name);
        var src = file != null ? AssetDatabase.LoadAssetAtPath<GameObject>(file) : null;
        if (src == null) { Debug.LogWarning("Vehicle missing: " + spec.name); return; }

        var root = new GameObject(spec.name);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
        model.transform.SetParent(root.transform, false);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // 0) downloads can carry their own scene lights / cameras: vehicles never keep them
        StripLightsAndCameras(model);

        // 1) drop Sketchfab's flat ground-shadow planes
        var drop = new List<GameObject>();
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null) continue;
            var b = r.bounds;
            bool flat = b.size.y < Mathf.Max(b.size.x, b.size.z) * 0.02f && Mathf.Max(b.size.x, b.size.z) > 0.0001f;
            string n = (r.name + " " + (r.sharedMaterial != null ? r.sharedMaterial.name : "")).ToLower();
            if (!spec.keepPlanes && (flat || n.Contains("ground") || n.Contains("shadow") || n.Contains("plane")))
            {
                // keep any child parts: just remove this renderer's mesh
                if (r.transform.childCount > 0) { var mf = r.GetComponent<MeshFilter>(); Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf); }
                else drop.Add(r.gameObject);
            }
        }
        foreach (var g in drop) if (g != null) Object.DestroyImmediate(g);
        if (!string.IsNullOrEmpty(spec.keepPrefix))
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                if (r != null && !r.name.StartsWith(spec.keepPrefix)) Object.DestroyImmediate(r.gameObject);

        // 2) materials: copy to editable assets, link missing textures, glossy paint
        var texByName = new Dictionary<string, string>();
        foreach (var f in Directory.GetFiles(VEH + spec.name, "*.*", SearchOption.AllDirectories))
        {
            string l = f.ToLower();
            if (l.EndsWith(".png") || l.EndsWith(".jpg") || l.EndsWith(".jpeg") || l.EndsWith(".tga"))
                texByName[Path.GetFileNameWithoutExtension(f).ToLower()] = f.Replace('\\', '/');
        }
        string matDir = VEH + spec.name + "/Mats";
        if (!AssetDatabase.IsValidFolder(matDir)) AssetDatabase.CreateFolder(VEH + spec.name, "Mats");
        var done = new Dictionary<Material, Material>();
        int idx = 0;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                if (!done.TryGetValue(m, out var copy))
                {
                    copy = new Material(m) { name = m.name };
                    if (copy.shader == null || copy.shader.name.Contains("Standard") || copy.shader.name.Contains("Legacy") || copy.shader.name.Contains("Error"))
                    {
                        var c = m.HasProperty("_Color") ? m.color : Color.white;
                        var t = m.mainTexture;
                        copy.shader = Lit();
                        copy.SetColor("_BaseColor", c); if (t != null) copy.SetTexture("_BaseMap", t);
                    }
                    LinkVehicleTextures(copy, texByName);
                    if (spec.texMap != null) ApplyTexMap(copy, m.name, spec.texMap, texByName);
                    if (spec.tint.HasValue && copy.GetTexture("_BaseMap") == null)
                    {
                        // untextured body panels take the paint colour
                        var bc = copy.GetColor("_BaseColor");
                        if (bc.grayscale > 0.6f) copy.SetColor("_BaseColor", spec.tint.Value);
                    }
                    if (copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", Mathf.Max(copy.GetFloat("_Smoothness"), 0.55f));
                    AssetDatabase.CreateAsset(copy, $"{matDir}/{spec.name}_{idx++}.mat");
                    done[m] = copy;
                }
                mats[i] = copy;
            }
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // 3) orient (long axis -> Z), scale to real length, sit on the ground
        var bounds = Bounds(root);
        if (bounds.size.x > bounds.size.z * 1.15f) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
        model.transform.localRotation = Quaternion.Euler(0f, spec.yaw, 0f) * model.transform.localRotation;
        bounds = Bounds(root);
        float len = Mathf.Max(bounds.size.z, 0.001f);
        model.transform.localScale *= spec.length / len;
        bounds = Bounds(root);
        model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        bounds = Bounds(root);

        var col = root.AddComponent<BoxCollider>();
        col.center = bounds.center; col.size = bounds.size;
        PrefabUtility.SaveAsPrefabAsset(root, VEHPREF + spec.name + ".prefab");
        Object.DestroyImmediate(root);
    }

    static void ApplyTexMap(Material m, string matName, string map, Dictionary<string, string> texByName)
    {
        foreach (var entry in map.Split(';'))
        {
            var kv = entry.Split('=');
            if (kv.Length != 2 || !string.Equals(kv[0].Trim(), matName.Trim(), System.StringComparison.OrdinalIgnoreCase)) continue;
            var parts = kv[1].Split('|');
            if (texByName.TryGetValue(parts[0].ToLower(), out var bp))
            { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(bp)); m.SetColor("_BaseColor", Color.white); }
            if (parts.Length > 1 && texByName.TryGetValue(parts[1].ToLower(), out var np))
            {
                var ti = AssetImporter.GetAtPath(np) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(np)); m.EnableKeyword("_NORMALMAP");
            }
        }
    }

    static void LinkVehicleTextures(Material m, Dictionary<string, string> texByName)
    {
        if (!m.HasProperty("_BaseMap") || m.GetTexture("_BaseMap") != null) return;
        string key = m.name.ToLower().Replace(" (instance)", "");
        string diff = null, norm = null;
        foreach (var kv in texByName)
        {
            string n = kv.Key;
            bool isDiff = n.EndsWith("_d") || n.Contains("diff") || n.Contains("basecolor") || n.Contains("albedo") || n.EndsWith("_dif") || n.Contains("texture");
            bool isNorm = n.EndsWith("_n") || n.Contains("normal");
            if (n.Contains("ground_ao") || n.Contains("shadow")) continue;
            bool match = n.Contains(key) || key.Contains(n.Split('_')[0]);
            if (isDiff && (match || diff == null)) diff = kv.Value;
            if (isNorm && (match || norm == null)) norm = kv.Value;
        }
        // only auto-link when the model has essentially one texture set
        if (diff != null && texByName.Count <= 6)
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diff));
            m.SetColor("_BaseColor", Color.white);
            if (norm != null)
            {
                var ti = AssetImporter.GetAtPath(norm) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(norm)); m.EnableKeyword("_NORMALMAP");
            }
        }
    }

    static Bounds Bounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.one);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
