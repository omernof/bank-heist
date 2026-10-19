using UnityEngine;

// A doorway to another scene (street <-> bank lobby <-> vault), shown with a
// glowing GTA-style marker. The player's crew comes along; HP, money, ammo and
// everyone's state live in GameData so they carry over.
public class ScenePortal : Interactable
{
    public string targetScene = "Bank";
    public string targetSpawn = "fromCity";
    public string label = "enter the bank";
    public Phase minPhase = Phase.Approach;
    public string lockedMessage = "";

    public override string Prompt => CanUse ? $"Press E to {label}" : lockedMessage;
    public override bool CanUse => GameData.phase >= minPhase || string.IsNullOrEmpty(lockedMessage);

    public override void Use(PlayerController p)
    {
        if (GameData.phase < minPhase) return;
        HeistDirector.Instance?.GoTo(targetScene, targetSpawn);
    }

    void Update()
    {
        // bob the marker (first child) like GTA's mission markers
        if (transform.childCount > 0)
        {
            var m = transform.GetChild(0);
            m.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(Time.time * 2.5f) * 0.12f, 0f);
            m.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        }
    }
}
