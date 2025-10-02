using UnityEngine;

public class CameraFollowScript : MonoBehaviour
{
    public Transform target;       // who we follow
    public Vector3 pivot;          // small offset from target (e.g. (0,1.5,0))
    public Vector3 offset;         // starting offset from target

    public float sensitivityX = 4f; // mouse look speed (left/right)
    public float sensitivityY = 4f; // mouse look speed (up/down)
    public float minPitch = -70f;   // how far we can look down
    public float maxPitch = 70f;    // how far we can look up
    public float followSmooth = 0.15f; // how quickly camera moves to spot

    float yaw;     // horizontal angle around target
    float pitch;   // vertical angle
    float distance; // how far the camera stays from target

    void Start()
    {
        // lock + hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // figure out yaw/pitch from the starting offset
        distance = offset.magnitude;
        if (distance < 0.0001f) distance = 3f; // just in case

        Vector3 dir = offset.normalized; // direction from target to camera
        yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        pitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void Update()
    {
        // keep cursor locked/hidden (helps when tabbing in/out of game window)
        if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
        if (Cursor.visible) Cursor.visible = false;

        // read mouse and update angles
        float mx = Input.GetAxis("Mouse X") * sensitivityX;
        float my = Input.GetAxis("Mouse Y") * sensitivityY;

        yaw += mx;
        pitch -= my; // minus so moving mouse up looks up
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // build a new offset from yaw/pitch (simple orbit)
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        offset = rot * new Vector3(0f, 0f, -distance);
    }

    void LateUpdate()
    {
        // move camera toward target + pivot + offset
        Vector3 wantedPos = target.position + pivot + offset;
        transform.position = Vector3.Lerp(transform.position, wantedPos, followSmooth);

        // look at the target (plus pivot so we look at the head/chest)
        transform.LookAt(target.position + pivot);
    }
}
