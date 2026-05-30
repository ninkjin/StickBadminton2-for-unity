using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class Shuttlecock : MonoBehaviour
{
    [Header("Physics")]
    public float gravity = 35f;
    public float airResistance = 0.97f;
    public float wallBounceDx = 0.8f;
    public float wallMinDx = 2f;
    public float netBounceDx = 0.1f;
    public float netBounceDy = 0.1f;
    public float netTopDy = 0.3f;
    public float netTopDx = 0.8f;
    public float groundBounceDy = 0.4f;
    public float ceilingBounceDy = 0.5f;

    [Header("Boundaries")]
    public float groundY = -4.5f;
    public float ceilingY = 5f;
    public float wallLeftX = -5f;
    public float wallRightX = 5f;

    [Header("Net")]
    public float netX = 0f;
    public float netTopY = 0.5f;
    public float netBottomY = -4.5f;
    public float netHalfWidth = 0.08f;

    [Header("State")]
    public bool isInPlay = false;
    public string lastHitter = "";
    [HideInInspector] public bool hasScored = false;

    [Header("Visual")]
    public Transform visual;

    // Manual physics state
    [HideInInspector] public float dx = 0f;
    [HideInInspector] public float dy = 0f;
    [HideInInspector] public float speed = 0f;
    [HideInInspector] public float dir = 0f;
    [HideInInspector] public float estimatedLandingX = 0f;

    private Vector3 startPosition;
    private CircleCollider2D col;

    public System.Action<string> OnLanded;

    void Awake()
    {
        col = GetComponent<CircleCollider2D>();
        col.isTrigger = true;
        startPosition = transform.position;

        if (visual == null)
        {
            var found = transform.Find("Visual");
            if (found != null) visual = found;
        }
    }

    float NetLeftX { get { return netX - netHalfWidth; } }
    float NetRightX { get { return netX + netHalfWidth; } }

    void Update()
    {
        if (!isInPlay) return;

        float dt = Time.deltaTime;

        // Air resistance first, then gravity — prevents flat-spot at trajectory peak
        float drag = Mathf.Pow(airResistance, 60f * dt);
        dx *= drag;
        dy *= drag;
        dy -= gravity * dt;

        // Stop completely if moving very slowly (threshold must be < gravity*dt to avoid locking)
        if (Mathf.Abs(dx) < 0.05f) dx = 0f;
        if (Mathf.Abs(dy) < 0.05f) dy = 0f;

        // Calculate new position
        float newX = transform.position.x + dx * dt;
        float newY = transform.position.y + dy * dt;

        // Wall collision
        if (newX > wallRightX)
        {
            newX = wallRightX;
            dx *= -wallBounceDx;
            if (dx > -wallMinDx) dx = -wallMinDx;
        }
        if (newX < wallLeftX)
        {
            newX = wallLeftX;
            dx *= -wallBounceDx;
            if (dx < wallMinDx) dx = wallMinDx;
        }

        // Ceiling bounce
        if (newY > ceilingY)
        {
            newY = ceilingY;
            dy *= -ceilingBounceDy;
            if (Mathf.Abs(dy) < 1f) dy = -1f;
        }

        // Net collision (top of net — ball going downward hits top edge)
        float oldX = transform.position.x;
        if (dy < 0 && oldX > NetLeftX && oldX < NetRightX
            && newY <= netTopY && transform.position.y >= netTopY)
        {
            newY = netTopY;
            dy *= -netTopDy;
            dx *= netTopDx;
            if (dy > -1f) dy = -1f;
            if (Mathf.Abs(dx) < 1f)
                dx = (Random.value > 0.5f ? 1.5f : -1.5f);
        }
        // Net body collision (side of net)
        else if (newY < netTopY && newY > netBottomY
            && ((oldX <= NetLeftX && newX > NetLeftX) || (oldX >= NetRightX && newX < NetRightX)))
        {
            if (dx > 0) newX = NetLeftX;
            else newX = NetRightX;
            dx *= -netBounceDx;
            dy *= netBounceDy;
        }

        transform.position = new Vector3(newX, newY, transform.position.z);

        // 飞行拖尾：速度越快拖尾越密（原版逻辑）
        if (isInPlay && speed > 5f)
        {
            float trailChance = speed / 80f * Time.deltaTime * 60f;
            if (Random.value < trailChance)
            {
                FeatherTrail.Spawn(transform.position);
                FeatherTrail.Spawn(transform.position);
            }
        }

        // Update polar coordinates for visual
        if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
        {
            dir = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            speed = Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Visual follows rotation
        if (visual != null && speed > 1f)
            visual.rotation = Quaternion.Euler(0, 0, dir);

        // Ground hit（触地即得分，反弹仅视觉效果）
        if (transform.position.y <= groundY)
        {
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
            if (!hasScored)
            {
                hasScored = true;
                Land(); // 得分
                dy *= -groundBounceDy;
            }
            else if (Mathf.Abs(dy) < 0.3f)
            {
                dy = 0f;
                isInPlay = false;
            }
            else
            {
                dy *= -groundBounceDy;
            }
        }

        Physics2D.SyncTransforms();
    }

    public void Serve(float directionX, float speedX, float speedY)
    {
        transform.position = startPosition;
        dx = speedX * directionX;
        dy = speedY;
        isInPlay = true;
        hasScored = false;
        lastHitter = "";
        RecalculatePolar();
    }

    public void HitMe(float hitSpeed, float hitDir, string hitType = "")
    {
        speed = hitSpeed;
        dir = hitDir;

        float rad = dir * Mathf.Deg2Rad;
        dx = Mathf.Cos(rad) * speed;
        dy = Mathf.Sin(rad) * speed;

        isInPlay = true;
        hasScored = false;

        HitSpark.Burst(transform.position, 8);
        SoundManager.PlaySFX("birdiehit");

        estimatedLandingX = SimulateLandingX();
        lastHitter = "";
    }

    public void Hit(Vector2 velocity)
    {
        dx = velocity.x;
        dy = velocity.y;
        isInPlay = true;
        RecalculatePolar();
        estimatedLandingX = SimulateLandingX();
        lastHitter = "";
    }

    float SimulateLandingX()
    {
        float simX = transform.position.x;
        float simY = transform.position.y;
        float simDx = dx;
        float simDy = dy;
        float simDt = 1f / 60f;

        for (int i = 0; i < 600; i++)
        {
            float drag = Mathf.Pow(airResistance, 60f * simDt);
            simDx *= drag;
            simDy *= drag;
            simDy -= gravity * simDt;

            simX += simDx * simDt;
            simY += simDy * simDt;

            // Wall bounce
            if (simX > wallRightX) { simX = wallRightX; simDx *= -wallBounceDx; }
            if (simX < wallLeftX) { simX = wallLeftX; simDx *= -wallBounceDx; }

            // Net top bounce
            if (simDy < 0 && simX > NetLeftX && simX < NetRightX && simY < netTopY)
            {
                simY = netTopY;
                simDy *= -netTopDy;
                simDx *= netTopDx;
            }

            if (simY <= groundY)
                return simX;
        }
        return simX;
    }

    public void ResetTo(Vector3 pos)
    {
        isInPlay = false;
        dx = 0f;
        dy = 0f;
        speed = 0f;
        transform.position = pos;
        startPosition = pos;
    }

    void Land()
    {
        if (!isInPlay) return;
        isInPlay = false;
        dx = 0f;
        dy = 0f;

        string scorer = transform.position.x < netX ? "Right" : "Left";
        OnLanded?.Invoke(scorer);
    }

    void RecalculatePolar()
    {
        speed = Mathf.Sqrt(dx * dx + dy * dy);
        if (speed > 0.01f)
            dir = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
    }

    void OnDrawGizmos()
    {
#if UNITY_EDITOR
        // 地面线
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(wallLeftX, groundY, 0), new Vector3(wallRightX, groundY, 0));
        UnityEditor.Handles.Label(new Vector3(wallLeftX, groundY - 0.3f, 0), "地面 groundY");

        // 左墙（羽毛球反弹边界）
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(new Vector3(wallLeftX, groundY, 0), new Vector3(wallLeftX, ceilingY, 0));
        UnityEditor.Handles.Label(new Vector3(wallLeftX, groundY - 0.3f, 0), $"墙 WallLeft={wallLeftX}");

        // 右墙（羽毛球反弹边界）
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(new Vector3(wallRightX, groundY, 0), new Vector3(wallRightX, ceilingY, 0));
        UnityEditor.Handles.Label(new Vector3(wallRightX, groundY - 0.3f, 0), $"墙 WallRight={wallRightX}");

        // 天花板线
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(wallLeftX, ceilingY, 0), new Vector3(wallRightX, ceilingY, 0));
        UnityEditor.Handles.Label(new Vector3(wallLeftX, ceilingY + 0.15f, 0), "天花板 ceilingY");

        // 网（矩形 + 标签）
        Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.7f);
        Vector3 netTL = new Vector3(NetLeftX, netTopY, 0);
        Vector3 netTR = new Vector3(NetRightX, netTopY, 0);
        Vector3 netBL = new Vector3(NetLeftX, netBottomY, 0);
        Vector3 netBR = new Vector3(NetRightX, netBottomY, 0);
        Gizmos.DrawLine(netTL, netTR);
        Gizmos.DrawLine(netTR, netBR);
        Gizmos.DrawLine(netBR, netBL);
        Gizmos.DrawLine(netBL, netTL);
        UnityEditor.Handles.Label(new Vector3(netX, netTopY + 0.15f, 0), $"网顶={netTopY}");

        // 预测落点（运行时可见）
        if (Application.isPlaying && isInPlay)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(new Vector3(estimatedLandingX, groundY, 0), 0.3f);
            UnityEditor.Handles.Label(new Vector3(estimatedLandingX, groundY - 0.5f, 0), "预测落点");
        }
#endif
    }
}
