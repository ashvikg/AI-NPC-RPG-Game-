using UnityEngine;

/// <summary>Hold the right mouse button to orbit, scroll to zoom. Avoids clipping into terrain and walls.</summary>
public class ThirdPersonCamera : MonoBehaviour
{
    public Transform target;
    public float Yaw { get { return yaw; } }

    float yaw = 0f, pitch = 18f, distance = 6.5f, current = 6.5f;

    public void SnapBehind(float newYaw)
    {
        yaw = newYaw; pitch = 18f;
    }

    void LateUpdate()
    {
        if (target == null) return;
        var gm = GameManager.I;
        bool paused = gm != null && gm.Paused;

        if (!paused)
        {
            if (GameInput.OrbitHeld)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Vector2 d = GameInput.LookDelta;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -8f, 70f);
            }
            else if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            distance = Mathf.Clamp(distance - GameInput.Scroll, 3f, 13f);
        }
        else if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;

        Vector3 pivot = target.position + Vector3.up * 1.6f;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 dir = rot * Vector3.back;

        float want = distance;
        RaycastHit hit;
        if (Physics.SphereCast(pivot, 0.25f, dir, out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            want = Mathf.Max(0.9f, hit.distance - 0.1f);

        float dt = Time.unscaledDeltaTime;
        current = want < current ? want : Mathf.Lerp(current, want, 1f - Mathf.Exp(-6f * dt));

        transform.position = pivot + dir * current;
        transform.rotation = rot;
    }
}
