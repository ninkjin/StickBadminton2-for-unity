using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PanelCloser : MonoBehaviour
{
    public GameObject targetPanel;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (targetPanel != null)
            {
                targetPanel.SetActive(false);
                SoundManager.PlaySFX("shotgun");
            }
        });
    }
}
