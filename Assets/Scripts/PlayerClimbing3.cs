using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerClimbing3 : MonoBehaviour
{
    public float rayLength;
    public enum PlayerState
    {
        WALKING,
        FALLING,
        CLIMBING
    }

    [SerializeField] public PlayerState state = PlayerState.WALKING;

    [SerializeField] float walkSpeed = 3f;
    [SerializeField] float climbSpeed = 2f;
    [SerializeField] Transform model;

    [Header("Air Control")]
    [SerializeField, Range(0f, 1f)] float airControl = 0.6f; // mid-air horizontal control

    Rigidbody rb;
    Animator anim;

    float h = 0f;
    float v = 0f;
    bool jumpDown = false;
    bool leftClickDown = false; // LMB toggles climbing

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        anim = model.GetComponent<Animator>();
    }

    void Update()
    {
        // Input happens per-frame not in the Physics Loop
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");
        if (!jumpDown)
            jumpDown = Input.GetButtonDown("Jump");

        if (!leftClickDown)
            leftClickDown = Input.GetMouseButtonDown(0); // Left Click
    }

    void FixedUpdate()
    {
        Vector2 input = SquareToCircle(new Vector2(h, v));
        Transform cam = Camera.main.transform;
        Vector3 moveDirection = Quaternion.FromToRotation(cam.up, Vector3.up)
                                * cam.TransformDirection(new Vector3(input.x, 0f, input.y));

        // Handle Left-Click climb toggle BEFORE state switch
        if (leftClickDown)
        {
            if (state == PlayerState.CLIMBING)
            {
                // Let go of wall
                state = PlayerState.WALKING;
            }
            else
            {
                // Enter climb only if near a wall in front
                RaycastHit wallHit;
                if (IsNearWall(out wallHit))
                {
                    state = PlayerState.CLIMBING;
                    // (optional) snap toward wall here if desired
                }
            }
        }

        if (anim) anim.SetInteger("State", (int)state);

        switch (state)
        {
            case PlayerState.WALKING: { HandleWalking(moveDirection); } break;
            case PlayerState.FALLING: { HandleFalling(moveDirection); } break; // pass moveDirection for air control
            case PlayerState.CLIMBING: { HandleClimbing(input); } break;
        }

        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, 1.02f))
            state = PlayerState.WALKING;
        else if (state == PlayerState.WALKING)
            state = PlayerState.FALLING;

        rb.useGravity = state != PlayerState.CLIMBING;

        rayLength = (state == PlayerState.CLIMBING) ? 0.05f : 1f;

        // Reset single-frame inputs
        jumpDown = false;
        leftClickDown = false;
    }

    void HandleWalking(Vector3 moveDirection)
    {
        anim.SetFloat("V", moveDirection.magnitude);

        Vector3 oldVelo = rb.velocity;
        Vector3 newVelo = moveDirection * walkSpeed;
        newVelo.y = oldVelo.y;

        if (jumpDown)
        {
            newVelo.y = 5f;
            state = PlayerState.FALLING;
        }

        rb.velocity = newVelo;

        if (moveDirection.sqrMagnitude > 0.01f)
        {
            transform.forward = Vector3.Lerp(transform.forward,
                                             moveDirection,
                                             10f * Time.fixedDeltaTime);
        }
    }

    // Air control while falling
    void HandleFalling(Vector3 moveDirection)
    {
        Vector3 v3 = rb.velocity;
        Vector3 horiz = moveDirection * walkSpeed * airControl;
        v3.x = horiz.x;
        v3.z = horiz.z;
        rb.velocity = v3;
    }

    void HandleClimbing(Vector2 input)
    {
        anim.SetFloat("H", input.x);
        anim.SetFloat("V", input.y);

        // Check walls in a cross pattern (unchanged)
        Vector3 offset = transform.TransformDirection(Vector2.one * 0.5f);
        Vector3 checkDirection = Vector3.zero;
        int k = 0;
        for (int i = 0; i < 4; i++)
        {
            RaycastHit checkHit;
            if (Physics.Raycast(transform.position + offset, transform.forward, out checkHit))
            {
                checkDirection += checkHit.normal;
                k++;
            }
            // Rotate Offset by 90 degrees
            offset = Quaternion.AngleAxis(90f, transform.forward) * offset;
        }
        if (k > 0) checkDirection /= k;

        // Check wall directly in front (unchanged)
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -checkDirection, out hit))
        {
            float dot = Vector3.Dot(transform.forward, -hit.normal);

            rb.position = Vector3.Lerp(rb.position,
                                       hit.point + hit.normal * 0.05f,
                                       5f * Time.fixedDeltaTime);
            transform.forward = Vector3.Lerp(transform.forward,
                                             -hit.normal,
                                             10f * Time.fixedDeltaTime);

            rb.useGravity = false;

            // FIX: map climb input to LOCAL X (left/right) and LOCAL Y (up/down)
            Vector3 climbLocal = new Vector3(input.x, input.y, 0f);
            Vector3 climbWorld = transform.TransformDirection(climbLocal);

            // prevent any push into/out of wall
            climbWorld = Vector3.ProjectOnPlane(climbWorld, hit.normal);

            rb.velocity = climbWorld * climbSpeed;

            if (jumpDown)
            {
                rb.velocity = Vector3.up * 5f + hit.normal * 2f;
                state = PlayerState.FALLING;
            }
        }
        else
        {
            state = PlayerState.FALLING;
        }
    }

    // simple front wall check using rayLength (1f when not climbing)
    bool IsNearWall(out RaycastHit wallHit)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f; // lift ray to avoid floor
        float dist = Mathf.Max(0.1f, (state == PlayerState.CLIMBING) ? 0.3f : 1.0f);
        return Physics.Raycast(origin, transform.forward, out wallHit, dist);
    }

    Vector2 SquareToCircle(Vector2 input)
    {
        return (input.sqrMagnitude >= 1f) ? input.normalized : input;
    }
}
