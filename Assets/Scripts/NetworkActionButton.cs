using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class NetworkActionButton : MonoBehaviour
{
    public enum Action { CreateRoom, JoinRoom, StopRoom }
    public Action action = Action.CreateRoom;
    public InputField ipInput;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        SoundManager.PlaySFX("shotgun");
        if (MultiplayerManager.Instance == null)
        {
            Debug.LogError("[NetBtn] MultiplayerManager.Instance is null!");
            return;
        }
        switch (action)
        {
            case Action.CreateRoom: MultiplayerManager.Instance.CreateRoom(); break;
            case Action.JoinRoom:
                string ip = (ipInput != null && !string.IsNullOrEmpty(ipInput.text.Trim())) ? ipInput.text.Trim() : null;
                if (string.IsNullOrEmpty(ip))
                    MultiplayerManager.Instance.StartDiscoveryAndJoin();
                else
                    MultiplayerManager.Instance.JoinRoom(ip);
                break;
            case Action.StopRoom: MultiplayerManager.Instance.StopNetwork(); break;
        }
    }
}
