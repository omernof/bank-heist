using UnityEngine;

// The police helicopter: circles the bank once the heist is on, rotors
// spinning, with a searchlight that tracks the player.
public class PoliceHelicopter : MonoBehaviour
{
    public Vector3 center = new Vector3(0f, 0f, 18f);
    public float radius = 34f, height = 32f, speed = 0.18f, volume = 0.7f;
    public Light searchlight;
    Transform[] _rotors;
    float _a;
    AudioSource _src;

    void Start()
    {
        var list = new System.Collections.Generic.List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("rotor") || n.Contains("blade") || n.Contains("prop")) list.Add(t);
        }
        _rotors = list.ToArray();
        var clip = Sfx.Clip("helicopter");
        if (clip != null)
        {
            _src = gameObject.AddComponent<AudioSource>();
            _src.clip = clip; _src.loop = true; _src.spatialBlend = 1f; _src.minDistance = volume > 0.8f ? 40f : 15f; _src.maxDistance = 200f; _src.volume = volume;
            _src.Play();
        }
    }

    void Update()
    {
        _a += speed * Time.deltaTime;
        Vector3 pos = center + new Vector3(Mathf.Cos(_a) * radius, height + Mathf.Sin(_a * 2.3f) * 2f, Mathf.Sin(_a) * radius);
        Vector3 vel = new Vector3(-Mathf.Sin(_a), 0f, Mathf.Cos(_a));
        transform.position = pos;
        // nose along the flight path, banked into the turn
        transform.rotation = Quaternion.LookRotation(vel) * Quaternion.Euler(6f, 0f, 14f);
        foreach (var r in _rotors) if (r != null) r.Rotate(0f, 1400f * Time.deltaTime, 0f, Space.Self);
        if (searchlight != null)
        {
            var p = PlayerController.Instance;
            Vector3 target = p != null ? p.transform.position : center;
            searchlight.transform.rotation = Quaternion.Slerp(searchlight.transform.rotation,
                Quaternion.LookRotation(target - searchlight.transform.position), 2f * Time.deltaTime);
        }
    }
}
