using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class BattleCharacter : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 5f;
    public float jumpForce = 12f;
    public float gravity = 25f;

    [Header("走路动画帧")]
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
    public KeyCode attackKey = KeyCode.S;
    public KeyCode attackKeyAlt = KeyCode.Space;

    [Header("碰撞")]
    public Vector2 colliderSize = new Vector2(0.8f, 1.5f);
    public LayerMask wallLayer = -1;

    [Header("引用")]
    public Shuttlecock shuttlecock;

    private enum CharState { Idle, Walking, Swinging, Serving }
    private CharState state = CharState.Idle;
    private SpriteRenderer sr;
    private SpriteRenderer swingRenderer;
    private CircleCollider2D hitZone;

    private bool isGrounded = true;
    private float groundY;
    private float velocityY = 0f;
    private int facing = 1;
    private bool isWalking = false;

    private int walkFrameIndex = 0;
    private float walkFrameTimer = 0f;

    private int swingFrameIndex = 0;
    private float swingFrameTimer = 0f;
    private Sprite[] currentSwingFrames;
    private bool hitZoneWasActive = false;
    private bool swingStartedThisPress = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        groundY = transform.position.y;

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

        if (walkRightFrames != null && walkRightFrames.Length > 0)
            sr.sprite = walkRightFrames[0];
    }

    void Update()
    {
        if (!Application.isPlaying) return;

        if (!isGrounded)
        {
            velocityY -= gravity * Time.deltaTime;
            if (state != CharState.Swinging && state != CharState.Serving)
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
            case CharState.Idle:
            case CharState.Walking:
                HandleMovement();
                HandleAttackInput();
                UpdateWalkAnimation();
                break;
            case CharState.Swinging:
                UpdateSwingAnimation();
                break;
            case CharState.Serving:
                UpdateServeAnimation();
                break;
        }
    }

    void HandleMovement()
    {
        isWalking = false;
        float moveX = 0f;

        if (Input.GetKey(KeyCode.D))
        {
            moveX = moveSpeed * Time.deltaTime;
            if (isGrounded) isWalking = true;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            moveX = -moveSpeed * Time.deltaTime;
            if (isGrounded) isWalking = true;
        }

        if (Input.GetKeyDown(KeyCode.W) && isGrounded)
        {
            velocityY = jumpForce;
            isGrounded = false;
        }

        Vector3 pos = transform.position;
        Vector2 checkH = new Vector2(pos.x + moveX, pos.y);
        Collider2D hitH = Physics2D.OverlapBox(checkH, colliderSize, 0f, wallLayer);
        if (hitH == null)
            transform.position = pos + new Vector3(moveX, 0, 0);

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        UpdateSwingOverlayFlip();
    }

    void HandleAttackInput()
    {
        if (Input.GetKeyDown(attackKey) || Input.GetKeyDown(attackKeyAlt))
        {
            if (shuttlecock != null && shuttlecock.isInPlay)
                StartSwing();
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
        swingStartedThisPress = false;
        UpdateSwingOverlayFlip();
        state = CharState.Swinging;
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
        state = CharState.Serving;
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
                state = CharState.Idle;
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

        if (hitZone.enabled && shuttlecock != null && shuttlecock.isInPlay && !swingStartedThisPress)
        {
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(hitZone.transform.position, hitZone.radius);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (overlaps[i] != null && overlaps[i].GetComponent<Shuttlecock>() != null)
                {
                    ApplyHit();
                    swingStartedThisPress = true;
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
                state = CharState.Idle;
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
        if (isWalking && isGrounded)
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
