using UnityEngine;
using UnityEngine.UI;

public class ModalPanel : MonoBehaviour
{
    private GameObject overlay;

    void OnEnable()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 在面板下面插一个全屏遮罩
        overlay = new GameObject("ModalOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        overlay.transform.SetSiblingIndex(transform.GetSiblingIndex()); // 在面板前面

        var img = overlay.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;
        var rt = overlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void OnDisable()
    {
        if (overlay != null)
        {
            Destroy(overlay);
            overlay = null;
        }
    }
}
