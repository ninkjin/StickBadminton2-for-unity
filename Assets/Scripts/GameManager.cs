using UnityEngine;

public enum GameState { WaitingToServe, Playing, PointScored, GameOver }

public class GameManager : MonoBehaviour
{
    [Header("References")]
    public Shuttlecock shuttlecock;
    public BattleCharacter player;
    public AIController opponent;

    [Header("Serve Settings")]
    public Vector3 defaultServePos = new Vector3(-3f, 1f, 0f);

    [Header("Score")]
    public int leftScore = 0;
    public int rightScore = 0;
    public int winScore = 7;

    [Header("State")]
    public GameState state = GameState.WaitingToServe;
    public string server = "Left";
    public static bool forceUnderhandOnly = false;

    private float stateTimer = 0f;
    private bool gameEnded = false;
    private bool confettiSpawned = false;
    private bool perfectVictory = false;

    void Start()
    {
        Application.targetFrameRate = 90;

        if (shuttlecock != null)
        {
            shuttlecock.OnLanded += OnShuttlecockLanded;
        }

        // 根据摄像机可视范围动态设移动边界
        float camHalfW = Camera.main.orthographicSize * Camera.main.aspect;
        float camHalfH = Camera.main.orthographicSize;
        float margin = 0.5f;
        if (player != null)
        {
            player.moveMinX = -camHalfW + margin;
            player.moveMaxX = -margin;
        }
        if (opponent != null)
        {
            opponent.moveMaxX = camHalfW - margin;
            opponent.moveMinX = margin;
        }

        // 羽毛球墙壁边界匹配摄像机
        if (shuttlecock != null)
        {
            shuttlecock.wallLeftX = -camHalfW;
            shuttlecock.wallRightX = camHalfW;
        }

        // 背景图宽高分别填满摄像机（非等比拉伸，和设计时一样）
        var bg = GameObject.Find("BattleBackground");
        if (bg != null)
        {
            var bgSr = bg.GetComponent<SpriteRenderer>();
            if (bgSr != null && bgSr.sprite != null)
            {
                Vector2 bgSize = bgSr.sprite.bounds.size;
                bg.transform.localScale = new Vector3(camHalfW * 2f / bgSize.x, camHalfH * 2f / bgSize.y, 1f);
            }
        }

        bool isNetwork = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsConnected();
        bool isHost = isNetwork && MultiplayerManager.Instance.IsHost;

        // Client端：球由服务器权威，客户端不检测得分
        if (isNetwork && !isHost && shuttlecock != null)
            shuttlecock.isInPlay = false;

        if (player != null)
        {
            player.SetShuttlecock(shuttlecock);
            player.isNetworkRemote = isNetwork && !MultiplayerManager.Instance.IsHost;
            player.isNetworkHost = isNetwork && MultiplayerManager.Instance.IsHost;
        }
        if (opponent != null)
        {
            opponent.SetShuttlecock(shuttlecock);
            if (isNetwork)
            {
                opponent.aiControlled = false;
                opponent.isNetworkRemote = MultiplayerManager.Instance.IsHost;
            }
            else
            {
                opponent.aiControlled = !CharacterSelection.TwoPlayerMode;
            }
        }
        SetupServe();
        SoundManager.PlayMusic("background");
    }

    void Update()
    {
        // 调试快捷键
        if (Input.GetKeyDown(KeyCode.F1))
            DebugServeLeft();
        if (Input.GetKeyDown(KeyCode.F2))
            DebugServeRight();
        if (Input.GetKeyDown(KeyCode.F3))
            forceUnderhandOnly = !forceUnderhandOnly;
        if (Input.GetKeyDown(KeyCode.F4))
            DebugForceWin();
        if (Input.GetKeyDown(KeyCode.F5))
            DebugForceLose();
        if (Input.GetKeyDown(KeyCode.F6))
            ToggleAIControl();

        if (gameEnded)
        {
            // 持续撒花
            if (Random.value < 0.15f)
                SpawnConfetti();

            if (Input.GetMouseButtonDown(0))
            {
                // 联机模式只有主机可以重启
                bool isNetwork = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsConnected();
                if (!isNetwork || MultiplayerManager.Instance.IsHost)
                    RestartGame();
            }
            return;
        }

        switch (state)
        {
            case GameState.WaitingToServe:
                // Player presses S to serve (handled in BattleCharacter.HandleSwingInput)
                // If server is AI, trigger serve
                if (server == "Right" && opponent != null && opponent.serving)
                {
                    // AI handles serve automatically via its UpdateAI
                }
                break;

            case GameState.PointScored:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    ResetAfterPoint();
                break;
        }
    }

