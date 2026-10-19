using UnityEngine;

// Shared "is the player close enough AND looking at this thing" test, used by the
// chest, the house entrance and the locked door.
//
// Distance is measured from the PLAYER (in third-person the camera hangs a few
// metres behind, so measuring from the camera would be off). The "looking at it"
// check compares the camera's forward with the direction from the player to the
// target.
public static class InteractUtil
{
    public static bool CanInteract(ThirdPersonController player, Transform target,
                                   float maxDist, float maxAngleDeg)
    {
        if (player == null || target == null) return false;

        Vector3 toTarget = target.position - player.transform.position;
        if (toTarget.magnitude > maxDist) return false;

        Vector3 look = player.cam != null ? player.cam.transform.forward : player.transform.forward;
        // flatten both so a slightly high/low camera pitch doesn't fail the check
        look.y = 0f;
        toTarget.y = 0f;
        if (look.sqrMagnitude < 0.001f || toTarget.sqrMagnitude < 0.001f) return true;
        return Vector3.Angle(look, toTarget) <= maxAngleDeg;
    }
}
