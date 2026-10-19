using UnityEngine;

// A named spawn location. ids: "start", "fromBank", "fromCity", "fromVault",
// "queue", "cops", or a member id ("R2", "P1", ...) for where that NPC waits.
public class SpawnPoint : MonoBehaviour
{
    public string id = "start";

    public static SpawnPoint Find(string id)
    {
        foreach (var s in FindObjectsByType<SpawnPoint>())
            if (s.id == id) return s;
        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = id.StartsWith("R") ? Color.red : id.StartsWith("P") ? Color.blue : Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        Gizmos.DrawLine(transform.position + Vector3.up, transform.position + Vector3.up + transform.forward);
    }
}
