using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Downloaded Sketchfab weapons (Assets/Heist/Weapons/<Name>/) -> runtime-loadable
// prefabs in Resources/Weapons: barrel along +Z, origin at the pistol grip,
// scaled to real length. Weapon.cs's GunModel loads "Weapons/<Name>" at runtime
// and falls back to the box-built rifle when a prefab is missing.
public static partial class HeistBuilder
{
    const string WEAPONS = ROOT + "Weapons/";
    const string WEAPONPREF = ROOT + "Resources/Weapons/";

    struct WSpec { public string name, fbx; public float length; public bool flip, keepSet; public float gripBack; }
    // gripBack: how far (fraction of length) behind the model centre the pistol grip sits
    static readonly WSpec[] WeaponSpecs =
    {
        new WSpec { name = "SMG", fbx = "SMG/source/MPASST-30.fbx", length = 0.62f, flip = true, keepSet = true, gripBack = -0.18f },
    };

    [MenuItem("Tools/Bank Heist/Build Weapon Prefabs")]
    public static void BuildWeaponPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(ROOT + "Resources/Weapons")) AssetDatabase.CreateFolder(ROOT + "Resources", "Weapons");
        foreach (var s in WeaponSpecs)
        {
            try { BuildWeapon(s); }
            catch (System.Exception e) { Debug.LogWarning($"Weapon {s.name} failed: {e}"); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("WEAPONS built");
    }

    static Texture2D WTex(string dir, string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "textures/" + file + ".png");

    static Material WeaponMat(string dir, string baseName, string label)
    {
        var m = new Material(Lit()) { name = "SMG_" + label };
        var b = WTex(dir, baseName + "_Base");
        if (b != null) { m.SetTexture("_BaseMap", b); m.mainTexture = b; }
        var n = WTex(dir, baseName + "_Normal");
        if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
        var mt = WTex(dir, baseName + "_Metallic");
        if (mt != null) { m.SetTexture("_MetallicGlossMap", mt); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        m.SetFloat("_Smoothness", 0.55f);
        string path = WEAPONS + "SMG/" + m.name + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static void BuildWeapon(WSpec spec)
    {
        string file = WEAPONS + spec.fbx;
        string dir = file.Substring(0, file.IndexOf("source/"));
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(file);
        if (src == null) { Debug.LogWarning("Weapon missing: " + file); return; }

        var body = WeaponMat(dir, "SST30_Worn_GR", "Body");
        var hand = WeaponMat(dir, "SST30_Handguard_Worn_ODGreen", "Handguard");
        var stock = WeaponMat(dir, "SST30_Stock_Worn_Black", "Stock");

        var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var names = new System.Collections.Generic.List<string>();
        // the file holds several colour variants of the gun laid out together: keep only one set
        // (parts without a 0xx copy suffix) and drop the alternative MDCQB stock + loose handguard kit
        foreach (var r in model.GetComponentsInChildren<Renderer>(true).ToArray())
        {
            string rn = r.name.Trim();
            if (spec.keepSet && (System.Text.RegularExpressions.Regex.IsMatch(rn, @"0\d\d$") || rn.Contains("MDCQB")
                                 // the optional handguard kit sits beside the gun in the file, not on it
                                 || rn.Contains("Handguard") || rn.Contains("Barrel Extension")))
            { Object.DestroyImmediate(r.gameObject); }
        }
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                string n = ((ms[i] != null ? ms[i].name : "") + " " + r.name).ToLower();
                names.Add(r.name + "/" + (ms[i] != null ? ms[i].name : "null"));
                ms[i] = n.Contains("stock") ? stock : n.Contains("hand") || n.Contains("guard") || n.Contains("rail") ? hand : body;
            }
            r.sharedMaterials = ms;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        Debug.Log("WEAPON parts: " + string.Join(", ", names));

        var root = new GameObject(spec.name);
        model.transform.SetParent(root.transform, false);
        var b = Bounds(root);
        Debug.Log($"WEAPON {spec.name} raw size {b.size}");
        // longest horizontal axis -> Z (barrel), so rotate the model if it lies along X
        if (b.size.x > b.size.z) model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
        if (spec.flip) model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
        b = Bounds(root);
        float s = spec.length / Mathf.Max(0.0001f, b.size.z);
        model.transform.localScale *= s;
        b = Bounds(root);
        // origin at the grip: centred sideways, resting at the receiver's mid-height, a bit behind the middle
        model.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.center.z - spec.gripBack * b.size.z);
        PrefabUtility.SaveAsPrefabAsset(root, WEAPONPREF + spec.name + ".prefab");
        Object.DestroyImmediate(root);
        Debug.Log($"WEAPON {spec.name} built, length {spec.length}, scale {s}");
    }

    // side + top view of the weapon prefab for a sanity check
    [MenuItem("Tools/Bank Heist/Debug: Preview Weapons")]
    static void PreviewWeapons()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WEAPONPREF + "SMG.prefab");
        if (prefab == null) { Debug.Log("PREVIEW: no SMG prefab"); return; }
        var pu = new PreviewRenderUtility();
        pu.camera.backgroundColor = new Color(0.45f, 0.48f, 0.52f); pu.camera.clearFlags = CameraClearFlags.SolidColor;
        pu.lights[0].intensity = 1.3f; pu.lights[0].transform.rotation = Quaternion.Euler(40f, 30f, 0f);
        pu.ambientColor = new Color(0.5f, 0.5f, 0.5f);
        var go = pu.InstantiatePrefabInScene(prefab);
        var tex = new Texture2D(1600, 600, TextureFormat.RGB24, false);
        var views = new[] { (new Vector3(1.3f, 0.1f, 0f), "side"), (new Vector3(0.2f, 1.2f, 0.01f), "top") };
        for (int v = 0; v < 2; v++)
        {
            pu.camera.transform.position = views[v].Item1; pu.camera.transform.LookAt(Vector3.zero);
            pu.camera.nearClipPlane = 0.05f; pu.camera.farClipPlane = 10f; pu.camera.fieldOfView = 35f;
            pu.BeginPreview(new Rect(0, 0, 800, 600), GUIStyle.none);
            pu.camera.Render();
            var t = pu.EndPreview() as RenderTexture;
            RenderTexture.active = t;
            tex.ReadPixels(new Rect(0, 0, 800, 600), v * 800, 0);
        }
        RenderTexture.active = null;
        tex.Apply();
        File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "heist_weapon.png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        pu.Cleanup();
        Debug.Log("PREVIEW saved heist_weapon.png");
    }
}
