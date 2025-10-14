using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerClimbing3 : MonoBehaviour
{
    public float rayLength;

    public enum PlayerState { WALKING, FALLING, CLIMBING }

    [SerializeField] public PlayerState state = PlayerState.WALKING;

    [Header("Speeds")]
    [SerializeField] float walkSpeed = 3f;
    [SerializeField] float climbSpeed = 2f;
    [SerializeField] Transform model;

    [Header("Jump / Air")]
    [SerializeField] float jumpForce = 5f;               // base jump
    [SerializeField] float maxChargedJumpForce = 9f;     // hold Space to reach this
    [SerializeField] float chargeDuration = 0.6f;        // time to max charge
    [SerializeField, Range(0f, 1f)] float airControl = 0.7f;
    [SerializeField] float fallTurnSpeed = 10f;

    [Header("Double Jump")]
    [SerializeField, Min(0)] int maxAirJumps = 1;        // 1 = classic double jump
    int airJumpsLeft = 0;

    [Header("Ground Check")]
    [SerializeField] float groundCheckDistance = 0.6f;
    [SerializeField] float groundCheckYOffset = 0.1f;

    [Header("Spawn Anti-Fall")]
    [SerializeField] float spawnSuppressTime = 0.35f;    // force WALKING this long after start
    float spawnTimer = 0f;

    // Animator param names (so you can rename in controller if needed)
    const string PARAM_STATE = "State";
    const string PARAM_READY = "Ready";

    Rigidbody rb;
    Animator anim;

    float h = 0f, v = 0f;

    // Jump input flags
    bool jumpDown = false;   // pressed this frame
    bool jumpHeld = false;   // currently held
    bool jumpUp = false;     // released this frame

    // Left-click climb toggle
    bool leftClickDown = false;

    // Charge jump state
    bool isCharging = false;
    float chargeTimer = 0f;

    // Track when we've armed the Animator to allow jumps
    bool armedAnimator = false;

    void Awake()
    {
        // Get Animator as early as possible (before first Animator evaluation)
        anim = (model ? model.GetComponent<Animator>() : null) ?? GetComponentInChildren<Animator>();

        // Hard-start as WALKING so Animator never sees FALLING on frame 0
        state = PlayerState.WALKING;

        if (anim)
        {
            anim.Rebind();           // reset graph to default state
            anim.Update(0f);         // apply immediately (frame 0)
            anim.SetInteger(PARAM_STATE, 0);
            // Gate jump transitions until we say so
            anim.SetBool(PARAM_READY, false);
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (!anim) anim = (model ? model.GetComponent<Animator>() : null) ?? GetComponentInChildren<Animator>();
        airJumpsLeft = maxAirJumps;
        armedAnimator = false; // will set Ready=true after the suppress window
    }

    void Update()
    {
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");

        // Jump inputs
        if (!jumpDown) jumpDown = Input.GetButtonDown("Jump");
        jumpHeld = Input.GetButton("Jump");
        if (!jumpUp) jumpUp = Input.GetButtonUp("Jump");

        // Climb toggle
        if (!leftClickDown) leftClickDown = Input.GetMouseButtonDown(0);
    }

    void FixedUpdate()
    {
        spawnTimer += Time.fixedDeltaTime;

        Vector2 input = SquareToCircle(new Vector2(h, v));
        Transform cam = Camera.main ? Camera.main.transform : transform;
        Vector3 moveDirection =
            Quaternion.FromToRotation(cam.up, Vector3.up) *
            cam.TransformDirection(new Vector3(input.x, 0f, input.y));

        // Ground probe
        Vector3 groundOrigin = transform.position + Vector3.up * groundCheckYOffset;
        bool grounded = Physics.Raycast(groundOrigin, Vector3.down, groundCheckDistance);

        // -------- SPAWN SUPPRESSION: lock to WALKING & block Jump transitions --------
        if (spawnTimer < spawnSuppressTime)
        {
            state = PlayerState.WALKING;
            if (anim)
            {
                anim.SetInteger(PARAM_STATE, 0);
                anim.SetBool(PARAM_READY, false); // DO NOT allow jump chain yet
            }

            // Optional: allow horizontal movement during grace window
            Vector3 v3 = rb.velocity;
            v3.x = moveDirection.x * walkSpeed;
            v3.z = moveDirection.z * walkSpeed;
            rb.velocity = v3;

            rb.useGravity = true;
            rayLength = 1f;

            // clear one-frame inputs so we don't queue actions during suppression
            jumpDown = false; jumpUp = false; leftClickDown = false; isCharging = false;
            return; // skip the rest this frame
        }
        else if (!armedAnimator)
        {
            // Arm the Animator exactly once after the window
            if (anim) anim.SetBool(PARAM_READY, true);
            armedAnimator = true;
        }
        // ---------------------------------------------------------------------------

        // Left-Click climb toggle BEFORE state machine
        if (leftClickDown)
        {
            if (state == PlayerState.CLIMBING)
            {
                state = PlayerState.WALKING;             // let go
                airJumpsLeft = maxAirJumps;              // reset air jumps on exit
                isCharging = false;                      // cancel any charge
            }
            else
            {
                RaycastHit wallHit;
                if (IsNearWall(out wallHit))
                {
                    state = PlayerState.CLIMBING;
                    isCharging = false;
                }
            }
        }

        // ---- CHARGE JUMP (ground only) ----
        if (state == PlayerState.WALKING)
        {
            // Start charging on initial press
            if (jumpDown && grounded && !isCharging)
            {
                isCharging = true;
                chargeTimer = 0f;
            }

            // Continue charging while grounded and held
            if (isCharging && grounded && jumpHeld)
            {
                chargeTimer += Time.fixedDeltaTime;
            }

            // Release (or walked off a ledge while holding) -> perform jump
            if (isCharging && (!jumpHeld || !grounded))
            {
                float t = Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, chargeDuration));
                float force = Mathf.Lerp(jumpForce, maxChargedJumpForce, t);
                PerformJump(force);                      // sets FALLING immediately
                isCharging = false;
                airJumpsLeft = maxAirJumps;              // refill air jumps after takeoff
            }
        }
        else
        {
            isCharging = false; // cancel if not walking
        }

        // ---- STATE MACHINE ----
        switch (state)
        {
            case PlayerState.WALKING: HandleWalking(moveDirection); break;
            case PlayerState.FALLING: HandleFalling(moveDirection); break;
            case PlayerState.CLIMBING: HandleClimbing(input); break;
        }

        // ---- GROUNDED TOGGLES ----
        if (state != PlayerState.CLIMBING)
        {
            if (grounded && rb.velocity.y <= 0.01f)
            {
                state = PlayerState.WALKING;
                airJumpsLeft = maxAirJumps;
            }
            else if (!grounded && state == PlayerState.WALKING)
            {
                state = PlayerState.FALLING;
            }
        }

        rb.useGravity = state != PlayerState.CLIMBING;
        rayLength = (state == PlayerState.CLIMBING) ? 0.05f : 1f;

        // Set Animator after final state is decided
        if (anim) anim.SetInteger(PARAM_STATE, (int)state);

        // reset one-frame inputs
        jumpDown = false;
        jumpUp = false;
        leftClickDown = false;
    }

    void HandleWalking(Vector3 moveDirection)
    {
        if (anim) anim.SetFloat("V", moveDirection.magnitude);

        Vector3 v3 = rb.velocity;
        v3.x = moveDirection.x * walkSpeed;
        v3.z = moveDirection.z * walkSpeed;

        // (charge jump is handled in FixedUpdate)
        rb.velocity = v3;

        // Face movement on ground
        Vector3 planar = new Vector3(moveDirection.x, 0f, moveDirection.z);
        if (planar.sqrMagnitude > 0.0001f)
        {
            Vector3 face = Vector3.Slerp(transform.forward, planar.normalized, 10f * Time.fixedDeltaTime);
            transform.forward = face;
        }
    }

    // Air control + (optional) double jump
    void HandleFalling(Vector3 moveDirection)
    {
        // Double jump tap in air
        if (jumpDown && airJumpsLeft > 0)
        {
            PerformJump(jumpForce);
            airJumpsLeft--;
        }

        // steer in air
        Vector3 planar = new Vector3(moveDirection.x, 0f, moveDirection.z);
        Vector3 vHoriz = planar.normalized * (walkSpeed * airControl);
        Vector3 vel = rb.velocity;
        vel.x = vHoriz.x;
        vel.z = vHoriz.z;
        rb.velocity = vel;

        // face input while falling
        if (planar.sqrMagnitude > 0.0001f)
        {
            Vector3 look = Vector3.Slerp(transform.forward, planar.normalized, fallTurnSpeed * Time.fixedDeltaTime);
            transform.forward = look;
        }
    }

    void HandleClimbing(Vector2 input)
    {
        if (anim)
        {
            anim.SetFloat("H", input.x);
            anim.SetFloat("V", input.y);
        }

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
            offset = Quaternion.AngleAxis(90f, transform.forward) * offset;
        }
        if (k > 0) checkDirection /= k;

        // Check wall directly in front (unchanged)
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -checkDirection, out hit))
        {
            rb.position = Vector3.Lerp(rb.position, hit.point + hit.normal * 0.05f, 5f * Time.fixedDeltaTime);
            transform.forward = Vector3.Lerp(transform.forward, -hit.normal, 10f * Time.fixedDeltaTime);

            rb.useGravity = false;

            // Local X = left/right, Local Y = up/down; keep out-of-wall component zeroed
            Vector3 climbLocal = new Vector3(input.x, input.y, 0f);
            Vector3 climbWorld = transform.TransformDirection(climbLocal);
            climbWorld = Vector3.ProjectOnPlane(climbWorld, hit.normal);
            rb.velocity = climbWorld * climbSpeed;

            // Push-off from wall with Jump
            if (jumpDown)
            {
                rb.velocity = Vector3.up * jumpForce + hit.normal * 2f;
                state = PlayerState.FALLING;
                airJumpsLeft = maxAirJumps; // allow air jumps after push-off
            }
        }
        else
        {
            state = PlayerState.FALLING;
        }
    }

    void PerformJump(float forceY)
    {
        Vector3 v3 = rb.velocity;
        v3.y = forceY;
        rb.velocity = v3;
        state = PlayerState.FALLING; // Animator sees this immediately
        if (anim) anim.SetInteger(PARAM_STATE, (int)PlayerState.FALLING);
    }

    bool IsNearWall(out RaycastHit wallHit)
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        float dist = Mathf.Max(0.1f, (state == PlayerState.CLIMBING) ? 0.3f : 1.0f);
        return Physics.Raycast(origin, transform.forward, out wallHit, dist);
    }

    Vector2 SquareToCircle(Vector2 input)
    {
        return (input.sqrMagnitude >= 1f) ? input.normalized : input;
    }
}





