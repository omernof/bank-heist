using UnityEngine;
using UnityEngine.InputSystem;

// A coloured dot for the minimap (only the minimap camera renders its layer).
public class MinimapBlip : MonoBehaviour
{
    public Color color = Color.red;
    public float size = 3.2f;
    Transform _dot;
    Health _h;

    void Start()
    {
        _h = GetComponent<Health>();
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(g.GetComponent<Collider>());
        g.name = "Blip";
        int layer = LayerMask.NameToLayer("Minimap");
        g.layer = layer >= 0 ? layer : 31;
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        m.SetColor("_BaseColor", color); m.color = color;
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _dot = g.transform;
        _dot.localScale = Vector3.one * size;
    }

    void LateUpdate()
    {
        if (_dot == null) return;
        _dot.position = transform.position + Vector3.up * 30f;
        _dot.rotation = Quaternion.Euler(90f, 0f, 0f);
        if (_h != null && _h.Dead) { Destroy(_dot.gameObject); enabled = false; }
    }

    void OnDisable() { if (_dot != null) _dot.gameObject.SetActive(false); }
    void OnEnable() { if (_dot != null) _dot.gameObject.SetActive(true); }
}
