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
    private Sprite leftPortrait;
    private Sprite rightPortrait;
    private bool isAnimating = false;
    private bool isNetworkMode = false;
    private int myNetworkSlot = -1; // 0=左, 1=右

    public bool CanSlide { get { return selectCount < (isNetworkMode ? 1 : 2) && !isAnimating; } }
    public int SelectedCount { get { return selectCount; } }
    public Sprite LeftPortrait { get { return leftPortrait; } }
    public Sprite RightPortrait { get { return rightPortrait; } }

    void Start()
    {
        isNetworkMode = MultiplayerManager.Instance != null && MultiplayerManager.Instance.IsConnected();
        if (isNetworkMode)
        {
            myNetworkSlot = MultiplayerManager.Instance.IsHost ? 0 : 1;
            var lobby = NetworkLobbySync.Instance;
            if (lobby != null)
            {
                lobby.OnSelectionChanged += OnNetworkSelectionChanged;
            }
        }
    }

    void OnDestroy()
    {
        if (isNetworkMode && NetworkLobbySync.Instance != null)
            NetworkLobbySync.Instance.OnSelectionChanged -= OnNetworkSelectionChanged;
    }

    void OnNetworkSelectionChanged()
    {
        var lobby = NetworkLobbySync.Instance;
        if (lobby == null) return;

        int opp = lobby.GetOpponentSelection();
        int my = lobby.GetMySelection();

        // 显示我的选择
        if (my >= 0 && my < characterSprites.Length)
        {
            if (myNetworkSlot == 0 && leftCharObj == null)
            {
                leftCharObj = CreateCharImage("LeftChar", characterSprites[my]);
                StartCoroutine(SlideToPosition(leftCharObj, leftEntryMarker, leftTargetMarker));
                selectCount++;
            }
            else if (myNetworkSlot == 1 && rightCharObj == null)
            {
                rightCharObj = CreateCharImage("RightChar", characterSprites[my]);
                StartCoroutine(SlideToPosition(rightCharObj, rightEntryMarker, rightTargetMarker));
                selectCount++;
            }
        }

        // 显示对手的选择 + 保存对手头像（从 CharButtonSelect 按钮读取）
        if (opp >= 0 && opp < characterSprites.Length)
        {
            Sprite oppPortrait = GetPortraitForIndex(opp);

            if (myNetworkSlot == 0 && rightCharObj == null)
            {
                rightCharObj = CreateCharImage("RightChar", characterSprites[opp]);
                CharacterSelection.RightSprite = oppPortrait;
                StartCoroutine(SlideToPosition(rightCharObj, rightEntryMarker, rightTargetMarker));
            }
            else if (myNetworkSlot == 1 && leftCharObj == null)
            {
                leftCharObj = CreateCharImage("LeftChar", characterSprites[opp]);
                CharacterSelection.LeftSprite = oppPortrait;
                StartCoroutine(SlideToPosition(leftCharObj, leftEntryMarker, leftTargetMarker));
            }
        }
    }

    Sprite GetPortraitForIndex(int idx)
    {
        var buttons = FindObjectsOfType<CharButtonSelect>();
        foreach (var btn in buttons)
        {
            int btnIdx = System.Array.IndexOf(characterSprites, btn.characterSprite);
            if (btnIdx == idx) return btn.portraitSprite;
        }
        return characterSprites[idx]; // fallback
    }

    public void TrySelect(Sprite displaySprite, Sprite portrait)
    {
        if (displaySprite == null || selectCount >= 2 || isAnimating) return;

        if (isNetworkMode)
        {
            TrySelectNetwork(displaySprite, portrait);
            return;
        }

        if (selectCount == 0)
        {
            leftCharObj = CreateCharImage("LeftChar", displaySprite);
            leftPortrait = portrait;
            StartCoroutine(SlideToPosition(leftCharObj, leftEntryMarker, leftTargetMarker));
        }
        else if (selectCount == 1)
        {
            rightCharObj = CreateCharImage("RightChar", displaySprite);
            rightPortrait = portrait;
            StartCoroutine(SlideToPosition(rightCharObj, rightEntryMarker, rightTargetMarker));
        }
        selectCount++;
    }

    void TrySelectNetwork(Sprite displaySprite, Sprite portrait)
    {
        var lobby = NetworkLobbySync.Instance;
        if (lobby == null) return;
        if (!lobby.IsMyTurn()) return;

        int spriteIndex = System.Array.IndexOf(characterSprites, displaySprite);
        if (spriteIndex < 0) return;

        // 保存 portrait 给战斗场景
        if (myNetworkSlot == 0)
        {
            leftPortrait = portrait;
            CharacterSelection.LeftSprite = portrait;
        }
        else
        {
            rightPortrait = portrait;
            CharacterSelection.RightSprite = portrait;
        }

        lobby.SelectCharacter(spriteIndex);
    }

    public void Undo()
    {
        if (isAnimating) return;

        if (selectCount == 2 && rightCharObj != null && !isNetworkMode)
        {
            selectCount--;
            StartCoroutine(SlideOutAndDestroy(rightCharObj, rightTargetMarker, rightEntryMarker, false));
        }
        else if (selectCount == 1 && leftCharObj != null && !isNetworkMode)
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
