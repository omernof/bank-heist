using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds two Animator controllers from whatever Mixamo clips are in the project:
//   Heist_Armed.controller    - robbers / police: rifle idle-walk-run, aim, fire,
//                               grenade toss, hit reaction, death
//   Heist_Civilian.controller - civilians / bystander: idle-walk-run, lie down
//                               on the floor, hit, death
// Drop new Mixamo FBX files (FBX for Unity, 30fps) into Assets/Mixamo/Combat and
// re-run Build All: files are matched by name ("Rifle Run", "Firing Rifle",
// "Toss Grenade", "Dying", "Laying"...). Anything missing falls back to the
// basic idle/walk, and gameplay code only uses parameters that exist.
public static partial class HeistBuilder
{
    const string COMBAT = MIX + "Combat/";

    static RuntimeAnimatorController ArmedController, CivilianController;

    static void BuildAnimator()
    {
        if (!AssetDatabase.IsValidFolder(ROOT + "Anim")) AssetDatabase.CreateFolder(ROOT.TrimEnd('/'), "Anim");
        ConfigureCombatImports();
        ConfigureCharacterImports();
        LinkCharacterTextures();
        var clips = AllClips();

        AnimationClip Find(params string[] keys)
        {
            foreach (var k in keys)
                foreach (var kv in clips)
                    if (kv.Key.Contains(k)) return kv.Value;
            return null;
        }

        var baseIdle = Find("breathing idle");
        var baseWalk = Find("walking animation", "walking");

        // measure how fast each locomotion clip really moves (from its feet)
        var gait = AssetDatabase.LoadAssetAtPath<GaitSettings>(ROOT + "Resources/Gait.asset");
        if (gait == null) { gait = ScriptableObject.CreateInstance<GaitSettings>(); AssetDatabase.CreateAsset(gait, ROOT + "Resources/Gait.asset"); }
        var aWalk = Find("rifle walk", "walking with rifle") ?? baseWalk;
        var aRun = Find("rifle run", "run with rifle");
        var cRun = Find("running", "fast run", "run");
        gait.armedWalk = GaitSpeed(aWalk, 1.4f);
        gait.armedRun = aRun != null ? GaitSpeed(aRun, 3.8f) : gait.armedWalk * 2.6f;
        gait.civWalk = GaitSpeed(baseWalk, 1.3f);
        gait.civRun = cRun != null ? GaitSpeed(cRun, 4.5f) : gait.civWalk * 3f;
        EditorUtility.SetDirty(gait);
        Debug.Log($"GAIT: armed walk {gait.armedWalk:F2} run {gait.armedRun:F2} | civ walk {gait.civWalk:F2} run {gait.civRun:F2} m/s");
        s_walkT = gait.armedWalk; s_runT = gait.armedRun;

        ArmedController = MakeController("Heist_Armed",
            idle: Find("rifle aiming idle", "rifle idle") ?? baseIdle,
            walk: Find("rifle walk", "walking with rifle") ?? baseWalk,
            run: Find("rifle run", "run with rifle"),
            aim: Find("rifle aiming idle", "aiming idle", "rifle idle"),
            fire: Find("firing rifle", "shooting"),
            thrw: Find("toss grenade", "throw"),
            hit: Find("hit reaction", "rifle hit"),
            death: Find("dying", "death"),
            down: null);

        s_walkT = gait.civWalk; s_runT = gait.civRun;
        CivilianController = MakeController("Heist_Civilian",
            idle: baseIdle,
            walk: baseWalk,
            run: Find("running", "fast run", "run"),
            aim: null, fire: null, thrw: null,
            hit: Find("hit reaction"),
            death: Find("dying", "death"),
            down: Find("laying", "lying", "prone"));
    }

    static float s_walkT = 1.4f, s_runT = 3.8f;

