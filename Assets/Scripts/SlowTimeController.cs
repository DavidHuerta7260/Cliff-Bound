


using UnityEngine;

public class SlowTimeController : MonoBehaviour
{
    public KeyCode slowKey = KeyCode.Q;
    [Range(0.05f, 1f)] public float slowScale = 0.4f;
    public float slowDuration = 2.5f;
    public float slowCooldown = 6f;

    public bool showBar = true;
    public bool autoFit = true;           
    [Range(3, 30)] public int barSegments = 14;

    public Vector2 barPos = new Vector2(32, 32);
    public Vector2 barSize = new Vector2(420, 32);
    public float barGap = 4f;
    public int borderThickness = 2;


    public Color colorActive = new Color(0f, 1f, 1f);
    public Color colorCooldown = new Color(0.6f, 0.6f, 0.6f);
    public Color colorReady = new Color(0.3f, 1f, 0.3f);
    public Color colorBack = new Color(0f, 0f, 0f, 0.38f);
    public Color colorBorder = new Color(1f, 1f, 1f, 0.25f);


    bool isActive = false;
    float activeTimer = 0f;
    float cooldownTimer = 0f;
    float baseFixedDelta = 0.02f;

    void Awake()
    {
        baseFixedDelta = Time.fixedDeltaTime;
    }

    void OnDisable()
    {
        if (isActive) RestoreTime();
    }

    void Update()
    {
        if (Input.GetKeyDown(slowKey))
        {
            if (!isActive && cooldownTimer <= 0f)
            {
                StartSlow();
            }
        }

        if (isActive)
        {
            activeTimer -= Time.unscaledDeltaTime;
            if (activeTimer <= 0f) EndSlow();
        }
        else if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.unscaledDeltaTime;
            if (cooldownTimer < 0f) cooldownTimer = 0f;
        }
    }

    void StartSlow()
    {
        isActive = true;
        activeTimer = slowDuration;
        Time.timeScale = Mathf.Clamp(slowScale, 0.05f, 1f);
        Time.fixedDeltaTime = baseFixedDelta * Time.timeScale;
    }

    void EndSlow()
    {
        isActive = false;
        RestoreTime();
        cooldownTimer = slowCooldown;
    }

    void RestoreTime()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = baseFixedDelta;
    }

    void OnGUI()
    {
        if (!showBar) return;

        float progress;
        Color tint;
        if (isActive)
        {
            progress = Mathf.Clamp01(activeTimer / Mathf.Max(0.0001f, slowDuration)); // drains 1->0
            tint = colorActive;
        }
        else if (cooldownTimer > 0f)
        {
            progress = 1f - Mathf.Clamp01(cooldownTimer / Mathf.Max(0.0001f, slowCooldown)); // fills 0->1
            tint = colorCooldown;
        }
        else
        {
            progress = 1f;
            tint = colorReady;
        }

        Rect box;
        float gap = barGap;
        int border = Mathf.Max(1, borderThickness);
        int segCount = Mathf.Max(1, barSegments);

        if (autoFit)
        {
            float w = Mathf.Round(0.44f * Screen.width);   
            float h = Mathf.Round(0.04f * Screen.height);  
            w = Mathf.Clamp(w, 360f, 720f);                
            h = Mathf.Clamp(h, 22f, 36f);

            float marginBottom = Mathf.Round(0.033f * Screen.height);
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - h - marginBottom;

            box = new Rect(x, y, w, h);

            
            gap = Mathf.Clamp(w * 0.006f, 3f, 6f);         
            border = (h >= 30f) ? 3 : 2;                   
            segCount = barSegments;                          
        }
        else
        {
            box = new Rect(barPos.x, barPos.y, barSize.x, barSize.y);
        }

        Color old = GUI.color;
        GUI.color = colorBack;
        GUI.DrawTexture(box, Texture2D.whiteTexture);

       
        GUI.color = colorBorder;
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, border), Texture2D.whiteTexture);                 // top
        GUI.DrawTexture(new Rect(box.x, box.yMax - border, box.width, border), Texture2D.whiteTexture);     // bottom
        GUI.DrawTexture(new Rect(box.x, box.y, border, box.height), Texture2D.whiteTexture);                // left
        GUI.DrawTexture(new Rect(box.xMax - border, box.y, border, box.height), Texture2D.whiteTexture);    // right

        
        float totalGap = gap * (segCount - 1);
        float segW = (box.width - totalGap) / segCount;
        float segH = box.height;
        int filled = Mathf.RoundToInt(progress * segCount);

        for (int i = 0; i < segCount; i++)
        {
            float x = box.x + i * (segW + gap);
            Rect r = new Rect(x, box.y, segW, segH);
            GUI.color = (i < filled) ? tint : new Color(tint.r, tint.g, tint.b, 0.18f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        GUI.color = old;
    }
}

