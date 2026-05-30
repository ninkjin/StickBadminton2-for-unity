using UnityEngine;
using UnityEngine.UI;

public class ShowAfterAnimation : MonoBehaviour
{
    public Animator backgroundAnimator;
    public CanvasGroup[] hideDuringIntro;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 从战斗场景返回时跳过开场动画
        if (CharacterSelection.SkipIntro)
        {
            CharacterSelection.SkipIntro = false;
            ShowAll();
            if (backgroundAnimator != null)
                backgroundAnimator.Play("MainMenu", 0, 1f);
        }
        else
        {
            HideAll();
        }
    }

    void Update()
    {
        if (CharacterSelection.SkipIntro) return;
        if (backgroundAnimator == null) return;

        var stateInfo = backgroundAnimator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.IsName("MainMenu") && stateInfo.normalizedTime >= 1f)
        {
            ShowAll();
        }
    }

    void ShowAll()
    {
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        foreach (var cg in hideDuringIntro)
        {
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.blocksRaycasts = true;
            }
        }
    }

    void HideAll()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        foreach (var cg in hideDuringIntro)
        {
            if (cg != null)
            {
                cg.alpha = 0f;
                cg.blocksRaycasts = false;
            }
        }
    }
}
