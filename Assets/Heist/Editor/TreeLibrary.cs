using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Real street trees: Poly Haven's "Jacaranda Tree" (CC0) - the classic LA street
// tree - turned into Prefabs/Props/Jacaranda with proper bark / branch materials
// and alpha-clipped, double-sided leaves. Placed in iron-grated tree pits along
// the sidewalks by PropPlacement.
public static partial class HeistBuilder
{
    const string TREES = ROOT + "Trees/";

    [MenuItem("Tools/Bank Heist/Build Tree Prefabs")]
    public static void BuildTreePrefabs()
    {
        if (!AssetDatabase.IsValidFolder(ROOT + "Prefabs/Props")) AssetDatabase.CreateFolder(ROOT + "Prefabs", "Props");
        try { BuildJacaranda(); }
        catch (System.Exception e) { Debug.LogWarning("Tree failed: " + e); }
        AssetDatabase.SaveAssets();
        Debug.Log("TREES built");
    }

    static Material TreeMat(string dir, string part, bool leaves)
    {
        string tex = dir + "textures/jacaranda_tree_" + part;
        var m = new Material(Lit()) { name = "Jacaranda_" + part };
        var diff = AssetDatabase.LoadAssetAtPath<Texture2D>(leaves ? dir + "textures/leaves_rgba.png" : tex + "_diff_1k.png");
        if (diff != null) { m.SetTexture("_BaseMap", diff); m.mainTexture = diff; }
        var nor = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_nor_gl_1k.png");
        if (nor != null) { m.SetTexture("_BumpMap", nor); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", leaves ? 0.25f : 0.1f);
        if (leaves)
        {
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.45f); m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f); m.doubleSidedGI = true;      // leaves are single planes
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        AssetDatabase.DeleteAsset(dir + m.name + ".mat");
        AssetDatabase.CreateAsset(m, dir + m.name + ".mat");
        return m;
    }

    static void BuildJacaranda()
    {
        string dir = TREES + "Jacaranda/";
        string file = dir + "jacaranda_tree.fbx";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(file);
        if (src == null) { Debug.LogWarning("Tree missing: " + file); return; }

        // leaves need their alpha mask in the texture's alpha channel
        var rgba = AssetImporter.GetAtPath(dir + "textures/leaves_rgba.png") as TextureImporter;
        if (rgba != null && !rgba.alphaIsTransparency) { rgba.alphaIsTransparency = true; rgba.mipMapsPreserveCoverage = true; rgba.alphaTestReferenceValue = 0.45f; rgba.SaveAndReimport(); }

        var trunk = TreeMat(dir, "trunk", false);
        var branches = TreeMat(dir, "branches", false);
        var leaves = TreeMat(dir, "leaves", true);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var ms = r.sharedMaterials;
            for (int i = 0; i < ms.Length; i++)
            {
                string n = ((ms[i] != null ? ms[i].name : "") + " " + r.name).ToLower();
                ms[i] = n.Contains("lea") || n.Contains("flower") ? leaves : n.Contains("branch") ? branches : trunk;
            }
            r.sharedMaterials = ms;
        }
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var root = new GameObject("Jacaranda");
        model.transform.SetParent(root.transform, false);
        var b = Bounds(root);
        float s = b.size.y > 0.001f ? 8f / b.size.y : 1f;          // a mature street tree, ~8 m
        Debug.Log($"TREE raw size {b.size} -> scale {s}");
        model.transform.localScale *= s;
        b = Bounds(root);
        model.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
        // only the trunk blocks people / the camera / the NavMesh
        // the model is centred on its canopy, so find the trunk itself: the vertices in
        // the bottom 1.2 m, their middle and spread give the collider's place and size
        Vector3 sum = Vector3.zero; int cnt = 0; var low = new List<Vector3>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = root.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                if (w.y < 1.2f) { low.Add(w); sum += w; cnt++; }
            }
        Vector3 trunkAt = cnt > 0 ? sum / cnt : Vector3.zero; trunkAt.y = 0f;
        float rad = 0.25f;
        foreach (var w in low) rad = Mathf.Max(rad, new Vector2(w.x - trunkAt.x, w.z - trunkAt.z).magnitude);
        rad = Mathf.Min(rad + 0.05f, 0.6f);
        Debug.Log($"TREE trunk at {trunkAt} radius {rad:F2} from {cnt} verts");
        // put the trunk on the prefab's origin, so it stands in the middle of its tree pit
        model.transform.localPosition -= trunkAt;
        var cap = root.AddComponent<CapsuleCollider>(); cap.radius = rad; cap.height = 4f; cap.center = new Vector3(0f, 2f, 0f);
        // drop it when it's tiny on screen (it's a heavy, detailed mesh)
        var lod = root.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(0.035f, root.GetComponentsInChildren<Renderer>()) });
        PrefabUtility.SaveAsPrefabAsset(root, PROPPREF + "Jacaranda.prefab");
        Object.DestroyImmediate(root);
    }

    // a tree in a square pit with a cast-iron grate, flush with the pavement
    static void StreetTree(Transform parent, Vector3 p)
    {
        var granite = Tex("granite.png", Vector2.one, null, 0.3f);
        var soil = Col(new Color(0.16f, 0.12f, 0.09f), 0f, 0.05f);
        Box("TreePit", parent, p + Vector3.up * 0.005f, new Vector3(1.5f, 0.012f, 1.5f), soil, false);
        foreach (var e in new[] { new Vector3(0.78f, 0f, 0f), new Vector3(-0.78f, 0f, 0f) })
            Box("PitEdge", parent, p + e + Vector3.up * 0.02f, new Vector3(0.08f, 0.04f, 1.64f), granite, false);
        foreach (var e in new[] { new Vector3(0f, 0f, 0.78f), new Vector3(0f, 0f, -0.78f) })
            Box("PitEdge", parent, p + e + Vector3.up * 0.02f, new Vector3(1.5f, 0.04f, 0.08f), granite, false);
        for (int i = -3; i <= 3; i++)
            if (i != 0) Box("Grate", parent, p + new Vector3(i * 0.2f, 0.018f, 0f), new Vector3(0.05f, 0.012f, 1.44f), MetalBlack, false);
        var t = P("Jacaranda", parent, p, R(0f, 360f), R(0.85f, 1.1f));
        if (t != null) GameObjectUtility.SetStaticEditorFlags(t, 0);   // no static batching of a 300k-tri mesh
    }
}
