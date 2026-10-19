using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Fade to black, load a scene, fade back in - so walking through a doorway
// into another scene feels like one continuous step instead of a hard cut.
public class ScreenFade : MonoBehaviour
{
    static ScreenFade s_active;
    Image _img;

    public static void FadeAndLoad(string sceneName, float seconds = 0.35f)
    {
        if (s_active != null) return;
        var go = new GameObject("ScreenFade");
        DontDestroyOnLoad(go);
        s_active = go.AddComponent<ScreenFade>();
        s_active.StartCoroutine(s_active.Run(sceneName, seconds));
    }

    void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var imgGo = new GameObject("Black");
        imgGo.transform.SetParent(transform, false);
        _img = imgGo.AddComponent<Image>();
        _img.color = new Color(0f, 0f, 0f, 0f);
        _img.raycastTarget = false;
        var rt = _img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    IEnumerator Run(string sceneName, float seconds)
    {
        yield return Fade(0f, 1f, seconds);
        yield return SceneManager.LoadSceneAsync(sceneName);
        yield return null;                       // let the new scene settle one frame
        yield return Fade(1f, 0f, seconds);
        s_active = null;
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds))
        {
            _img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t));
            yield return null;
        }
        _img.color = new Color(0f, 0f, 0f, to);
    }
}
