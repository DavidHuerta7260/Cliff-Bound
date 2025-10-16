using UnityEngine;

public class PlayerClimbing3 : MonoBehaviour
{
    public enum PlayerState { WALKING, FALLING, CLIMBING }
    public PlayerState state = PlayerState.WALKING;

    [Header("Refs")]
    public Transform model;
    Rigidbody rb;
    Animator anim;

    [Header("Speeds")]
    public float walkSpeed = 3f;
    public float climbSpeed = 2f;

    [Header("Jump / Charge")]
    public float jumpForce = 5f;
    public float maxChargedJumpForce = 9f;
    public float chargeDuration = 0.6f;
    public float crouchEnterDelay = 0.12f;

    [Header("Air Control")]
    [Range(0f, 1f)] public float airControl = 0.7f;
    public float fallTurnSpeed = 10f;
    public int maxAirJumps = 1;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.6f;
    public float groundCheckYOffset = 0.1f;
    public float firmGroundYVel = 0.05f;

    [Header("Startup")]
    public float spawnSuppressTime = 0.35f;

    [Header("Climb Detect (improves LMB-from-ground)")]
    public float climbDetectRadius = 0.25f;   // fatter than a ray
    public float climbDetectDistance = 0.9f;  // how far forward to search
    public float climbDetectUpOffset = 0.9f;  // chest/head height
    public float climbDetectDownOffset = 0.3f;// hips height
    public LayerMask climbableLayers = ~0;    // by default: everything

    [Header("Climb Enter/Exit")]
    public string climbEnterClip = "Rig|Climb_Enter";
    public float climbEnterLockTime = 0.35f;
    public string climbExitClip = "Rig|Climb_Exit";
    public float climbExitLockTime = 0.35f;
    public float exitFloorCheckDist = 0.25f;
    [Range(0f, 1f)] public float exitMinUpDot = 0.6f;

    // Animator params / clips
    const string P_STATE = "State";   // 0 walk, 1 fall, 2 climb
    const string P_GROUNDED = "Grounded";
    const string P_READY = "Ready";
    const string P_CHARGING = "Charging";
    const string P_CHARGEAMOUNT = "ChargeAmount";
    const string P_CHARGERELEASE = "ChargeRelease";
    const string P_H = "H";
    const string P_V = "V";
    const string C_CROUCHIDLE = "Rig|Crouch_Idle_Loop";
    const string C_JUMPLOOP = "Rig|Jump_Loop";

    // runtime flags
    bool isClimbEntering = false; float climbEnterTimer = 0f;
    bool isClimbExiting = false; float climbExitTimer = 0f; Vector3 exitTargetPoint;

    int airJumpsLeft; float spawnTimer;
    float h, v; bool jumpDown, jumpHeld, jumpUp, leftClickDown;

    // charge (no crouch-move; holding Space freezes WASD)
    bool consideringCharge, isCharging, passedCrouchDelay, showedCrouchIdle; float chargeTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        anim = (model ? model.GetComponent<Animator>() : null) ?? GetComponentInChildren<Animator>();

