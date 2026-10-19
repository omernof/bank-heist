using UnityEditor;
using UnityEngine;

public static class HeistDebug
{
    [MenuItem("Tools/Bank Heist/Debug: Log Player Rig")]
    static void LogRig()
    {
        var p = PlayerController.Instance;
        if (p == null) { Debug.Log("RIG: no player"); return; }
        var anim = p.GetComponentInChildren<Animator>();
        var hand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        var rifle = p.transform.Find("Model") != null ? FindDeep(p.transform, "Rifle") : null;
        Debug.Log($"RIG: player {p.transform.position} hand {(hand ? hand.position.ToString() : "none")} handScale {(hand ? hand.lossyScale.ToString() : "-")} " +
                  $"rifle {(rifle ? rifle.position + " scale " + rifle.lossyScale + " fwd " + rifle.forward : "none")} " +
                  $"playerFwd {p.transform.forward} animCtrl {(anim ? anim.runtimeAnimatorController?.name : "-")} human {(anim ? anim.isHuman : false)}");
    }

    [MenuItem("Tools/Bank Heist/Debug: Check Looks")]
    static void CheckLooks()
    {
        foreach (var n in new[] { "Robber_0", "Civilian_0", "Cop_0" })
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Heist/Prefabs/Looks/" + n + ".prefab");
            var a = pf != null ? pf.GetComponent<Animator>() : null;
            var c = a != null ? a.runtimeAnimatorController : null;
            Debug.Log($"LOOK {n}: prefab {(pf != null)} anim {(a != null)} ctrl {(c != null ? c.name + " " + AssetDatabase.GetAssetPath(c) : "NULL")} avatar {(a != null && a.avatar != null ? a.avatar.name + " valid " + a.avatar.isValid : "none")} comps {(pf != null ? string.Join(",", System.Linq.Enumerable.Select(pf.GetComponents<Component>(), x => x == null ? "MISSING" : x.GetType().Name)) : "")}");
        }
        var ac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Heist/Anim/Heist_Armed.controller");
        Debug.Log("LOOK controller asset: " + (ac != null ? ac.name + " clips " + ac.animationClips.Length : "NULL"));
    }

    // renders the game camera to a PNG (works while the editor is unfocused)
    [MenuItem("Tools/Bank Heist/Debug: Capture Camera")]
    static void Capture()
    {
        var cam = Camera.main;
        if (cam == null) { Debug.Log("CAPTURE: no camera"); return; }
        var rt = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "heist_capture.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex); rt.Release();
        Debug.Log("CAPTURE: " + path + " t=" + Time.time.ToString("F1"));
    }

    [MenuItem("Tools/Bank Heist/Debug: Capture Street Views")]
    static void StreetViews()
    {
        var cam = Camera.main; if (cam == null) return;
        var shots = new[] {
            (new Vector3(-30f, 1.9f, -7f), new Vector3(-12f, 6f, 16f)),
            (new Vector3(30f, 1.9f, 7f), new Vector3(48f, 7f, -15f)),
            (new Vector3(-8f, 1.8f, -9f), new Vector3(0f, 7f, 14f)),
            (new Vector3(-45f, 2.2f, 0f), new Vector3(20f, 4f, 0f)),
            (new Vector3(1.5f, 1.8f, 7.5f), new Vector3(0f, 3.2f, 17f)),
            (new Vector3(6f, 2.6f, 9.5f), new Vector3(-1f, 0.6f, 14f)),
            (new Vector3(-15f, 0.9f, 9.2f), new Vector3(-17.5f, 0.25f, 11f)),
            (new Vector3(4.6f, 1.25f, 10.9f), new Vector3(5.6f, 0.75f, 12.4f)),
            (new Vector3(-9.5f, 10.5f, 7.5f), new Vector3(-11.8f, 11.6f, 12f)),
            (new Vector3(108f, 2.2f, -66f), new Vector3(112f, 2.5f, -88f)),       // 9: old store by the safehouse
            (new Vector3(0f, 30f, -60f), new Vector3(0f, 35f, -280f)),           // 10: skyline
            (new Vector3(0f, 2f, -14f), new Vector3(28f, 0.5f, -8.8f)) };          // 11: cordon end
        int i = 0;
        foreach (var (pos, look) in shots)
        {
            cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = 60f;
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"heist_view{i++}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release();
        }
        Debug.Log("VIEWS saved " + i);
    }

    // renders the bank lobby from a few spots in edit mode (no play needed)
    [MenuItem("Tools/Bank Heist/Debug: Capture Bank Views")]
    static void BankViews()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Heist/Scenes/Bank.unity");
        var cam = new GameObject("TempCam").AddComponent<Camera>();
        cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
        if (data != null) data.renderPostProcessing = true;
        var shots = new[] {
            (new Vector3(0f, 2.2f, 1.5f), new Vector3(0f, 1.5f, 20f)),
            (new Vector3(6f, 1.7f, 6.5f), new Vector3(12f, 0.6f, 2f)),
            (new Vector3(6.5f, 1.7f, 11f), new Vector3(12f, 0.8f, 11f)),
            (new Vector3(-9f, 1.6f, 4f), new Vector3(-14f, 1f, 3f)),
            (new Vector3(8f, 1.8f, 20f), new Vector3(-6f, 0.8f, 25.5f)),
            (new Vector3(0f, 7.2f, 1.5f), new Vector3(0f, 0f, 17f)),
            (new Vector3(11f, 2.3f, 30f), new Vector3(10.5f, 1.2f, 20f)),    // 6: from the vault stairs (where the glare was)
            (new Vector3(6f, 3.2f, 7f), new Vector3(8f, 3.4f, 0f)),          // 7: south wall art
            (new Vector3(-8f, 3.2f, 22.5f), new Vector3(-14f, 3.4f, 22.5f)) };  // 8: west wall art
        int i = 0;
        foreach (var (pos, look) in shots)
        {
            cam.transform.position = pos; cam.transform.LookAt(look); cam.fieldOfView = 62f;   // bank views
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply(); RenderTexture.active = null;
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"heist_bank{i++}.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release();
        }
        Object.DestroyImmediate(cam.gameObject);
        Debug.Log("BANKVIEWS saved " + i);
    }

    // puts the play-mode camera somewhere specific for a look around
    [MenuItem("Tools/Bank Heist/Debug: Orbit Camera 45")]
    static void Orbit()
    {
        var p = PlayerController.Instance;
        if (p != null) p.Yaw += 45f;
    }

    static void Teleport(Vector3 pos, Vector3 lookAt)
    {
        var p = PlayerController.Instance;
        if (p == null) return;
        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = pos;
        Vector3 d = lookAt - pos; d.y = 0f;
        if (d.sqrMagnitude > 0.01f) { p.transform.rotation = Quaternion.LookRotation(d); p.Yaw = p.transform.eulerAngles.y; }
        if (cc != null) cc.enabled = true;
    }

    [MenuItem("Tools/Bank Heist/Debug: Go To Vault Door")]
    static void ToVault()
    {
        var v = Object.FindAnyObjectByType<VaultDoor>();
        if (v != null) Teleport(v.transform.position + Vector3.back * 0.8f, v.transform.position + Vector3.forward * 3f);
    }

    [MenuItem("Tools/Bank Heist/Debug: Go To Nearest Loot")]
    static void ToLoot()
    {
        var p = PlayerController.Instance; if (p == null) return;
        Loot best = null; float bd = float.MaxValue;
        foreach (var l in Object.FindObjectsByType<Loot>())
        { float d = Vector3.Distance(l.transform.position, p.transform.position); if (d < bd) { bd = d; best = l; } }
        if (best != null) Teleport(best.transform.position - Vector3.forward * 1.2f - Vector3.up * best.transform.position.y, best.transform.position);
    }

    [MenuItem("Tools/Bank Heist/Debug: Use Nearest (E)")]
    static void UseNearest()
    {
        var p = PlayerController.Instance; if (p == null) return;
        Interactable best = null; float bd = 6f;
        foreach (var i in Interactable.All)
        {
            if (i == null || !i.CanUse) continue;
            float d = Vector3.Distance(i.transform.position, p.transform.position);
            if (d < bd) { bd = d; best = i; }
        }
        Debug.Log("USE: " + (best != null ? best.name + " -> " + best.Prompt : "nothing in range"));
        if (best != null) best.Use(p);
    }

    [MenuItem("Tools/Bank Heist/Debug: Go To Getaway Van")]
    static void ToVan()
    {
        var v = Object.FindAnyObjectByType<Vehicle>();
        if (v != null) Teleport(v.transform.position - v.transform.right * 2.2f, v.transform.position);
    }

    [MenuItem("Tools/Bank Heist/Debug: Drive To Safehouse")]
    static void DriveToSafehouse()
    {
        var v = Vehicle.Current;
        var d = HeistDirector.Instance;
        if (v == null || d == null || d.safehouse == null) { Debug.Log("DRIVE: not in a car / no safehouse"); return; }
        var rb = v.GetComponent<Rigidbody>();
        rb.position = d.safehouse.position + Vector3.up * 1f; rb.linearVelocity = Vector3.zero;
        v.transform.position = rb.position;
    }

    [MenuItem("Tools/Bank Heist/Debug: Kill All Enemies")]
    static void KillEnemies()
    {
        var enemy = GameData.EnemyOf(GameData.PlayerTeam);
        foreach (var h in Health.All.ToArray())
            if (h != null && !h.Dead && h.team == enemy) h.TakeDamage(9999f, null, Vector3.forward);
    }

    [MenuItem("Tools/Bank Heist/Debug: Go Up To Bank")]   static void ToBank() => HeistDirector.Instance?.GoTo("Bank", "fromVault");
    [MenuItem("Tools/Bank Heist/Debug: Kill Player")]
    static void KillPlayer()
    {
        var p = PlayerController.Instance;
        if (p != null) p.GetComponent<Health>().TakeDamage(9999f, null, Vector3.forward);
    }
    [MenuItem("Tools/Bank Heist/Debug: Go Down To Vault")] static void ToVaultScene() => HeistDirector.Instance?.GoTo("Vault", "fromBank");
    [MenuItem("Tools/Bank Heist/Debug: Go Out To City")]  static void ToCity() => HeistDirector.Instance?.GoTo("City", "fromBank");

    [MenuItem("Tools/Bank Heist/Debug: Log State")]
    static void LogState()
    {
        int alive(Team t) { int n = 0; foreach (var h in Health.All) if (h != null && !h.Dead && h.team == t) n++; return n; }
        Debug.Log($"STATE: side {GameData.side} phase {GameData.phase} money {GameData.money} hp {GameData.hp:F0} armor {GameData.armor:F0} wanted {GameData.wanted} " +
                  $"robbersHere {alive(Team.Robbers)} policeHere {alive(Team.Police)} civHere {alive(Team.Civilians)} vaultOpen {VaultDoor.IsOpen} inCar {Vehicle.Current != null} scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
    }

    // press a main-menu button through its real onClick (tests the menu wiring)
    [MenuItem("Tools/Bank Heist/Debug: Menu Press ROBBER")]    static void MenuRobber() => PressMenu("ROBBER");
    [MenuItem("Tools/Bank Heist/Debug: Menu Press POLICE")]    static void MenuCop() => PressMenu("POLICE");
    [MenuItem("Tools/Bank Heist/Debug: Menu Press BYSTANDER")] static void MenuBystander() => PressMenu("BYSTANDER");
    static void PressMenu(string n)
    {
        var g = GameObject.Find(n);
        var b = g != null ? g.GetComponent<UnityEngine.UI.Button>() : null;
        Debug.Log(b != null ? "MENU press " + n : "MENU button not found: " + n);
        if (b != null) b.onClick.Invoke();
    }

    // dumps the glTFast importer's report for every .glb/.gltf under Assets/Heist
    [MenuItem("Tools/Bank Heist/Debug: Log glTF Import Reports")]
    static void GltfReports()
    {
        foreach (var path in System.IO.Directory.GetFiles("Assets/Heist", "*.gl*", System.IO.SearchOption.AllDirectories))
        {
            if (!(path.EndsWith(".glb") || path.EndsWith(".gltf"))) continue;
            var imp = AssetImporter.GetAtPath(path);
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            var sb = new System.Text.StringBuilder($"GLTFREPORT {path} importer {(imp != null ? imp.GetType().Name : "-")} main {(main != null ? main.GetType().Name : "null")}");
            if (imp != null)
                foreach (var f in imp.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public))
                    if (f.FieldType.IsArray && f.Name.ToLower().Contains("report"))
                        if (f.GetValue(imp) is System.Array arr)
                            foreach (var it in arr)
                            {
                                var t = it.GetType();
                                var msg = t.GetMethod("ToString")?.Invoke(it, null);
                                var code = t.GetField("code")?.GetValue(it); var type = t.GetField("type")?.GetValue(it);
                                var mm = t.GetField("messages")?.GetValue(it) as string[];
                                sb.Append($"\n   {type} {code} {(mm != null ? string.Join(",", mm) : msg)}");
                            }
            Debug.Log(sb.ToString());
        }
    }

    [MenuItem("Tools/Bank Heist/Debug: Log Pack Hierarchy")]
    static void PackHierarchy()
    {
        foreach (var f in new[] { "Props/JapaneseTraffic/AssetSet01.fbx", "Props/DeskFlags/scene.gltf", "Props/ParkingLot/ParkingLotUF.fbx", "Props/CityNight/model.dae", "Props/OldStore/model.dae" })
        {
            var g = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Heist/" + f);
            if (g == null) { Debug.Log("HIER missing " + f); continue; }
            var sb = new System.Text.StringBuilder("HIER " + f);
            void Walk(Transform t, int d)
            {
                if (d > 3) return;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) return;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                sb.Append($"\n{new string(' ', d * 2)}{t.name} kids {t.childCount} rends {rs.Length} size {b.size:F1}");
                int n = 0; foreach (Transform c in t) { if (n++ > 14) { sb.Append($"\n{new string(' ', d * 2 + 2)}..."); break; } Walk(c, d + 1); }
            }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(g);
            Walk(inst.transform, 0);
            Object.DestroyImmediate(inst);
            Debug.Log(sb.ToString());
        }
    }

    [MenuItem("Tools/Bank Heist/Debug: Log Lights")]
    static void LogLights()
    {
        foreach (var l in Object.FindObjectsByType<Light>())
            if (l.enabled && l.gameObject.activeInHierarchy)
                Debug.Log($"LIGHT {l.name} type {l.type} pos {l.transform.position} int {l.intensity:F1} range {l.range:F1} shadows {l.shadows} parent {(l.transform.parent ? l.transform.parent.name : "-")}");
        var cam = Camera.main;
        if (cam != null) Debug.Log($"LIGHT camera at {cam.transform.position}");
    }

    [MenuItem("Tools/Bank Heist/Debug: Measure FPS")]
    static void MeasureFps()
    {
        if (!Application.isPlaying) return;
        new GameObject("FpsProbe").AddComponent<FpsProbe>();
    }

    [MenuItem("Tools/Bank Heist/Debug: Heaviest Models")]
    static void Heaviest()
    {
        var tris = new System.Collections.Generic.Dictionary<string, long>();
        var count = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>())
        {
            if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
            string key = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(mf.gameObject);
            if (string.IsNullOrEmpty(key)) key = "(scene) " + mf.transform.root.name;
            long t = 0; for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) t += mf.sharedMesh.GetIndexCount(i) / 3;
            tris[key] = (tris.TryGetValue(key, out var v) ? v : 0) + t;
        }
        foreach (var sk in Object.FindObjectsByType<SkinnedMeshRenderer>())
        {
            if (sk.sharedMesh == null) continue;
            string key = "(skinned) " + sk.transform.root.name;
            tris[key] = (tris.TryGetValue(key, out var v) ? v : 0) + sk.sharedMesh.triangles.Length / 3;
        }
        var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, long>>(tris);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        var sb = new System.Text.StringBuilder("HEAVY:");
        for (int i = 0; i < Mathf.Min(30, list.Count); i++) sb.Append($"\n  {list[i].Value / 1000}k  {list[i].Key}");
        Debug.Log(sb.ToString());
    }

    [MenuItem("Tools/Bank Heist/Debug: Trace Player Movement")]
    static void TraceMove()
    {
        if (!Application.isPlaying) return;
        new GameObject("MoveTrace").AddComponent<MoveTrace>();
    }

    [MenuItem("Tools/Bank Heist/Debug: Log Audio")]
    static void LogAudio()
    {
        if (!Application.isPlaying) return;
        var cam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (var a in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude))
            if (a.isPlaying && a.clip != null)
            {
                string k = $"{a.clip.name} vol {a.volume:F2} {(a.spatialBlend > 0.5f ? $"3D d={Vector3.Distance(cam, a.transform.position):F0}m" : "2D")}";
                counts[k] = counts.TryGetValue(k, out var c) ? c + 1 : 1;
            }
        foreach (var kv in counts) Debug.Log($"AUDIO {kv.Key} x{kv.Value}");
        foreach (var pl in Object.FindObjectsByType<PoliceLights>(FindObjectsInactive.Include))
        {
            var a = pl.GetComponent<AudioSource>();
            Debug.Log($"AUDIO police {pl.name} active {pl.isActiveAndEnabled} siren {pl.siren} src {(a != null ? $"{a.isPlaying} clip {a.clip?.name} vol {a.volume}" : "none")}");
        }
        foreach (var h in Object.FindObjectsByType<PoliceHelicopter>(FindObjectsInactive.Include))
        {
            var a = h.GetComponent<AudioSource>();
            Debug.Log($"AUDIO heli active {h.isActiveAndEnabled} src {(a != null ? $"{a.isPlaying} vol {a.volume}" : "none")}");
        }
    }

    [MenuItem("Tools/Bank Heist/Debug: Log Civilians")]
    static void LogCivilians()
    {
        foreach (var c in Civilian.All)
        {
            var a = c.GetComponentInChildren<Animator>();
            if (a == null) { Debug.Log($"CIV {c.name} no animator"); continue; }
            var st = a.GetCurrentAnimatorStateInfo(0);
            var clips = a.GetCurrentAnimatorClipInfo(0);
            Debug.Log($"CIV {c.name} mode {c.mode} speed {a.speed:F2} down {(HeistAnimInfo.HasParam(a, "Down") && a.GetBool("Down"))} isDownState {st.IsName("Down")} t {st.normalizedTime:F2} len {st.length:F2} trans {a.IsInTransition(0)} " +
                      $"clip {(clips.Length > 0 ? clips[0].clip.name : "-")} layers {a.layerCount} rootRot {c.transform.eulerAngles} hips {(a.isHuman ? a.GetBoneTransform(HumanBodyBones.Hips).position.y.ToString("F2") : "-")}");
        }
    }

    [MenuItem("Tools/Bank Heist/Debug: Log Squads")]
    static void LogSquads()
    {
        foreach (var ai in Object.FindObjectsByType<SoldierAI>())
        {
            var h = ai.Health; var t = ai.Target;
            var ag = ai.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Debug.Log($"SQUAD {h.displayName} team {ai.team} hp {h.hp:F0} dead {h.Dead} order {ai.Squad.order} leader {(ai.Squad.leader != null ? ai.Squad.leader.name : "-")} " +
                      $"pos {ai.transform.position} onMesh {(ag != null && ag.enabled && ag.isOnNavMesh)} target {(t != null ? t.displayName : "-")} " +
                      $"los {(t != null && ai.HasLineOfSight(t))} weapon {(ai.GetComponentInChildren<Weapon>() != null)}");
        }
        var p = PlayerController.Instance;
        if (p != null) Debug.Log($"SQUAD player pos {p.transform.position} hp {GameData.hp:F0}");
    }

    static Transform FindDeep(Transform t, string n)
    {
        if (t.name == n) return t;
        foreach (Transform c in t) { var r = FindDeep(c, n); if (r != null) return r; }
        return null;
    }
}

