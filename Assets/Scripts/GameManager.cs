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
    public float serveSpeedX = 6f;
    public float serveSpeedY = 9f;

    [Header("Score")]
    public int leftScore = 0;
    public int rightScore = 0;
    public int winScore = 7;

    [Header("State")]
    public GameState state = GameState.WaitingToServe;
    public string server = "Left";

    private float stateTimer = 0f;
    private bool gameEnded = false;

    void Start()
    {
        if (shuttlecock != null)
        {
            shuttlecock.OnLanded += OnShuttlecockLanded;
            shuttlecock.ResetTo(defaultServePos);
        }
        if (player != null)
            player.transform.position = new Vector3(defaultServePos.x, player.transform.position.y, player.transform.position.z);
        state = GameState.WaitingToServe;
    }

    void Update()
    {
        if (gameEnded) return;

        switch (state)
        {
            case GameState.WaitingToServe:
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Space))
                    ServeShuttlecock();
                break;

            case GameState.PointScored:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    ResetAfterPoint();
                break;
        }
    }

    void ServeShuttlecock()
    {
        if (shuttlecock == null) return;

        Vector3 servePos = server == "Left"
            ? defaultServePos
            : new Vector3(-defaultServePos.x, defaultServePos.y, defaultServePos.z);
        float dir = server == "Left" ? 1f : -1f;

        shuttlecock.ResetTo(servePos);
        shuttlecock.Serve(dir, serveSpeedX, serveSpeedY);

        if (server == "Left" && player != null)
            player.PlayServeAnimation();
        else if (server == "Right" && opponent != null)
            opponent.PlayServeAnimation();

        state = GameState.Playing;
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

        Vector3 pos = server == "Left"
            ? defaultServePos
            : new Vector3(-defaultServePos.x, defaultServePos.y, defaultServePos.z);
        shuttlecock.ResetTo(pos);
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
        shuttlecock.ResetTo(defaultServePos);
        state = GameState.WaitingToServe;
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
            stateText = "按 S 发球";
        else if (state == GameState.GameOver)
            stateText = $"游戏结束！{(leftScore >= winScore ? "左边" : "右边")}获胜！按R重新开始";

        GUI.Label(new Rect(0, 70, Screen.width, 40), stateText, style);

        if (state == GameState.GameOver && Input.GetKeyDown(KeyCode.R))
            RestartGame();
    }
}
