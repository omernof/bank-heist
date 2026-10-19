using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Downloaded animals (Assets/Heist/Animals/<Name>/) -> prefabs in
// Prefabs/Animals with a generic-rig Animator whose states are named by role
// (Idle, Peck, Coo, Walk, TakeOff, Fly, Land) so Pigeon.cs can drive any model.
// Then the city gets flocks on the sidewalks / bank steps and a few birds
// circling high over the street.
public static partial class HeistBuilder
{
    const string ANIMALS = ROOT + "Animals/";
    const string ANIMALPREF = ROOT + "Prefabs/Animals/";
    static readonly string[] PigeonModels = { "PigeonA", "PigeonB" };

    // role -> clip short names, best first (short name = text after the last '_' or '|')
    static readonly (string role, string[] names, bool loop)[] PigeonRoles =
    {
        ("Idle",    new[] { "idleloop", "idle", "look" }, true),
        ("Peck",    new[] { "peck", "bite" }, true),
        ("Coo",     new[] { "cooing", "look" }, true),
        ("Walk",    new[] { "walk" }, true),
        ("TakeOff", new[] { "takeoff", "jfly" }, false),
        ("Fly",     new[] { "flyloop", "fly" }, true),
        ("Land",    new[] { "land" }, false),
    };

    public static GameObject AnimalPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(ANIMALPREF + name + ".prefab");

