using UnityEngine;
using UnityEngine.UI;

public class ShowAfterAnimation : MonoBehaviour
{
    public Animator backgroundAnimator;
    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Hide at start
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    void Update()
    {
        if (backgroundAnimator == null) return;

        var stateInfo = backgroundAnimator.GetCurrentAnimatorStateInfo(0);

        // When MainMenu animation finishes, show the button
        if (stateInfo.IsName("MainMenu") && stateInfo.normalizedTime >= 1f)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }
}
