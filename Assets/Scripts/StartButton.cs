using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

[RequireComponent(typeof(Button))]
public class StartButton : MonoBehaviour
{
    [Header("选人管理器")]
    public CharacterSlideIn manager;

    [Header("选满后进入的场景名")]
    public string targetScene = "Battle Background";

    [Header("联机时禁用")]
    public bool disableInNetwork = false;

    private Button button;
    private bool isNetworkMode;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
        isNetworkMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (disableInNetwork)
            button.interactable = !isNetworkMode;
    }

    void Start()
    {
        if (isNetworkMode)
        {
            var lobby = NetworkLobbySync.Instance;
            if (lobby != null)
            {
                lobby.OnBothReady += OnBothPlayersReady;
            }
        }
    }

    void OnDestroy()
    {
        if (isNetworkMode && NetworkLobbySync.Instance != null)
            NetworkLobbySync.Instance.OnBothReady -= OnBothPlayersReady;
    }

    void OnBothPlayersReady()
    {
        CharacterSelection.TwoPlayerMode = true;
        SceneManager.LoadScene(targetScene);
    }

    private void OnClicked()
    {
        SoundManager.PlaySFX("shotgun");
        if (manager == null) return;

        if (isNetworkMode)
        {
            var lobby = NetworkLobbySync.Instance;
            // 双方都选了才能 Start
            if (lobby != null && lobby.GetMySelection() >= 0 && lobby.GetOpponentSelection() >= 0)
            {
                lobby.SetReady();
            }
        }
        else if (manager.SelectedCount >= 2)
        {
            CharacterSelection.LeftSprite = manager.LeftPortrait;
            CharacterSelection.RightSprite = manager.RightPortrait;
            SceneManager.LoadScene(targetScene);
        }
    }
}
