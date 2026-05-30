using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CharButtonSelect : MonoBehaviour
{
    [Header("选人界面展示的全身图")]
    public Sprite characterSprite;

    [Header("传给战斗场景的头像")]
    public Sprite portraitSprite;

    [Header("管理器")]
    public CharacterSlideIn manager;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (manager != null)
            manager.TrySelect(characterSprite, portraitSprite);
    }
}
