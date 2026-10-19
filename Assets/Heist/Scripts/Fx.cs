using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Code-built effects (no prefabs needed): muzzle flash, bullet tracer, impact
// sparks / blood puffs, and grenade explosions with light + smoke + shake.
public static class Fx
{
    static Material s_add, s_smoke, s_tracer, s_blood;

    static Material Unlit(Color c, bool additive)
    {
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.SetColor("_BaseColor", c); m.color = c;
        // transparent + (optionally) additive blending
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    }
    static Material MatAdd    => s_add    != null ? s_add    : (s_add    = Unlit(new Color(1f, 0.75f, 0.35f, 1f), true));
    static Material MatSmoke  => s_smoke  != null ? s_smoke  : (s_smoke  = Unlit(new Color(0.25f, 0.23f, 0.22f, 0.55f), false));
    static Material MatTracer => s_tracer != null ? s_tracer : (s_tracer = Unlit(new Color(1f, 0.85f, 0.5f, 0.9f), true));
    static Material MatBlood  => s_blood  != null ? s_blood  : (s_blood  = Unlit(new Color(0.55f, 0.02f, 0.02f, 1f), false));

    static FxRunner Runner
    {
        get
        {
            if (FxRunner.I == null) new GameObject("FxRunner").AddComponent<FxRunner>();
            return FxRunner.I;
        }
    }

    public static void MuzzleFlash(Vector3 pos, Vector3 dir)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.position = pos;
        g.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        g.transform.localScale = Vector3.one * Random.Range(0.25f, 0.4f);
        g.GetComponent<Renderer>().sharedMaterial = MatAdd;
        var l = new GameObject("Flash").AddComponent<Light>();
        l.transform.SetParent(g.transform, false);
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.45f); l.intensity = 6f; l.range = 6f;
        l.shadows = LightShadows.None;
        Object.Destroy(g, 0.05f);
    }

    public static void Tracer(Vector3 from, Vector3 to)
    {
        var g = new GameObject("Tracer");
        var lr = g.AddComponent<LineRenderer>();
        lr.sharedMaterial = MatTracer;
        lr.positionCount = 2;
        lr.SetPosition(0, from); lr.SetPosition(1, to);
        lr.startWidth = 0.035f; lr.endWidth = 0.01f;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Runner.StartCoroutine(FadeLine(lr, 0.07f));
    }

    static IEnumerator FadeLine(LineRenderer lr, float t)
    {
        float w0 = lr.startWidth;
        for (float k = 0f; k < 1f && lr != null; k += Time.deltaTime / t)
        { lr.startWidth = w0 * (1f - k); yield return null; }
        if (lr != null) Object.Destroy(lr.gameObject);
    }

    public static void Impact(Vector3 pos, Vector3 normal, bool flesh)
    {
        var ps = Burst(pos, Quaternion.LookRotation(normal), flesh ? MatBlood : MatAdd,
                       flesh ? 14 : 10, flesh ? 2.2f : 5f, flesh ? 0.35f : 0.18f, flesh ? 0.07f : 0.04f, flesh ? 1.5f : 0.6f);
        Object.Destroy(ps.gameObject, 1.2f);
        if (!flesh)
        {
            // a small dark scorch decal so hits read on walls
            var d = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(d.GetComponent<Collider>());
            d.transform.position = pos + normal * 0.01f;
            d.transform.rotation = Quaternion.LookRotation(-normal);
            d.transform.localScale = Vector3.one * 0.09f;
            d.GetComponent<Renderer>().sharedMaterial = MatSmoke;
            Object.Destroy(d, 12f);
        }
    }

    public static void Explosion(Vector3 pos)
    {
        var fire = Burst(pos, Quaternion.identity, MatAdd, 60, 9f, 0.5f, 0.6f, 0f, true);
        var smoke = Burst(pos + Vector3.up * 0.3f, Quaternion.identity, MatSmoke, 40, 3f, 2.2f, 1.4f, -0.4f, true);
        Object.Destroy(fire.gameObject, 2f); Object.Destroy(smoke.gameObject, 4f);
        var l = new GameObject("Boom").AddComponent<Light>();
        l.transform.position = pos + Vector3.up;
        l.type = LightType.Point; l.color = new Color(1f, 0.6f, 0.25f); l.intensity = 25f; l.range = 18f;
        Runner.StartCoroutine(FadeLight(l, 0.5f));
        Sfx.Play("explosion", pos, 1f, 0.05f);
        CameraShake.Shake(pos, 0.6f, 0.5f);
    }

    static IEnumerator FadeLight(Light l, float t)
    {
        float i0 = l.intensity;
        for (float k = 0f; k < 1f && l != null; k += Time.deltaTime / t) { l.intensity = i0 * (1f - k); yield return null; }
        if (l != null) Object.Destroy(l.gameObject);
    }

    static ParticleSystem Burst(Vector3 pos, Quaternion rot, Material mat, int count, float speed, float life,
                                float size, float gravity, bool sphere = false)
    {
        var g = new GameObject("Burst");
        g.transform.SetPositionAndRotation(pos, rot);
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 0.2f; main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape;
        sh.shapeType = sphere ? ParticleSystemShapeType.Sphere : ParticleSystemShapeType.Cone;
        sh.angle = 35f; sh.radius = sphere ? 0.4f : 0.02f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, sphere ? 0.6f : 1f, 1f, sphere ? 1.6f : 0.3f));
        var r = g.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
        return ps;
    }
}

