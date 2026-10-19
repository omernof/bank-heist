using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Title screen over a slow fly-over of the city: choose ROBBER / COP /
// BYSTANDER (the brief's opening menu), see the controls, or quit.
public class MainMenu : MonoBehaviour
{
    public Font font, bigFont;
    public Transform orbitCenter;
    public float orbitRadius = 70f, orbitHeight = 38f;

    Camera _cam;
    GameObject _controls;
    float _a;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        _cam = Camera.main;
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
        BuildUi();
        var music = Sfx.Clip("menu_music");
        if (music != null)
        {
            var a = gameObject.AddComponent<AudioSource>();
            a.clip = music; a.loop = true; a.volume = 0.35f; a.Play();
        }
    }

    void Update()
    {
        if (_cam == null || orbitCenter == null) return;
        _a += Time.deltaTime * 3f;
        Vector3 c = orbitCenter.position;
        _cam.transform.position = c + Quaternion.Euler(0f, _a, 0f) * new Vector3(0f, orbitHeight, -orbitRadius);
        _cam.transform.LookAt(c + Vector3.up * 6f);
    }

    Text Label(Transform p, string s, int size, Color c, Font f, TextAnchor a = TextAnchor.MiddleLeft)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(p, false);
        var t = g.AddComponent<Text>(); t.font = f; t.text = s; t.fontSize = size; t.color = c; t.alignment = a;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
        var sh = g.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(3, -3);
        return t;
    }

    static RectTransform Rect(Component c, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var r = (RectTransform)c.transform; r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size; return r;
    }

    void BuildUi()
    {
        var go = new GameObject("MenuCanvas");
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var sc = go.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        var root = go.transform;

        // dark gradient panel on the left, GTA pause-menu style
        var panel = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
        panel.transform.SetParent(root, false);
        panel.color = new Color(0f, 0f, 0f, 0.62f);
        var pr = (RectTransform)panel.transform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(0, 1);
        pr.pivot = new Vector2(0, 0.5f); pr.sizeDelta = new Vector2(760, 0); pr.anchoredPosition = Vector2.zero;

        Rect(Label(root, "BANK HEIST", 150, Color.white, bigFont), new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, -70), new Vector2(900, 170));
        Rect(Label(root, "CHOOSE YOUR SIDE", 34, new Color(0.9f, 0.76f, 0.3f), bigFont), new Vector2(0, 1), new Vector2(0, 1), new Vector2(86, -250), new Vector2(700, 50));

        MakeButton(root, 0, "ROBBER", "Lead a crew of three into the bank, crack the vault and\nescape in the getaway car. Command your crew with 1-5 / TAB.",
                   new Color(0.78f, 0.2f, 0.18f), () => Play(Side.Robber, "City", "start"));
        MakeButton(root, 1, "POLICE", "A robbery is in progress. Lead your squad into the bank\nand take the crew down before they get away.",
                   new Color(0.2f, 0.42f, 0.85f), () => Play(Side.Cop, "City", "cops"));
        MakeButton(root, 2, "BYSTANDER", "You're just a customer in line at the bank when it all\ngoes down. Stay low - and stay alive.",
                   new Color(0.45f, 0.62f, 0.3f), () => Play(Side.Bystander, "Bank", "queue"));
        MakeButton(root, 3, "CONTROLS", "", new Color(0.3f, 0.3f, 0.3f), () => _controls.SetActive(!_controls.activeSelf), 64f);
        MakeButton(root, 4, "QUIT", "", new Color(0.3f, 0.3f, 0.3f), Application.Quit, 64f);

        // controls card
        _controls = new GameObject("Controls", typeof(RectTransform));
        _controls.transform.SetParent(root, false);
        var cimg = _controls.AddComponent<Image>(); cimg.color = new Color(0, 0, 0, 0.8f);
        Rect(cimg, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-80, 0), new Vector2(760, 640));
        var ct = Label(_controls.transform,
            "<b>ON FOOT</b>\nWASD  move      SHIFT  sprint      MOUSE  look\nRIGHT MOUSE  aim      LEFT MOUSE  shoot      R  reload\nG  throw grenade      E  use / hold to interact      C  get down\n\n" +
            "<b>CREW ORDERS</b>  (or hold TAB for the order wheel)\n1  follow me      2  open fire      3  throw a grenade\n4  fall back      5  hold position\n\n" +
            "<b>DRIVING</b>\nF  get in / out      WASD  drive      SPACE  handbrake\n\nESC  free the mouse cursor",
            26, Color.white, font, TextAnchor.UpperLeft);
        var ctr = (RectTransform)ct.transform; ctr.anchorMin = Vector2.zero; ctr.anchorMax = Vector2.one; ctr.offsetMin = new Vector2(36, 30); ctr.offsetMax = new Vector2(-30, -30);
        _controls.SetActive(false);
    }

    void MakeButton(Transform root, int i, string title, string desc, Color accent, UnityEngine.Events.UnityAction onClick, float height = 118f)
    {
        float y = -320f - (i < 3 ? i * 136f : 3 * 136f + (i - 3) * 78f);
        var g = new GameObject(title, typeof(RectTransform));
        g.transform.SetParent(root, false);
        var img = g.AddComponent<Image>(); img.color = new Color(1f, 1f, 1f, 0.07f);
        Rect(img, new Vector2(0, 1), new Vector2(0, 1), new Vector2(80, y), new Vector2(620, height));
        var btn = g.AddComponent<Button>();
        var cols = btn.colors; cols.normalColor = new Color(1, 1, 1, 0.07f); cols.highlightedColor = new Color(1, 1, 1, 0.22f);
        cols.pressedColor = new Color(1, 1, 1, 0.35f); cols.selectedColor = cols.highlightedColor; btn.colors = cols;
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => Sfx.Play2D("ui_select", 0.8f));
        btn.onClick.AddListener(onClick);
        var bar = new GameObject("Accent", typeof(RectTransform)).AddComponent<Image>();
        bar.transform.SetParent(g.transform, false); bar.color = accent; bar.raycastTarget = false;
        Rect(bar, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, height));
        var t = Label(g.transform, title, desc.Length > 0 ? 50 : 38, Color.white, bigFont);
        t.raycastTarget = false;
        Rect(t, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, desc.Length > 0 ? -6 : -8), new Vector2(560, desc.Length > 0 ? 56 : 50));
        if (desc.Length > 0)
        {
            var d = Label(g.transform, desc, 19, new Color(0.85f, 0.85f, 0.85f), font, TextAnchor.UpperLeft);
            d.raycastTarget = false;
            Rect(d, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -62), new Vector2(570, 50));
        }
    }

    static void Play(Side side, string scene, string spawn)
    {
        GameData.NewGame(side);
        HeistDirector.LoadScene(scene, spawn);
    }
}
