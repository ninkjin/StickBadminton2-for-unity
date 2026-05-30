using UnityEngine;

[ExecuteAlways]
public class PortraitFollow : MonoBehaviour
{
    [Header("头像偏移（相对父物体位置）")]
    public Vector2 offset = new Vector2(0f, 2f);

    [Header("头像大小")]
    public float size = 0.8f;

    [Header("是左边玩家还是右边对手")]
    public bool isLeftPlayer = true;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 80;

        transform.localScale = Vector3.one * size;

        if (Application.isPlaying)
        {
            Sprite portrait = isLeftPlayer ? CharacterSelection.LeftSprite : CharacterSelection.RightSprite;
            if (portrait != null) sr.sprite = portrait;
        }
    }

    void LateUpdate()
    {
        if (transform.parent == null) return;
        transform.position = transform.parent.position + (Vector3)offset;

        float facing = Mathf.Sign(transform.parent.localScale.x);
        Vector3 s = Vector3.one * size;
        s.x *= facing;
        transform.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        if (transform.parent == null) return;
        Gizmos.color = Color.yellow;
        Vector3 pos = transform.parent.position + (Vector3)offset;
        Gizmos.DrawWireSphere(pos, 0.15f);
    }
}
