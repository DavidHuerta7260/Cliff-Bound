using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem; // New Input System
#endif

public class SlowTimeController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode slowKey = KeyCode.Q;   // works with old input system

    [Header("Timing")]
    [Range(0.05f, 1f)] public float slowTimeScale = 0.4f; // 40% speed
    public float slowDuration = 2.5f;    // seconds
    public float slowCooldown = 6f;      // seconds

    [Header("UI (optional)")]
    public Text slowUIText;              // Canvas -> UI -> Text (legacy)

    bool active = false;
    float activeTimer = 0f;
    float cooldownTimer = 0f;

    float originalFixedDelta = 0.02f;    // default Unity value
    bool inited = false;

    void Awake()
    {
        originalFixedDelta = Time.fixedDeltaTime;
        inited = true;
        UpdateUI();
    }

    void OnDisable()
    {
        // Safety: never leave time slowed if this component disables
        if (active) RestoreTime();
    }

    void Update()
    {
        if (!inited) return;

        // --- INPUT: supports both systems ---
        bool pressedQ = false;

        // Old/legacy input manager:
        if (Input.GetKeyDown(slowKey)) pressedQ = true;

        // New Input System (if enabled in project):
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            pressedQ = true;
#endif
        // ------------------------------------

        if (pressedQ && !active && cooldownTimer <= 0f)
        {
            BeginSlowTime();
        }

        // Timers use UN-SCALED time (so the countdown is accurate)
        if (active)
        {
            activeTimer -= Time.unscaledDeltaTime;
            if (activeTimer <= 0f)
            {
                EndSlowTime();
            }
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.unscaledDeltaTime;
            if (cooldownTimer < 0f) cooldownTimer = 0f;
        }

        UpdateUI();
    }

    void BeginSlowTime()
    {
        active = true;
        activeTimer = slowDuration;

        Time.timeScale = Mathf.Clamp(slowTimeScale, 0.05f, 1f);
        Time.fixedDeltaTime = originalFixedDelta * Time.timeScale;

        UpdateUI();
    }

    void EndSlowTime()
    {
        active = false;
        RestoreTime();
        cooldownTimer = slowCooldown;
        UpdateUI();
    }

    void RestoreTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = originalFixedDelta;
    }

    void UpdateUI()
    {
        if (!slowUIText) return;

        if (active)
        {
            slowUIText.text = $"SLOW: {Mathf.Ceil(activeTimer)}s";
            slowUIText.color = Color.cyan;
        }
        else if (cooldownTimer > 0f)
        {
            slowUIText.text = $"CD: {Mathf.Ceil(cooldownTimer)}s";
            slowUIText.color = Color.gray;
        }
        else
        {
            slowUIText.text = "Q: Slow Time (Ready)";
            slowUIText.color = Color.white;
        }
    }

    
}
