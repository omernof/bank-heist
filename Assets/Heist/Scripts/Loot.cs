using UnityEngine;

// Cash stacks / gold bars in the vault and the tellers' cash in the bank lobby.
// Press E to grab: adds to GameData.money (the brief's GOLD), which carries
// through every scene. What was taken is remembered in GameData, so it stays
// gone when you come back to the scene.
public class Loot : Interactable
{
    public int value = 25000;
    public string label = "grab the cash";
    public bool inVault = true;      // vault loot needs the door open; lobby cash just needs the robbery started

    string Key => gameObject.scene.name + "/" + name;

    void Start()
    {
        if (GameData.looted.Contains(Key)) gameObject.SetActive(false);
    }

    public override string Prompt => $"Press E to {label} (+${value:N0})";
    public override bool CanUse => GameData.side == Side.Robber && (inVault ? VaultDoor.IsOpen : GameData.phase >= Phase.Breached);

    public override void Use(PlayerController p) => Take(null);

    // the crew loot too: a robber NPC walks up, grabs it and it counts for the heist
    public SoldierAI claimedBy;
    public bool Available => isActiveAndEnabled && CanUse;

    public void Take(Health byCrew)
    {
        if (!gameObject.activeSelf) return;
        GameData.money += value;
        GameData.looted.Add(Key);
        Sfx.Play("cash", transform.position, 0.9f);
        Hud.Notify(byCrew != null ? $"{byCrew.displayName}: +${value:N0}" : $"+${value:N0}", Hud.Good, 2f);
        Debug.Log($"LOOT {name} by {(byCrew != null ? byCrew.displayName : "player")} total {GameData.money}");
        HeistDirector.Instance?.OnLooted();
        gameObject.SetActive(false);
    }
}