        state = PlayerState.WALKING;
        if (anim)
        {
            anim.Rebind(); anim.Update(0f);
            anim.SetInteger(P_STATE, 0);
            anim.SetBool(P_READY, false);
            anim.SetBool(P_GROUNDED, true);
            anim.SetBool(P_CHARGING, false);
            anim.SetFloat(P_CHARGEAMOUNT, 0f);
        }
        airJumpsLeft = maxAirJumps;
    }

    void Update()
    {
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");
        if (!jumpDown) jumpDown = Input.GetButtonDown("Jump");
        jumpHeld = Input.GetButton("Jump");
        if (!jumpUp) jumpUp = Input.GetButtonUp("Jump");
        if (!leftClickDown) leftClickDown = Input.GetMouseButtonDown(0);
    }

    void FixedUpdate()
    {
        spawnTimer += Time.fixedDeltaTime;

        Transform cam = Camera.main ? Camera.main.transform : transform;
        Vector2 input2 = SquareToCircle(new Vector2(h, v));
        Vector3 moveDir = Quaternion.FromToRotation(cam.up, Vector3.up) *
                          cam.TransformDirection(new Vector3(input2.x, 0f, input2.y));

        bool grounded = Physics.Raycast(transform.position + Vector3.up * groundCheckYOffset, Vector3.down, groundCheckDistance);
        bool firmGrounded = grounded && rb.velocity.y <= firmGroundYVel;
        if (anim) anim.SetBool(P_GROUNDED, firmGrounded);

        // startup grace
        if (spawnTimer < spawnSuppressTime)
        {
            state = PlayerState.WALKING;
            if (anim)
            {
                anim.SetInteger(P_STATE, 0);
                anim.SetBool(P_READY, false);
                anim.SetBool(P_GROUNDED, true);
                anim.SetBool(P_CHARGING, false);
                anim.SetFloat(P_CHARGEAMOUNT, 0f);
                anim.ResetTrigger(P_CHARGERELEASE);
            }
            Vector3 gv = rb.velocity; gv.x = moveDir.x * walkSpeed; gv.z = moveDir.z * walkSpeed; rb.velocity = gv;
            ResetFrameInputs();
            return;
        }
        else if (anim) anim.SetBool(P_READY, true);

        if (!firmGrounded) // no charge visuals while airborne
        { consideringCharge = false; isCharging = false; passedCrouchDelay = false; chargeTimer = 0f; showedCrouchIdle = false; KillChargeVisuals(); }

        // ===== LMB: Enter/Exit climb (GROUND: allow enter without jumping) =====
        if (leftClickDown && !isClimbExiting)
        {
            if (state == PlayerState.CLIMBING)
            {
                state = PlayerState.WALKING; airJumpsLeft = maxAirJumps;
                isClimbEntering = false; KillChargeVisuals();
            }
            else
            {
                // NEW: robust wall find (capsule/sphere cast) so you can click from ground
                RaycastHit wallHit;
                if (FindWall(out wallHit))
                {
                    // Snap & face wall
                    transform.forward = Vector3.Lerp(transform.forward, -wallHit.normal, 1f);
                    rb.position = wallHit.point + wallHit.normal * 0.05f;

                    state = PlayerState.CLIMBING;

                    if (firmGrounded)
                    {
                        // Grounded: play enter
                        if (anim) anim.CrossFade(climbEnterClip, 0.05f, 0);
                        isClimbEntering = true;
                        climbEnterTimer = Mathf.Max(0.1f, climbEnterLockTime);
                    }
                    else
                    {
                        // Air latch: no enter
                        isClimbEntering = false;
                    }
                }
            }
        }
        // ======================================================================

        // ===== CHARGE JUMP (holding Space freezes WASD) =====
        if (state == PlayerState.WALKING)
        {
            if (jumpDown && firmGrounded && !consideringCharge && !isCharging)
            { consideringCharge = true; chargeTimer = 0f; passedCrouchDelay = false; showedCrouchIdle = false; }

            if (consideringCharge)
            {
                chargeTimer += Time.fixedDeltaTime;
                if (jumpUp)
                {
                    if (chargeTimer < crouchEnterDelay) { KillChargeVisuals(); DoJump(jumpForce); }
                    else
                    {
                        float t = Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, chargeDuration));
                        BeginChargeVisuals(t); DoChargedJump(Mathf.Lerp(jumpForce, maxChargedJumpForce, t));
                    }
                    consideringCharge = false;
                }
                else if (!passedCrouchDelay && chargeTimer >= crouchEnterDelay && firmGrounded)
                {
                    passedCrouchDelay = true; isCharging = true;
                    float t = Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, chargeDuration));
                    BeginChargeVisuals(t);
                    consideringCharge = false;
                }
            }

            if (isCharging && firmGrounded)
            {
                chargeTimer += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(chargeTimer / Mathf.Max(0.0001f, chargeDuration));
                UpdateChargeVisuals(t);

                // Freeze horizontal while holding Space
                FreezeHorizontal();

                if (!jumpHeld || jumpUp)
                {
                    float jf = Mathf.Lerp(jumpForce, maxChargedJumpForce, t);
                    DoChargedJump(jf);
                    isCharging = false; passedCrouchDelay = false; chargeTimer = 0f; showedCrouchIdle = false; airJumpsLeft = maxAirJumps;
                }
            }
        }
        else
        {
            consideringCharge = false; isCharging = false; passedCrouchDelay = false; chargeTimer = 0f; showedCrouchIdle = false;
            KillChargeVisuals();
        }

        // main states
        if (state == PlayerState.WALKING) DoWalking(moveDir);
        else if (state == PlayerState.FALLING) DoFalling(moveDir);
        else if (state == PlayerState.CLIMBING) DoClimbing(input2);

        // transitions outside climbing
        if (state != PlayerState.CLIMBING)
        {
            if (firmGrounded && rb.velocity.y <= 0.01f) { state = PlayerState.WALKING; airJumpsLeft = maxAirJumps; }
            else if (!firmGrounded && state == PlayerState.WALKING) { state = PlayerState.FALLING; }
        }

        if (anim) anim.SetInteger(P_STATE, (int)state);
        ResetFrameInputs();
    }

    // ---------- Movement Handlers ----------
    void DoWalking(Vector3 moveDir)
    {
        // If charging (or still holding during consider), keep still
        bool frozen = (isCharging || (consideringCharge && jumpHeld));
        if (frozen) { FreezeHorizontal(); if (anim) anim.SetFloat("V", 0f); return; }

        if (anim) anim.SetFloat("V", moveDir.magnitude);

        Vector3 v3 = rb.velocity; v3.x = moveDir.x * walkSpeed; v3.z = moveDir.z * walkSpeed; rb.velocity = v3;

        Vector3 planar = new Vector3(moveDir.x, 0f, moveDir.z);
        if (planar.sqrMagnitude > 0.0001f)
            transform.forward = Vector3.Slerp(transform.forward, planar.normalized, 10f * Time.fixedDeltaTime);
    }

    void DoFalling(Vector3 moveDir)
    {
        if (jumpDown && airJumpsLeft > 0) { DoJump(jumpForce); airJumpsLeft--; }

        Vector3 planar = new Vector3(moveDir.x, 0f, moveDir.z);
        Vector3 vHoriz = planar.normalized * (walkSpeed * airControl);
        Vector3 vel = rb.velocity; vel.x = vHoriz.x; vel.z = vHoriz.z; rb.velocity = vel;

        if (planar.sqrMagnitude > 0.0001f)
            transform.forward = Vector3.Slerp(transform.forward, planar.normalized, fallTurnSpeed * Time.fixedDeltaTime);
    }

    void DoClimbing(Vector2 input)
    {
        if (anim) { anim.SetFloat(P_H, input.x); anim.SetFloat(P_V, input.y); }

        // sample wall normal around us
        Vector3 offset = transform.TransformDirection(Vector2.one * 0.5f);
        Vector3 sumN = Vector3.zero; int hits = 0;
        for (int i = 0; i < 4; i++)
        {
            RaycastHit ch;
            if (Physics.Raycast(transform.position + offset, transform.forward, out ch)) { sumN += ch.normal; hits++; }
            offset = Quaternion.AngleAxis(90f, transform.forward) * offset;
        }
        if (hits > 0) sumN /= hits;

        RaycastHit hinfo;
        if (Physics.Raycast(transform.position, -sumN, out hinfo))
        {
            // stick + face wall
            rb.position = Vector3.Lerp(rb.position, hinfo.point + hinfo.normal * 0.05f, 5f * Time.fixedDeltaTime);
            transform.forward = Vector3.Lerp(transform.forward, -hinfo.normal, 10f * Time.fixedDeltaTime);

            // AUTO EXIT when very close to floor
            if (!isClimbEntering && !isClimbExiting)
            {
                RaycastHit groundHit;
                bool nearFloor = Physics.Raycast(transform.position, Vector3.down, out groundHit, exitFloorCheckDist + 0.05f);
                bool goodFloor = nearFloor && Vector3.Dot(groundHit.normal, Vector3.up) >= exitMinUpDot;
                if (goodFloor)
                {
                    if (anim) anim.CrossFade(climbExitClip, 0.05f, 0);
                    isClimbExiting = true; climbExitTimer = Mathf.Max(0.1f, climbExitLockTime);
                    exitTargetPoint = groundHit.point;
                }
            }

            if (isClimbExiting)
            {
                rb.useGravity = false; rb.velocity = Vector3.zero;
                rb.position = Vector3.Lerp(rb.position, exitTargetPoint, 8f * Time.fixedDeltaTime);
                climbExitTimer -= Time.fixedDeltaTime;
                if (climbExitTimer <= 0f)
                { state = PlayerState.WALKING; isClimbExiting = false; rb.useGravity = true; rb.position += (-hinfo.normal) * 0.02f; }
                return;
            }

            rb.useGravity = false;

            // freeze during enter
            if (isClimbEntering)
            {
                climbEnterTimer -= Time.fixedDeltaTime;
                if (climbEnterTimer <= 0f) isClimbEntering = false;
                rb.velocity = Vector3.zero;
                return;
            }

            // normal climb movement
            Vector3 local = new Vector3(input.x, input.y, 0f);
            Vector3 world = transform.TransformDirection(local);
            world = Vector3.ProjectOnPlane(world, hinfo.normal);
            rb.velocity = world * climbSpeed;

            if (jumpDown)
            {
                rb.velocity = Vector3.up * jumpForce + hinfo.normal * 2f;
                state = PlayerState.FALLING;
                airJumpsLeft = maxAirJumps;
                isClimbEntering = false; isClimbExiting = false;
            }
        }
        else
        {
            state = PlayerState.FALLING; isClimbEntering = false; isClimbExiting = false;
        }
    }

    // ---------- Helpers ----------
    bool FindWall(out RaycastHit hit)
    {
        // Capsule from hips to head, cast forward with small radius
        Vector3 origin = transform.position;
        Vector3 p1 = origin + Vector3.up * climbDetectDownOffset; // hips
        Vector3 p2 = origin + Vector3.up * climbDetectUpOffset;   // chest/head
        Vector3 dir = transform.forward;

        // CapsuleCast for wide detection; fall back to SphereCast then Raycast
        if (Physics.CapsuleCast(p1, p2, climbDetectRadius, dir, out hit, climbDetectDistance, climbableLayers, QueryTriggerInteraction.Ignore))
            return true;

        Vector3 sphereStart = origin + Vector3.up * ((climbDetectDownOffset + climbDetectUpOffset) * 0.5f);
        if (Physics.SphereCast(sphereStart, climbDetectRadius, dir, out hit, climbDetectDistance, climbableLayers, QueryTriggerInteraction.Ignore))
            return true;

        return Physics.Raycast(sphereStart, dir, out hit, climbDetectDistance, climbableLayers, QueryTriggerInteraction.Ignore);
    }

    void FreezeHorizontal()
    {
        Vector3 v3 = rb.velocity; v3.x = 0f; v3.z = 0f; rb.velocity = v3;
    }

    void DoJump(float yForce)
    {
        Vector3 v3 = rb.velocity; v3.y = yForce; rb.velocity = v3;
        state = PlayerState.FALLING;
        if (anim) anim.SetInteger(P_STATE, 1);
        KillChargeVisuals();
        isClimbEntering = false; isClimbExiting = false;
    }

    void DoChargedJump(float yForce)
    {
        if (anim) { anim.SetBool(P_CHARGING, false); anim.ResetTrigger(P_CHARGERELEASE); anim.SetTrigger(P_CHARGERELEASE); }
        DoJump(yForce);
    }

    void BeginChargeVisuals(float t)
    {
        if (!anim) return;
        anim.SetBool(P_CHARGING, true);
        anim.SetFloat(P_CHARGEAMOUNT, t);
        if (!showedCrouchIdle) { anim.CrossFade(C_CROUCHIDLE, 0.05f, 0); showedCrouchIdle = true; }
    }
    void UpdateChargeVisuals(float t)
    {
        if (!anim) return;
        anim.SetBool(P_CHARGING, true);
        anim.SetFloat(P_CHARGEAMOUNT, t);
    }
    void KillChargeVisuals()
    {
        if (!anim) return;
        anim.SetBool(P_CHARGING, false);
        anim.SetFloat(P_CHARGEAMOUNT, 0f);
        anim.ResetTrigger(P_CHARGERELEASE);
        showedCrouchIdle = false;
        if (state == PlayerState.FALLING) anim.CrossFade(C_JUMPLOOP, 0.05f, 0);
    }

    Vector2 SquareToCircle(Vector2 p) { return (p.sqrMagnitude > 1f) ? p.normalized : p; }

    void ResetFrameInputs() { jumpDown = false; jumpUp = false; leftClickDown = false; }

    // (kept for compatibility if something else reads it)
    public float rayLength
    {
        get { return (state == PlayerState.CLIMBING) ? 0.05f : 1f; }
        set { }
    }
}


















