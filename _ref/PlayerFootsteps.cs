using UnityEngine;

// HW3 - plays a footstep sound as the player walks: one random step clip every
// `stride` metres of ground travel (so fast/slow walking stays in sync), with a
// little pitch variation so repeated steps don't sound robotic. Ignores the
// big jump when VillageSpawns teleports the player on scene load.
[RequireComponent(typeof(AudioSource))]
public class PlayerFootsteps : MonoBehaviour
{
    public AudioClip[] clips;
    public float stride = 0.75f;
    public float volume = 0.7f;

    AudioSource _src;
    CharacterController _cc;
    Vector3 _last;
    float _acc;

    void Start()
    {
        _src = GetComponent<AudioSource>();
        _cc = GetComponent<CharacterController>();
        _last = transform.position;
    }

    void Update()
    {
        Vector3 p = transform.position;
        Vector3 d = p - _last; d.y = 0f;
        _last = p;

        float dist = d.magnitude;
        if (dist > 1f) { _acc = 0f; return; }              // teleport/spawn, not a step
        bool grounded = _cc == null || _cc.isGrounded;
        if (!grounded || dist < 0.0005f) return;

        _acc += dist;
        if (_acc < stride || clips == null || clips.Length == 0) return;
        _acc = 0f;
        _src.pitch = Random.Range(0.92f, 1.08f);
        _src.PlayOneShot(clips[Random.Range(0, clips.Length)], volume);
    }
}
