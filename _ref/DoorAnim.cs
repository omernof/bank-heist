using System.Collections;
using UnityEngine;

// Shared door animation: swings a hinge pivot open over `seconds` with an
// ease-in/ease-out curve, and plays an optional creak at the door.
public static class DoorAnim
{
    public static IEnumerator Swing(Transform pivot, float openAngle, float seconds, AudioClip creak)
    {
        if (pivot == null) yield break;
        if (creak != null) AudioSource.PlayClipAtPoint(creak, pivot.position, 1f);
        Quaternion from = pivot.localRotation;
        Quaternion to = Quaternion.Euler(0f, openAngle, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, seconds))
        {
            pivot.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        pivot.localRotation = to;
    }
}
