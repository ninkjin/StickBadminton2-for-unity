using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[ExecuteAlways]
public class AIController : MonoBehaviour
{
    [Header("移动")]
    public float moveSpeed = 5f;
    public float jumpSpeed = 9f;
    public float gravity = 20f;
    public float powerLevel = 0.9f;
    public float moveMinX = 0.5f;
    public float moveMaxX = 4.5f;

    [Header("Modifier（难度调节）")]
    public float moveModifier = 1f;
    public float jumpModifier = 1f;
    public float powerModifier = 1f;

    [Header("走路动画帧")]
    public Sprite[] walkRightFrames;
    public Sprite[] walkLeftFrames;
    public Sprite[] idleFrames;
    public Sprite[] jumpFrame;
    public float walkFPS = 12f;

    [Header("挥拍动画帧")]
    public Sprite[] forehandSwingFrames;
    public Sprite[] backhandSwingFrames;
    public Sprite[] serveFrames;
    public Sprite[] missSwingFrames;
    public Sprite[] serveRecoveryFrames;
    public float serveRecoveryFPS = 30f;
    public Sprite idleOverlaySprite;
    public float swingFPS = 40f;
    public int hitStartFrame = 0;
    public int hitEndFrame = 26;
    public int serveHitFrame = 10;

    [Header("SwingOverlay 位置偏移")]
    public Vector2 overlayOffsetOverhead = Vector2.zero;
    public Vector2 overlayOffsetUnderhand = new Vector2(0f, -0.3f);
    public Vector2 overlayOffsetServe = new Vector2(0f, -0.3f);

    [Header("击球")]
    public float hitSpeedOverhead = 50f;
    public float hitSpeedUnderhand = 45f;
    public float hitSpeedServe = 27f;
    public float hitZoneRadius = 0.5f;
    public Vector2 hitZoneOffset = new Vector2(0.8f, 0.3f);
    public float racketLength = 1.0f;      // 球拍长度
    public Vector2 gripOffsetOverhead = new Vector2(0.3f, 1.5f);   // 正手握拍手位置
    public Vector2 gripOffsetUnderhand = new Vector2(0.3f, -0.3f); // 反手握拍手位置
    public float racketAngleStart = 50f;   // 正手挥拍早期（逆时针）
    public float racketAngleEnd = -40f;    // 正手挥拍晚期
    public float racketAngleStartBackhand = 30f;   // 反手挥拍早期（逆时针）
    public float racketAngleEndBackhand = 210f;    // 反手挥拍晚期（逆时针）
    public float backhandShotAngleStart = 70f;     // 反手挥拍早期出球角度
    public float backhandShotAngleEnd = 15f;       // 反手挥拍晚期出球角度

    [Header("碰撞")]
    public Vector2 colliderSize = new Vector2(0.8f, 1.5f);
    public LayerMask wallLayer = 0;

    [Header("控制模式")]
    public bool humanControlled = true;
    public bool aiControlled = false;

    [Header("AI 参数")]
    public float aiReactDistance = 3f;
    public float aiSwingCooldown = 0.3f;
    public float aiIdlePositionX = 3f;
    [Range(0f, 1f)] public float aiSkillLevel = 0.75f;
    public float aiReactDelay = 0.15f;

    [Header("引用")]
    public Shuttlecock shuttlecock;
    public GameManager gameManager;

    [HideInInspector] public bool isNetworkRemote = false;
    [HideInInspector] public bool isNetworkHost = false;

    private enum CharState { Idle, Walking, Jumping, Swinging, Serving, Recovering }
    private CharState state = CharState.Idle;
    private SpriteRenderer sr;
    private SpriteRenderer swingRenderer;
    private CircleCollider2D hitZone;

    private bool isGrounded = true;
    private float groundY;
    private float dy = 0f;
    private int facing = -1;
    private Vector3 serveFollowOffset = new Vector3(0.5f, 0.5f, 0);
    private bool isWalking = false;
    private bool doubleJumped = false;
    private bool jumpKeyReady = false;

    private int walkFrameIndex = 0;
    private float walkFrameTimer = 0f;

    private int swingFrameIndex = 0;
    private float swingFrameTimer = 0f;
    private Sprite[] currentSwingFrames;
    private bool hitZoneWasActive = false;
    private bool swingStartedThisPress = false;
    private Vector3 prevHeadWorldPos;

    private Transform gripOverheadMarker;
    private Transform gripUnderhandMarker;
    private Transform serveMarker;

    // AI state
    private float aiLastSwingTime = -10f;
    private float aiServeReadyTime = 0f;
    private bool aiServeStarted = false;
    private float aiPlayerHitTime = -10f;
    private float aiSwingCommitted = -10f;
    private bool aiWasHit = false;
    private float aiTargetX = 3f;
    private float aiCachedError = 0f;

    private bool serveRecovering = false;
    private int serveRecoveryFrameIndex = 0;
    private float serveRecoveryFrameTimer = 0f;
    private bool serveHitApplied = false;
    private bool holdingSwing = false;
    private bool swingIsMiss = false;
    private Vector3 idleOverlayLocalPos;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        groundY = transform.position.y;

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

