using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class NetworkActionButton : MonoBehaviour
{
    public enum Action { CreateRoom, JoinRoom, StopRoom }
    public Action action = Action.CreateRoom;

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
            case Action.JoinRoom: MultiplayerManager.Instance.JoinRoom(); break;
            case Action.StopRoom: MultiplayerManager.Instance.StopNetwork(); break;
        }
    }
}
