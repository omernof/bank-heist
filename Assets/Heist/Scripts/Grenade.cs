using UnityEngine;

// A thrown frag grenade: bounces, then explodes after a short fuse, damaging
// everyone in range (less for the thrower's own team) and pushing props around.
public class Grenade : MonoBehaviour
{
    public float fuse = 2.2f;
    public float radius = 6.5f;
    public float maxDamage = 130f;
    public Health owner;

    float _t;

    public static void Throw(Vector3 from, Vector3 target, Health owner)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = "Grenade";
        g.transform.position = from;
        g.transform.localScale = Vector3.one * 0.14f;
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", new Color(0.2f, 0.26f, 0.14f)); m.color = new Color(0.2f, 0.26f, 0.14f);
        g.GetComponent<Renderer>().sharedMaterial = m;
        var rb = g.AddComponent<Rigidbody>();
        rb.mass = 0.4f; rb.linearDamping = 0.2f; rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var gr = g.AddComponent<Grenade>();
        gr.owner = owner;
        if (owner != null)
            foreach (var c in owner.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(c, g.GetComponent<Collider>());

        // lob it: solve a ballistic arc that lands on the target in ~1 second
        Vector3 d = target - from;
        float t = Mathf.Clamp(d.magnitude / 14f, 0.6f, 1.3f);
        Vector3 v = d / t - 0.5f * Physics.gravity * t;
        rb.linearVelocity = v;
        rb.angularVelocity = Random.insideUnitSphere * 8f;
        Sfx.Play("throw", from, 0.7f);
    }

    void Update()
    {
        _t += Time.deltaTime;
        if (_t >= fuse) Explode();
    }

    void Explode()
    {
        Vector3 p = transform.position;
        Fx.Explosion(p);
        var done = new System.Collections.Generic.HashSet<Health>();
        foreach (var c in Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var h = c.GetComponentInParent<Health>();
            if (h != null && done.Add(h))
            {
                float k = 1f - Mathf.Clamp01(Vector3.Distance(p, h.AimPoint) / radius);
                // walls block the blast
                if (Physics.Linecast(p + Vector3.up * 0.3f, h.AimPoint, out var hit, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Health>() != h) k *= 0.25f;
                float dmg = maxDamage * k * k;
                if (owner != null && h.team == owner.team && h != owner) dmg *= 0.25f;
                h.TakeDamage(dmg, owner, (h.AimPoint - p).normalized);
            }
            if (c.attachedRigidbody != null && c.attachedRigidbody != GetComponent<Rigidbody>())
                c.attachedRigidbody.AddExplosionForce(900f, p, radius, 0.6f);
        }
        HeistDirector.Instance?.OnGunfire(owner, p);
        Destroy(gameObject);
    }
}

// Screen shake for explosions / nearby gunfire; the player camera reads Offset.
public static class CameraShake
{
    static float s_until, s_strength;
    public static void Shake(Vector3 at, float strength, float duration)
    {
        var cam = Camera.main;
        if (cam != null) strength *= Mathf.Clamp01(1.2f - Vector3.Distance(cam.transform.position, at) / 30f);
        if (strength <= 0.01f) return;
        s_strength = Mathf.Max(s_strength * Mathf.Clamp01((s_until - Time.time) / 0.5f), strength);
        s_until = Time.time + duration;
    }
    public static Vector3 Offset
    {
        get
        {
            float k = Mathf.Clamp01((s_until - Time.time) / 0.5f) * s_strength;
            if (k <= 0f) return Vector3.zero;
            float t = Time.time * 40f;
            return new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * k;
        }
    }
}