        // HitZone 挂载在 SwingOverlay 下，跟随球拍移动
        Transform swingTrans = swingRenderer.transform;
        Transform existingHit = swingTrans.Find("HitZone");
        if (existingHit != null)
        {
            hitZone = existingHit.GetComponent<CircleCollider2D>();
        }
        else
        {
            GameObject hitObj = new GameObject("HitZone");
            hitObj.transform.SetParent(swingTrans);
            hitObj.transform.localPosition = Vector3.zero;
            hitZone = hitObj.AddComponent<CircleCollider2D>();
            hitZone.isTrigger = true;
            hitZone.radius = hitZoneRadius;
            hitZone.enabled = false;
        }

        // Grip position markers (editable in Scene view)
        gripOverheadMarker = swingTrans.Find("GripOverhead");
        gripUnderhandMarker = swingTrans.Find("GripUnderhand");

        // Serve marker (editable in Scene view)
        serveMarker = transform.Find("ServeMarker");

        if (idleOverlaySprite != null)
            swingRenderer.sprite = idleOverlaySprite;
        else if (forehandSwingFrames != null && forehandSwingFrames.Length > 0)
            swingRenderer.sprite = forehandSwingFrames[0];
        swingRenderer.enabled = true;

        if (idleFrames != null && idleFrames.Length > 0)
            sr.sprite = idleFrames[0];
        else if (walkLeftFrames != null && walkLeftFrames.Length > 0)
            sr.sprite = walkLeftFrames[0];

        idleOverlayLocalPos = swingRenderer.transform.localPosition;
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        if (isNetworkRemote)
        {
            ApplyRemoteState();
            UpdateRemoteVisuals();
            return;
        }

        float dt = Time.deltaTime;

        // Gravity + vertical movement (always, even during swings)
        if (!isGrounded)
        {
            dy -= gravity * dt;
            float moveY = dy * dt;
            Vector3 pos = transform.position;
            Vector2 checkPos = new Vector2(pos.x, pos.y + moveY);
            Collider2D hitV = Physics2D.OverlapBox(checkPos, colliderSize, 0f, wallLayer);
            if (hitV != null && moveY < 0)
            {
                isGrounded = true;
                dy = 0f;
            }
            else if (hitV != null && moveY > 0)
            {
                dy = 0f;
            }
            else
            {
                transform.position = pos + new Vector3(0, moveY, 0);
            }

            if (transform.position.y <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                dy = 0f;
                isGrounded = true;
                doubleJumped = false;
                if (state != CharState.Swinging && state != CharState.Serving && state != CharState.Recovering)
                    state = CharState.Idle;
            }
        }

        // During serve state, shuttlecock follows player at scene-defined offset
        if (serving && shuttlecock != null && !shuttlecock.isInPlay)
        {
            if (serveMarker != null)
            {
                shuttlecock.transform.position = serveMarker.position;
            }
            else
            {
                Vector3 offset = serveFollowOffset;
                offset.x = Mathf.Abs(offset.x) * facing;
                shuttlecock.transform.position = transform.position + offset;
            }
        }

        // Movement + Swing input
        if (state != CharState.Serving && state != CharState.Recovering)
        {
            if (aiControlled)
                UpdateAI(state != CharState.Swinging);
            else
            {
                HandleMovement();
                if (state != CharState.Swinging)
                    HandleSwingInput();
            }
        }

        // Walk animation
        if (state != CharState.Swinging && state != CharState.Serving && state != CharState.Recovering)
            UpdateWalkAnimation();

