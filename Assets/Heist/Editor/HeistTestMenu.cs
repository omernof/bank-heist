using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Quick test entry points: open the right scene and press Play as a given side
// (skips the main menu).
// Unity's own Play button always starts the game from the main menu, whatever
// scene happens to be open in the editor (the test menus below skip that).
[InitializeOnLoad]
public static class HeistTestMenu
{
    const string MenuScene = "Assets/Heist/Scenes/MainMenu.unity";

    static HeistTestMenu()
    {
        EditorApplication.delayCall += UseMenuAsStart;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) UseMenuAsStart(); };
    }

    static void UseMenuAsStart()
    {
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene);
        // a test run leaves its side / spawn behind: a normal Play starts a fresh game
        PlayerPrefs.DeleteKey("heist_test_side");
        PlayerPrefs.DeleteKey("heist_test_spawn");
    }

    [MenuItem("Tools/Bank Heist/Test: Robber (City)")]    static void Robber() => Go(Side.Robber, "City", "start");
    [MenuItem("Tools/Bank Heist/Test: Police (City)")]    static void Cop() => Go(Side.Cop, "City", "cops");
    [MenuItem("Tools/Bank Heist/Test: Bystander (Bank)")] static void Bystander() => Go(Side.Bystander, "Bank", "queue");
    [MenuItem("Tools/Bank Heist/Test: Robber (Bank)")]    static void RobberBank() => Go(Side.Robber, "Bank", "fromCity");
    [MenuItem("Tools/Bank Heist/Test: Robber (Vault)")]   static void RobberVault() => Go(Side.Robber, "Vault", "fromBank");

    [MenuItem("Tools/Bank Heist/Test: Main Menu")]
    static void Menu()
    {
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
        EditorSceneManager.OpenScene(MenuScene);
        EditorApplication.EnterPlaymode();
    }

    static void Go(Side side, string scene, string spawn)
    {
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
        PlayerSettings.runInBackground = true;   // keep the game running while the editor is unfocused
        PlayerPrefs.SetInt("heist_test_side", (int)side);
        PlayerPrefs.SetString("heist_test_spawn", spawn);
        PlayerPrefs.Save();
        EditorSceneManager.OpenScene("Assets/Heist/Scenes/" + scene + ".unity");
        EditorSceneManager.playModeStartScene = null;   // this run starts in the opened scene
        EditorApplication.EnterPlaymode();
    }
}

// One-off fix for scenes built before the minimap camera was saved disabled:
// with no target texture outside Play mode it drew the flat map over the Game view.
public static class HeistMinimapFix
{
    [MenuItem("Tools/Bank Heist/Fix: Hide Minimap Camera In Editor")]
    static void Fix()
    {
        if (EditorApplication.isPlaying) return;
        string current = EditorSceneManager.GetActiveScene().path;
        foreach (var name in new[] { "City", "Bank", "Vault" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Heist/Scenes/" + name + ".unity");
            var d = Object.FindAnyObjectByType<HeistDirector>();
            if (d != null && d.minimapCam != null && d.minimapCam.enabled)
            {
                d.minimapCam.enabled = false;
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("MINIMAP camera disabled in " + name);
            }
        }
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current);
    }
}

// Performance: the heaviest downloads (the Jacaranda street tree ~1M triangles,
// the Volvo ~2M, the chandeliers ~0.25M each) get Unity's automatic mesh LODs,
// so distant copies draw a fraction of the triangles.
public static class HeistOptimize
{
    static readonly string[] Heavy =
    {
        "Assets/Heist/Trees/Jacaranda", "Assets/Heist/Vehicles/Volvo", "Assets/Heist/Vehicles/BMW",
        "Assets/Heist/Vehicles/NakedBike", "Assets/Heist/Props/ChandelierGrand", "Assets/Heist/Props/Chandelier",
        "Assets/Heist/Props/OfficeDesk", "Assets/Heist/Props/OfficeChair",
    };

