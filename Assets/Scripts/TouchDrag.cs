using UnityEngine;
using UnityEngine.EventSystems;

public class TouchDrag : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    private RectTransform rt;
    private float pinchStartDist;
    private float pinchStartScale;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (Input.touchCount == 2)
        {
            pinchStartDist = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            pinchStartScale = rt.localScale.x;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (TouchButtonLayout.Instance == null || !TouchButtonLayout.Instance.EditMode) return;

        if (Input.touchCount == 2)
        {
            // 双指捏合缩放
            float dist = Vector2.Distance(Input.GetTouch(0).position, Input.GetTouch(1).position);
            float ratio = dist / Mathf.Max(pinchStartDist, 1f);
            float s = Mathf.Clamp(pinchStartScale * ratio, 0.5f, 3f);
            rt.localScale = new Vector3(s, s, 1f);
            TouchButtonLayout.Instance.UpdateResizerPosition(rt);
        }
        else
        {
            // 单指拖动位置
            rt.anchoredPosition += eventData.delta / GetCanvasScale();
            TouchButtonLayout.Instance.UpdateResizerPosition(rt);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // no-op
    }

    float GetCanvasScale()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas != null) return canvas.scaleFactor;
        return 1f;
    }
}