public class FxRunner : MonoBehaviour
{
    public static FxRunner I;
    void Awake() { I = this; }
}

// One-shot 3D sounds loaded from Resources/Sounds (generated or downloaded clips).
public static class Sfx
{
    static readonly Dictionary<string, AudioClip> s_cache = new Dictionary<string, AudioClip>();

    public static AudioClip Clip(string name)
    {
        if (!s_cache.TryGetValue(name, out var c) || c == null)
        {
            c = Resources.Load<AudioClip>("Sounds/" + name);
            s_cache[name] = c;
        }
        return c;
    }

    // A pool of reusable 3D sources: creating and destroying a GameObject for every
    // footstep / gunshot caused GC hitches (the "jumpy" walking) once every NPC's
    // feet started making sound.
    static readonly List<AudioSource> s_pool = new List<AudioSource>();
    static Transform s_poolRoot;
    static int s_next;
    const int PoolSize = 40;

    static AudioSource Source()
    {
        if (s_poolRoot == null)   // new scene: the old pool went with it
        {
            s_pool.Clear();
            var root = new GameObject("SfxPool") { hideFlags = HideFlags.HideInHierarchy };
            s_poolRoot = root.transform;
        }
        foreach (var src in s_pool) if (src != null && !src.isPlaying) return src;
        if (s_pool.Count < PoolSize)
        {
            var g = new GameObject("Sfx") { hideFlags = HideFlags.HideInHierarchy };
            g.transform.SetParent(s_poolRoot, false);
            var a = g.AddComponent<AudioSource>();
            a.playOnAwake = false;
            s_pool.Add(a);
            return a;
        }
        s_next = (s_next + 1) % s_pool.Count;   // all busy: steal the oldest slot
        return s_pool[s_next];
    }

    public static AudioSource Play(string name, Vector3 pos, float volume = 1f, float pitchJitter = 0.06f, float maxDist = 90f)
    {
        var clip = Clip(name);
        if (clip == null) return null;
        var a = Source();
        a.transform.position = pos;
        a.clip = clip; a.volume = volume;
        a.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = 4f; a.maxDistance = maxDist; a.dopplerLevel = 0f; a.loop = false;
        a.Play();
        return a;
    }

    public static void Play2D(string name, float volume = 1f)
    {
        var clip = Clip(name);
        if (clip == null) return;
        var g = new GameObject("Sfx2D_" + name);
        var a = g.AddComponent<AudioSource>();
        a.clip = clip; a.volume = volume; a.spatialBlend = 0f; a.Play();
        Object.Destroy(g, clip.length + 0.1f);
    }
}