// Renders every Mixamo character FBX (and every Look prefab) in a row to a PNG
// without entering play mode, to check how they look.
public static class HeistPreview
{
    [MenuItem("Tools/Bank Heist/Debug: Preview Characters")]
    static void Preview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var items = new System.Collections.Generic.List<GameObject>();
        foreach (var f in System.IO.Directory.GetFiles("Assets/Mixamo/Characters", "*.fbx"))
            items.Add(AssetDatabase.LoadAssetAtPath<GameObject>(f));
        float x = 0f; var names = new System.Text.StringBuilder("PREVIEW order: ");
        foreach (var src in items)
        {
            if (src == null) continue;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
            g.transform.position = new Vector3(x, 0f, 0f);
            g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            names.Append(src.name + " | ");
            x += 1.1f;
        }
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f;
        sun.transform.rotation = Quaternion.Euler(30f, 160f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.transform.position = new Vector3((x - 1.1f) * 0.5f, 1.1f, -4.2f - x * 0.35f);
        cam.transform.LookAt(new Vector3((x - 1.1f) * 0.5f, 0.95f, 0f));
        cam.backgroundColor = new Color(0.35f, 0.37f, 0.4f); cam.clearFlags = CameraClearFlags.SolidColor;
        var rt = new RenderTexture(1600, 700, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 700, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 700), 0, 0); tex.Apply();
        RenderTexture.active = null; cam.targetTexture = null;
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "heist_chars.png");
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Debug.Log(names + " -> " + path);
    }
}