    // Plays the clip on a humanoid and tracks a foot: while planted it slides
    // backwards relative to the hips at exactly the body's ground speed.
    static float GaitSpeed(AnimationClip clip, float fallback)
    {
        if (clip == null) return fallback;
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(MIX + "Characters/Char2.fbx");
        if (src == null) return fallback;
        var g = (GameObject)Object.Instantiate(src);
        var anim = g.GetComponent<Animator>();
        float result = fallback;
        if (anim != null && anim.isHuman)
        {
            var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            const int N = 60;
            var z = new float[N]; var y = new float[N];
            for (int i = 0; i < N; i++)
            {
                clip.SampleAnimation(g, clip.length * i / N);
                Vector3 local = g.transform.InverseTransformPoint(foot.position) - g.transform.InverseTransformPoint(hips.position);
                z[i] = local.z; y[i] = g.transform.InverseTransformPoint(foot.position).y;
            }
            // stance = the frames where the foot is lowest; take its backward slide rate
            float minY = float.MaxValue; foreach (var v in y) minY = Mathf.Min(minY, v);
            float sum = 0f; int cnt = 0;
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                if (y[i] < minY + 0.03f && y[j] < minY + 0.03f)
                {
                    float dz = z[i] - z[j];
                    if (dz > 0f) { sum += dz; cnt++; }
                }
            }
            if (cnt > 2)
            {
                float v = sum / (cnt * clip.length / N);
                if (v > 0.4f && v < 9f) result = v;
            }
        }
        Object.DestroyImmediate(g);
        return result;
    }

    static Dictionary<string, AnimationClip> AllClips()
    {
        var d = new Dictionary<string, AnimationClip>();
        foreach (var dir in new[] { MIX, COMBAT })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.GetFiles(dir, "*.fbx"))
            {
                string key = Path.GetFileNameWithoutExtension(f).ToLower();
                var clip = AssetDatabase.LoadAllAssetsAtPath(f.Replace('\\', '/')).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip != null && !d.ContainsKey(key)) d[key] = clip;
            }
        }
        return d;
    }

    // new Mixamo files: humanoid rig, looping locomotion, root motion baked in place
    static void ConfigureCombatImports()
    {
        if (!Directory.Exists(COMBAT)) return;
        foreach (var f in Directory.GetFiles(COMBAT, "*.fbx"))
        {
            string path = f.Replace('\\', '/');
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null || mi.userData == "heist-configured") continue;
            string key = Path.GetFileNameWithoutExtension(path).ToLower();
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.SaveAndReimport();
            var defs = mi.defaultClipAnimations;
            bool loop = !(key.Contains("fir") || key.Contains("toss") || key.Contains("throw") || key.Contains("hit") ||
                          key.Contains("dying") || key.Contains("death") ||
                          key.Contains("laying"));   // lie down once and stay down (holds the last pose)
            foreach (var c in defs)
            {
                c.name = Path.GetFileNameWithoutExtension(path);
                c.loopTime = loop;
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                c.lockRootHeightY = true; c.keepOriginalPositionY = true;
                // walk / run cycles travel forward: keep that out of the pose (root motion is
                // ignored at runtime), or the body snaps back each loop
                bool travels = key.Contains("walk") || key.Contains("run") || key.Contains("idle");
                c.lockRootPositionXZ = !travels; c.keepOriginalPositionXZ = !travels;
            }
            mi.clipAnimations = defs;
            mi.userData = "heist-configured";
            mi.SaveAndReimport();
        }
    }

    // newly downloaded Mixamo characters: humanoid rig, no animation, materials
    // + textures extracted next to the others (URP Lit)
    static void ConfigureCharacterImports()
    {
        foreach (var f in Directory.GetFiles(MIX + "Characters", "*.fbx"))
        {
            string path = f.Replace('\\', '/');
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null || mi.animationType == ModelImporterAnimationType.Human && mi.userData == "heist-char") continue;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.materialLocation = ModelImporterMaterialLocation.External;
            mi.userData = "heist-char";
            mi.SaveAndReimport();
        }
    }

    // FBX materials extracted before their textures were imported end up blank:
    // hook each one up to "<name>.png" (+ its _Normal / _normal map) by name.
    static void LinkCharacterTextures()
    {
        var byName = new Dictionary<string, string>();
        foreach (var dir in Directory.GetDirectories(MIX + "Characters", "*.fbm"))
            foreach (var f in Directory.GetFiles(dir, "*.png"))
                byName[Path.GetFileNameWithoutExtension(f).ToLower()] = f.Replace('\\', '/');
        foreach (var f in Directory.GetFiles(MIX + "Characters/Materials", "*.mat"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(f.Replace('\\', '/'));
            if (m == null) continue;
            string key = m.name.ToLower();
            // imported "white emission" makes whole body parts glow: use the
            // character's own emissive map if it has one, otherwise switch it off
            if (m.IsKeywordEnabled("_EMISSION") && m.GetTexture("_EmissionMap") == null)
            {
                string ek = key.Replace("diffuse", "emissive");
                if (ek != key && byName.TryGetValue(ek, out var ePath))
                    m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ePath));
                else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
                EditorUtility.SetDirty(m);
            }
            if (m.GetTexture("_BaseMap") != null) continue;
            if (!byName.TryGetValue(key, out var texPath)) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            m.SetTexture("_BaseMap", tex); m.mainTexture = tex;
            m.SetColor("_BaseColor", Color.white);
            string nk = key.Replace("diffuse", "normal");
            if (nk != key && byName.TryGetValue(nk, out var nPath))
            {
                var ti = AssetImporter.GetAtPath(nPath) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nPath));
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetFloat("_Smoothness", 0.3f);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    static AnimatorController MakeController(string name, AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip aim, AnimationClip fire, AnimationClip thrw, AnimationClip hit, AnimationClip death, AnimationClip down)
    {
        string path = $"{ROOT}Anim/{name}.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = ctrl.layers[0].stateMachine;

        // locomotion blend tree: idle -> walk -> run
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        if (idle != null) tree.AddChild(idle, 0f);
        if (walk != null) tree.AddChild(walk, s_walkT);
        if (run != null) tree.AddChild(run, s_runT);
        sm.defaultState = loco;

        if (aim != null)
        {
            ctrl.AddParameter("Aim", AnimatorControllerParameterType.Bool);
            var aimState = sm.AddState("Aim");
            aimState.motion = aim;
            var t1 = loco.AddTransition(aimState); t1.AddCondition(AnimatorConditionMode.If, 0, "Aim"); t1.AddCondition(AnimatorConditionMode.Less, 0.6f, "Speed"); t1.duration = 0.12f; t1.hasExitTime = false;
            var t2 = aimState.AddTransition(loco); t2.AddCondition(AnimatorConditionMode.IfNot, 0, "Aim"); t2.duration = 0.15f; t2.hasExitTime = false;
            // moving while aiming blends back to locomotion
            var t3 = aimState.AddTransition(loco); t3.AddCondition(AnimatorConditionMode.Greater, 0.6f, "Speed"); t3.duration = 0.15f; t3.hasExitTime = false;
        }

        if (down != null)
        {
            ctrl.AddParameter("Down", AnimatorControllerParameterType.Bool);
            var s = sm.AddState("Down"); s.motion = down;
            var t = sm.AddAnyStateTransition(s); t.AddCondition(AnimatorConditionMode.If, 0, "Down"); t.duration = 0.3f; t.hasExitTime = false; t.canTransitionToSelf = false;
            var back = s.AddTransition(loco); back.AddCondition(AnimatorConditionMode.IfNot, 0, "Down"); back.duration = 0.3f; back.hasExitTime = false;
        }

        if (death != null)
        {
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var s = sm.AddState("Death"); s.motion = death;
            var t = sm.AddAnyStateTransition(s); t.AddCondition(AnimatorConditionMode.If, 0, "Dead"); t.duration = 0.1f; t.hasExitTime = false; t.canTransitionToSelf = false;
        }

        // upper-body actions on their own layer so the legs keep walking
        var mask = UpperBodyMask();
        if (fire != null || thrw != null || hit != null)
        {
            var layer = new AnimatorControllerLayer
            {
                name = "UpperBody", defaultWeight = 1f, avatarMask = mask,
                stateMachine = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy }
            };
            AssetDatabase.AddObjectToAsset(layer.stateMachine, ctrl);
            var empty = layer.stateMachine.AddState("Empty");
            layer.stateMachine.defaultState = empty;
            void Action(string param, AnimationClip clip, float dur)
            {
                if (clip == null) return;
                ctrl.AddParameter(param, AnimatorControllerParameterType.Trigger);
                var s = layer.stateMachine.AddState(param); s.motion = clip;
                var t = layer.stateMachine.AddAnyStateTransition(s); t.AddCondition(AnimatorConditionMode.If, 0, param); t.duration = 0.05f; t.hasExitTime = false;
                var back = s.AddTransition(empty); back.hasExitTime = true; back.exitTime = dur; back.duration = 0.15f;
            }
            Action("Fire", fire, 0.6f);
            Action("Throw", thrw, 0.85f);
            Action("Hit", hit, 0.7f);
            ctrl.AddLayer(layer);
        }

        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static AvatarMask UpperBodyMask()
    {
        string path = ROOT + "Anim/UpperBody.mask";
        var m = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (m != null) return m;
        m = new AvatarMask();
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
        {
            var part = (AvatarMaskBodyPart)i;
            bool on = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                      part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                      part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
            m.SetHumanoidBodyPartActive(part, on);
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
