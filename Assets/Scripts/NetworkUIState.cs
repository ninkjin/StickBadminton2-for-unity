using UnityEngine;

public class NetworkUIState : MonoBehaviour
{
    public GameObject[] showWhenDisconnected;
    public GameObject[] showWhenConnected;

    void Update()
    {
        bool connected = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsConnected();
        foreach (var go in showWhenDisconnected)
            if (go != null) go.SetActive(!connected);
        foreach (var go in showWhenConnected)
            if (go != null) go.SetActive(connected);
    }
}
