using System.Collections;
using UnityEngine;

// The big round vault door. Robbers hold E to burn through it with thermite
// (sparks + hiss); then it swings open on its hinge and the loot is reachable.
// If the heist already got this far (cop / bystander arriving later) it starts open.
public class VaultDoor : Interactable
{
    public Transform hinge;          // rotate around Y to open
    public float openAngle = -105f;
    public Transform sparkPoint;
    public static bool IsOpen;

    bool _busy;

    void Start()
    {
        IsOpen = GameData.phase >= Phase.VaultOpen || GameData.side != Side.Robber;
        if (IsOpen && hinge != null) hinge.localRotation = Quaternion.Euler(0f, openAngle, 0f);
        holdSeconds = 5f;
    }

    public override string Prompt => "Hold E to burn through the vault door with thermite";
    public override bool CanUse => GameData.side == Side.Robber && !IsOpen && !_busy;

    public override void Use(PlayerController p) { StartCoroutine(Open()); }

    IEnumerator Open()
    {
        _busy = true;
        Hud.Notify("Thermite burning...", Hud.Warn, 3f);
        for (int i = 0; i < 16; i++)
        {
            if (sparkPoint != null) Fx.Impact(sparkPoint.position + Random.insideUnitSphere * 0.3f, -transform.forward, false);
            yield return new WaitForSeconds(0.12f);
        }
        Sfx.Play("vault_open", transform.position, 1f, 0f);
        IsOpen = true;
        // the player stands in the door's swing arc while burning it: a collider
        // sweeping through the CharacterController shoved it through the floor,
        // so the door is ghosted while it moves
        var cols = hinge.GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;
        Quaternion from = hinge.localRotation, to = Quaternion.Euler(0f, openAngle, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 3f)
        {
            hinge.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        hinge.localRotation = to;
        foreach (var c in cols) c.enabled = true;
        HeistDirector.Instance?.OnVaultOpened();
        _busy = false;
    }
}
