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
    public float netX = 0f;
    public float wallLeftX = -13f;
    public float wallRightX = 13f;

    [Header("State")]
    public bool isInPlay = false;
    public string lastHitter = "";

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
    private float netLeftX, netRightX, netTopY, netBottomY;

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

        // Find net boundaries
        var net = GameObject.FindGameObjectWithTag("Net");
        if (net != null)
        {
            var netCol = net.GetComponent<BoxCollider2D>();
            if (netCol != null)
            {
                Bounds b = netCol.bounds;
                netLeftX = b.min.x;
                netRightX = b.max.x;
                netTopY = b.max.y;
                netBottomY = b.min.y;
            }
        }
    }

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
            if (Mathf.Abs(dx) < wallMinDx)
                dx = wallMinDx * Mathf.Sign(dx);
        }
        if (newX < wallLeftX)
        {
            newX = wallLeftX;
            dx *= -wallBounceDx;
            if (Mathf.Abs(dx) < wallMinDx)
                dx = wallMinDx * Mathf.Sign(dx);
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
        if (dy < 0 && oldX > netLeftX && oldX < netRightX
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
            && ((oldX <= netLeftX && newX > netLeftX) || (oldX >= netRightX && newX < netRightX)))
        {
            if (dx > 0) newX = netLeftX;
            else newX = netRightX;
            dx *= -netBounceDx;
            dy *= netBounceDy;
        }

        transform.position = new Vector3(newX, newY, transform.position.z);

        // Update polar coordinates for visual
        if (Mathf.Abs(dx) > 0.01f || Mathf.Abs(dy) > 0.01f)
        {
            dir = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            speed = Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Visual follows rotation
        if (visual != null && speed > 1f)
            visual.rotation = Quaternion.Euler(0, 0, dir);

        // Ground hit
        if (transform.position.y <= groundY)
        {
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
            dy *= -groundBounceDy;
            if (Mathf.Abs(dy) < 0.3f) dy = 0f;
            Land();
        }

        Physics2D.SyncTransforms();
    }

    public void Serve(float directionX, float speedX, float speedY)
    {
        transform.position = startPosition;
        dx = speedX * directionX;
        dy = speedY;
        isInPlay = true;
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
            if (simDy < 0 && simX > netLeftX && simX < netRightX && simY < netTopY)
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

        // 左墙
        Gizmos.color = Color.gray;
        Gizmos.DrawLine(new Vector3(wallLeftX, groundY, 0), new Vector3(wallLeftX, ceilingY, 0));

        // 右墙
        Gizmos.DrawLine(new Vector3(wallRightX, groundY, 0), new Vector3(wallRightX, ceilingY, 0));

        // 天花板线
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(wallLeftX, ceilingY, 0), new Vector3(wallRightX, ceilingY, 0));
        UnityEditor.Handles.Label(new Vector3(wallLeftX, ceilingY + 0.15f, 0), "天花板 ceilingY");

        // 网
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(new Vector3(netX, groundY, 0), new Vector3(netX, ceilingY, 0));

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
