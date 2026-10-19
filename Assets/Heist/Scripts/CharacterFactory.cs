using UnityEngine;
using UnityEngine.AI;

// Builds characters at runtime from "look" prefabs (a Mixamo model with its
// Animator + outfit, made by the editor builder): a root with the gameplay
// components and the model as a child called "Model".
public static class CharacterFactory
{
    static GameObject Root(GameObject look, Vector3 pos, Quaternion rot, string name)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(pos, rot);
        if (look != null)
        {
            var model = Object.Instantiate(look, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var anim = model.GetComponent<Animator>();
            if (anim != null) anim.applyRootMotion = false;
        }
        else
        {
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = Vector3.up * 0.9f;
        }
        return root;
    }

    static void Body(GameObject root)
    {
        var c = root.AddComponent<CapsuleCollider>();
        c.center = new Vector3(0f, 0.9f, 0f); c.height = 1.8f; c.radius = 0.32f;
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;   // moved by the NavMeshAgent, still hit by bullets
    }

    static Health AddHealth(GameObject root, Team team, string name, string memberId, float hp = 100f, float armor = 0f)
    {
        var h = root.AddComponent<Health>();
        h.team = team; h.displayName = name; h.memberId = memberId;
        h.maxHp = 100f; h.hp = hp; h.armor = armor;
        return h;
    }

    public static PlayerController Player(GameObject look, Vector3 pos, Quaternion rot, Camera cam, bool armed)
    {
        var root = Root(look, pos, rot, "Player");
        root.tag = "Player";
        var cc = root.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 0.9f, 0f); cc.height = 1.8f; cc.radius = 0.3f;
        cc.stepOffset = 0.4f; cc.slopeLimit = 50f;
        // NPC agents ignore CharacterControllers - a (non-carving) obstacle makes them step around you
        var ob = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        ob.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule; ob.center = new Vector3(0f, 0.9f, 0f);
        ob.radius = 0.35f; ob.height = 1.8f; ob.carving = false;
        var h = AddHealth(root, GameData.PlayerTeam, "You", "");
        h.isPlayer = true;
        if (armed) AddGun(root, 34f, 0.1f);
        var pc = root.AddComponent<PlayerController>();
        pc.cam = cam;
        root.AddComponent<Footsteps>();
        return pc;
    }

    public static SoldierAI Soldier(GameObject look, MemberState m, Vector3 pos, Quaternion rot, bool leader)
    {
        var root = Root(look, pos, rot, m.name);
        Body(root);
        var agent = root.AddComponent<NavMeshAgent>();
        agent.baseOffset = 0f;
        AddHealth(root, m.team, m.name, m.id, m.hp, m.armor);
        bool crew = m.team == GameData.PlayerTeam;
        AddGun(root, crew ? 16f : 6.5f, crew ? 0.13f : 0.17f);   // enemies hit softer and slower so fights last
        var ai = root.AddComponent<SoldierAI>();
        ai.team = m.team; ai.isLeader = leader;
        ai.grenades = m.team == GameData.PlayerTeam ? 2 : 1;
        root.AddComponent<Footsteps>();
        var blip = root.AddComponent<MinimapBlip>();
        blip.color = m.team == GameData.PlayerTeam ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.2f, 0.2f);
        return ai;
    }

    public static Civilian Civilian(GameObject look, Vector3 pos, Quaternion rot, string name)
    {
        var root = Root(look, pos, rot, name);
        Body(root);
        root.AddComponent<NavMeshAgent>();
        AddHealth(root, Team.Civilians, name, "", 60f);
        var c = root.AddComponent<Civilian>();
        root.AddComponent<Footsteps>();
        return c;
    }

    static void AddGun(GameObject root, float damage, float interval)
    {
        var w = root.AddComponent<Weapon>();
        w.damage = damage; w.fireInterval = interval;
        GunModel.Attach(root.GetComponentInChildren<Animator>(), w);
    }
}

// Footstep sounds synced to the animation: each foot bone is tracked and a step
// plays the moment that foot comes down onto the ground (falls back to distance
// walked for models without a humanoid rig).
public class Footsteps : MonoBehaviour
{
    public float stride = 0.8f;
    Vector3 _last; float _acc;
    Health _h;
    Transform[] _feet;
    float[] _floor = new float[2];
    bool[] _up = new bool[2];
    float[] _next = new float[2];

    void Start()
    {
        _last = transform.position; _h = GetComponent<Health>();
        var a = GetComponentInChildren<Animator>();
        if (a != null && a.isHuman)
        {
            var l = a.GetBoneTransform(HumanBodyBones.LeftFoot); var r = a.GetBoneTransform(HumanBodyBones.RightFoot);
            if (l != null && r != null) _feet = new[] { l, r };
        }
        for (int i = 0; i < 2; i++) _floor[i] = float.MaxValue;
    }

    void LateUpdate()   // after the animator has posed the feet
    {
        Vector3 p = transform.position;
        Vector3 d = p - _last; d.y = 0f; _last = p;
        float moved = d.magnitude;
        if (moved > 1f || (_h != null && _h.Dead)) return;   // teleport / dead
        float speed = moved / Mathf.Max(Time.deltaTime, 0.0001f);
        bool player = _h != null && _h.isPlayer;

        if (_feet == null)
        {
            _acc += moved;
            if (_acc >= stride) { _acc = 0f; Step(p, player); }
            return;
        }
        for (int i = 0; i < 2; i++)
        {
            float y = transform.InverseTransformPoint(_feet[i].position).y;
            // lowest point the foot reaches = the ground; let it creep up slowly in case of slopes / crouch
            _floor[i] = Mathf.Min(_floor[i] + 0.05f * Time.deltaTime, y);
            if (y > _floor[i] + 0.07f) _up[i] = true;
            else if (_up[i] && y < _floor[i] + 0.03f)
            {
                _up[i] = false;
                if (speed > 0.4f && Time.time >= _next[i]) { _next[i] = Time.time + 0.2f; Step(_feet[i].position, player); }
            }
        }
    }

    public static int Count;   // steps played (debug: FpsProbe reports it)
    static void Step(Vector3 at, bool player)
    {
        // nobody hears a step 25 m away: don't spend a sound on it
        var cam = Camera.main;
        if (!player && cam != null && (cam.transform.position - at).sqrMagnitude > 25f * 25f) return;
        Count++; Play(at, player);
    }
    static void Play(Vector3 at, bool player) => Sfx.Play("step" + Random.Range(1, 6), at, player ? 0.45f : 0.3f, 0.08f, 25f);
}
