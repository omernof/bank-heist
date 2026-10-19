using UnityEngine;

// Simple city traffic: drives along a straight lane at a steady speed, brakes
// for anything in front (people, cars, the police roadblock) and wraps around
// at the end of the street so the avenue always has cars moving on it.
public class TrafficCar : MonoBehaviour
{
    public Vector3 dir = Vector3.right;     // lane direction (world)
    public float cruise = 9f;
    public float minX = -150f, maxX = 150f; // wrap range along the lane
    float _speed;

    void Start() { _speed = cruise; transform.rotation = Quaternion.LookRotation(dir); }

    void Update()
    {
        float want = cruise;
        Vector3 nose = transform.position + Vector3.up * 0.8f + dir * 2.8f;
        if (Physics.SphereCast(nose, 0.9f, dir, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(transform))
            want = hit.distance < 4f ? 0f : cruise * Mathf.Clamp01((hit.distance - 4f) / 8f);
        if (Hud.Blocking) want = 0f;
        _speed = Mathf.MoveTowards(_speed, want, (want < _speed ? 14f : 4f) * Time.deltaTime);
        transform.position += dir * _speed * Time.deltaTime;

        float x = Vector3.Dot(transform.position, dir);
        if (x > maxX) transform.position -= dir * (maxX - minX);
    }
}

