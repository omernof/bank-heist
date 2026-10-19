using UnityEngine;
using UnityEngine.InputSystem;

// Flashing red/blue light bar + siren for police cars.
public class PoliceLights : MonoBehaviour
{
    public Light red, blue;
    public Renderer redLamp, blueLamp;
    public bool siren = true;
    public float sirenVolume = 0.45f, sirenRange = 70f;
    float _phase;

    void Start()
    {
        _phase = Random.value;
        if (siren)
        {
            var clip = Sfx.Clip("siren");
            if (clip != null)
            {
                var a = gameObject.AddComponent<AudioSource>();
                a.clip = clip; a.loop = true; a.spatialBlend = 1f; a.minDistance = 6f; a.maxDistance = sirenRange; a.volume = sirenVolume;
                a.time = Random.value * clip.length; a.Play();
            }
        }
    }

    void Update()
    {
        bool r = ((Time.time + _phase) * 3f) % 1f < 0.5f;
        if (red != null) red.enabled = r;
        if (blue != null) blue.enabled = !r;
        if (redLamp != null) redLamp.material.SetColor("_EmissionColor", r ? new Color(4f, 0.2f, 0.2f) : Color.black);
        if (blueLamp != null) blueLamp.material.SetColor("_EmissionColor", !r ? new Color(0.2f, 0.4f, 4f) : Color.black);
    }
}
