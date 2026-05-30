using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public Button pauseButton;
    public Button resumeButton;
    public Button menuButton;

    private Text toastText;
    private CanvasGroup toastGroup;
    private Coroutine toastRoutine;
    private bool isHost;
    private bool isNetwork;

    void Start()
    {
        pausePanel.SetActive(false);
        pauseButton.onClick.AddListener(OnPauseClicked);
        resumeButton.onClick.AddListener(OnResumeClicked);
        menuButton.onClick.AddListener(OnReturnClicked);

        isNetwork = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsConnected();
        isHost = isNetwork && MultiplayerManager.Instance.IsHost;

        CreateToast();
    }

    void CreateToast()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var go = new GameObject("ToastCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
        }

        var toastObj = new GameObject("Toast");
        toastObj.transform.SetParent(canvas.transform, false);
        var rt = toastObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -30);
        rt.sizeDelta = new Vector2(500, 60);

        toastText = toastObj.AddComponent<Text>();
        toastText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        toastText.fontSize = 22;
        toastText.alignment = TextAnchor.MiddleCenter;
        toastText.color = new Color(1f, 0.85f, 0.3f, 1f);

        toastGroup = toastObj.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
    }

    void ShowToast(string msg)
    {
        if (toastText != null)
            toastText.text = msg;
        if (toastRoutine != null)
            StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(ToastFade());
    }

    IEnumerator ToastFade()
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(1.5f);
        float t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - t / 0.5f;
            yield return null;
        }
        toastGroup.alpha = 0f;
    }

    void OnPauseClicked()
    {
        if (isNetwork && !isHost)
        {
            ShowToast("仅主机可以暂停游戏");
            return;
        }
        DoPause();
        var sync = NetworkBattleSync.Instance;
        if (sync != null) sync.SendPauseCommand("P");
    }

    void OnResumeClicked()
    {
        if (isNetwork && !isHost)
        {
            ShowToast("仅主机可以继续游戏");
            return;
        }
        DoResume();
        var sync = NetworkBattleSync.Instance;
        if (sync != null) sync.SendPauseCommand("R");
    }

    void OnReturnClicked()
    {
        if (isNetwork && !isHost)
        {
            ShowToast("仅主机可以返回主页");
            return;
        }
        DoReturn();
        var sync = NetworkBattleSync.Instance;
        if (sync != null) sync.SendPauseCommand("M");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (pausePanel.activeSelf)
                OnResumeClicked();
            else
                OnPauseClicked();
        }
    }

    public void DoPause()
    {
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
        pauseButton.gameObject.SetActive(false);
    }

    public void DoResume()
    {
        Time.timeScale = 1f;
        pausePanel.SetActive(false);
        pauseButton.gameObject.SetActive(true);
    }

    public void DoReturn()
    {
        Time.timeScale = 1f;

        // 重置选人和大厅状态
        CharacterSelection.Clear();
        CharacterSelection.SkipIntro = true;
        if (isNetwork)
        {
            var lobby = NetworkLobbySync.Instance;
            if (lobby != null) lobby.ResetState();
        }

        SoundManager.PlaySFX("shotgun");
        SceneManager.LoadScene("SampleScene");
    }

}
