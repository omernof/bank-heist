using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// ============================================================================
//  BANK HEIST - builds the whole game from code.
//  Menu: Tools > Bank Heist > Build All
//    Assets/Heist/Scenes/MainMenu.unity  - title + side selection over the city
//    Assets/Heist/Scenes/City.unity      - downtown street, the bank's facade,
//                                          police cordon, getaway car, safehouse
//    Assets/Heist/Scenes/Bank.unity      - lobby, teller counter, customer queue
//    Assets/Heist/Scenes/Vault.unity     - basement corridor + the vault
//  Character "looks" (robbers / police / civilians) become prefabs in
//  Assets/Heist/Prefabs/Looks and are spawned at runtime by the HeistDirector.
// ============================================================================
public static partial class HeistBuilder
{
    const string ROOT = "Assets/Heist/";
    const string SCENES = ROOT + "Scenes/";
    const string TEX = ROOT + "Textures/";
    const string MIX = "Assets/Mixamo/";
    const string CARS = ROOT + "Kenney/Cars/";

    // fills an area (x by z, in the parent's space) with copies of a downloaded
    // money model (MoneyStacks by default), stacked up to `height`; the copies are
    // laid out from the model's measured size. Returns the height of the top.
    public static float MoneyGrid(Transform parent, Vector3 at, Vector2 area, float height, string prefab = "MoneyStacks", float scale = 1f, float yaw = 0f)
    {
        var probe = P(prefab, null, Vector3.zero, 0f, scale);
        if (probe == null) return 0f;
        var rs = probe.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        Object.DestroyImmediate(probe);
        Vector3 size = b.size; float bottom = -b.min.y;
        int cols = Mathf.Max(1, Mathf.FloorToInt(area.x / (size.x + 0.02f)));
        int rows = Mathf.Max(1, Mathf.FloorToInt(area.y / (size.z + 0.02f)));
        int layers = Mathf.Max(1, Mathf.FloorToInt(height / size.y));
        var g = new GameObject(prefab + "Pile").transform;
        g.SetParent(parent, false);
        g.localPosition = at; g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        for (int l = 0; l < layers; l++)
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                {
                    float x = (c - (cols - 1) * 0.5f) * (size.x + 0.02f) - b.center.x;
                    float z = (r - (rows - 1) * 0.5f) * (size.z + 0.02f) - b.center.z;
                    // every other copy turned around so the pile doesn't look tiled
                    var m = P(prefab, g, new Vector3(x, bottom + l * size.y, z), (l + c + r) % 2 == 0 ? 0f : 180f, scale);
                    if ((l + c + r) % 2 == 1) m.transform.localPosition += new Vector3(2f * b.center.x, 0f, 2f * b.center.z);
                }
        return layers * size.y;
    }

