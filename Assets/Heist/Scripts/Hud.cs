using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// GTA V-style HUD built entirely from code:
//  bottom-left  : rotating minimap with blips, green health + blue armor bars
//  top-right    : wanted stars, cash, weapon + ammo, grenades
//  top-left     : help box ("Press E to ...") and notification feed
//  bottom-centre: mission objective subtitle
//  centre       : crosshair, hold-progress bar, TAB command wheel
//  full screen  : damage flash, WASTED / MISSION PASSED / MISSION FAILED banners
public class Hud : MonoBehaviour
{
    public static Hud I;
    public static readonly Color Info = new Color(0.85f, 0.9f, 1f);
    public static readonly Color Good = new Color(0.45f, 0.85f, 0.4f);
    public static readonly Color Warn = new Color(1f, 0.78f, 0.25f);
    public static readonly Color Bad = new Color(1f, 0.35f, 0.3f);
    static readonly Color GtaGreen = new Color(0.45f, 0.72f, 0.36f);
    static readonly Color GtaBlue = new Color(0.36f, 0.62f, 0.86f);

    public Font font, bigFont;
    public Camera minimapCam;

    Canvas _canvas;
    RectTransform _root;
    Image _hpFill, _armorFill, _dmgFlash, _promptBar, _promptBarBg, _fade;
    Text _money, _ammo, _grenades, _objective, _prompt, _bannerBig, _bannerSmall, _bannerSub;
    Image[] _stars = new Image[5];
    RectTransform _feed, _wheel, _crosshair, _promptBox;
    GameObject _bannerRoot;
    RawImage _map;
    readonly List<(GameObject go, float until)> _notes = new List<(GameObject, float)>();
    Text[] _wheelLabels = new Text[5];
    Image[] _wheelSlices = new Image[5];
    Vector2 _wheelVec;
    int _wheelPick = -1;
    string _promptText; float _promptProgress = -1f; float _promptUntil;
    int _shownMoney; float _lastWanted;

    public static bool Blocking { get; private set; }
    public static bool WheelOpen { get; private set; }

    public static void Ensure(Font f, Font big)
    {
        if (I != null) return;
        var go = new GameObject("HUD");
        var h = go.AddComponent<Hud>();
        h.font = f; h.bigFont = big != null ? big : f;
        h.Build();
    }

    void Awake() { I = this; Blocking = false; WheelOpen = false; }

    // ------------------------------------------------------------------ build

