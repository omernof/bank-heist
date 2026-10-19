using UnityEngine;

// Anything the player can use with E: loot, the vault door, the getaway car
// (F), doors between scenes. holdSeconds > 0 makes it a "hold E" action with a
// progress bar (drilling the vault, cracking a deposit box).
public abstract class Interactable : MonoBehaviour
{
    public float range = 2.4f;
    public float holdSeconds;
    public abstract string Prompt { get; }
    public virtual bool CanUse => true;
    public abstract void Use(PlayerController p);

    public static readonly System.Collections.Generic.List<Interactable> All = new System.Collections.Generic.List<Interactable>();
    protected virtual void OnEnable() { All.Add(this); }
    protected virtual void OnDisable() { All.Remove(this); }
}
