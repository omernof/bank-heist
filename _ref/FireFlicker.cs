using UnityEngine;

// Gently flickers a light to fake a fire / candle glow.
[RequireComponent(typeof(Light))]
public class FireFlicker : MonoBehaviour
{
    public float baseIntensity = 6f;
    public float amount = 1.4f;
    public float speed = 9f;

    Light _light;
    float _seed;

    void Awake()
    {
        _light = GetComponent<Light>();
        baseIntensity = _light.intensity;
        _seed = Random.value * 100f;
    }

    void Update()
    {
        float n = Mathf.PerlinNoise(_seed, Time.time * speed);
        _light.intensity = baseIntensity + (n - 0.5f) * 2f * amount;
    }
}