    [MenuItem("Tools/Bank Heist/Build Animal Prefabs")]
    public static void BuildAnimalPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(ROOT + "Prefabs/Animals")) AssetDatabase.CreateFolder(ROOT + "Prefabs", "Animals");
        foreach (var n in PigeonModels)
        {
            try { BuildPigeon(n); }
            catch (System.Exception e) { Debug.LogWarning($"Animal {n} failed: {e}"); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("ANIMALS built");
    }

    static string ShortClip(string n)
    {
        n = n.ToLower();
        int i = Mathf.Max(n.LastIndexOf('|'), n.LastIndexOf('_'));
        return i >= 0 ? n.Substring(i + 1) : n;
    }

    static void BuildPigeon(string name)
    {
        string dir = ANIMALS + name;
        string file = Directory.GetFiles(dir).FirstOrDefault(f => f.ToLower().EndsWith(".fbx"))?.Replace('\\', '/');
        if (file == null) { Debug.LogWarning("Animal missing: " + name); return; }

        // generic rig, loop the cycles, no root motion
        var imp = (ModelImporter)AssetImporter.GetAtPath(file);
        imp.animationType = ModelImporterAnimationType.Generic;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.importAnimation = true;
        var clips = imp.defaultClipAnimations;
        foreach (var c in clips)
        {
            string sn = ShortClip(c.takeName);
            c.name = c.takeName;
            c.loopTime = PigeonRoles.Any(r => r.loop && r.names.Contains(sn)) && sn != "takeoff" && sn != "land" && sn != "jfly";
            c.lockRootPositionXZ = true; c.lockRootHeightY = true; c.lockRootRotation = true;
            c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true; c.keepOriginalOrientation = true;
        }
        imp.clipAnimations = clips;
        imp.SaveAndReimport();

        var all = AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        var ctrlPath = ANIMALPREF + name + ".controller";
        AssetDatabase.DeleteAsset(ctrlPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
        var sm = ctrl.layers[0].stateMachine;
        var found = new List<string>();
        foreach (var (role, names, _) in PigeonRoles)
        {
            AnimationClip clip = null;
            foreach (var n in names) { clip = all.FirstOrDefault(c => ShortClip(c.name) == n); if (clip != null) break; }
            if (clip == null) continue;
            var st = sm.AddState(role); st.motion = clip;
            if (role == "Idle") sm.defaultState = st;
            found.Add(role + "=" + clip.name);
        }
        Debug.Log($"ANIMAL {name}: {string.Join(", ", found)}");

        // material from the diffuse texture in the folder
        var texFile = Directory.GetFiles(dir).FirstOrDefault(f => { var l = f.ToLower(); return (l.EndsWith(".png") || l.EndsWith(".jpg")) && !l.Contains("normal"); });
        var mat = new Material(Lit()) { name = name };
        if (texFile != null)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texFile.Replace('\\', '/'));
            mat.SetTexture("_BaseMap", t); mat.mainTexture = t;
        }
        mat.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.DeleteAsset(ANIMALPREF + name + ".mat");
        AssetDatabase.CreateAsset(mat, ANIMALPREF + name + ".mat");

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(file);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials; for (int i = 0; i < ms.Length; i++) ms[i] = mat; r.sharedMaterials = ms;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
        }
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var root = new GameObject(name);
        model.transform.SetParent(root.transform, false);
        model.name = "Model";
        // a feral pigeon stands ~25 cm tall (bind pose may have its wings out, so go by height)
        var b = Bounds(root);
        float s = b.size.y > 0.0001f ? 0.25f / b.size.y : 1f;
        Debug.Log($"ANIMAL {name} raw size {b.size} -> scale {s}");
        model.transform.localScale *= s;
        b = Bounds(root);
        model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

        var anim = model.GetComponent<Animator>(); if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        root.AddComponent<Pigeon>();
        PrefabUtility.SaveAsPrefabAsset(root, ANIMALPREF + name + ".prefab");
        Object.DestroyImmediate(root);
    }

    // ---- placement ----

    static bool HasPigeons => AnimalPrefab("PigeonA") != null;

    static void BuildPigeons(Transform city)
    {
        if (!HasPigeons) return;
        var root = new GameObject("Pigeons").transform; root.SetParent(city, false);
        var kinds = PigeonModels.Select(AnimalPrefab).Where(p => p != null).ToArray();
        float sw = AveHalf + Walk * 0.5f;      // middle of the avenue sidewalks
        var flocks = new (Vector3 pos, int n, float r)[]
        {
            (new Vector3(-17f, Curb, sw + 0.3f), 8, 2.2f),       // in front of the bank
            (new Vector3(17.5f, Curb, sw), 6, 1.8f),
            (new Vector3(5.5f, Curb, 12.3f), 4, 1.2f),           // on the bank steps
            (new Vector3(-38f, Curb, -sw), 7, 2f),
            (new Vector3(44f, Curb, sw), 6, 2f),
            (new Vector3(88f, Curb, -sw), 7, 2.2f),
            (new Vector3(-92f, Curb, sw), 6, 2f),
            (new Vector3(-118f, Curb, -sw), 5, 1.8f),
        };
        int k = 0;
        foreach (var (pos, n, r) in flocks)
        {
            var f = new GameObject("PigeonFlock").AddComponent<PigeonFlock>();
            f.transform.SetParent(root, false); f.transform.position = pos; f.radius = r;
            for (int i = 0; i < n; i++)
            {
                var p = (GameObject)PrefabUtility.InstantiatePrefab(kinds[(k++ % 5 == 4 && kinds.Length > 1) ? 1 : 0]);
                p.transform.SetParent(f.transform, false);
                Vector2 o = Random.insideUnitCircle * r;
                p.transform.position = GroundAt(pos + new Vector3(o.x, 0f, o.y));
                p.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                p.transform.localScale = Vector3.one * R(0.92f, 1.08f);
            }
        }
        // a few birds circling high over the avenue
        for (int i = 0; i < 7; i++)
        {
            var p = (GameObject)PrefabUtility.InstantiatePrefab(kinds[0]);
            p.transform.SetParent(root, false);
            var b = p.GetComponent<Pigeon>();
            b.state = Pigeon.State.Soar;
            b.soarCenter = new Vector3(R(-60f, 60f), 0f, R(-6f, 6f));
            b.soarRadius = R(18f, 40f); b.soarHeight = R(26f, 42f);
            b.soarSpeed = R(0.15f, 0.25f) * (i % 2 == 0 ? 1f : -1f);
            p.transform.position = b.soarCenter + Vector3.up * b.soarHeight;
        }
    }

    static Vector3 GroundAt(Vector3 p)
    {
        Physics.SyncTransforms();
        return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
    }
}
