using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CharacterSlideIn : MonoBehaviour
{
    [Header("角色图片（按顺序对应7个按钮）")]
    public Sprite[] characterSprites;

    [Header("位置标记（拖入Hierarchy里的标记物体）")]
    public RectTransform leftEntryMarker;
    public RectTransform leftTargetMarker;
    public RectTransform rightEntryMarker;
    public RectTransform rightTargetMarker;

    [Header("图片大小")]
    public float imageSizeMultiplier = 3f;

    [Header("动画")]
    public float slideDuration = 0.5f;
    public Canvas parentCanvas;

    private int selectCount = 0;
    private GameObject leftCharObj;
    private GameObject rightCharObj;
    private bool isAnimating = false;

    public bool CanSlide { get { return selectCount < 2 && !isAnimating; } }
    public int SelectedCount { get { return selectCount; } }

    public void TrySelect(Sprite sprite)
    {
        if (sprite == null || selectCount >= 2 || isAnimating) return;

        if (selectCount == 0)
        {
            leftCharObj = CreateCharImage("LeftChar", sprite);
            StartCoroutine(SlideToPosition(leftCharObj, leftEntryMarker, leftTargetMarker));
        }
        else if (selectCount == 1)
        {
            rightCharObj = CreateCharImage("RightChar", sprite);
            StartCoroutine(SlideToPosition(rightCharObj, rightEntryMarker, rightTargetMarker));
        }

        selectCount++;
    }

    public void Undo()
    {
        if (isAnimating) return;

        if (selectCount == 2 && rightCharObj != null)
        {
            selectCount--;
            StartCoroutine(SlideOutAndDestroy(rightCharObj, rightTargetMarker, rightEntryMarker, false));
        }
        else if (selectCount == 1 && leftCharObj != null)
        {
            selectCount--;
            StartCoroutine(SlideOutAndDestroy(leftCharObj, leftTargetMarker, leftEntryMarker, true));
        }
    }

    private GameObject CreateCharImage(string name, Sprite sprite)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parentCanvas != null ? parentCanvas.transform : transform, false);

        Image img = obj.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;

        RectTransform rt = obj.GetComponent<RectTransform>();
        float w = sprite.rect.width * imageSizeMultiplier;
        float h = sprite.rect.height * imageSizeMultiplier;
        rt.sizeDelta = new Vector2(w, h);

        return obj;
    }

    private IEnumerator SlideToPosition(GameObject obj, RectTransform fromMarker, RectTransform toMarker)
    {
        if (fromMarker == null || toMarker == null) yield break;

        isAnimating = true;
        RectTransform rt = obj.GetComponent<RectTransform>();
        Vector2 from = fromMarker.anchoredPosition;
        Vector2 to = toMarker.anchoredPosition;
        rt.anchoredPosition = from;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = 1f - (1f - t) * (1f - t);
            rt.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }
        rt.anchoredPosition = to;
        isAnimating = false;
    }

    private IEnumerator SlideOutAndDestroy(GameObject obj, RectTransform fromMarker, RectTransform toMarker, bool isLeft)
    {
        if (fromMarker == null || toMarker == null) yield break;

        isAnimating = true;
        RectTransform rt = obj.GetComponent<RectTransform>();
        Vector2 from = fromMarker.anchoredPosition;
        Vector2 to = toMarker.anchoredPosition;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = t * t;
            rt.anchoredPosition = Vector2.Lerp(from, to, t);
            yield return null;
        }

        Destroy(obj);
        if (isLeft) leftCharObj = null;
        else rightCharObj = null;
        isAnimating = false;
    }

    public void ResetSelection()
    {
        selectCount = 0;
        if (leftCharObj != null) Destroy(leftCharObj);
        if (rightCharObj != null) Destroy(rightCharObj);
        leftCharObj = null;
        rightCharObj = null;
    }
}
