using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class AIController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 4f;
    public float jumpForce = 12f;
    public float gravity = 25f;

    [Header("走路动画帧（镜像，右侧人物）")]
    public Sprite[] walkRightFrames;
    public Sprite[] walkLeftFrames;
    public float walkFPS = 12f;

    [Header("挥拍动画帧")]
    public Sprite[] forehandSwingFrames;
    public Sprite[] backhandSwingFrames;
    public Sprite[] serveFrames;
    public float swingFPS = 40f;
    public int hitStartFrame = 8;
    public int hitEndFrame = 28;

    [Header("击球")]
    public float hitForceX = 10f;
    public float hitForceY = 8f;
    public float hitZoneRadius = 1.5f;
    public Vector2 hitZoneOffset = new Vector2(0.8f, 0.3f);

    [Header("AI参数")]
    public float idealXOffset = 1f;
    public float chaseSpeed = 4.5f;
    public float swingRange = 2f;
    public float reactionDelay = 0.1f;
    public float returnToCenterSpeed = 2f;

    [Header("碰撞")]
    public Vector2 colliderSize = new Vector2(0.8f, 1.5f);
    public LayerMask wallLayer = -1;

    [Header("引用")]
    public Shuttlecock shuttlecock;

    private enum AIState { Idle, Chasing, Returning, Swinging, Serving }
    private AIState state = AIState.Idle;
    private SpriteRenderer sr;
    private SpriteRenderer swingRenderer;
    private CircleCollider2D hitZone;

    private int facing = -1;
    private bool isGrounded = true;
    private float groundY;
    private float velocityY = 0f;

    private int walkFrameIndex = 0;
    private float walkFrameTimer = 0f;

    private int swingFrameIndex = 0;
    private float swingFrameTimer = 0f;
    private Sprite[] currentSwingFrames;
    private bool hitZoneWasActive = false;

    private float reactionTimer = 0f;
    private float centerX;
    private bool swingStarted = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        groundY = transform.position.y;
        centerX = transform.position.x;

        facing = -1;
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;

        Transform existingSwing = transform.Find("SwingOverlay");
        if (existingSwing != null)
        {
            swingRenderer = existingSwing.GetComponent<SpriteRenderer>();
        }
        else
        {
            GameObject swingObj = new GameObject("SwingOverlay");
            swingObj.transform.SetParent(transform);
            swingObj.transform.localPosition = Vector3.zero;
            swingObj.transform.localScale = Vector3.one;
            swingRenderer = swingObj.AddComponent<SpriteRenderer>();
            swingRenderer.sortingOrder = sr.sortingOrder + 1;
        }

        Transform existingHit = transform.Find("HitZone");
        if (existingHit != null)
        {
            hitZone = existingHit.GetComponent<CircleCollider2D>();
        }
        else
        {
            GameObject hitObj = new GameObject("HitZone");
            hitObj.transform.SetParent(transform);
            hitObj.transform.localPosition = Vector3.zero;
            hitZone = hitObj.AddComponent<CircleCollider2D>();
            hitZone.isTrigger = true;
            hitZone.radius = hitZoneRadius;
            hitZone.enabled = false;
        }

        // 第一帧常驻显示
        if (forehandSwingFrames != null && forehandSwingFrames.Length > 0)
            swingRenderer.sprite = forehandSwingFrames[0];
        swingRenderer.enabled = true;

        if (walkLeftFrames != null && walkLeftFrames.Length > 0)
            sr.sprite = walkLeftFrames[0];
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (!isGrounded)
        {
            velocityY -= gravity * Time.deltaTime;
            if (state != AIState.Swinging && state != AIState.Serving)
            {
                float moveY = velocityY * Time.deltaTime;
                Vector3 pos = transform.position;
                Vector2 checkPos = new Vector2(pos.x, pos.y + moveY);
                Collider2D hitV = Physics2D.OverlapBox(checkPos, colliderSize, 0f, wallLayer);
                if (hitV != null && moveY < 0)
                {
                    isGrounded = true;
                    velocityY = 0f;
                }
                else if (hitV != null && moveY > 0)
                {
                    velocityY = 0f;
                }
                else
                {
                    transform.position = pos + new Vector3(0, moveY, 0);
                }
            }

            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                velocityY = 0f;
                isGrounded = true;
            }
        }

        switch (state)
        {
            case AIState.Idle:
            case AIState.Chasing:
            case AIState.Returning:
                UpdateAI();
                UpdateWalkAnimation();
                break;
            case AIState.Swinging:
                UpdateSwingAnimation();
                break;
            case AIState.Serving:
                UpdateServeAnimation();
                break;
        }
    }

    void UpdateAI()
    {
        if (shuttlecock == null) return;

        float myX = transform.position.x;
        float shuttleX = shuttlecock.transform.position.x;
        float shuttleY = shuttlecock.transform.position.y;

        reactionTimer -= Time.deltaTime;

        if (shuttlecock.isInPlay)
        {
            bool comingToMe = (facing == -1 && shuttleX < myX) || (facing == 1 && shuttleX > myX);

            if (comingToMe || Mathf.Abs(shuttleX - myX) < swingRange * 2f)
            {
                state = AIState.Chasing;

                float targetX = shuttleX - idealXOffset * facing;
                float distanceToTarget = targetX - myX;

                if (Mathf.Abs(distanceToTarget) > 0.15f)
                {
                    float moveX = Mathf.Sign(distanceToTarget) * chaseSpeed * Time.deltaTime;
                    if (Mathf.Abs(moveX) > Mathf.Abs(distanceToTarget))
                        moveX = distanceToTarget;

                    Vector3 pos = transform.position;
                    Vector2 checkH = new Vector2(pos.x + moveX, pos.y);
                    Collider2D hitH = Physics2D.OverlapBox(checkH, colliderSize, 0f, wallLayer);
                    if (hitH == null)
                        transform.position = pos + new Vector3(moveX, 0, 0);
                }

                bool inSwingRange = Mathf.Abs(shuttleX - myX) < swingRange && shuttleY < transform.position.y + 1.5f;
                if (inSwingRange && reactionTimer <= 0f && !swingStarted)
                {
                    swingStarted = true;
                    StartSwing();
                }
            }
            else
            {
                state = AIState.Returning;
                ReturnToCenter();
            }
        }
        else
        {
            state = AIState.Idle;
            swingStarted = false;
            reactionTimer = reactionDelay;
        }
    }

    void ReturnToCenter()
    {
        float distanceToCenter = centerX - transform.position.x;
        if (Mathf.Abs(distanceToCenter) > 0.1f)
        {
            float moveX = Mathf.Sign(distanceToCenter) * returnToCenterSpeed * Time.deltaTime;
            if (Mathf.Abs(moveX) > Mathf.Abs(distanceToCenter))
                moveX = distanceToCenter;

            Vector3 pos = transform.position;
            Vector2 checkH = new Vector2(pos.x + moveX, pos.y);
            Collider2D hitH = Physics2D.OverlapBox(checkH, colliderSize, 0f, wallLayer);
            if (hitH == null)
                transform.position = pos + new Vector3(moveX, 0, 0);
        }

        if (shuttlecock.isInPlay)
        {
            bool comingToMe = (facing == -1 && shuttlecock.transform.position.x < transform.position.x);
            if (comingToMe)
                reactionTimer = reactionDelay;
        }
    }

    void StartSwing()
    {
        bool useForehand = true;
        if (shuttlecock != null)
            useForehand = shuttlecock.transform.position.y >= transform.position.y - 0.3f;

        currentSwingFrames = useForehand ? forehandSwingFrames : backhandSwingFrames;
        if (currentSwingFrames == null || currentSwingFrames.Length <= 1)
            return;

        swingFrameIndex = 0;
        swingFrameTimer = 0f;
        hitZoneWasActive = false;
        UpdateSwingOverlayFlip();
        state = AIState.Swinging;
    }

    public void PlayServeAnimation()
    {
        currentSwingFrames = serveFrames;
        if (currentSwingFrames == null || currentSwingFrames.Length <= 1)
        {
            currentSwingFrames = backhandSwingFrames;
            if (currentSwingFrames == null || currentSwingFrames.Length <= 1)
                return;
        }

        swingFrameIndex = 0;
        swingFrameTimer = 0f;
        hitZoneWasActive = false;
        UpdateSwingOverlayFlip();
        state = AIState.Serving;
    }

    void UpdateSwingAnimation()
    {
        swingFrameTimer += Time.deltaTime;
        float frameDuration = 1f / swingFPS;

        if (swingFrameTimer >= frameDuration)
        {
            swingFrameTimer -= frameDuration;
            swingFrameIndex++;

            if (swingFrameIndex >= currentSwingFrames.Length)
            {
                swingRenderer.sprite = currentSwingFrames[0];
                if (forehandSwingFrames != null && forehandSwingFrames.Length > 0
                    && currentSwingFrames != forehandSwingFrames)
                {
                    currentSwingFrames = forehandSwingFrames;
                    swingRenderer.sprite = forehandSwingFrames[0];
                }
                UpdateSwingOverlayFlip();
                hitZone.enabled = false;
                hitZoneWasActive = false;
                swingStarted = false;
                reactionTimer = reactionDelay * 2f;
                state = AIState.Idle;
                return;
            }

            swingRenderer.sprite = currentSwingFrames[swingFrameIndex];
        }

        bool inHitWindow = swingFrameIndex >= hitStartFrame && swingFrameIndex <= hitEndFrame;
        if (inHitWindow && !hitZoneWasActive)
        {
            hitZone.enabled = true;
            UpdateHitZonePosition();
            hitZoneWasActive = true;
        }
        else if (!inHitWindow && hitZoneWasActive)
        {
            hitZone.enabled = false;
            hitZoneWasActive = false;
        }

        if (hitZone.enabled && shuttlecock != null && shuttlecock.isInPlay)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(hitZone.transform.position, hitZone.radius);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && overlaps[i].GetComponent<Shuttlecock>() != null)
                {
                    ApplyHit();
                    break;
                }
            }
        }
    }

    void UpdateServeAnimation()
    {
        swingFrameTimer += Time.deltaTime;
        float frameDuration = 1f / swingFPS;

        if (swingFrameTimer >= frameDuration)
        {
            swingFrameTimer -= frameDuration;
            swingFrameIndex++;

            if (swingFrameIndex >= currentSwingFrames.Length)
            {
                swingRenderer.sprite = currentSwingFrames[0];
                if (forehandSwingFrames != null && forehandSwingFrames.Length > 0
                    && currentSwingFrames != forehandSwingFrames)
                {
                    currentSwingFrames = forehandSwingFrames;
                    swingRenderer.sprite = forehandSwingFrames[0];
                }
                UpdateSwingOverlayFlip();
                state = AIState.Idle;
                return;
            }

            swingRenderer.sprite = currentSwingFrames[swingFrameIndex];
        }
    }

    void ApplyHit()
    {
        if (shuttlecock == null) return;

        float dirX = facing;
        float relativeY = shuttlecock.transform.position.y - transform.position.y;
        float forceY = relativeY > 0.2f ? hitForceY * 1.3f : hitForceY * 0.7f;

        Vector2 force = new Vector2(hitForceX * dirX, forceY);
        shuttlecock.Hit(force);
        shuttlecock.lastHitter = name;

        hitZone.enabled = false;
        hitZoneWasActive = false;
    }

    void UpdateWalkAnimation()
    {
        bool moving = state == AIState.Chasing || state == AIState.Returning;
        if (moving && isGrounded)
        {
            walkFrameTimer += Time.deltaTime;
            if (walkFrameTimer >= 1f / walkFPS)
            {
                walkFrameTimer = 0f;
                Sprite[] frames = facing == 1 ? walkRightFrames : walkLeftFrames;
                if (frames != null && frames.Length > 0)
                {
                    walkFrameIndex = (walkFrameIndex + 1) % frames.Length;
                    sr.sprite = frames[walkFrameIndex];
                }
            }
        }
        else
        {
            walkFrameIndex = 0;
            walkFrameTimer = 0f;
            Sprite[] idleFrames = facing == 1 ? walkRightFrames : walkLeftFrames;
            if (idleFrames != null && idleFrames.Length > 0)
                sr.sprite = idleFrames[0];
        }

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        UpdateSwingOverlayFlip();
    }

    void UpdateSwingOverlayFlip()
    {
        Vector3 sScale = swingRenderer.transform.localScale;
        sScale.x = facing;
        swingRenderer.transform.localScale = sScale;
    }

    void UpdateHitZonePosition()
    {
        hitZone.transform.localPosition = new Vector3(hitZoneOffset.x * facing, hitZoneOffset.y, 0);
    }

    public void SetShuttlecock(Shuttlecock s)
    {
        shuttlecock = s;
    }
}