public static class VehiclePreview
{
    // renders one vehicle prefab from two angles to /tmp-ish PNG (checking a new download)
    [MenuItem("Tools/Bank Heist/Debug: Preview Getaway Model")]
    static void PreviewOne()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Heist/Prefabs/Vehicles/ChevyVan.prefab");
        if (src == null) { Debug.Log("PREVIEW: no prefab"); return; }
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
        int tris = 0;
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
        var b = g.GetComponent<BoxCollider>().bounds;
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
        var cam = new GameObject("Cam").AddComponent<Camera>(); cam.backgroundColor = new Color(0.5f, 0.55f, 0.6f); cam.clearFlags = CameraClearFlags.SolidColor; cam.fieldOfView = 35f;
        var rt = new RenderTexture(900, 560, 24);
        var sheet = new Texture2D(1800, 560, TextureFormat.RGB24, false);
        Vector3[] views = { new Vector3(1f, 0.45f, 1f), new Vector3(-1f, 0.45f, -1f) };
        for (int i = 0; i < 2; i++)
        {
            cam.transform.position = b.center + views[i].normalized * b.size.magnitude * 1.5f;
            cam.transform.LookAt(b.center);
            cam.targetTexture = rt; cam.Render();
            RenderTexture.active = rt;
            var tmp = new Texture2D(900, 560, TextureFormat.RGB24, false); tmp.ReadPixels(new Rect(0, 0, 900, 560), 0, 0); tmp.Apply();
            sheet.SetPixels(i * 900, 0, 900, 560, tmp.GetPixels());
        }
        RenderTexture.active = null; cam.targetTexture = null;
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "heist_getaway.png");
        System.IO.File.WriteAllBytes(path, sheet.EncodeToPNG());
        Debug.Log($"PREVIEW saved {path} tris {tris} size {b.size}");
    }

    [MenuItem("Tools/Bank Heist/Debug: Preview Vehicles")]
    static void Preview()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var log = new System.Text.StringBuilder("VEHICLES:\n");
        float x = 0f;
        bool usePrefabs = System.IO.Directory.Exists("Assets/Heist/Prefabs/Vehicles");
        var dirs = usePrefabs ? System.IO.Directory.GetFiles("Assets/Heist/Prefabs/Vehicles", "*.prefab") : System.IO.Directory.GetDirectories("Assets/Heist/Vehicles");
        foreach (var dir in dirs)
        {
            string file = usePrefabs ? dir : null;
            if (!usePrefabs)
            foreach (var f in System.IO.Directory.GetFiles(dir))
                if (f.ToLower().EndsWith(".fbx") || f.ToLower().EndsWith(".obj")) { file = f; break; }
            if (file == null) continue;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
            if (src == null) { log.AppendLine(dir + ": failed to load"); continue; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src);
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { log.AppendLine(dir + ": no renderers"); continue; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            int tris = 0;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3;
            foreach (var sm in g.GetComponentsInChildren<SkinnedMeshRenderer>()) if (sm.sharedMesh != null) tris += sm.sharedMesh.triangles.Length / 3;
            int noTex = 0, mats = 0;
            foreach (var r in rs) foreach (var m in r.sharedMaterials) { mats++; if (m != null && m.mainTexture == null) noTex++; }
            log.AppendLine($"{System.IO.Path.GetFileName(dir)}: size {b.size} center {b.center - g.transform.position} tris {tris} mats {mats} noTex {noTex}");
            // normalise to ~4.5 m long for the picture
            float scale = 4.5f / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            g.transform.localScale *= scale;
            b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            g.transform.position += new Vector3(x - b.center.x, -b.min.y, -b.center.z);
            x += 5.5f;
        }
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.backgroundColor = new Color(0.4f, 0.42f, 0.45f); cam.clearFlags = CameraClearFlags.SolidColor;
        // one framed tile per vehicle, 4 per row
        var roots = new System.Collections.Generic.List<GameObject>();
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.GetComponentInChildren<Renderer>() != null && go.GetComponent<Camera>() == null) roots.Add(go);
        int cols = 4, tw = 400, th = 300, rowsN = (roots.Count + cols - 1) / cols;
        var atlas = new Texture2D(cols * tw, rowsN * th, TextureFormat.RGB24, false);
        var rt = new RenderTexture(tw, th, 24);
        for (int k = 0; k < roots.Count; k++)
        {
            var rs2 = roots[k].GetComponentsInChildren<Renderer>();
            var bb = rs2[0].bounds; foreach (var r in rs2) bb.Encapsulate(r.bounds);
            float rad = bb.extents.magnitude;
            // front-right 3/4 view: a vehicle whose front faces +Z shows its nose
            cam.transform.position = bb.center + new Vector3(0.8f, 0.55f, 1f).normalized * rad * 2.3f;
            cam.transform.LookAt(bb.center); cam.fieldOfView = 40f;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt;
            atlas.ReadPixels(new Rect(0, 0, tw, th), (k % cols) * tw, (rowsN - 1 - k / cols) * th);
            RenderTexture.active = null;
        }
        atlas.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "heist_vehicles.png"), atlas.EncodeToPNG());
        Debug.Log(log.ToString());
    }
}

