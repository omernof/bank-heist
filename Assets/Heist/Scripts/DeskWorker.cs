using UnityEngine;

// A bank employee at a back-office desk (the downloaded "Office Worker" models,
// which come with their own typing animation). Types away in a loop; once the
// robbery starts they freeze where they sit.
public class DeskWorker : MonoBehaviour
{
    Animator _anim;
    bool _frozen;

    void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        if (_anim != null) _anim.applyRootMotion = false;
    }

    void Update()
    {
        if (_anim == null || _frozen) return;
        if (GameData.phase >= Phase.Breached) { _frozen = true; _anim.speed = 0f; return; }
        // the imported clip doesn't loop on its own
        var st = _anim.GetCurrentAnimatorStateInfo(0);
        if (st.normalizedTime >= 0.98f) _anim.Play(st.fullPathHash, 0, 0f);
    }
}
