using UnityEngine;

// Natural ground speeds (m/s) of the walk / run clips, measured from the
// animations' foot movement by the editor builder (HeistAnim). Movement code
// uses these so characters travel exactly as fast as their legs move - no
// moonwalking or ice-skating.
public class GaitSettings : ScriptableObject
{
    public float armedWalk = 1.4f, armedRun = 3.8f;
    public float civWalk = 1.3f, civRun = 4.5f;

    static GaitSettings s_i;
    public static GaitSettings I
    {
        get
        {
            if (s_i == null) s_i = Resources.Load<GaitSettings>("Gait");
            if (s_i == null) s_i = CreateInstance<GaitSettings>();
            return s_i;
        }
    }

    // animator playback rate so the clip keeps pace with the actual speed:
    // between walk and run the blend tree handles it; beyond run, speed up
    public static float PlaybackRate(float speed, float runSpeed) =>
        speed > runSpeed ? Mathf.Clamp(speed / runSpeed, 1f, 1.8f) : 1f;
}