    public void SetupServe()
    {
        if (server == "Left" && player != null)
        {
            player.SetupServe();
            player.serving = true;
        }
        else if (server == "Right" && opponent != null)
        {
            opponent.SetupServe();
            opponent.serving = true;
        }
    }

    void OnShuttlecockLanded(string scorer)
    {
        if (state != GameState.Playing) return;

        bool isNetwork = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsConnected();
        bool isHost = isNetwork && MultiplayerManager.Instance.IsHost;

        SoundManager.PlaySFX("ding");

        if (isHost || !isNetwork)
        {
            // 主机端（或单机）：正常计分
            if (scorer == "Left")
                leftScore++;
            else
                rightScore++;

            server = scorer;

            var sync = NetworkBattleSync.Instance;
            if (sync != null)
                sync.SyncScore(leftScore, rightScore, server);

            CheckWinCondition();
        }
        else
        {
            // 客户端：不自行计分（等服务器S/P消息同步），但切状态防止卡住
            Debug.Log($"[GM] 客户端球落地，等待服务器比分同步... state={state} -> PointScored");
        }

        if (!gameEnded)
        {
            state = GameState.PointScored;
            stateTimer = 1.5f;
        }
    }

    public void ApplyScoreSync(int left, int right, string newServer)
    {
        Debug.Log($"[GM] ApplyScoreSync: {leftScore}-{rightScore} -> {left}-{right}, server={newServer}, prevState={state}");
        leftScore = left;
        rightScore = right;
        server = newServer;

        if (left == 0 && right == 0)
        {
            gameEnded = false;
            confettiSpawned = false;
            perfectVictory = false;
        }

        if (shuttlecock != null)
        {
            shuttlecock.isInPlay = false;
            shuttlecock.hasScored = false;
            shuttlecock.dx = 0f;
            shuttlecock.dy = 0f;
        }

        CheckWinCondition();

        if (!gameEnded)
        {
            state = GameState.PointScored;
            stateTimer = 1.5f;
            SetupServe();
        }
    }

    void ResetAfterPoint()
    {
        if (gameEnded) return;

        SetupServe();
        state = GameState.WaitingToServe;
    }

    void CheckWinCondition()
    {
        if (leftScore >= winScore || rightScore >= winScore)
        {
            gameEnded = true;
            state = GameState.GameOver;

            if (!confettiSpawned)
            {
                confettiSpawned = true;
                if ((leftScore >= winScore && rightScore == 0) || (rightScore >= winScore && leftScore == 0))
                    perfectVictory = true;

                if (leftScore >= winScore)
                    SoundManager.PlaySFX("cheer");
                else
                    SoundManager.PlaySFX("sigh");
            }
        }
    }

    void SpawnConfetti()
    {
        // 从屏幕顶部随机位置生成多个纸屑
        for (int i = 0; i < 1; i++)
        {
            float screenX = Camera.main != null
                ? Camera.main.ScreenToWorldPoint(new Vector3(Random.Range(0f, Screen.width), 0, 0)).x
                : Random.Range(-5f, 5f);
            float topY = Camera.main != null
                ? Camera.main.ScreenToWorldPoint(new Vector3(0, Screen.height, 0)).y + 0.5f
                : 6f;
            ConfettiPiece.Spawn(new Vector3(screenX, topY, 0));
        }
    }

