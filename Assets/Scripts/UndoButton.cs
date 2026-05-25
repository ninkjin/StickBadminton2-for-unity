using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class UndoButton : MonoBehaviour
{
    [Header("选人管理器")]
    public CharacterSlideIn manager;

    [Header("没有角色时切回的场景名")]
    public string previousScene = "SampleScene";

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        if (manager == null) return;

        if (manager.SelectedCount > 0)
        {
            manager.Undo();
        }
        else
        {
            SceneManager.LoadScene(previousScene);
        }
    }
}