        // Swing animation
        if (state == CharState.Swinging)
            UpdateSwingAnimation();
        else if (state == CharState.Serving)
            UpdateServeAnimation();
        else if (state == CharState.Recovering)
            UpdateServeRecovery();
    }

    void UpdateAI(bool canSwing)
    {
        if (shuttlecock == null) return;

        Vector3 birdPos = shuttlecock.transform.position;
        float myX = transform.position.x;
        float myY = transform.position.y;
        bool birdInPlay = shuttlecock.isInPlay;

        // --- 追踪谁打了球 ---
        bool iHitIt = birdInPlay && shuttlecock.lastHitter == name;
        bool opponentHitIt = birdInPlay && !string.IsNullOrEmpty(shuttlecock.lastHitter) && shuttlecock.lastHitter != name;

        if (iHitIt && !aiWasHit)
        {
            Debug.Log($"[AI] 我击球了, 回防0.8s");
            aiWasHit = true;
        }
        else if (opponentHitIt && aiWasHit)
        {
            // 对手刚击球 → 记录时间，延迟后再缓存目标（等落点稳定）
            Debug.Log($"[AI] 对手击球, 等待落点稳定...");
            aiPlayerHitTime = Time.time;
            aiTargetX = -999f; // 标记需要重新缓存
            aiCachedError = (1f - aiSkillLevel) * Random.Range(-1f, 1f);
            aiWasHit = false;
        }
        else if (opponentHitIt && !aiWasHit && aiPlayerHitTime < 0)
        {
            // 初始状态 → 也走正常延迟流程
            Debug.Log($"[AI] 初始状态, 等待落点稳定...");
            aiPlayerHitTime = Time.time;
            aiTargetX = -999f;
            aiCachedError = (1f - aiSkillLevel) * Random.Range(-1f, 1f);
        }

        // --- 发球 ---
        if (serving && !birdInPlay)
        {
            if (!aiServeStarted)
            {
                aiServeReadyTime = Time.time + 0.8f;
                aiServeStarted = true;
            }
            if (Time.time >= aiServeReadyTime && canSwing)
            {
                SwingMe();
                aiServeStarted = false;
                aiLastSwingTime = Time.time;
                aiSwingCommitted = Time.time;
            }
            return;
        }
        if (!serving)
            aiServeStarted = false;

        // --- 移动策略：混合落点预测 + 实时追球 ---
        float targetX = aiIdlePositionX;

        if (birdInPlay)
        {
            if (aiWasHit && Time.time - aiSwingCommitted < 0.5f)
            {
                // 刚击球后快速回中
                targetX = aiIdlePositionX;
            }
            else if (Time.time - aiPlayerHitTime > aiReactDelay * (1f - aiSkillLevel * 0.3f))
            {
                // 优先追球当前位置（更直接），远处时切换落点预测
                float distToBirdie = Mathf.Abs(birdPos.x - myX);
                if (distToBirdie < aiReactDistance * 1.2f)
                {
                    targetX = Mathf.Clamp(birdPos.x, moveMinX + 0.3f, moveMaxX - 0.3f);
                }
                else
                {
                    float newTarget = Mathf.Clamp(shuttlecock.estimatedLandingX + aiCachedError,
                        moveMinX + 0.5f, moveMaxX - 0.5f);
                    if (aiTargetX < -99f || Mathf.Abs(newTarget - aiTargetX) > 1f)
                        aiTargetX = newTarget;
                    targetX = aiTargetX;
                }
            }
        }
        else
        {
            aiTargetX = -999f;
        }

        // 平滑移动
        float diff = targetX - myX;
        float moveThreshold = aiWasHit ? 0.3f : 0.15f;
        if (Mathf.Abs(diff) > moveThreshold)
        {
            float spd = moveSpeed * moveModifier;
            if (aiWasHit) spd *= 0.85f;
            if (Mathf.Abs(diff) > 2.5f) spd *= 1.3f;
            if (Mathf.Abs(diff) > 4f) spd *= 1.3f;
            float moveX = Mathf.Sign(diff) * spd * Time.deltaTime;
            moveX = Mathf.Clamp(moveX, -Mathf.Abs(diff), Mathf.Abs(diff));

            Vector3 pos = transform.position;
            Vector2 checkH = new Vector2(pos.x + moveX, pos.y);
            Collider2D hitH = Physics2D.OverlapBox(checkH, colliderSize, 0f, wallLayer);
            if (hitH == null)
            {
                pos.x += moveX;
                pos.x = Mathf.Clamp(pos.x, moveMinX, moveMaxX);
                transform.position = pos;
                isWalking = true;
            }
        }
        else
        {
            isWalking = false;
        }

        // --- 跳跃：球高+近+朝我飞来才跳 ---
        if (birdInPlay && canSwing && state != CharState.Jumping && !aiWasHit)
        {
            bool ballHigh = birdPos.y > myY + hitZoneOffset.y + 1f;
            bool ballClose = Mathf.Abs(birdPos.x - myX) < aiReactDistance * 1.5f;
            bool ballApproaching = (facing == -1 && shuttlecock.dx < -0.5f) || (facing == 1 && shuttlecock.dx > 0.5f);

            if (ballHigh && ballClose && ballApproaching)
            {
                if (!isGrounded && !doubleJumped)
                {
                    doubleJumped = true;
                    dy = jumpSpeed * jumpModifier;
                    state = CharState.Jumping;
                }
                else if (isGrounded)
                {
                    dy = jumpSpeed * jumpModifier;
                    isGrounded = false;
                    state = CharState.Jumping;
                }
            }
        }

        // --- 挥拍：更大反应距离 + 更短冷却 ---
        if (canSwing && birdInPlay
            && Time.time - aiLastSwingTime > aiSwingCooldown * 0.6f
            && !aiWasHit)
        {
            bool ballBouncing = birdPos.y < myY - 1f && shuttlecock.dy > -3f;
            if (ballBouncing) goto skipSwing;

            float dist = Vector2.Distance(
                new Vector2(birdPos.x, birdPos.y),
                new Vector2(myX, myY + hitZoneOffset.y));
            bool ballInFront = (facing == -1 && birdPos.x < myX + 0.8f)
                            || (facing == 1 && birdPos.x > myX - 0.8f);
            float effectiveDist = ballInFront ? aiReactDistance * 1.3f : aiReactDistance * 0.7f;

            if (dist < effectiveDist)
            {
                aiLastSwingTime = Time.time;
                aiSwingCommitted = Time.time;
                SwingMe();
            }
        }
        skipSwing:

        // Always face opponent
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        UpdateSwingOverlayFlip();

        if (isGrounded && !isWalking && state == CharState.Jumping)
            state = CharState.Idle;
    }

    void HandleMovement()
    {
        isWalking = false;
        float moveX = 0f;

        if (MobileInput.MoveRight() || Input.GetKey(KeyCode.RightArrow))
        {
            moveX = moveSpeed * moveModifier * Time.deltaTime;
            if (isGrounded) isWalking = true;
        }
        else if (MobileInput.MoveLeft() || Input.GetKey(KeyCode.LeftArrow))
        {
            moveX = -moveSpeed * moveModifier * Time.deltaTime;
            if (isGrounded) isWalking = true;
        }

        // Jump: UpArrow, with double jump
        bool jumpPressed = MobileInput.JumpDown() || Input.GetKey(KeyCode.UpArrow);
        if (jumpPressed && !jumpKeyReady)
        {
            // Key was already down, don't re-trigger
        }
        else if (jumpPressed && jumpKeyReady && state != CharState.Swinging)
        {
            if (!isGrounded && !doubleJumped)
            {
                doubleJumped = true;
                dy = jumpSpeed * jumpModifier;
                isGrounded = false;
                state = CharState.Jumping;
            }
            else if (isGrounded)
            {
                dy = jumpSpeed * jumpModifier;
                isGrounded = false;
                state = CharState.Jumping;
            }
            jumpKeyReady = false;
        }
        else if (!jumpPressed)
        {
            jumpKeyReady = true;
        }

        // Horizontal movement
        Vector3 pos = transform.position;
        Vector2 checkH = new Vector2(pos.x + moveX, pos.y);
        Collider2D hitH = Physics2D.OverlapBox(checkH, colliderSize, 0f, wallLayer);
        if (hitH == null)
            transform.position = pos + new Vector3(moveX, 0, 0);

        // Clamp horizontal position
        Vector3 clampedPos = transform.position;
        clampedPos.x = Mathf.Clamp(clampedPos.x, moveMinX, moveMaxX);
        transform.position = clampedPos;

        // Always face opponent (net)
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        UpdateSwingOverlayFlip();

        if (isGrounded && !isWalking && state == CharState.Jumping)
            state = CharState.Idle;
    }

    void HandleSwingInput()
    {
        bool swingKeyDown = MobileInput.Swing() || Input.GetKey(KeyCode.DownArrow);

        if (swingKeyDown && !holdingSwing)
        {
            holdingSwing = true;
            SwingMe();
        }
        else if (!swingKeyDown)
        {
            holdingSwing = false;
        }
    }

    public void SwingMe()
    {
        if (state == CharState.Swinging || state == CharState.Serving) return;

        // 立即发送挥拍事件（不经节流）
        var sync = NetworkBattleSync.Instance;
        if (sync != null && !isNetworkRemote)
            sync.SendSwingEvent(serving);

        // Serving: start serve swing animation
        if (serving)
        {
            currentSwingFrames = serveFrames;
            if (currentSwingFrames == null) currentSwingFrames = backhandSwingFrames;
            swingFrameIndex = 0;
            swingFrameTimer = 0f;
            serveHitApplied = false;
            swingRenderer.transform.localPosition = new Vector3(overlayOffsetServe.x, overlayOffsetServe.y, 0);
            UpdateSwingOverlayFlip();
            state = CharState.Serving;
            return;
        }

        swingFrameIndex = 0;
        swingFrameTimer = 0f;
        hitZoneWasActive = false;
        swingStartedThisPress = false;
        swingIsMiss = false;

        if (shuttlecock != null && shuttlecock.isInPlay)
        {
            bool useOverhead = !GameManager.forceUnderhandOnly
                && shuttlecock.transform.position.y >= transform.position.y + hitZoneOffset.y;
            if (useOverhead)
                SetupOverhead();
            else
                SetupUnderhand();
        }
        else
        {
            if (GameManager.forceUnderhandOnly)
                SetupUnderhand();
            else
                SetupOverhead();
        }

        UpdateSwingOverlayFlip();
        state = CharState.Swinging;
        SoundManager.PlaySFX("whoosh");
    }

    void SetupOverhead()
    {
        currentSwingFrames = forehandSwingFrames;
        swingRenderer.transform.localPosition = new Vector3(overlayOffsetOverhead.x, overlayOffsetOverhead.y, 0);
    }

    void SetupUnderhand()
    {
        currentSwingFrames = backhandSwingFrames;
        swingRenderer.transform.localPosition = new Vector3(overlayOffsetUnderhand.x, overlayOffsetUnderhand.y, 0);
    }

    [HideInInspector] public bool serving = false;

    public void SetupServe()
    {
        serving = true;
        state = CharState.Idle;
        currentSwingFrames = serveFrames;
        if (currentSwingFrames == null) currentSwingFrames = backhandSwingFrames;

        if (serveFrames != null && serveFrames.Length > 0)
            swingRenderer.sprite = serveFrames[0];
        else if (backhandSwingFrames != null && backhandSwingFrames.Length > 0)
            swingRenderer.sprite = backhandSwingFrames[0];

        swingRenderer.transform.localPosition = new Vector3(overlayOffsetUnderhand.x, overlayOffsetUnderhand.y, 0);

        // 确保球在发球位置（remote角色不会跑Update的发球跟随逻辑）
        if (shuttlecock != null && !shuttlecock.isInPlay && serveMarker != null)
            shuttlecock.transform.position = serveMarker.position;
    }

    void ApplyServeHit()
    {
        if (shuttlecock == null) return;

        // 远程端只播放动画，不执行实际击球
        if (!isNetworkRemote)
        {
            float hitSpeed = hitSpeedServe * powerModifier * powerLevel;
            float hitDir = 45f * facing;
            if (facing < 0) hitDir += 180f;
            shuttlecock.HitMe(hitSpeed, hitDir, "serve");
            shuttlecock.lastHitter = name;

            var sync = NetworkBattleSync.Instance;
            if (sync != null) sync.SendHit(hitSpeed, hitDir, shuttlecock.transform.position);
        }

        serving = false;

        if (!isNetworkRemote && gameManager != null)
            gameManager.OnOpponentServe();
    }

    public void PlayServeAnimation()
    {
        SetupServe();
    }

    void UpdateSwingAnimation()
    {
        if (currentSwingFrames == null || currentSwingFrames.Length == 0) return;

        swingFrameTimer += Time.deltaTime;
        float frameDuration = 1f / swingFPS;

        if (swingFrameTimer >= frameDuration)
        {
            swingFrameTimer -= frameDuration;
            swingFrameIndex++;

            if (swingFrameIndex >= currentSwingFrames.Length)
            {
                if (forehandSwingFrames != null && forehandSwingFrames.Length > 0
                    && currentSwingFrames != forehandSwingFrames)
                {
                    currentSwingFrames = forehandSwingFrames;
                }
                swingRenderer.transform.localPosition = idleOverlayLocalPos;
                swingRenderer.sprite = idleOverlaySprite != null ? idleOverlaySprite : currentSwingFrames[0];
                UpdateSwingOverlayFlip();
                hitZone.enabled = false;
                hitZoneWasActive = false;
                state = CharState.Idle;

                // 没有打到球 + 按键还按着 → 立刻再挥
                if (!swingStartedThisPress && shuttlecock != null && shuttlecock.isInPlay)
                {
                    bool swingHeld = MobileInput.Swing() || Input.GetKey(KeyCode.DownArrow);
                    if (swingHeld)
                    {
                        SwingMe();
                        return;
                    }
                }
                return;
            }

            swingRenderer.sprite = currentSwingFrames[swingFrameIndex];
        }

        if (!swingIsMiss)
        {
            float smoothIdx = swingFrameIndex + Mathf.Clamp01(swingFrameTimer / frameDuration) + 5f;
            bool inHitWindow = swingFrameIndex >= hitStartFrame && swingFrameIndex <= hitEndFrame;
            if (inHitWindow && !hitZoneWasActive)
            {
                hitZone.enabled = true;
                hitZoneWasActive = true;
                UpdateHitZonePosition(smoothIdx);
                prevHeadWorldPos = hitZone.transform.position;
            }
            else if (!inHitWindow && hitZoneWasActive)
            {
                hitZone.enabled = false;
                hitZoneWasActive = false;
            }

            if (hitZone.enabled)
            {
                UpdateHitZonePosition(smoothIdx);
                if (shuttlecock != null && shuttlecock.isInPlay && !shuttlecock.hasScored && !swingStartedThisPress)
                    CheckSweptHit();
                prevHeadWorldPos = hitZone.transform.position;
            }
        }
    }

    void UpdateServeAnimation()
    {
        if (currentSwingFrames == null || currentSwingFrames.Length == 0) return;

        swingFrameTimer += Time.deltaTime;
        float frameDuration = 1f / swingFPS;

        if (swingFrameTimer >= frameDuration)
        {
            swingFrameTimer -= frameDuration;
            swingFrameIndex++;

            if (!serveHitApplied && swingFrameIndex >= serveHitFrame)
            {
                ApplyServeHit();
                serveHitApplied = true;
                StartServeRecovery();
                return;
            }

            if (swingFrameIndex >= currentSwingFrames.Length)
            {
                StartServeRecovery();
                return;
            }

            swingRenderer.sprite = currentSwingFrames[swingFrameIndex];
        }
    }

    void StartServeRecovery()
    {
        // 确保发球击球一定被应用（防止动画结束但没到 serveHitFrame）
        if (!serveHitApplied)
            ApplyServeHit();

        if (serveRecoveryFrames != null && serveRecoveryFrames.Length > 0)
        {
            serveRecovering = true;
            serveRecoveryFrameIndex = 0;
            serveRecoveryFrameTimer = 0f;
            state = CharState.Recovering;
        }
        else
        {
            ReturnToOverheadIdle();
        }
    }

    void UpdateServeRecovery()
    {
        if (serveRecoveryFrames == null || serveRecoveryFrames.Length == 0)
        {
            ReturnToOverheadIdle();
            return;
        }

        serveRecoveryFrameTimer += Time.deltaTime;
        float frameDuration = 1f / serveRecoveryFPS;

        if (serveRecoveryFrameTimer >= frameDuration)
        {
            serveRecoveryFrameTimer -= frameDuration;
            serveRecoveryFrameIndex++;

            if (serveRecoveryFrameIndex >= serveRecoveryFrames.Length)
            {
                ReturnToOverheadIdle();
                return;
            }

            swingRenderer.sprite = serveRecoveryFrames[serveRecoveryFrameIndex];
        }

        float t = (float)serveRecoveryFrameIndex / serveRecoveryFrames.Length;
        swingRenderer.transform.localPosition = new Vector3(
            Mathf.Lerp(overlayOffsetServe.x, overlayOffsetOverhead.x, t),
            Mathf.Lerp(overlayOffsetServe.y, overlayOffsetOverhead.y, t),
            0);
    }

    void ReturnToOverheadIdle()
    {
        serveRecovering = false;
        currentSwingFrames = forehandSwingFrames;
        swingRenderer.sprite = idleOverlaySprite != null ? idleOverlaySprite : currentSwingFrames[0];
        swingRenderer.transform.localPosition = idleOverlayLocalPos;
        UpdateSwingOverlayFlip();
        state = CharState.Idle;
    }

    void ApplyHit()
    {
        if (shuttlecock == null || !shuttlecock.isInPlay) return;

        if (isNetworkRemote)
        {
            // 远程端只播动画不击球
            hitZone.enabled = false;
            hitZoneWasActive = false;
            return;
        }

        float hitSpeed;
        float hitDir;

        if (currentSwingFrames == forehandSwingFrames)
        {
            hitSpeed = hitSpeedOverhead * powerModifier * powerLevel;

            float t = Mathf.InverseLerp(hitStartFrame, hitEndFrame, swingFrameIndex);
            float racketAngle = Mathf.Lerp(racketAngleStart, racketAngleEnd, t);
            hitDir = (racketAngle + 30f) * facing;
            if (facing < 0) hitDir += 180f;
        }
        else
        {
            hitSpeed = hitSpeedUnderhand * powerModifier * powerLevel;
            float t = Mathf.InverseLerp(hitStartFrame, hitEndFrame, swingFrameIndex);
            float shotAngle = Mathf.Lerp(backhandShotAngleStart, backhandShotAngleEnd, t);
            hitDir = shotAngle * facing;
            if (facing < 0) hitDir += 180f;
        }

        shuttlecock.HitMe(hitSpeed, hitDir);
        shuttlecock.lastHitter = name;

        var sync = NetworkBattleSync.Instance;
        if (sync != null) sync.SendHit(hitSpeed, hitDir, shuttlecock.transform.position);

        hitZone.enabled = false;
        hitZoneWasActive = false;
    }

    void CheckSweptHit()
    {
        Vector3 headPos = hitZone.transform.position;
        Vector3 birdPos = shuttlecock.transform.position;

        Vector3 seg = headPos - prevHeadWorldPos;
        float segLen = seg.magnitude;
        float r = hitZone.radius;

        if (segLen < 0.001f)
        {
            if (Vector3.Distance(birdPos, headPos) <= r)
            {
                ApplyHit();
                swingStartedThisPress = true;
            }
            return;
        }

        float t = Mathf.Clamp01(Vector3.Dot(birdPos - prevHeadWorldPos, seg) / (segLen * segLen));
        Vector3 closest = prevHeadWorldPos + t * seg;

        if (Vector3.Distance(birdPos, closest) <= r)
        {
            ApplyHit();
            swingStartedThisPress = true;
        }
    }

    void UpdateWalkAnimation()
    {
        if (!isGrounded)
        {
            if (jumpFrame != null && jumpFrame.Length > 0)
                sr.sprite = jumpFrame[0];
            return;
        }

        if (isWalking)
        {
            walkFrameTimer += Time.deltaTime;
            if (walkFrameTimer >= 1f / walkFPS)
            {
                walkFrameTimer = 0f;
                bool movingForward = (facing == 1 && (MobileInput.MoveRight() || Input.GetKey(KeyCode.RightArrow)))
                    || (facing == -1 && (MobileInput.MoveLeft() || Input.GetKey(KeyCode.LeftArrow)));
                Sprite[] frames = movingForward ? walkRightFrames : walkLeftFrames;
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
            if (idleFrames != null && idleFrames.Length > 0)
                sr.sprite = idleFrames[0];
        }
    }

    void UpdateSwingOverlayFlip()
    {
        // parent's localScale already handles facing direction; don't double-flip
        Vector3 sScale = swingRenderer.transform.localScale;
        sScale.x = 1f;
        swingRenderer.transform.localScale = sScale;
    }

    void UpdateHitZonePosition(float smoothFrameIndex)
    {
        float t = Mathf.InverseLerp(hitStartFrame, hitEndFrame, smoothFrameIndex);
        bool isForehand = currentSwingFrames == forehandSwingFrames;
        if (!isForehand) t = 1f - t; // 反手：从结束点走到开始点

        Transform gripMarker = isForehand ? gripOverheadMarker : gripUnderhandMarker;
        Vector2 gripOffset;
        if (isForehand)
            gripOffset = (gripOverheadMarker != null) ? (Vector2)gripOverheadMarker.localPosition : gripOffsetOverhead;
        else
            gripOffset = (gripUnderhandMarker != null) ? (Vector2)gripUnderhandMarker.localPosition : gripOffsetUnderhand;

        // Use grip marker's overrideArc if enabled, otherwise use character params
        float aStart, aEnd, arcLen = racketLength;
        if (gripMarker != null)
        {
            var gm = gripMarker.GetComponent<GripMarker>();
            if (gm != null && gm.overrideArc)
            {
                aStart = gm.arcAngleStart + gm.arcRotation;
                aEnd = gm.arcAngleEnd + gm.arcRotation;
                arcLen = gm.arcLength;
            }
            else
            {
                aStart = isForehand ? racketAngleStart : racketAngleStartBackhand;
                aEnd = isForehand ? racketAngleEnd : racketAngleEndBackhand;
            }
        }
        else
        {
            aStart = isForehand ? racketAngleStart : racketAngleStartBackhand;
            aEnd = isForehand ? racketAngleEnd : racketAngleEndBackhand;
        }

        float racketAngle = Mathf.Lerp(aStart, aEnd, t);
        float rad = racketAngle * Mathf.Deg2Rad;
        float headX = -Mathf.Sin(rad) * arcLen;
        float headY = Mathf.Cos(rad) * arcLen;

        hitZone.radius = hitZoneRadius;
        hitZone.transform.localPosition = new Vector3(gripOffset.x + headX, gripOffset.y + headY, 0);
        hitZone.transform.localRotation = Quaternion.Euler(0, 0, racketAngle);
    }


    public void DebugResetState()
    {
        serving = false;
        serveRecovering = false;
        serveHitApplied = false;
        swingFrameIndex = 0;
        swingFrameTimer = 0f;
        serveRecoveryFrameIndex = 0;
        serveRecoveryFrameTimer = 0f;
        hitZoneWasActive = false;
        swingStartedThisPress = false;
        swingIsMiss = false;
        hitZone.enabled = false;
        currentSwingFrames = forehandSwingFrames;
        state = CharState.Idle;
    }

    public void SetShuttlecock(Shuttlecock s)
    {
        shuttlecock = s;
        if (shuttlecock != null)
        {
            if (serveMarker != null)
                shuttlecock.transform.position = serveMarker.position;
            serveFollowOffset = shuttlecock.transform.position - transform.position;
        }
    }

    void LateUpdate()
    {
        if (isNetworkRemote) return;
        var sync = NetworkBattleSync.Instance;
        if (sync != null && shuttlecock != null)
            sync.SendClientState(transform.position, facing,
                state == CharState.Swinging, state == CharState.Serving, isWalking,
                shuttlecock.transform.position, shuttlecock.isInPlay);
    }

    private bool remoteWasSwing;
    private Vector2 charSmoothVel;

    void UpdateRemoteVisuals()
    {
        if (state == CharState.Swinging)
            UpdateSwingAnimation();
        else if (state == CharState.Serving)
            UpdateServeAnimation();
        else if (state == CharState.Recovering)
            UpdateServeRecovery();
        UpdateWalkAnimation();
    }

    void ApplyRemoteState()
    {
        var sync = NetworkBattleSync.Instance;
        if (sync == null || !sync.Received) return;

        transform.position = Vector2.SmoothDamp(
            transform.position, sync.RemotePos, ref charSmoothVel, 0.06f);

        facing = sync.RemoteFacing;
        if ((sync.RemoteSwing || sync.RemoteServe) && !remoteWasSwing)
            SwingMe();
        remoteWasSwing = sync.RemoteSwing || sync.RemoteServe;
        isWalking = sync.RemoteWalk;

        if (shuttlecock != null && !sync.BirdieInPlay && serving)
            shuttlecock.transform.position = sync.BirdiePos;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * facing;
        transform.localScale = scale;
        UpdateSwingOverlayFlip();
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        Vector3 pos = transform.position;
        float s = 0.15f;

        // 正手/反手分界线
        float boundaryY = pos.y + hitZoneOffset.y;
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawLine(new Vector3(pos.x - 3f, boundaryY, 0), new Vector3(pos.x + 3f, boundaryY, 0));
        UnityEditor.Handles.Label(new Vector3(pos.x - 3f, boundaryY + 0.2f, 0), "正手↑\n反手↓");

        // 正手击球检测区
        Vector3 oh = pos + new Vector3((overlayOffsetOverhead.x + hitZoneOffset.x) * facing, overlayOffsetOverhead.y + hitZoneOffset.y, 0);
        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.7f);
        UnityEditor.Handles.DrawWireDisc(oh, Vector3.forward, hitZoneRadius);
        UnityEditor.Handles.DrawLine(oh + Vector3.left * s, oh + Vector3.right * s);
        UnityEditor.Handles.DrawLine(oh + Vector3.down * s, oh + Vector3.up * s);
        UnityEditor.Handles.Label(oh + Vector3.up * (hitZoneRadius + 0.25f), "正手击球区");

        // 反手击球检测区
        Vector3 uh = pos + new Vector3((overlayOffsetUnderhand.x + hitZoneOffset.x) * facing, overlayOffsetUnderhand.y + hitZoneOffset.y, 0);
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.7f);
        UnityEditor.Handles.DrawWireDisc(uh, Vector3.forward, hitZoneRadius);
        UnityEditor.Handles.DrawLine(uh + Vector3.left * s, uh + Vector3.right * s);
        UnityEditor.Handles.DrawLine(uh + Vector3.down * s, uh + Vector3.up * s);
        UnityEditor.Handles.Label(uh + Vector3.up * (hitZoneRadius + 0.25f), "反手击球区");

        // 正手球拍位置
        UnityEditor.Handles.color = new Color(1f, 0.5f, 0f, 0.5f);
        Vector3 ovOh = pos + new Vector3(overlayOffsetOverhead.x * facing, overlayOffsetOverhead.y, 0);
        UnityEditor.Handles.DrawWireDisc(ovOh, Vector3.forward, 0.12f);
        UnityEditor.Handles.Label(ovOh + Vector3.right * 0.2f, "正手球拍");

        // 反手球拍位置
        UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.5f);
        Vector3 ovUh = pos + new Vector3(overlayOffsetUnderhand.x * facing, overlayOffsetUnderhand.y, 0);
        UnityEditor.Handles.DrawWireDisc(ovUh, Vector3.forward, 0.12f);
        UnityEditor.Handles.Label(ovUh + Vector3.right * 0.2f, "反手球拍");

        // 发球球拍位置
        UnityEditor.Handles.color = new Color(0.5f, 0.5f, 1f, 0.7f);
        Vector3 ovSv = pos + new Vector3(overlayOffsetServe.x * facing, overlayOffsetServe.y, 0);
        UnityEditor.Handles.DrawWireDisc(ovSv, Vector3.forward, 0.14f);
        UnityEditor.Handles.Label(ovSv + Vector3.right * 0.2f, "发球球拍");

        // 握拍标记（场景中可拖动的子物体 GripOverhead / GripUnderhand）
        if (swingRenderer != null)
        {
            float rMid = Mathf.Lerp(racketAngleStart, racketAngleEnd, 0.5f) * Mathf.Deg2Rad;
            if (gripOverheadMarker == null) gripOverheadMarker = swingRenderer.transform.Find("GripOverhead");
            if (gripUnderhandMarker == null) gripUnderhandMarker = swingRenderer.transform.Find("GripUnderhand");
            DrawGripGizmo(gripOverheadMarker, rMid, new Color(1f, 0.35f, 0f, 0.9f), "握拍(正手)");
            DrawGripGizmo(gripUnderhandMarker, rMid, new Color(0f, 0.75f, 0f, 0.9f), "握拍(反手)");
        }

        // 移动边界
        float gy = shuttlecock != null ? shuttlecock.groundY : -4.5f;
        float cy = shuttlecock != null ? shuttlecock.ceilingY : 5f;
        UnityEditor.Handles.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        UnityEditor.Handles.DrawLine(new Vector3(moveMinX, gy, 0), new Vector3(moveMinX, cy, 0));
        UnityEditor.Handles.Label(new Vector3(moveMinX, gy - 0.3f, 0), $"左边界 {moveMinX}");
        UnityEditor.Handles.color = new Color(0.3f, 0.3f, 1f, 0.8f);
        UnityEditor.Handles.DrawLine(new Vector3(moveMaxX, gy, 0), new Vector3(moveMaxX, cy, 0));
        UnityEditor.Handles.Label(new Vector3(moveMaxX, gy - 0.3f, 0), $"右边界 {moveMaxX}");

        // AI 防守站位
        UnityEditor.Handles.color = new Color(0f, 1f, 1f, 0.7f);
        UnityEditor.Handles.DrawWireDisc(new Vector3(aiIdlePositionX, transform.position.y, 0), Vector3.forward, 0.3f);
        UnityEditor.Handles.Label(new Vector3(aiIdlePositionX, transform.position.y - 0.5f, 0), $"AI防守={aiIdlePositionX}");
#endif
    }

    void DrawGripGizmo(Transform marker, float midAngleRad, Color color, string label)
    {
#if UNITY_EDITOR
        if (marker == null) return;
        Vector3 g = marker.position;
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.DrawWireDisc(g, Vector3.forward, 0.18f);
        UnityEditor.Handles.Label(g + Vector3.up * 0.25f, label);
        UnityEditor.Handles.color = new Color(color.r, color.g, color.b, 0.4f);
        Vector3 head = g + new Vector3(-Mathf.Sin(midAngleRad), Mathf.Cos(midAngleRad), 0) * racketLength;
        UnityEditor.Handles.DrawLine(g, head);
        UnityEditor.Handles.DrawWireDisc(head, Vector3.forward, 0.08f);
#endif
    }
}
