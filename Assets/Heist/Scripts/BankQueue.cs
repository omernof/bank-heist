using System.Collections.Generic;
using UnityEngine;

// The customer line in the bank lobby: slot 0 is at the teller's window, the
// rest form a line back toward the entrance (between the rope stanchions).
// The front customer is served for a few seconds, walks out, and a new customer
// comes in from the door and joins the back - until the robbery stops it all.
public class BankQueue : MonoBehaviour
{
    public Transform[] slots;          // [0] = at the counter
    public Transform entrance, exit;
    public float serveTime = 7f;
    public bool running = true;

    readonly List<Civilian> _line = new List<Civilian>();

    public Vector3 SlotPosition(int i) => slots[Mathf.Clamp(i, 0, slots.Length - 1)].position;
    public Vector3 SlotFacing(int i)
    {
        Vector3 f = (i == 0 ? slots[0].forward : (slots[i - 1].position - slots[i].position)); f.y = 0f;
        return f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
    }

    public int Join(Civilian c)
    {
        if (!_line.Contains(c)) _line.Add(c);
        return _line.IndexOf(c);
    }

    public void Leave(Civilian c)
    {
        if (!_line.Remove(c)) return;
        for (int i = 0; i < _line.Count; i++) _line[i].SetSlot(i);   // everyone shuffles forward
    }

    // a served customer has left through the door: bring them back in as a
    // "new" customer at the end of the line (keeps the lobby busy)
    public void Respawn(Civilian c)
    {
        if (!running || _line.Count >= slots.Length) { c.gameObject.SetActive(false); return; }
        var agent = c.GetComponent<UnityEngine.AI.NavMeshAgent>();
        agent.Warp(entrance.position);
        c.JoinQueue(this, exit.position);
    }

    public void Stop()
    {
        running = false;
        foreach (var c in _line.ToArray()) if (c != null) c.GetDown();
        _line.Clear();
    }
}
