using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CharButtonSelect : MonoBehaviour
{
    [Header("对应的角色图片")]
    public Sprite characterSprite;

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
            manager.TrySelect(characterSprite);
    }
}
