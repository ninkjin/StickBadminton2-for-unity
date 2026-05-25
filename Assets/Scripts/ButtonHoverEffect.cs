using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("按钮状态图片")]
    public Sprite normalSprite;    // 1_up.png
    public Sprite hoverSprite;     // 2_over.png
    public Sprite pressedSprite;   // 3_down.png

    [Header("目标图片（子物体Image）")]
    public Image targetImage;      // 拖入子物体的Image

    [Header("悬停动画")]
    public float hoverScale = 1.1f;
    public float animDuration = 0.2f;

    [Header("按下动画")]
    public float pressedScale = 0.95f;

    [Header("颜色变化（可选）")]
    public bool useColorTint = true;
    public Color normalColor = Color.white;
    public Color hoverColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private Vector3 originalScale;
    private Coroutine scaleCoroutine;
    private Coroutine colorCoroutine;

    void Awake()
    {
        // 如果没指定targetImage，自动查找子物体的Image
        if (targetImage == null)
            targetImage = GetComponentInChildren<Image>();

        originalScale = targetImage.transform.localScale;

        if (normalSprite == null && targetImage != null)
            normalSprite = targetImage.sprite;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSprite != null)
            targetImage.sprite = hoverSprite;

        StartScaleAnimation(originalScale * hoverScale);
        if (useColorTint)
            StartColorAnimation(hoverColor);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (normalSprite != null)
            targetImage.sprite = normalSprite;

        StartScaleAnimation(originalScale);
        if (useColorTint)
            StartColorAnimation(normalColor);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pressedSprite != null)
            targetImage.sprite = pressedSprite;

        StartScaleAnimation(originalScale * pressedScale, animDuration * 0.5f);
        if (useColorTint)
            StartColorAnimation(pressedColor, animDuration * 0.5f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (hoverSprite != null)
            targetImage.sprite = hoverSprite;

        StartScaleAnimation(originalScale * hoverScale, animDuration * 0.5f);
        if (useColorTint)
            StartColorAnimation(hoverColor, animDuration * 0.5f);
    }

    private void StartScaleAnimation(Vector3 targetScale, float duration = -1f)
    {
        if (duration < 0) duration = animDuration;
        if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
        scaleCoroutine = StartCoroutine(AnimateScale(targetScale, duration));
    }

    private void StartColorAnimation(Color targetColor, float duration = -1f)
    {
        if (duration < 0) duration = animDuration;
        if (colorCoroutine != null) StopCoroutine(colorCoroutine);
        colorCoroutine = StartCoroutine(AnimateColor(targetColor, duration));
    }

    private IEnumerator AnimateScale(Vector3 targetScale, float duration)
    {
        Vector3 startScale = targetImage.transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - (1f - t) * (1f - t);
            targetImage.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        targetImage.transform.localScale = targetScale;
    }

    private IEnumerator AnimateColor(Color targetColor, float duration)
    {
        Color startColor = targetImage.color;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = 1f - (1f - t) * (1f - t);
            targetImage.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        targetImage.color = targetColor;
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (targetImage != null)
        {
            targetImage.transform.localScale = originalScale;
            if (normalSprite != null)
            {
                targetImage.sprite = normalSprite;
                targetImage.color = normalColor;
            }
        }
    }
}
