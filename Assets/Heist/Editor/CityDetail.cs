using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Realistic building fronts for the downtown blocks: instead of a painted
// window texture, every window is real geometry (reflective glass, frame,
// mullion, stone sill and lintel; some lit from inside, some with curtains),
// with floor cornices, a crowning cornice, NYC-style fire escapes on the brick
// walk-ups, glass-railed balconies on the stucco ones, and proper shop fronts
// (granite plinth, glass over a lit interior, door, sign band, striped awning).
// All the little boxes are merged into one mesh per material per building.
public static partial class HeistBuilder
{
    // ---- mesh batching ----------------------------------------------------
    class Batch
    {
        static Mesh s_cube;
        readonly Dictionary<Material, List<CombineInstance>> _parts = new Dictionary<Material, List<CombineInstance>>();
        public static Mesh Cube
        {
            get
            {
                if (s_cube == null)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    s_cube = g.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(g);
                }
                return s_cube;
            }
        }
        static Mesh s_cyl;
        public static Mesh Cylinder
        {
            get
            {
                if (s_cyl == null)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    s_cyl = g.GetComponent<MeshFilter>().sharedMesh;
                    Object.DestroyImmediate(g);
                }
                return s_cyl;
            }
        }
        public void Add(Material m, Matrix4x4 trs) => Add(m, trs, Cube);
        public void Add(Material m, Matrix4x4 trs, Mesh mesh)
        {
            if (!_parts.TryGetValue(m, out var list)) _parts[m] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = trs });
        }
        public void Flush(Transform root, string name, bool shadows = true)
        {
            foreach (var kv in _parts)
            {
                var mesh = new Mesh { name = name + "_" + kv.Key.name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                var g = new GameObject(name); g.transform.SetParent(root, false);
                g.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = kv.Key;
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            }
            _parts.Clear();
        }
    }

    // a face of a building: origin on the wall surface, x along the wall,
    // y up, z out of the wall
    struct Face
    {
        public Vector3 origin; public Quaternion rot; public float length;
        public Matrix4x4 M(Vector3 local, Vector3 size, Quaternion extra) =>
            Matrix4x4.TRS(origin + rot * local, rot * extra, size);
        public Matrix4x4 M(Vector3 local, Vector3 size) => M(local, size, Quaternion.identity);
    }

    static Face MakeFace(Vector2 size, Vector3 outward)
    {
        float half = Mathf.Abs(outward.x) > 0.5f ? size.x * 0.5f : size.y * 0.5f;
        float len = Mathf.Abs(outward.x) > 0.5f ? size.y : size.x;
        return new Face { origin = outward * half, rot = Quaternion.LookRotation(outward, Vector3.up), length = len };
    }

    // ---- materials ----------------------------------------------------------
    static Material s_glassDark, s_glassLit, s_curtainM, s_frameWhite, s_frameDark, s_frameBrown, s_stone, s_metalBlack, s_balconyGlass, s_shopGlass;
    static Material GlassDark => s_glassDark != null ? s_glassDark : (s_glassDark = Col(new Color(0.07f, 0.09f, 0.11f), 0.85f, 0.96f));
    static Material GlassLit
    {
        get
        {
            if (s_glassLit != null) return s_glassLit;
            s_glassLit = new Material(Lit());
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + "window_lit.png");
            s_glassLit.SetTexture("_BaseMap", t); s_glassLit.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.42f));
            s_glassLit.EnableKeyword("_EMISSION"); s_glassLit.SetTexture("_EmissionMap", t);
            s_glassLit.SetColor("_EmissionColor", new Color(0.75f, 0.62f, 0.42f));
            s_glassLit.SetFloat("_Smoothness", 0.92f);
            s_glassLit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return s_glassLit;
        }
    }
    static Material CurtainM => s_curtainM != null ? s_curtainM : (s_curtainM = Tex("curtain.png", Vector2.one, new Color(0.85f, 0.82f, 0.76f), 0.85f));
    static Material FrameWhite => s_frameWhite != null ? s_frameWhite : (s_frameWhite = Col(new Color(0.86f, 0.85f, 0.82f), 0f, 0.45f));
    static Material FrameDark => s_frameDark != null ? s_frameDark : (s_frameDark = Col(new Color(0.16f, 0.17f, 0.18f), 0.7f, 0.55f));
    static Material FrameBrown => s_frameBrown != null ? s_frameBrown : (s_frameBrown = Col(new Color(0.24f, 0.17f, 0.11f), 0f, 0.4f));
    static Material Stone => s_stone != null ? s_stone : (s_stone = Tex("concrete.png", Vector2.one, new Color(0.95f, 0.93f, 0.88f), 0.25f));
    static Material MetalBlack => s_metalBlack != null ? s_metalBlack : (s_metalBlack = Col(new Color(0.06f, 0.06f, 0.065f), 0.8f, 0.4f));
    static Material BalconyGlass => s_balconyGlass != null ? s_balconyGlass : (s_balconyGlass = Transparent(new Color(0.6f, 0.75f, 0.8f, 0.35f)));
    static Material ShopGlass => s_shopGlass != null ? s_shopGlass : (s_shopGlass = Transparent(new Color(0.75f, 0.85f, 0.9f, 0.18f)));

    static readonly string[] ShopInteriors = { "interior_diner.png", "interior_pharmacy.png", "interior_electronics.png", "interior_liquor.png" };
    static readonly string[] Awnings = { "awning_red.png", "awning_green.png", "awning_blue.png", "awning_yellow.png" };

    enum Style { Brick, Concrete, Stucco, Glass }

    static Style StyleOf(string facade) =>
        facade.Contains("glass") ? Style.Glass : facade.Contains("brick") ? Style.Brick : facade.Contains("cream") ? Style.Stucco : Style.Concrete;

    // ---- the building -------------------------------------------------------

    // shopFace: which side gets the shop front (Vector3.zero = none)
    static void DetailedBuilding(Transform b, Vector2 size, float h, string facade, Vector3 shopFace, int shopIdx, bool exposeW, bool exposeE)
    {
        var style = StyleOf(facade);
        var batch = new Batch();
        float groundH = shopFace != Vector3.zero ? 4.6f : 3.8f;
        const float floorH = 3.4f;

        var faces = new List<Vector3> { Vector3.back, Vector3.forward };
        if (exposeW) faces.Add(Vector3.left);
        if (exposeE) faces.Add(Vector3.right);

        foreach (var n in faces)
        {
            var f = MakeFace(size, n);
            // wall surface
            string wallTex = style == Style.Brick ? "wall_brick.png" : style == Style.Stucco ? "wall_stucco.png" : style == Style.Concrete ? "wall_concrete.png" : "facade_glass.png";
            float tile = style == Style.Brick ? 2f : style == Style.Glass ? 12f : 4f;
            Facade(b, f.origin + n * 0.02f + Vector3.up * h * 0.5f, n, f.length, h, wallTex, tile, tile);

            if (style == Style.Glass) { GlassTowerFace(batch, f, h); continue; }

            bool shop = n == shopFace;
            float bay = style == Style.Brick ? 3.1f : 3.4f;
            int bays = Mathf.Max(1, Mathf.FloorToInt((f.length - 1.2f) / bay));
            float start = -bays * bay * 0.5f + bay * 0.5f;
            float ww = style == Style.Concrete ? 2.2f : 1.4f, wh = style == Style.Concrete ? 1.9f : 2.0f;
            var frame = style == Style.Brick ? FrameWhite : style == Style.Stucco ? FrameBrown : FrameDark;

            // floors above the ground floor
            int floors = Mathf.FloorToInt((h - groundH - 0.8f) / floorH);
            for (int fl = 0; fl < floors; fl++)
            {
                float y0 = groundH + fl * floorH;
                for (int i = 0; i < bays; i++)
                {
                    float u = start + i * bay;
                    Window(batch, f, new Vector3(u, y0 + 0.95f, 0f), ww, wh, frame, style);
                    if (style == Style.Stucco && fl > 0 && i % 2 == 0) Balcony(batch, f, u, y0, ww);
                    if (s_rng.NextDouble() < 0.06) batch.Add(Col(new Color(0.75f, 0.75f, 0.73f), 0.2f, 0.4f), f.M(new Vector3(u, y0 + 0.75f, 0.25f), new Vector3(0.7f, 0.42f, 0.5f)));   // window AC unit
                }
                // floor band
                if (style != Style.Brick || fl == 0)
                    batch.Add(Stone, f.M(new Vector3(0f, y0 - 0.05f, 0.07f), new Vector3(f.length + 0.08f, style == Style.Concrete ? 0.22f : 0.16f, 0.14f)));
            }
            // crowning cornice
            batch.Add(Stone, f.M(new Vector3(0f, h - 0.35f, 0.22f), new Vector3(f.length + 0.5f, 0.5f, 0.45f)));
            batch.Add(Stone, f.M(new Vector3(0f, h - 0.75f, 0.1f), new Vector3(f.length + 0.2f, 0.14f, 0.2f)));

            // NYC fire escape on brick fronts
            if (style == Style.Brick && f.length > 12f && floors >= 2 && (n == Vector3.back || n == Vector3.forward))
                FireEscape(batch, f, start + bay * 0.5f + (bays > 3 ? bay : 0f), bay * 2f - 0.4f, groundH, floorH, floors);

            // ground floor
            if (shop) StoreFront(b, batch, f, shopIdx, groundH);
            else
            {
                batch.Add(Tex("granite.png", Vector2.one, null, 0.4f), f.M(new Vector3(0f, groundH * 0.5f, 0.06f), new Vector3(f.length, groundH, 0.12f)));
                // a building entrance door and two barred windows
                batch.Add(FrameDark, f.M(new Vector3(0f, 1.3f, 0.14f), new Vector3(1.6f, 2.6f, 0.1f)));
                batch.Add(GlassDark, f.M(new Vector3(0f, 1.3f, 0.2f), new Vector3(1.3f, 2.3f, 0.04f)));
                foreach (float u in new[] { -f.length * 0.3f, f.length * 0.3f })
                {
                    batch.Add(GlassDark, f.M(new Vector3(u, 2.0f, 0.14f), new Vector3(1.6f, 1.4f, 0.04f)));
                    for (int k = -3; k <= 3; k++) batch.Add(MetalBlack, f.M(new Vector3(u + k * 0.22f, 2.0f, 0.2f), new Vector3(0.03f, 1.4f, 0.03f)));
                }
            }
        }
        batch.Flush(b, "Detail");
    }

    // a classical stone wall: tall arched-style windows with stone surrounds
    static void WindowWall(Transform root, Batch batch, Vector3 wallCenterBase, Vector3 outward, float width, float height, string wallTex = "wall_stucco.png")
    {
        var f = new Face { origin = wallCenterBase, rot = Quaternion.LookRotation(outward, Vector3.up), length = width };
        Facade(root, wallCenterBase + outward * 0.02f + Vector3.up * height * 0.5f, outward, width, height, wallTex, 3f, 3f);
        int bays = Mathf.Max(1, Mathf.FloorToInt(width / 3.6f));
        float start = -bays * 3.6f * 0.5f + 1.8f;
        for (int fl = 0; fl < 4; fl++)
        {
            float y = 2.6f + fl * 4.8f;
            for (int i = 0; i < bays; i++)
            {
                float u = start + i * 3.6f;
                Window(batch, f, new Vector3(u, y + 1.4f, 0f), 1.5f, 2.8f, FrameDark, Style.Stucco);
                batch.Add(Stone, f.M(new Vector3(u, y + 3.05f, 0.08f), new Vector3(2.2f, 0.35f, 0.16f)));     // pediment cap
            }
            batch.Add(Stone, f.M(new Vector3(0f, y - 0.25f, 0.1f), new Vector3(width + 0.1f, 0.25f, 0.2f)));
        }
        batch.Add(Stone, f.M(new Vector3(0f, height - 0.4f, 0.3f), new Vector3(width + 0.6f, 0.8f, 0.6f)));
        batch.Add(Stone, f.M(new Vector3(0f, 1.1f, 0.12f), new Vector3(width, 2.2f, 0.24f)));               // rusticated base
    }

    static void Window(Batch batch, Face f, Vector3 c, float w, float h, Material frame, Style style)
    {
        double r = s_rng.NextDouble();
        var glass = r < 0.16 ? GlassLit : r < 0.28 ? CurtainM : GlassDark;
        const float d = 0.12f, t = 0.09f;
        batch.Add(glass, f.M(c + new Vector3(0f, 0f, 0.03f), new Vector3(w, h, 0.04f)));
        batch.Add(frame, f.M(c + new Vector3(0f, h * 0.5f + t * 0.5f, d * 0.5f), new Vector3(w + t * 2f, t, d)));
        batch.Add(frame, f.M(c + new Vector3(0f, -h * 0.5f - t * 0.5f, d * 0.5f), new Vector3(w + t * 2f, t, d)));
        batch.Add(frame, f.M(c + new Vector3(-w * 0.5f - t * 0.5f, 0f, d * 0.5f), new Vector3(t, h, d)));
        batch.Add(frame, f.M(c + new Vector3(w * 0.5f + t * 0.5f, 0f, d * 0.5f), new Vector3(t, h, d)));
        if (style != Style.Concrete) batch.Add(frame, f.M(c + new Vector3(0f, 0.15f, 0.06f), new Vector3(w, 0.05f, 0.05f)));   // sash bar
        else batch.Add(frame, f.M(c + new Vector3(0f, 0f, 0.06f), new Vector3(0.06f, h, 0.05f)));
        // stone sill (+ lintel on brick)
        batch.Add(Stone, f.M(c + new Vector3(0f, -h * 0.5f - 0.13f, 0.13f), new Vector3(w + 0.36f, 0.09f, 0.26f)));
        if (style == Style.Brick) batch.Add(Stone, f.M(c + new Vector3(0f, h * 0.5f + 0.2f, 0.05f), new Vector3(w + 0.3f, 0.2f, 0.1f)));
    }

    static void Balcony(Batch batch, Face f, float u, float y0, float ww)
    {
        float bw = ww + 1.0f;
        batch.Add(Stone, f.M(new Vector3(u, y0 + 0.02f, 0.6f), new Vector3(bw, 0.14f, 1.2f)));
        batch.Add(BalconyGlass, f.M(new Vector3(u, y0 + 0.62f, 1.18f), new Vector3(bw, 1.0f, 0.03f)));
        batch.Add(FrameDark, f.M(new Vector3(u, y0 + 1.13f, 1.18f), new Vector3(bw + 0.04f, 0.05f, 0.06f)));
        foreach (float s in new[] { -1f, 1f })
            batch.Add(BalconyGlass, f.M(new Vector3(u + s * bw * 0.5f, y0 + 0.62f, 0.6f), new Vector3(0.03f, 1.0f, 1.16f)));
    }

    static void FireEscape(Batch batch, Face f, float u, float width, float groundH, float floorH, int floors)
    {
        var m = MetalBlack;
        for (int fl = 0; fl < floors; fl++)
        {
            float y = groundH + fl * floorH;
            batch.Add(m, f.M(new Vector3(u, y, 0.62f), new Vector3(width, 0.05f, 1.15f)));                       // grated platform
            batch.Add(m, f.M(new Vector3(u, y + 1.0f, 1.18f), new Vector3(width, 0.05f, 0.05f)));                // top rail
            batch.Add(m, f.M(new Vector3(u, y + 0.5f, 1.18f), new Vector3(width, 0.03f, 0.03f)));                // mid rail
            for (float x = -width * 0.5f; x <= width * 0.5f + 0.01f; x += width / 4f)
                batch.Add(m, f.M(new Vector3(u + x, y + 0.5f, 1.18f), new Vector3(0.04f, 1.0f, 0.04f)));         // balusters
            foreach (float s in new[] { -1f, 1f })
                batch.Add(m, f.M(new Vector3(u + s * width * 0.5f, y + 0.5f, 0.62f), new Vector3(0.04f, 1.0f, 1.15f)));
            if (fl < floors - 1)
            {
                // zig-zag stair to the next landing
                float run = width * 0.7f, ang = Mathf.Atan2(floorH, run) * Mathf.Rad2Deg * (fl % 2 == 0 ? 1f : -1f);
                float len = Mathf.Sqrt(run * run + floorH * floorH);
                batch.Add(m, f.M(new Vector3(u, y + floorH * 0.5f, 0.5f), new Vector3(len, 0.06f, 0.55f), Quaternion.Euler(0f, 0f, ang)));
            }
        }
        // drop ladder (raised) under the first landing
        batch.Add(m, f.M(new Vector3(u + width * 0.35f, groundH - 1.2f, 1.0f), new Vector3(0.45f, 2.2f, 0.04f)));
    }

    static void GlassTowerFace(Batch batch, Face f, float h)
    {
        var fin = Col(new Color(0.55f, 0.58f, 0.62f), 0.85f, 0.7f);
        for (float x = -f.length * 0.5f; x <= f.length * 0.5f + 0.01f; x += 1.6f)
            batch.Add(fin, f.M(new Vector3(x, h * 0.5f, 0.12f), new Vector3(0.1f, h, 0.24f)));
        for (float y = 4.5f; y < h; y += 4f)
            batch.Add(fin, f.M(new Vector3(0f, y, 0.06f), new Vector3(f.length, 0.12f, 0.12f)));
        // lobby: tall glass at the base
        batch.Add(Tex("granite.png", Vector2.one, null, 0.5f), f.M(new Vector3(0f, 0.25f, 0.1f), new Vector3(f.length, 0.5f, 0.2f)));
    }

    static void StoreFront(Transform b, Batch batch, Face f, int idx, float groundH)
    {
        float w = f.length;
        var granite = Tex("granite.png", new Vector2(w / 3f, 1f), null, 0.55f);
        // pilasters + plinth
        foreach (float s in new[] { -1f, 1f })
            batch.Add(granite, f.M(new Vector3(s * (w * 0.5f - 0.25f), groundH * 0.5f, 0.15f), new Vector3(0.5f, groundH, 0.3f)));
        batch.Add(granite, f.M(new Vector3(0f, 0.25f, 0.12f), new Vector3(w, 0.5f, 0.24f)));
        float gw = w - 1.2f, gh = 3.0f, gy = 0.5f + gh * 0.5f;

        // lit interior behind the glass (a separate quad so it can glow)
        var interior = new Material(Lit());
        var it = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + ShopInteriors[idx]);
        interior.SetTexture("_BaseMap", it); interior.SetColor("_BaseColor", new Color(0.6f, 0.6f, 0.6f));
        interior.EnableKeyword("_EMISSION"); interior.SetTexture("_EmissionMap", it); interior.SetColor("_EmissionColor", new Color(0.85f, 0.8f, 0.72f));
        interior.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "ShopInterior"; q.transform.SetParent(b, false);
        q.transform.localPosition = f.origin + f.rot * new Vector3(0f, gy, 0.03f);
        q.transform.localRotation = f.rot * Quaternion.Euler(0f, 180f, 0f);
        q.transform.localScale = new Vector3(gw, gh, 1f);
        q.GetComponent<Renderer>().sharedMaterial = interior;

        batch.Add(ShopGlass, f.M(new Vector3(0f, gy, 0.16f), new Vector3(gw, gh, 0.03f)));
        // frame: top/bottom rails + mullions + door
        batch.Add(FrameDark, f.M(new Vector3(0f, 0.5f + gh + 0.05f, 0.18f), new Vector3(gw + 0.1f, 0.1f, 0.12f)));
        batch.Add(FrameDark, f.M(new Vector3(0f, 0.55f, 0.18f), new Vector3(gw + 0.1f, 0.1f, 0.12f)));
        for (float x = -gw * 0.5f; x <= gw * 0.5f + 0.01f; x += gw / Mathf.Max(2, Mathf.Round(gw / 2.2f)))
            batch.Add(FrameDark, f.M(new Vector3(x, gy, 0.18f), new Vector3(0.08f, gh, 0.12f)));
        float dx = gw * 0.5f - 0.75f;
        batch.Add(FrameDark, f.M(new Vector3(dx, 1.5f, 0.2f), new Vector3(1.3f, 2.5f, 0.06f)));
        batch.Add(GlassDark, f.M(new Vector3(dx, 1.5f, 0.24f), new Vector3(1.1f, 2.3f, 0.03f)));
        batch.Add(Col(new Color(0.8f, 0.8f, 0.78f), 1f, 0.85f), f.M(new Vector3(dx, 1.3f, 0.3f), new Vector3(0.9f, 0.05f, 0.05f)));   // push bar

        // sign band with an emissive sign + small downlights
        batch.Add(FrameDark, f.M(new Vector3(0f, groundH - 0.55f, 0.2f), new Vector3(w - 0.2f, 0.9f, 0.3f)));
        var sign = Facade(b, f.origin + f.rot * new Vector3(0f, groundH - 0.55f, 0.36f), f.rot * Vector3.forward,
                          Mathf.Min(9f, w - 1.5f), 0.75f, ShopSigns[idx], Mathf.Min(9f, w - 1.5f), 0.75f);
        var sm = new Material(sign.GetComponent<Renderer>().sharedMaterial);
        sm.EnableKeyword("_EMISSION"); sm.SetTexture("_EmissionMap", sm.mainTexture); sm.SetColor("_EmissionColor", new Color(1.4f, 1.4f, 1.4f));
        sign.GetComponent<Renderer>().sharedMaterial = sm;

        // striped awning
        var aw = Tex(Awnings[idx], new Vector2(Mathf.Max(1f, Mathf.Round(gw / 1.2f)), 1f), null, 0.3f);
        batch.Add(aw, f.M(new Vector3(0f, groundH - 1.25f, 0.85f), new Vector3(gw, 0.06f, 1.6f), Quaternion.Euler(-16f, 0f, 0f)));
        batch.Add(aw, f.M(new Vector3(0f, groundH - 1.62f, 1.62f), new Vector3(gw, 0.3f, 0.04f)));   // valance
    }
}