    public void RestartGame()
    {
        leftScore = 0;
        rightScore = 0;
        gameEnded = false;
        confettiSpawned = false;
        perfectVictory = false;
        server = "Left";
        state = GameState.WaitingToServe;

        // 重置角色和球
        if (player != null)
        {
            player.DebugResetState();
            player.serving = false;
        }
        if (opponent != null)
        {
            opponent.DebugResetState();
            opponent.serving = false;
        }
        if (shuttlecock != null)
        {
            shuttlecock.isInPlay = false;
            shuttlecock.hasScored = false;
            shuttlecock.dx = 0f;
            shuttlecock.dy = 0f;
        }

        SetupServe();

        // 联机模式同步重置到客户端
        var sync = NetworkBattleSync.Instance;
        if (sync != null)
        {
            sync.SyncScore(0, 0, "Left");
            Debug.Log("[GM] 游戏重置，同步0-0到客户端");
        }
    }

    // Called by BattleCharacter when player serves
    public void OnPlayerServe()
    {
        state = GameState.Playing;
    }

    // Called by AIController when AI serves
    public void OnOpponentServe()
    {
        state = GameState.Playing;
    }

    [ContextMenu("调试：左边发球")]
    public void DebugServeLeft()
    {
        server = "Left";
        ForceResetToServe();
    }

    [ContextMenu("调试：右边发球")]
    public void DebugServeRight()
    {
        server = "Right";
        ForceResetToServe();
    }

    public void DebugForceWin()
    {
        leftScore = winScore;
        rightScore = 0;
        CheckWinCondition();
    }

    public void DebugForceLose()
    {
        rightScore = winScore;
        leftScore = 0;
        CheckWinCondition();
    }

    public void ToggleAIControl()
    {
        if (opponent != null)
        {
            opponent.aiControlled = !opponent.aiControlled;
            Debug.Log("[AI] 对手 AI 控制: " + (opponent.aiControlled ? "开启" : "关闭"));
        }
    }

    void ForceResetToServe()
    {
        state = GameState.WaitingToServe;
        stateTimer = 0f;

        if (shuttlecock != null)
        {
            shuttlecock.isInPlay = false;
            shuttlecock.dx = 0f;
            shuttlecock.dy = 0f;
            shuttlecock.speed = 0f;
        }

        if (player != null)
        {
            player.DebugResetState();
            player.transform.position = new Vector3(defaultServePos.x, player.transform.position.y, player.transform.position.z);
        }
        if (opponent != null)
        {
            opponent.DebugResetState();
            opponent.transform.position = new Vector3(-defaultServePos.x, opponent.transform.position.y, opponent.transform.position.z);
        }

        SetupServe();
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 40;
        style.normal.textColor = Color.white;
        style.alignment = TextAnchor.UpperCenter;

        GUI.Label(new Rect(0, 10, Screen.width, 60), $"{leftScore}  -  {rightScore}", style);

        if (state == GameState.GameOver)
        {
            string resultText;
            if (perfectVictory)
                resultText = "完美胜利！";
            else if (leftScore >= winScore)
                resultText = "你赢了！";
            else
                resultText = "再接再厉";

            GUIStyle bigStyle = new GUIStyle();
            bigStyle.fontSize = 60;
            bigStyle.normal.textColor = Color.white;
            bigStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 100), resultText, bigStyle);

            GUIStyle smallStyle = new GUIStyle();
            smallStyle.fontSize = 24;
            smallStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            smallStyle.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(0, Screen.height * 0.3f + 80, Screen.width, 40), "点击屏幕重新开始", smallStyle);
        }
        else
        {
            style.fontSize = 24;
            string stateText = "";
            if (state == GameState.WaitingToServe)
                stateText = server == "Left" ? "按 S 发球" : "对手发球中...";

            GUI.Label(new Rect(0, 70, Screen.width, 40), stateText, style);
        }

        if (forceUnderhandOnly)
        {
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, 110, Screen.width, 40), "[F3] 仅反手模式", style);
        }

        // AI控制提示已移除（手机版不需要调试信息）
    }
}
