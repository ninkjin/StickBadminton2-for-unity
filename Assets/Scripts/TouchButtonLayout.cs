using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TouchButtonLayout : MonoBehaviour
{
    public static TouchButtonLayout Instance { get; private set; }
    public bool EditMode { get; private set; }

    [Header("场景按钮引用")]
    public GameObject editButton;
    public GameObject doneButton;

    private List<GameObject> previews = new List<GameObject>();
    private Dictionary<RectTransform, GameObject> activeResizers = new Dictionary<RectTransform, GameObject>();

    private static readonly Color[] BtnColors = {
        new Color(1f, 1f, 1f, 0.157f),
        new Color(1f, 1f, 1f, 0.157f),
        new Color(0.209f, 1f, 0.071f, 0.157f),
        new Color(1f, 0f, 0f, 0.157f),
    };
    private static readonly Vector2[] BtnSizes = {
        new Vector2(102.5f, 101.43f),
        new Vector2(112.19f, 101.44f),
        new Vector2(150f, 150f),
        new Vector2(300f, 300f),
    };
    private static readonly float[,] DefaultLayout = {
        { -566, -187, 1f },
        { -349, -186, 1f },
        { 439, -190, 0.73f },
        { 575, -95, 0.36f },
    };

    void Awake() { Instance = this; }

    void Start()
    {
        if (editButton != null)
            editButton.GetComponent<Button>().onClick.AddListener(EnterEditMode);

        // 完成按钮动态创建（场景GameObject存不住）
        if (doneButton == null)
        {
            doneButton = CreateUIButton(editButton.transform.parent, "完成调整",
                new Color(0.3f, 0.5f, 0.3f, 0.85f), ExitEditMode);
            var drt = doneButton.GetComponent<RectTransform>();
            drt.anchoredPosition = editButton.GetComponent<RectTransform>().anchoredPosition;
            doneButton.SetActive(false);
        }
        else
        {
            doneButton.GetComponent<Button>().onClick.AddListener(ExitEditMode);
        }
    }

    GameObject CreateUIButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        go.AddComponent<Button>().onClick.AddListener(onClick);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(160, 40);

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 18;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
        return go;
    }

    private GameObject modalOverlay;

    public void EnterEditMode()
    {
        EditMode = true;
        if (editButton != null) editButton.SetActive(false);
        if (doneButton != null) doneButton.SetActive(true);

        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // 全屏遮罩挡住其他按钮
        modalOverlay = new GameObject("EditModalOverlay");
        modalOverlay.transform.SetParent(canvas.transform, false);
        modalOverlay.transform.SetAsLastSibling(); // 最上层
        var ovImg = modalOverlay.AddComponent<Image>();
        ovImg.color = new Color(0f, 0f, 0f, 0f); // 全透明
        ovImg.raycastTarget = true;
        var ovRt = modalOverlay.GetComponent<RectTransform>();
        ovRt.anchorMin = Vector2.zero;
        ovRt.anchorMax = Vector2.one;
        ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;

        // 预览和+/-在遮罩上面
        string[] names = { "BtnLeft", "BtnRight", "BtnJump", "BtnSwing" };
        for (int i = 0; i < 4; i++)
            previews.Add(CreatePreviewButton(canvas.transform, names[i], i));

        // 完成按钮也在遮罩上面
        if (doneButton != null) doneButton.transform.SetAsLastSibling();
    }

    GameObject CreatePreviewButton(Transform parent, string name, int idx)
    {
        var go = new GameObject(name + "_preview");
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = BtnColors[idx];
        img.sprite = GetCircleSprite();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = BtnSizes[idx];

        float x = PlayerPrefs.GetFloat("TouchBtn_" + name + "_x", DefaultLayout[idx, 0]);
        float y = PlayerPrefs.GetFloat("TouchBtn_" + name + "_y", DefaultLayout[idx, 1]);
        float s = PlayerPrefs.GetFloat("TouchBtn_" + name + "_s", DefaultLayout[idx, 2]);
        rt.anchoredPosition = new Vector2(x, y);
        rt.localScale = new Vector3(s, s, 1f);

        go.AddComponent<TouchDrag>();

        var resizerGo = new GameObject("Resizer");
        resizerGo.transform.SetParent(parent, false);
        var resizerRt = resizerGo.AddComponent<RectTransform>();
        resizerRt.pivot = new Vector2(0.5f, 1f);
        resizerRt.sizeDelta = new Vector2(120, 48);
        UpdatePreviewResizerPos(resizerRt, rt);

        var hlg = resizerGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var csf = resizerGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        MakeResizeBtn(resizerGo, " - ", () => { Resize(rt, resizerRt, -0.1f); });
        MakeResizeBtn(resizerGo, " + ", () => { Resize(rt, resizerRt, 0.1f); });

        activeResizers[rt] = resizerGo;
        return go;
    }

    void Resize(RectTransform rt, RectTransform resizerRt, float delta)
    {
        float s = Mathf.Clamp(rt.localScale.x + delta, 0.5f, 3f);
        rt.localScale = new Vector3(s, s, 1f);
        UpdatePreviewResizerPos(resizerRt, rt);
    }

    void MakeResizeBtn(GameObject parent, string label, UnityEngine.Events.UnityAction cb)
    {
        var go = new GameObject("Btn");
        go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.35f, 0.95f);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 52;
        le.preferredHeight = 44;
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(52, 44);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(cb);

        var txtGo = new GameObject("Lbl");
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<Text>();
        txt.text = label;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 26;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
        var txtRt = txtGo.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
    }

    void UpdatePreviewResizerPos(RectTransform resizerRt, RectTransform btnRt)
    {
        resizerRt.anchoredPosition = btnRt.anchoredPosition +
            new Vector2(0, btnRt.sizeDelta.y * btnRt.localScale.y * 0.5f + 30);
    }

    public void UpdateResizerPosition(RectTransform rt)
    {
        if (activeResizers.TryGetValue(rt, out var r) && r != null)
            UpdatePreviewResizerPos(r.GetComponent<RectTransform>(), rt);
    }

    static Sprite cachedCircleSprite;
    Sprite GetCircleSprite()
    {
        if (cachedCircleSprite != null) return cachedCircleSprite;
        int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        var c = new Vector2(s / 2f, s / 2f);
        float r = s / 2f - 4f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = d < r - 2f ? 1f : (d < r ? (r - d) / 2f : 0f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        return cachedCircleSprite;
    }

    public void ExitEditMode()
    {
        EditMode = false;
        if (editButton != null) editButton.SetActive(true);
        if (doneButton != null) doneButton.SetActive(false);

        if (modalOverlay != null) { Destroy(modalOverlay); modalOverlay = null; }

        string[] names = { "BtnLeft", "BtnRight", "BtnJump", "BtnSwing" };
        for (int i = 0; i < previews.Count && i < 4; i++)
        {
            var rt = previews[i].GetComponent<RectTransform>();
            PlayerPrefs.SetFloat("TouchBtn_" + names[i] + "_x", rt.anchoredPosition.x);
            PlayerPrefs.SetFloat("TouchBtn_" + names[i] + "_y", rt.anchoredPosition.y);
            PlayerPrefs.SetFloat("TouchBtn_" + names[i] + "_s", rt.localScale.x);
        }
        PlayerPrefs.Save();

        foreach (var r in activeResizers.Values) if (r != null) Destroy(r);
        activeResizers.Clear();
        foreach (var p in previews) if (p != null) Destroy(p);
        previews.Clear();
    }
}
