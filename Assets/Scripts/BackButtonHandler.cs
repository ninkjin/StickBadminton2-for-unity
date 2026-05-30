using UnityEngine;

public class BackButtonHandler : MonoBehaviour
{
    private float lastBackTime;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.unscaledTime - lastBackTime < 2f)
            {
                Application.Quit();
            }
            else
            {
                lastBackTime = Time.unscaledTime;
                var toast = new GameObject("Toast");
                toast.transform.SetParent(transform, false);
                var txt = toast.AddComponent<UnityEngine.UI.Text>();
                txt.text = "再按一次退出游戏";
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 28;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(1f, 1f, 0f, 1f);
                txt.raycastTarget = false;
                var rt = toast.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0, -50);
                rt.sizeDelta = new Vector2(400, 60);
                Destroy(toast, 2f);
            }
        }
    }
}
