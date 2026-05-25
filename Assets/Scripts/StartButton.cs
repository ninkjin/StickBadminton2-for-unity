using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StartButton : MonoBehaviour
{
    [Header("选人管理器")]
    public CharacterSlideIn manager;

    [Header("选满后进入的场景名")]
    public string targetScene = "SampleScene";

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        if (manager == null) return;

        if (manager.SelectedCount >= 2)
        {
            SceneManager.LoadScene(targetScene);
        }
    }
}
