using UnityEngine;

public class TouchLayoutLoader : MonoBehaviour
{
    void Start()
    {
        string key = "TouchBtn_" + name;
        float x = PlayerPrefs.GetFloat(key + "_x", float.NaN);
        float y = PlayerPrefs.GetFloat(key + "_y", float.NaN);
        float s = PlayerPrefs.GetFloat(key + "_s", float.NaN);

        var rt = GetComponent<RectTransform>();
        if (!float.IsNaN(x))
            rt.anchoredPosition = new Vector2(x, y);
        if (!float.IsNaN(s))
            rt.localScale = new Vector3(s, s, 1f);
    }
}
