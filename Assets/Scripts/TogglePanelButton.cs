using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class TogglePanelButton : MonoBehaviour
{
    public GameObject targetPanel;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    void Toggle()
    {
        if (targetPanel != null)
        {
            targetPanel.SetActive(!targetPanel.activeSelf);
            SoundManager.PlaySFX("shotgun");
        }
    }
}