    Text Label(RectTransform p, string s, int size, TextAnchor a, Color c, Font f = null, bool shadow = true)
    {
        var g = new GameObject("Text", typeof(RectTransform));
        g.transform.SetParent(p, false);
        var t = g.AddComponent<Text>();
        t.font = f != null ? f : font; t.fontSize = size; t.alignment = a; t.color = c; t.text = s;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        if (shadow) { var sh = g.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.85f); sh.effectDistance = new Vector2(2, -2); }
        return t;
    }

    Image Box(RectTransform p, Color c, string name = "Box")
    {
        var g = new GameObject(name, typeof(RectTransform));
        g.transform.SetParent(p, false);
        var i = g.AddComponent<Image>(); i.color = c; i.raycastTarget = false;
        return i;
    }

    static RectTransform Place(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)c.transform;
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static Sprite s_star, s_circle;
    static Sprite StarSprite()
    {
        if (s_star != null) return s_star;
        const int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var c = new Vector2(n / 2f, n / 2f);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - c;
            float a = Mathf.Atan2(p.y, p.x) + Mathf.PI / 2f;
            float k = Mathf.Repeat(a, 2f * Mathf.PI / 5f) / (2f * Mathf.PI / 5f);
            float r = Mathf.Lerp(n * 0.2f, n * 0.47f, Mathf.Abs(k - 0.5f) * 2f);
            float inside = Mathf.Clamp01(r - p.magnitude + 0.5f);
            tex.SetPixel(x, y, new Color(1, 1, 1, inside));
        }
        tex.Apply();
        return s_star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }
    static Sprite CircleSprite()
    {
        if (s_circle != null) return s_circle;
        const int n = 128; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float d = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude;
            tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
        }
        tex.Apply();
        return s_circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
    }

    void Build()
    {
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 50;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        _root = (RectTransform)transform;

        // --- minimap (GTA: rounded-ish rectangle, bottom-left) ---
        var mapFrame = Box(_root, new Color(0, 0, 0, 0.55f), "MapFrame");
        Place(mapFrame, new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 58), new Vector2(330, 214));
        var mask = Box((RectTransform)mapFrame.transform, Color.white, "Mask");
        Place(mask, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 204));
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var mapGo = new GameObject("Map", typeof(RectTransform)); mapGo.transform.SetParent(mask.transform, false);
        _map = mapGo.AddComponent<RawImage>(); _map.raycastTarget = false;
        Place(_map, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 330));
        var arrow = Label((RectTransform)mask.transform, "▲", 26, TextAnchor.MiddleCenter, Color.white);
        Place(arrow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
        // health (green, wide) + armor (blue) bars under the map
        var hpBg = Box(_root, new Color(0.12f, 0.22f, 0.1f, 0.85f));
        Place(hpBg, Vector2.zero, Vector2.zero, new Vector2(40, 40), new Vector2(162, 12));
        _hpFill = Box((RectTransform)hpBg.transform, GtaGreen);
        var hr = (RectTransform)_hpFill.transform; hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.offsetMin = hr.offsetMax = Vector2.zero;
        var arBg = Box(_root, new Color(0.1f, 0.16f, 0.24f, 0.85f));
        Place(arBg, Vector2.zero, Vector2.zero, new Vector2(208, 40), new Vector2(162, 12));
        _armorFill = Box((RectTransform)arBg.transform, GtaBlue);
        var ar = (RectTransform)_armorFill.transform; ar.anchorMin = Vector2.zero; ar.anchorMax = Vector2.one; ar.offsetMin = ar.offsetMax = Vector2.zero;

        // --- top-right: stars, cash, weapon ---
        for (int i = 0; i < 5; i++)
        {
            var s = Box(_root, new Color(1, 1, 1, 0.25f), "Star");
            s.sprite = StarSprite();
            Place(s, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40 - (4 - i) * 44, -34), new Vector2(40, 40));
            _stars[i] = s;
        }
        _money = Label(_root, "$0", 46, TextAnchor.UpperRight, GtaGreen, bigFont);
        Place(_money, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -82), new Vector2(400, 56));
        _ammo = Label(_root, "", 34, TextAnchor.UpperRight, Color.white, bigFont);
        Place(_ammo, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -140), new Vector2(400, 44));
        _grenades = Label(_root, "", 26, TextAnchor.UpperRight, new Color(0.85f, 0.85f, 0.85f), bigFont);
        Place(_grenades, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -184), new Vector2(400, 34));

        // --- top-left: help box + feed ---
        var pb = Box(_root, new Color(0, 0, 0, 0.72f), "HelpBox");
        _promptBox = Place(pb, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(460, 64));
        _prompt = Label(_promptBox, "", 24, TextAnchor.MiddleLeft, Color.white, font, false);
        var prr = (RectTransform)_prompt.transform; prr.anchorMin = Vector2.zero; prr.anchorMax = Vector2.one; prr.offsetMin = new Vector2(18, 10); prr.offsetMax = new Vector2(-14, -8);
        _promptBarBg = Box(_promptBox, new Color(1, 1, 1, 0.2f));
        Place(_promptBarBg, new Vector2(0, 0), new Vector2(0, 0), new Vector2(18, 8), new Vector2(424, 6));
        _promptBar = Box((RectTransform)_promptBarBg.transform, Warn);
        var pbr = (RectTransform)_promptBar.transform; pbr.anchorMin = Vector2.zero; pbr.anchorMax = Vector2.one; pbr.offsetMin = pbr.offsetMax = Vector2.zero;
        _promptBox.gameObject.SetActive(false);

        var feedGo = new GameObject("Feed", typeof(RectTransform)); feedGo.transform.SetParent(_root, false);
        _feed = Place(feedGo.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -120), new Vector2(460, 400));
        var vl = feedGo.AddComponent<VerticalLayoutGroup>(); vl.spacing = 6; vl.childControlHeight = false; vl.childControlWidth = false; vl.childForceExpandHeight = false;

        // --- objective subtitle ---
        _objective = Label(_root, "", 30, TextAnchor.LowerCenter, Color.white, font);
        Place(_objective, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1400, 60));

        // --- crosshair ---
        var ch = Label(_root, "·", 44, TextAnchor.MiddleCenter, Color.white);
        _crosshair = Place(ch, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(40, 40));

        // --- damage flash + fade ---
        _dmgFlash = Box(_root, new Color(0.7f, 0f, 0f, 0f), "DamageFlash");
        var dr = (RectTransform)_dmgFlash.transform; dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = dr.offsetMax = Vector2.zero;

        // --- command wheel ---
        var wheelGo = new GameObject("Wheel", typeof(RectTransform)); wheelGo.transform.SetParent(_root, false);
        _wheel = Place(wheelGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560, 560));
        var disc = Box(_wheel, new Color(0, 0, 0, 0.55f)); disc.sprite = CircleSprite();
        var drr = (RectTransform)disc.transform; drr.anchorMin = Vector2.zero; drr.anchorMax = Vector2.one; drr.offsetMin = drr.offsetMax = Vector2.zero;
        string[] names = { "FOLLOW ME", "OPEN FIRE", "GRENADE", "FALL BACK", "HOLD" };
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Deg2Rad * (90f - i * 72f);
            var slice = Box(_wheel, new Color(1, 1, 1, 0.08f)); slice.sprite = CircleSprite();
            Place(slice, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 190f, new Vector2(150, 150));
            _wheelSlices[i] = slice;
            var t = Label(_wheel, $"{i + 1}\n{names[i]}", 24, TextAnchor.MiddleCenter, Color.white, bigFont);
            Place(t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 190f, new Vector2(180, 90));
            _wheelLabels[i] = t;
        }
        var cap = Label(_wheel, "CREW ORDERS", 30, TextAnchor.MiddleCenter, Warn, bigFont);
        Place(cap, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 60));
        _wheel.gameObject.SetActive(false);

        // --- banners ---
        _bannerRoot = new GameObject("Banner", typeof(RectTransform));
        _bannerRoot.transform.SetParent(_root, false);
        var brt = (RectTransform)_bannerRoot.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero;
        var tint = Box(brt, new Color(0.15f, 0.15f, 0.15f, 0.45f), "Tint");
        var tr = (RectTransform)tint.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        var strip = Box(brt, new Color(0, 0, 0, 0.6f), "Strip");
        Place(strip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(4000, 190));
        _bannerBig = Label(brt, "", 140, TextAnchor.MiddleCenter, Bad, bigFont);
        Place(_bannerBig, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(1600, 160));
        _bannerSmall = Label(brt, "", 34, TextAnchor.MiddleCenter, Color.white, font);
        Place(_bannerSmall, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1600, 50));
        _bannerSub = Label(brt, "", 24, TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.85f), font);
        Place(_bannerSub, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(1600, 40));
        _bannerRoot.SetActive(false);

        _fade = Box(_root, new Color(0, 0, 0, 1f), "Fade");
        var fr = (RectTransform)_fade.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
        { _fade.color = new Color(0, 0, 0, 1f - t); yield return null; }
        _fade.color = new Color(0, 0, 0, 0);
    }

    public IEnumerator FadeOut(float seconds = 0.6f)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
        { _fade.color = new Color(0, 0, 0, t); yield return null; }
        _fade.color = Color.black;
    }

    // ----------------------------------------------------------------- update

    void Update()
    {
        if (minimapCam != null && _map != null)
        {
            if (_map.texture != minimapCam.targetTexture) _map.texture = minimapCam.targetTexture;
            var p = PlayerController.Instance;
            Transform follow = p != null ? (p.InVehicle && Vehicle.Current != null ? Vehicle.Current.transform : p.transform) : null;
            if (follow != null)
            {
                float yaw = p.InVehicle && Vehicle.Current != null ? follow.eulerAngles.y : p.Yaw;
                minimapCam.transform.position = follow.position + Vector3.up * 60f;
                minimapCam.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
            }
        }

        ((RectTransform)_hpFill.transform).anchorMax = new Vector2(Mathf.Clamp01(GameData.hp / GameData.maxHp), 1f);
        _hpFill.color = GameData.hp < 30f ? Color.Lerp(Bad, GtaGreen, Mathf.PingPong(Time.time * 2f, 1f)) : GtaGreen;
        ((RectTransform)_armorFill.transform).anchorMax = new Vector2(Mathf.Clamp01(GameData.armor / GameData.maxArmor), 1f);

        // money counts up like GTA
        _shownMoney = (int)Mathf.MoveTowards(_shownMoney, GameData.money, Mathf.Max(50f, Mathf.Abs(GameData.money - _shownMoney) * 4f) * Time.deltaTime);
        _money.text = "$" + _shownMoney.ToString("N0");

        bool armed = GameData.side != Side.Bystander;
        _ammo.text = armed ? $"RIFLE  {GameData.ammoInClip} / {GameData.ammoReserve}" : "UNARMED";
        _grenades.text = armed ? $"GRENADES  {GameData.grenades}" : "";
        _crosshair.gameObject.SetActive(armed && !Blocking);

        if (GameData.wanted != (int)_lastWanted) _lastWanted = GameData.wanted;
        for (int i = 0; i < 5; i++)
        {
            bool on = i < GameData.wanted;
            float flash = on && Time.time % 0.6f < 0.3f && GameData.phase != Phase.Done ? 1f : 0.85f;
            _stars[i].color = on ? new Color(1, 1, 1, flash) : new Color(1, 1, 1, 0.22f);
        }

        // help prompt: set every frame by the player while in range
        bool showPrompt = Time.time < _promptUntil && !string.IsNullOrEmpty(_promptText);
        _promptBox.gameObject.SetActive(showPrompt && !Blocking);
        if (showPrompt)
        {
            _prompt.text = _promptText;
            _promptBarBg.gameObject.SetActive(_promptProgress >= 0f);
            ((RectTransform)_promptBar.transform).anchorMax = new Vector2(Mathf.Clamp01(_promptProgress), 1f);
        }

        for (int i = _notes.Count - 1; i >= 0; i--)
            if (Time.time > _notes[i].until) { Destroy(_notes[i].go); _notes.RemoveAt(i); }

        var dc = _dmgFlash.color; dc.a = Mathf.MoveTowards(dc.a, GameData.hp < 25f ? 0.18f : 0f, Time.deltaTime * 0.8f); _dmgFlash.color = dc;
    }

    // --------------------------------------------------------------- static API

    public static void Notify(string msg, Color c, float seconds = 4f)
    {
        if (I == null) return;
        var box = I.Box(I._feed, new Color(0, 0, 0, 0.7f), "Note");
        ((RectTransform)box.transform).sizeDelta = new Vector2(460, 44);
        var bar = I.Box((RectTransform)box.transform, c);
        Place(bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 44));
        var t = I.Label((RectTransform)box.transform, msg, 22, TextAnchor.MiddleLeft, Color.white, I.font, false);
        var r = (RectTransform)t.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = new Vector2(18, 0); r.offsetMax = new Vector2(-8, 0);
        I._notes.Add((box.gameObject, Time.time + seconds));
        while (I._notes.Count > 5) { Destroy(I._notes[0].go); I._notes.RemoveAt(0); }
    }

    public static void Objective(string text) { if (I != null) I._objective.text = text; }

    public static void Prompt(string text, float progress = -1f)
    {
        if (I == null) return;
        I._promptText = text; I._promptProgress = progress; I._promptUntil = Time.time + 0.15f;
    }

    public static void DamageFlash()
    {
        if (I == null) return;
        var c = I._dmgFlash.color; c.a = Mathf.Max(c.a, 0.35f); I._dmgFlash.color = c;
    }

    public static void ShowWheel(Vector2 mouseDelta)
    {
        if (I == null) return;
        if (!WheelOpen) { WheelOpen = true; I._wheelVec = Vector2.zero; I._wheel.gameObject.SetActive(true); Time.timeScale = 0.3f; }
        I._wheelVec = Vector2.ClampMagnitude(I._wheelVec + mouseDelta * 0.5f, 120f);
        I._wheelPick = -1;
        if (I._wheelVec.magnitude > 30f)
        {
            float ang = Mathf.Atan2(I._wheelVec.y, I._wheelVec.x) * Mathf.Rad2Deg;   // 90 = up = slot 0
            I._wheelPick = Mathf.RoundToInt(Mathf.Repeat(90f - ang, 360f) / 72f) % 5;
        }
        for (int i = 0; i < 5; i++)
            I._wheelSlices[i].color = i == I._wheelPick ? new Color(1f, 0.78f, 0.25f, 0.55f) : new Color(1, 1, 1, 0.08f);
    }

    public static int CloseWheel()
    {
        WheelOpen = false; Time.timeScale = 1f;
        if (I == null) return -1;
        I._wheel.gameObject.SetActive(false);
        return I._wheelPick;
    }

    // GTA end-of-mission / death banners. kind: "wasted", "passed", "failed"
    public static void Banner(string kind, string big, string small, string sub)
    {
        if (I == null) return;
        Blocking = true;
        I._bannerRoot.SetActive(true);
        I._bannerBig.text = big;
        I._bannerBig.color = kind == "passed" ? new Color(1f, 0.8f, 0.2f) : kind == "wasted" ? new Color(0.78f, 0.12f, 0.1f) : Bad;
        I._bannerSmall.text = small;
        I._bannerSub.text = sub;
        I._crosshair.gameObject.SetActive(false);
    }
}
