using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;

public class SceneSwitcher : MonoBehaviour, IPointerClickHandler
{
    public string targetScene;
    public bool isTwoPlayerMode = false;
    public bool requireConnection = false;
    public bool disableInNetwork = false;

    bool waitingForOther;
    bool subscribed;
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void Update()
    {
        if (requireConnection && !subscribed && NetworkLobbySync.Instance != null)
        {
            NetworkLobbySync.Instance.OnBothSceneReady += OnBothSceneReady;
            subscribed = true;
        }

        if (button != null)
        {
            bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            if (disableInNetwork)
                button.interactable = !connected;
            else if (requireConnection)
                button.interactable = connected;
        }
    }

    void OnDestroy()
    {
        if (NetworkLobbySync.Instance != null)
            NetworkLobbySync.Instance.OnBothSceneReady -= OnBothSceneReady;
    }

    void OnBothSceneReady()
    {
        SceneManager.LoadScene(targetScene);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool connected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        // 直接拦截：联机时禁用的按钮 / 未联机时要求联机的按钮
        if (disableInNetwork && connected)
        {
            Debug.Log($"[SceneSwitcher] {name} 联机时被禁用，拦截点击");
            return;
        }
        if (requireConnection && !connected)
        {
            Debug.Log($"[SceneSwitcher] {name} 未联机时要求联机，拦截点击");
            return;
        }

        Debug.Log($"[SceneSwitcher] {name} 点击通过, targetScene={targetScene}");

        SoundManager.PlaySFX("shotgun");

        if (requireConnection)
        {
            if (MultiplayerManager.Instance == null || !MultiplayerManager.Instance.HasRemoteClient())
            {
                SoundManager.PlaySFX("sigh");
                Debug.Log($"[SceneSwitcher] {name} 无远程客户端，拒绝进入");
                return;
            }

            var lobby = NetworkLobbySync.Instance;
            if (lobby == null) return;

            if (waitingForOther) return;
            waitingForOther = true;
            CharacterSelection.TwoPlayerMode = true;
            lobby.SetSceneReady();
        }
        else
        {
            Debug.Log($"[SceneSwitcher] {name} 进入场景: {targetScene}");
            CharacterSelection.TwoPlayerMode = isTwoPlayerMode;
            SceneManager.LoadScene(targetScene);
        }
    }
}
