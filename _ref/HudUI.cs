using UnityEngine;

// HW3 - a plain IMGUI overlay: the coin counter + key status top-left, and a
// centered prompt line ("Press SPACE to open the chest", "You need a key...").
// Lives on the Player, so it exists in every scene. Prompts are pushed through
// the static Show(): callers re-send them every frame while the player is in
// range, so a short default lifetime makes the text vanish as soon as he looks
// away.
public class HudUI : MonoBehaviour
{
    static string _msg;
    static float _msgUntil;

    public static void Show(string message, float seconds = 0.2f)
    {
        _msg = message;
        _msgUntil = Time.time + seconds;
    }

    GUIStyle _line, _prompt;

    void BuildStyles()
    {
        _line = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
        _line.normal.textColor = Color.white;
        _prompt = new GUIStyle(_line) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
    }

    void OnGUI()
    {
        if (_line == null) BuildStyles();

        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.Box(new Rect(8f, 8f, 230f, 66f), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(22f, 12f, 220f, 26f), "Coins: " + GameState.Coins, _line);
        GUI.Label(new Rect(22f, 40f, 220f, 26f), "Key: " + (GameState.HasKey ? "Yes" : "No"), _line);

        if (Time.time < _msgUntil && !string.IsNullOrEmpty(_msg))
        {
            const float w = 640f, h = 46f;
            var r = new Rect((Screen.width - w) * 0.5f, Screen.height - 130f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Box(r, GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(r, _msg, _prompt);
        }
    }
}