    [MenuItem("Tools/Bank Heist/Optimize: Generate Mesh LODs")]
    public static void Run()
    {
        foreach (var dir in Heavy)
        {
            if (!AssetDatabase.IsValidFolder(dir)) continue;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.generateMeshLods)
                {
                    mi.generateMeshLods = true;
                    mi.SaveAndReimport();
                    Debug.Log("MESHLOD on " + path);
                }
            }
        }
        Debug.Log("MESHLOD done");
    }
}

// The Mixamo rifle walk / run (and "Running") clips aren't "in place": the body
// travels forward through the clip. They were imported with that motion baked
// into the pose, so the character drifted ahead for a cycle and then snapped back
// when the clip looped (the "jump back" every second). Unbaking XZ turns it into
// root motion, which the Animator ignores (applyRootMotion = false): the body stays
// put and the CharacterController does the moving. The clips' true speed then
// becomes measurable, so the gait speeds / blend thresholds are matched to it.
public static class HeistLocomotionFix
{
    static readonly string[] Clips = { "Rifle Run", "Rifle Walk", "Running", "Rifle Idle" };

    [MenuItem("Tools/Bank Heist/Fix: Locomotion In Place")]
    public static void Run()
    {
        const string dir = "Assets/Mixamo/Combat/";
        foreach (var n in Clips)
        {
            var mi = AssetImporter.GetAtPath(dir + n + ".fbx") as ModelImporter;
            if (mi == null) continue;
            var clips = mi.clipAnimations.Length > 0 ? mi.clipAnimations : mi.defaultClipAnimations;
            foreach (var c in clips) { c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false; }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }
        float Speed(string n)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(dir + n + ".fbx"))
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                    return new Vector2(c.averageSpeed.x, c.averageSpeed.z).magnitude;
            return 0f;
        }
        float walk = Speed("Rifle Walk"), run = Speed("Rifle Run"), civRun = Speed("Running");
        Debug.Log($"LOCO clip speeds: rifle walk {walk:F2} rifle run {run:F2} running {civRun:F2} m/s");

        var gait = AssetDatabase.LoadAssetAtPath<GaitSettings>("Assets/Heist/Resources/Gait.asset");
        if (gait != null)
        {
            Debug.Log($"LOCO gait before: armed {gait.armedWalk:F2}/{gait.armedRun:F2} civ run {gait.civRun:F2}");
            if (walk > 0.4f && walk < 3f) gait.armedWalk = walk;
            if (run > 1.5f && run < 8f) gait.armedRun = run;
            if (civRun > 1.5f && civRun < 8f) gait.civRun = civRun;
            EditorUtility.SetDirty(gait);
        }
        // move the blend-tree thresholds to the same speeds (edited in place: the look
        // prefabs reference these controllers, so they must not be recreated)
        void Thresholds(string ctrlPath, float w, float r)
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
            if (ctrl == null) return;
            foreach (var st in ctrl.layers[0].stateMachine.states)
                if (st.state.motion is UnityEditor.Animations.BlendTree tree)
                {
                    var ch = tree.children;
                    for (int i = 0; i < ch.Length; i++)
                    {
                        string mn = ch[i].motion != null ? ch[i].motion.name.ToLower() : "";
                        if (mn.Contains("walk") && w > 0f) ch[i].threshold = w;
                        else if (mn.Contains("run") && r > 0f) ch[i].threshold = r;
                    }
                    tree.children = ch;
                    EditorUtility.SetDirty(tree);
                }
            EditorUtility.SetDirty(ctrl);
        }
        if (gait != null)
        {
            Thresholds("Assets/Heist/Anim/Heist_Armed.controller", gait.armedWalk, gait.armedRun);
            Thresholds("Assets/Heist/Anim/Heist_Civilian.controller", 0f, gait.civRun);
            Debug.Log($"LOCO gait after: armed {gait.armedWalk:F2}/{gait.armedRun:F2} civ run {gait.civRun:F2}");
        }
        AssetDatabase.SaveAssets();
        Debug.Log("LOCO done");
    }
}
