using UnityEngine;

// A hitscan gun used by the player and every armed NPC. Fire() traces from an
// aim origin (the camera for the player, the chest for NPCs) with some spread,
// damages the first Health it hits (no friendly fire on bullets) and draws the
// muzzle flash / tracer / impact.
public class Weapon : MonoBehaviour
{
    public float damage = 20f;
    public float fireInterval = 0.12f;
    public float range = 150f;
    public float spreadDeg = 1.2f;
    public string sound = "gunshot";
    public Transform muzzle;           // set by GunModel.Attach

    float _next;
    public bool Ready => Time.time >= _next;

    public bool Fire(Vector3 origin, Vector3 dir, Health owner, float extraSpread = 0f, float volume = 0.9f)
    {
        if (!Ready) return false;
        _next = Time.time + fireInterval;

        float s = spreadDeg + extraSpread;
        dir = Quaternion.Euler(Random.Range(-s, s), Random.Range(-s, s), 0f) * dir.normalized;

        Vector3 end = origin + dir * range;
        var hits = Physics.RaycastAll(origin, dir, range, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (owner != null && h.collider.transform.IsChildOf(owner.transform)) continue;
            end = h.point;
            var victim = h.collider.GetComponentInParent<Health>();
            if (victim != null)
            {
                if (owner == null || victim.team != owner.team || victim.team == Team.Civilians)
                    victim.TakeDamage(damage * Random.Range(0.85f, 1.15f), owner, dir);
                Fx.Impact(h.point, h.normal, true);
            }
            else
            {
                Fx.Impact(h.point, h.normal, false);
                if (h.rigidbody != null) h.rigidbody.AddForceAtPosition(dir * 3f, h.point, ForceMode.Impulse);
            }
            break;
        }

        Vector3 from = muzzle != null ? muzzle.position : origin;
        Fx.MuzzleFlash(from, dir);
        Fx.Tracer(from, end);
        Sfx.Play(sound, from, volume, 0.08f, 120f);
        HeistDirector.Instance?.OnGunfire(owner, from);
        return true;
    }
}

// A code-built assault rifle (receiver, barrel, magazine, stock, sight) parented
// to a character's right hand, with a muzzle point for effects.
public static class GunModel
{
    static Material s_metal, s_grip;
    static GameObject s_smg;

    static Material Mat(Color c, float metal, float smooth)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", c); m.color = c;
        m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
        return m;
    }

    static GameObject Part(Transform p, Vector3 pos, Vector3 size, Material m, bool cyl = false)
    {
        var g = GameObject.CreatePrimitive(cyl ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(p, false);
        g.transform.localPosition = pos;
        g.transform.localScale = size;
        if (cyl) g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }

    // Builds the rifle with its barrel along local +Z. `hand` is the right-hand bone.
    public static Transform Attach(Animator anim, Weapon weapon)
    {
        if (s_metal == null) s_metal = Mat(new Color(0.09f, 0.09f, 0.1f), 0.8f, 0.55f);
        if (s_grip == null) s_grip = Mat(new Color(0.18f, 0.15f, 0.12f), 0f, 0.25f);

        Transform hand = anim != null && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
        var root = new GameObject("Rifle").transform;
        root.SetParent(weapon.transform, false);
        root.localPosition = new Vector3(0.25f, 1.15f, 0.35f);
        // follows the hand but always points where its owner aims (see GunHold),
        // so it reads right with or without rifle-specific animations
        var hold = root.gameObject.AddComponent<GunHold>();
        hold.hand = hand; hold.owner = weapon.transform;

        // the real SMG model (built by Tools > Bank Heist > Build Weapon Prefabs:
        // barrel along +Z, origin at the grip); boxes below are only a fallback
        if (s_smg == null) s_smg = Resources.Load<GameObject>("Weapons/SMG");
        if (s_smg != null)
        {
            var gun = Object.Instantiate(s_smg, root, false);
            gun.name = "SMG";
            // muzzle = the front-most point of the model along the barrel
            float front = 0.1f;
            foreach (var mf in gun.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var b = mf.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3(c & 1, (c >> 1) & 1, (c >> 2) & 1) * 2f - Vector3.one);
                    front = Mathf.Max(front, root.InverseTransformPoint(mf.transform.TransformPoint(corner)).z);
                }
            }
            var m = new GameObject("Muzzle").transform;
            m.SetParent(root, false);
            m.localPosition = new Vector3(0f, 0.03f, front);
            weapon.muzzle = m;
            return root;
        }

        Part(root, new Vector3(0f, 0.03f, 0.12f), new Vector3(0.06f, 0.09f, 0.42f), s_metal);          // receiver
        Part(root, new Vector3(0f, 0.045f, 0.46f), new Vector3(0.025f, 0.13f, 0.025f), s_metal, true);  // barrel
        Part(root, new Vector3(0f, 0.03f, 0.36f), new Vector3(0.07f, 0.07f, 0.2f), s_grip);             // handguard
        Part(root, new Vector3(0f, -0.07f, 0.16f), new Vector3(0.045f, 0.16f, 0.07f), s_metal)           // magazine
            .transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        Part(root, new Vector3(0f, -0.05f, 0.0f), new Vector3(0.045f, 0.12f, 0.05f), s_grip)             // pistol grip
            .transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
        Part(root, new Vector3(0f, 0.02f, -0.16f), new Vector3(0.05f, 0.1f, 0.2f), s_grip);              // stock
        Part(root, new Vector3(0f, 0.1f, 0.14f), new Vector3(0.03f, 0.04f, 0.12f), s_metal);            // sight

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root, false);
        muzzle.localPosition = new Vector3(0f, 0.045f, 0.6f);
        weapon.muzzle = muzzle;
        return root;
    }
}

// Keeps the rifle in the right hand's position while pointing it forward /
// at the owner's aim - a cheap stand-in for arm IK.
public class GunHold : MonoBehaviour
{
    public Transform hand, owner;
    PlayerController _player;
    SoldierAI _ai;

    void Start() { _player = owner.GetComponent<PlayerController>(); _ai = owner.GetComponent<SoldierAI>(); }

    void LateUpdate()
    {
        if (owner == null) return;
        Vector3 dir = owner.forward;
        bool aiming = false;
        if (_player != null && _player.Aiming) { dir = (_player.AimPoint - transform.position).normalized; aiming = true; }
        else if (_ai != null && _ai.Target != null) { dir = (_ai.Target.AimPoint - transform.position).normalized; aiming = true; }
        if (!aiming) dir = (owner.forward - owner.up * 0.35f).normalized;          // low ready
        Vector3 anchor = hand != null ? hand.position : owner.position + owner.up * 1.1f + owner.right * 0.25f;
        // grip in the hand, stock tucked back toward the shoulder when aiming
        transform.position = anchor + (aiming ? owner.up * 0.12f : Vector3.zero) - dir * 0.02f;
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }
}
