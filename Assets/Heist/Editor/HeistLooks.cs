using UnityEditor;
using UnityEngine;

// Character "looks": the five Mixamo characters dressed for their role.
//   Robber_*   - black ski mask with an eye slit, tactical vest, duffel bag
//   Cop_*      - navy police cap + vest printed POLICE (Cop_2 = SWAT helmet/vest)
//   Civilian_* - as they come
// Accessories are rigid pieces parented to the head / chest bones, so they
// move with the animation.
public static partial class HeistBuilder
{
    static readonly string[] CharFiles = { "Char1", "Char2", "Char3", "Char4", "Char5" };


    static void BuildLooks()
    {
        BuildAnimator();
        // real Mixamo outfits - no code-built accessories:
        //   civilians = the four everyday characters, robbers = Ch28 in the
        //   black hoodie (a crew in matching outfits), police = the SWAT models
        string[] civilians = { "Char1", "Char3", "Char4", "Char5" };
        for (int i = 0; i < civilians.Length; i++) MakeLook($"Civilian_{i}", civilians[i], Dress.None);
        // robbers: masked, helmeted tactical gear (Ch35 = all black, Ch15 = camo); the player is Robber_3
        string[] robbers = { "Ch15", "Ch35", "Ch15", "Ch35" };
        for (int i = 0; i < 4; i++) MakeLook($"Robber_{i}", robbers[i], Dress.None, armed: true);
        // police: blue SWAT
        for (int i = 0; i < 3; i++) MakeLook($"Cop_{i}", "Swat", Dress.None, armed: true);
        foreach (var stale in new[] { "Civilian_4" })
            AssetDatabase.DeleteAsset($"{ROOT}Prefabs/Looks/{stale}.prefab");
    }

    enum Dress { None, Robber, Police, Swat }

    static void MakeLook(string name, string charFile, Dress dress, bool armed = false)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{MIX}Characters/{charFile}.fbx");
        if (fbx == null) { Debug.LogWarning("Missing " + charFile); return; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        // standalone prefab (not an FBX variant): the controller then lives on the prefab
        // itself and can't be lost as a stale override when the FBX is reimported
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = name;
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        anim.runtimeAnimatorController = armed || dress != Dress.None ? ArmedController : CivilianController;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        Transform head = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
        Transform chest = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.UpperChest) : null;
        if (chest == null && anim.isHuman) chest = anim.GetBoneTransform(HumanBodyBones.Chest);
        Vector3 fwd = inst.transform.forward, up = inst.transform.up, right = inst.transform.right;

        if (head != null && chest != null)
        {
            Vector3 headC = head.position + up * 0.09f + fwd * 0.015f;
            Vector3 chestC = chest.position + up * 0.02f + fwd * 0.02f;
            switch (dress)
            {
                case Dress.Robber:
                {
                    var black = Col(new Color(0.05f, 0.05f, 0.06f), 0f, 0.15f);
                    Acc("SkiMask", head, PrimitiveType.Sphere, headC, new Vector3(0.215f, 0.26f, 0.235f), black, fwd, up);
                    Acc("EyeSlit", head, PrimitiveType.Cube, headC + up * 0.02f + fwd * 0.1f, new Vector3(0.13f, 0.035f, 0.04f),
                        Col(new Color(0.72f, 0.55f, 0.45f), 0f, 0.3f), fwd, up);
                    foreach (float s in new[] { -1f, 1f })
                        Acc("Eye", head, PrimitiveType.Sphere, headC + up * 0.02f + fwd * 0.118f + right * s * 0.032f, Vector3.one * 0.022f,
                            Col(new Color(0.08f, 0.06f, 0.05f), 0f, 0.8f), fwd, up);
                    break;
                }
                case Dress.Police:
                {
                    var navy = Col(new Color(0.07f, 0.1f, 0.2f), 0f, 0.25f);
                    Acc("CapCrown", head, PrimitiveType.Cylinder, headC + up * 0.1f, new Vector3(0.22f, 0.05f, 0.24f), navy, fwd, up);
                    Acc("CapTop", head, PrimitiveType.Cylinder, headC + up * 0.15f + fwd * 0.01f, new Vector3(0.25f, 0.015f, 0.27f), navy, fwd, up);
                    Acc("CapBrim", head, PrimitiveType.Cube, headC + up * 0.075f + fwd * 0.14f, new Vector3(0.18f, 0.015f, 0.09f), Col(Color.black, 0f, 0.7f), fwd, up);
                    Acc("Badge", head, PrimitiveType.Cube, headC + up * 0.12f + fwd * 0.12f, new Vector3(0.04f, 0.04f, 0.01f), Col(new Color(0.9f, 0.75f, 0.3f), 1f, 0.8f), fwd, up);
                    break;
                }
                case Dress.Swat:
                {
                    var olive = Col(new Color(0.1f, 0.11f, 0.1f), 0f, 0.3f);
                    Acc("Helmet", head, PrimitiveType.Sphere, headC + up * 0.045f, new Vector3(0.25f, 0.21f, 0.27f), olive, fwd, up);
                    Acc("Visor", head, PrimitiveType.Cube, headC + up * 0.02f + fwd * 0.12f, new Vector3(0.17f, 0.05f, 0.02f), Col(new Color(0.05f, 0.08f, 0.1f), 0.5f, 0.95f), fwd, up);
                    break;
                }
            }
        }

        string path = $"{ROOT}Prefabs/Looks/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(inst, path);
        Object.DestroyImmediate(inst);
    }

    static GameObject Acc(string n, Transform bone, PrimitiveType type, Vector3 worldPos, Vector3 worldSize, Material m, Vector3 fwd, Vector3 up)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = n;
        g.transform.SetPositionAndRotation(worldPos, Quaternion.LookRotation(fwd, up));
        g.transform.localScale = worldSize;
        g.transform.SetParent(bone, true);
        g.GetComponent<Renderer>().sharedMaterial = Persist(m);
        return g;
    }

    // prefabs can only reference materials that are assets on disk
    static int s_matCount;
    static Material Persist(Material m)
    {
        if (m == null || AssetDatabase.Contains(m)) return m;
        if (!AssetDatabase.IsValidFolder(ROOT + "Materials/Looks")) AssetDatabase.CreateFolder(ROOT + "Materials", "Looks");
        AssetDatabase.CreateAsset(m, $"{ROOT}Materials/Looks/LookMat_{s_matCount++}.mat");
        return m;
    }
}