    [MenuItem("Tools/Bank Heist/Rebuild Bank + Vault Only")]
    public static void BuildBankAndVault()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildBankScene();
        BuildVaultScene();
        Debug.Log("BANK + VAULT rebuilt");
    }

    [MenuItem("Tools/Bank Heist/Rebuild City + Menu Only")]
    public static void BuildCityAndMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildCityScene(menu: false);
        BuildCityScene(menu: true);
        Debug.Log("CITY + MENU rebuilt");
    }

    [MenuItem("Tools/Bank Heist/Build All")]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EnsureFolders();
        EnsureLayer("Minimap", 8);
        PlayerSettings.runInBackground = true;
        ConfigureImports();
        BuildLooks();

        BuildCityScene(menu: false);
        BuildBankScene();
        BuildVaultScene();
        BuildCityScene(menu: true);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SCENES + "MainMenu.unity", true),
            new EditorBuildSettingsScene(SCENES + "City.unity", true),
            new EditorBuildSettingsScene(SCENES + "Bank.unity", true),
            new EditorBuildSettingsScene(SCENES + "Vault.unity", true),
        };
        EditorSceneManager.OpenScene(SCENES + "MainMenu.unity");
        Debug.Log("BANK HEIST built. Open MainMenu.unity and press Play.");
    }

    static void EnsureFolders()
    {
        foreach (var f in new[] { "Scenes", "Prefabs", "Prefabs/Looks", "Materials" })
            if (!AssetDatabase.IsValidFolder(ROOT + f))
            {
                string parent = (ROOT + f).Substring(0, (ROOT + f).LastIndexOf('/'));
                AssetDatabase.CreateFolder(parent, f.Substring(f.LastIndexOf('/') + 1));
            }
    }

    // adds a named user layer (the minimap-only layer) if it's missing
    static void EnsureLayer(string name, int preferred)
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return;
        int slot = preferred;
        if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(slot).stringValue))
            for (slot = 8; slot < layers.arraySize; slot++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(slot).stringValue)) break;
        layers.GetArrayElementAtIndex(slot).stringValue = name;
        tm.ApplyModifiedProperties();
    }

    static int MinimapLayer => LayerMask.NameToLayer("Minimap");

    // textures repeat; car models get readable, glossy materials
    static void ConfigureImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TEX.TrimEnd('/') }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) continue;
            bool normal = p.EndsWith("_n.png");
            var wrap = p.Contains("/flag_") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;   // flags: no edge bleed
            if (ti.wrapMode != wrap || ti.anisoLevel < 8 || (normal && ti.textureType != TextureImporterType.NormalMap))
            {
                ti.wrapMode = wrap; ti.anisoLevel = 8;   // crisp at grazing angles (roads!)
                if (normal) ti.textureType = TextureImporterType.NormalMap;
                ti.SaveAndReimport();
            }
        }
    }

    // ------------------------------------------------------------ materials

    static readonly Dictionary<string, Material> s_mats = new Dictionary<string, Material>();

    static Shader Lit() => Shader.Find("Universal Render Pipeline/Lit");

    public static Material Col(Color c, float metal = 0f, float smooth = 0.3f)
    {
        string key = $"c{c}{metal}{smooth}";
        if (s_mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Lit()); m.SetColor("_BaseColor", c); m.color = c;
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        return s_mats[key] = m;
    }

    public static Material Emissive(Color c, Color emis)
    {
        string key = $"e{c}{emis}";
        if (s_mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Lit()); m.SetColor("_BaseColor", c); m.color = c;
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emis);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return s_mats[key] = m;
    }

    public static Material Tex(string file, Vector2 tiling, Color? tint = null, float smooth = 0.2f, float metal = 0f)
    {
        string key = $"t{file}{tiling}{tint}{smooth}{metal}";
        if (s_mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Lit());
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + file);
        if (t != null) { m.SetTexture("_BaseMap", t); m.mainTexture = t; }
        m.mainTextureScale = tiling;
        var c = tint ?? Color.white;
        m.SetColor("_BaseColor", c); m.color = c;
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        // matching "<name>_n.png" normal map gives surfaces real relief
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + file.Replace(".png", "_n.png"));
        if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        return s_mats[key] = m;
    }

    public static Material Glass => Transparent(new Color(0.55f, 0.7f, 0.8f, 0.28f));
    public static Material Transparent(Color c)
    {
        string key = $"g{c}";
        if (s_mats.TryGetValue(key, out var m) && m != null) return m;
        m = new Material(Lit());
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
        m.SetColor("_BaseColor", c); m.color = c; m.SetFloat("_Smoothness", 0.95f);
        return s_mats[key] = m;
    }

    // ------------------------------------------------------------ geometry

    public static GameObject Box(string n, Transform p, Vector3 pos, Vector3 size, Material m, bool collide = true, float yaw = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = n; g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localScale = size;
        g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collide) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    // a box whose texture is tiled per world metre (tpm = tiles per metre), like
    // a wall of bricks or a floor of slabs keeping their real-world size
    public static GameObject BoxT(string n, Transform p, Vector3 pos, Vector3 size, string tex, float tileW, float tileH, Color? tint = null, bool collide = true, float smooth = 0.2f)
    {
        float ax = Mathf.Abs(size.x), ay = Mathf.Abs(size.y), az = Mathf.Abs(size.z), a, b;
        if (ay <= ax && ay <= az) { a = ax; b = az; }
        else if (ax <= ay && ax <= az) { a = az; b = ay; }
        else { a = ax; b = ay; }
        var m = Tex(tex, new Vector2(Mathf.Max(0.25f, a / tileW), Mathf.Max(0.25f, b / tileH)), tint, smooth);
        return Box(n, p, pos, size, m, collide);
    }

    public static GameObject Cyl(string n, Transform p, Vector3 pos, Vector3 size, Material m, bool collide = true, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = n; g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localScale = size; g.transform.localRotation = Quaternion.Euler(euler);
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collide) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    public static Light PointLight(string n, Transform p, Vector3 pos, Color c, float intensity, float range, bool shadows = false)
    {
        var g = new GameObject(n); g.transform.SetParent(p, false); g.transform.localPosition = pos;
        var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        return l;
    }

    // flat coloured quad on the minimap layer (roads, blocks, rooms)
    public static void MapQuad(Transform p, Vector3 center, Vector2 size, Color c, float y = 0.05f, float yaw = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = "Map"; g.layer = MinimapLayer;
        g.transform.SetParent(p, false);
        g.transform.position = new Vector3(center.x, y, center.z);
        g.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        g.transform.localScale = new Vector3(size.x, size.y, 1f);
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", c); m.color = c;
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        g.GetComponent<Renderer>().receiveShadows = false;
    }

    public static void MapIcon(Transform p, Vector3 at, Color c, float size = 6f)
    {
        MapQuad(p, at, new Vector2(size, size), c, 5f, 45f);
    }

    public static T Spawn<T>(string id, Transform p, Vector3 pos, float yaw) where T : Component
    {
        var g = new GameObject(id); g.transform.SetParent(p, false);
        g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return g.AddComponent<T>();
    }

    public static SpawnPoint SpawnPt(string id, Transform p, Vector3 pos, float yaw)
    {
        var s = Spawn<SpawnPoint>("Spawn_" + id, p, pos, yaw); s.id = id; return s;
    }

    // GTA-style glowing marker for a doorway/objective
    public static ScenePortal Portal(Transform p, Vector3 pos, string scene, string spawn, string label, Phase min = Phase.Approach, string locked = "")
    {
        var sp = Spawn<ScenePortal>("Portal_" + scene, p, pos, 0f);
        sp.targetScene = scene; sp.targetSpawn = spawn; sp.label = label; sp.minPhase = min; sp.lockedMessage = locked;
        sp.range = 2.2f;
        // GTA-style marker: a slim translucent glowing cylinder + a small chevron
        var mk = Cyl("Marker", sp.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.35f, 0.05f, 0.35f),
                     Emissive(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.75f, 0.1f) * 1.6f), false);
        mk.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        var ring = Cyl("Corona", sp.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.45f, 1.1f),
                       Transparent(new Color(1f, 0.8f, 0.2f, 0.14f)), false);
        ring.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return sp;
    }

    // ------------------------------------------------------------ scene setup

    static Scene NewScene(string name)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, SCENES + name + ".unity");
        return scene;
    }

    static void Save(Scene s, string name)
    {
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s, SCENES + name + ".unity");
    }

    // golden-hour sun, sky, fog and a GTA-ish colour grade
    static void Atmosphere(bool interior, float sunIntensity = 1.6f)
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = interior ? new Color(1f, 0.95f, 0.88f) : new Color(1f, 0.86f, 0.68f);
        sun.intensity = interior ? 0.25f : sunIntensity;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
        sun.transform.rotation = Quaternion.Euler(26f, -38f, 0f);   // low golden-hour sun = long shadows
        sun.shadowBias = 0.03f; sun.shadowNormalBias = 0.3f;
        RenderSettings.sun = sun;
        ConfigurePipeline();

        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.SetFloat("_SunSize", 0.05f); sky.SetFloat("_AtmosphereThickness", 1.15f);
        sky.SetColor("_SkyTint", new Color(0.55f, 0.62f, 0.78f));
        sky.SetColor("_GroundColor", new Color(0.35f, 0.33f, 0.3f));
        sky.SetFloat("_Exposure", 1.25f);
        AssetDatabase.CreateAsset(sky, ROOT + "Materials/Sky_" + (interior ? "in" : "out") + ".mat");
        RenderSettings.skybox = sky;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = interior ? new Color(0.5f, 0.48f, 0.45f) : new Color(0.62f, 0.66f, 0.75f);
        RenderSettings.ambientEquatorColor = interior ? new Color(0.4f, 0.37f, 0.33f) : new Color(0.55f, 0.5f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
        RenderSettings.fog = !interior;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.78f, 0.72f, 0.66f);
        RenderSettings.fogStartDistance = 60f; RenderSettings.fogEndDistance = 380f;

        // post-processing
        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(interior ? 0.7f : 0.5f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.65f);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(interior ? 0.35f : 0.15f); ca.contrast.Override(14f); ca.saturation.Override(12f);
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(interior ? 6f : 10f);
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(0.26f); vg.smoothness.Override(0.45f);
        // GTA V-ish grade: warm highlights, cool teal shadows, a touch of grain
        var smh = prof.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.92f, 0.98f, 1.06f, -0.02f));
        smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.92f, 0.02f));
        var grain = prof.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.12f);
        var ca2 = prof.Add<ChromaticAberration>(true); ca2.intensity.Override(0.06f);
        AssetDatabase.CreateAsset(prof, ROOT + "Materials/PostFX_" + (interior ? "in" : "out") + ".asset");
        vol.sharedProfile = prof;
    }

    // URP quality: long, sharp sun shadows (4 cascades) and 4x MSAA
    static void ConfigurePipeline()
    {
        var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (rp == null) return;
        rp.shadowDistance = 140f;
        rp.shadowCascadeCount = 4;
        rp.msaaSampleCount = 4;
        rp.mainLightShadowmapResolution = 4096;
        EditorUtility.SetDirty(rp);
    }

    // a reflection probe so glossy floors, glass and car paint reflect the scene
    public static void Probe(Vector3 center, Vector3 size, bool boxProjection)
    {
        var g = new GameObject("ReflectionProbe");
        g.transform.position = center;
        var rp = g.AddComponent<ReflectionProbe>();
        rp.mode = ReflectionProbeMode.Realtime;
        rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        rp.size = size; rp.boxProjection = boxProjection;
        rp.resolution = 256; rp.intensity = 1f;
        rp.cullingMask = ~(1 << MinimapLayer);
    }

    static Camera MinimapCamera(float size)
    {
        var g = new GameObject("MinimapCamera");
        var c = g.AddComponent<Camera>();
        c.orthographic = true; c.orthographicSize = size;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.12f, 0.14f, 0.16f);
        c.cullingMask = 1 << MinimapLayer;
        c.depth = -10;
        // off in the saved scene so it doesn't fill the editor's Game view;
        // HeistDirector switches it on once it renders into the HUD's texture
        c.enabled = false;
        g.transform.position = new Vector3(0f, 60f, 0f);
        g.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        return c;
    }

    static HeistDirector Director(SceneKind kind)
    {
        var d = new GameObject("HeistDirector").AddComponent<HeistDirector>();
        d.kind = kind;
        d.font = AssetDatabase.LoadAssetAtPath<Font>(ROOT + "Fonts/BarlowCondensed-SemiBold.ttf");
        d.bigFont = AssetDatabase.LoadAssetAtPath<Font>(ROOT + "Fonts/BebasNeue-Regular.ttf");
        d.robberLooks = LoadLooks("Robber");
        d.copLooks = LoadLooks("Cop");
        d.civilianLooks = LoadLooks("Civilian");
        return d;
    }

    static GameObject[] LoadLooks(string prefix)
    {
        var list = new List<GameObject>();
        for (int i = 0; i < 8; i++)
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>($"{ROOT}Prefabs/Looks/{prefix}_{i}.prefab");
            if (g != null) list.Add(g);
        }
        return list.ToArray();
    }

    static void BakeNavMesh(GameObject root)
    {
        var surf = root.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
        surf.collectObjects = Unity.AI.Navigation.CollectObjects.All;
        surf.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
        surf.layerMask = ~((1 << MinimapLayer) | (1 << 2));   // skip minimap quads + the vault door
        // paint the carriageways as "Road" so pedestrians can be kept on the sidewalks
        if (root.name == "City")
        {
            void RoadVol(Vector3 c, Vector3 size)
            {
                var v = new GameObject("RoadArea").AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
                v.transform.SetParent(root.transform, false);
                v.center = c; v.size = size; v.area = Civilian.RoadArea;
            }
            RoadVol(Vector3.zero, new Vector3(CityX * 2f + 60f, 1f, AveHalf * 2f - 0.2f));
            foreach (float z in BackZ) RoadVol(new Vector3(0f, 0f, z), new Vector3(CityX * 2f + 60f, 1f, StreetHalf * 2f - 0.2f));
            foreach (float x in CrossX) RoadVol(new Vector3(x, 0f, 0f), new Vector3(StreetHalf * 2f - 0.2f, 1f, CityZ * 2f + 60f));
        }
        surf.BuildNavMesh();
    }
}