public static class PropPreview
{
    [MenuItem("Tools/Bank Heist/Debug: Preview Props")]
    static void Preview() => Preview(null, "heist_props.png");

    [MenuItem("Tools/Bank Heist/Debug: Preview Bank Interior Props")]
    static void PreviewBank() => Preview(new[] { "ParkingLot", "JapaneseTraffic", "OldStore", "CityNight_00", "CityNight_05", "CityNight_12", "LootBag", "DeskFlags", "OfficeWorker", "Briefcase", "OfficeDesk",
        "OfficePack_05", "OfficePack_07", "OfficePack_08", "OfficePack_11", "OfficePack_100", "OfficePack_103", "OfficePack_110", "OfficePack_111", "OfficePack_112", "OfficePack_136" }, "heist_bankprops.png");

    static void Preview(string[] only, string outName)
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
        var files = System.IO.Directory.GetFiles("Assets/Heist/Prefabs/Props", "*.prefab");
        if (only != null) files = System.Array.FindAll(files, f => System.Array.Exists(only, o => System.IO.Path.GetFileName(f).StartsWith(o)));
        System.Array.Sort(files);
        var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.3f; sun.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
        RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.65f);
        var cam = new GameObject("Cam").AddComponent<Camera>(); cam.backgroundColor = new Color(0.4f, 0.42f, 0.45f); cam.clearFlags = CameraClearFlags.SolidColor;
        int cols = 6, tw = 320, th = 240, rows = (files.Length + cols - 1) / cols;
        var atlas = new Texture2D(cols * tw, rows * th, TextureFormat.RGB24, false);
        var rt = new RenderTexture(tw, th, 24);
        var log = new System.Text.StringBuilder("PROPLIST:\n");
        for (int k = 0; k < files.Length; k++)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(files[k]));
            g.transform.position = new Vector3(k * 50f, 0f, 0f);
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) { Object.DestroyImmediate(g); continue; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            int tris = 0; foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh) tris += mf.sharedMesh.triangles.Length / 3;
            log.AppendLine($"{k}: {System.IO.Path.GetFileNameWithoutExtension(files[k])} size {b.size} tris {tris}");
            cam.transform.position = b.center + new Vector3(0.7f, 0.5f, -1f).normalized * b.extents.magnitude * 2.4f;
            cam.transform.LookAt(b.center); cam.fieldOfView = 40f;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            RenderTexture.active = rt; atlas.ReadPixels(new Rect(0, 0, tw, th), (k % cols) * tw, (rows - 1 - k / cols) * th); RenderTexture.active = null;
            Object.DestroyImmediate(g);
        }
        atlas.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetTempPath(), outName), atlas.EncodeToPNG());
        Debug.Log(log.ToString());
    }
}

