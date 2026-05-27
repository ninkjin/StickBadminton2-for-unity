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

    void Start()
    {
        if (shuttlecock != null)
        {
            shuttlecock.OnLanded += OnShuttlecockLanded;
        }
        if (player != null)
        {
            player.transform.position = new Vector3(defaultServePos.x, player.transform.position.y, player.transform.position.z);
            player.SetShuttlecock(shuttlecock);
        }
        if (opponent != null)
        {
            opponent.SetShuttlecock(shuttlecock);
        }
        SetupServe();
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

        if (gameEnded)
        {
            if (Input.GetKeyDown(KeyCode.R))
                RestartGame();
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

    void SetupServe()
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

        if (scorer == "Left")
            leftScore++;
        else
            rightScore++;

        server = scorer;
        CheckWinCondition();

        state = GameState.PointScored;
        stateTimer = 1.5f;
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
        }
    }

    public void RestartGame()
    {
        leftScore = 0;
        rightScore = 0;
        gameEnded = false;
        server = "Left";
        state = GameState.WaitingToServe;
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

        style.fontSize = 24;
        string stateText = "";
        if (state == GameState.WaitingToServe)
            stateText = server == "Left" ? "按 S 发球" : "对手发球中...";
        else if (state == GameState.GameOver)
            stateText = $"游戏结束！{(leftScore >= winScore ? "左边" : "右边")}获胜！按R重新开始";

        GUI.Label(new Rect(0, 70, Screen.width, 40), stateText, style);

        if (forceUnderhandOnly)
        {
            style.normal.textColor = Color.yellow;
            GUI.Label(new Rect(0, 110, Screen.width, 40), "[F3] 仅反手模式", style);
        }
    }
}
