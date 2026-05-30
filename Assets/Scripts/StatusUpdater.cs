using UnityEngine;
using UnityEngine.UI;

public class StatusUpdater : MonoBehaviour
{
    public Text text;
    public MultiplayerManager manager;

    void Update()
    {
        if (manager == null)
            manager = MultiplayerManager.Instance;
        if (text != null && manager != null)
            text.text = manager.statusText;
    }
}