// play-mode helper: average frame time over 4 seconds + what is being drawn
public class FpsProbe : MonoBehaviour
{
    float _t; int _n; float _worst; int _steps0;
    void Update()
    {
        if (_n == 0) _steps0 = Footsteps.Count;
        _t += Time.unscaledDeltaTime; _n++; _worst = Mathf.Max(_worst, Time.unscaledDeltaTime);
        if (_t < 4f) return;
        int rends = 0, tris = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>()) if (r.enabled && r.gameObject.activeInHierarchy) rends++;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>()) if (mf.sharedMesh != null && mf.gameObject.activeInHierarchy) tris += (int)mf.sharedMesh.GetIndexCount(0) / 3;
        int lights = 0, shadowLights = 0;
        foreach (var l in Object.FindObjectsByType<Light>()) if (l.enabled && l.gameObject.activeInHierarchy) { lights++; if (l.shadows != LightShadows.None) shadowLights++; }
        Debug.Log($"FPS {(_n / _t):F1} worst {(1f / _worst):F1} scene {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} renderers {rends} tris~{tris / 1000}k lights {lights} shadowLights {shadowLights} footsteps {Footsteps.Count - _steps0} in 4s");
        Destroy(gameObject);
    }
}

// play-mode helper: per-frame player motion for 6 s (to diagnose jerky walking)
public class MoveTrace : MonoBehaviour
{
    Unity.Profiling.ProfilerRecorder _main, _gpu, _render, _gc;
    void OnEnable()
    {
        _main = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "CPU Main Thread Frame Time");
        _render = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "CPU Render Thread Frame Time");
        _gpu = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Internal, "GPU Frame Time");
        _gc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
    }
    void OnDisable() { _main.Dispose(); _gpu.Dispose(); _render.Dispose(); _gc.Dispose(); }
    float _t; Vector3 _last; Vector3 _lastCam; readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder("TRACE dt | step | y | camStep | animSpeed | grounded");
    void LateUpdate()
    {
        var p = PlayerController.Instance; if (p == null) return;
        var a = p.GetComponentInChildren<Animator>(); var cc = p.GetComponent<CharacterController>();
        Vector3 pos = p.transform.position; Vector3 cam = Camera.main.transform.position;
        if (_t > 0f)
            _sb.Append($"\n{Time.deltaTime * 1000f:F0}ms | {(pos - _last).magnitude:F3} | {pos.y:F3} | {(cam - _lastCam).magnitude:F3} | {(a != null ? a.GetFloat("Speed") : 0f):F2} | {cc.isGrounded} | main {_main.LastValue / 1e6:F1} render {_render.LastValue / 1e6:F1} gpu {_gpu.LastValue / 1e6:F1} gcKB {_gc.LastValue / 1024} hipsZ {(a != null && a.isHuman ? p.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Hips).position).z : 0f):F3}");
        _last = pos; _lastCam = cam; _t += Time.deltaTime;
        if (_t > 6f) { Debug.Log(_sb.ToString()); Destroy(gameObject); }
    }
}
