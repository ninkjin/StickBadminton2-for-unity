using UnityEngine;

public class ScaleBackgroundToCamera : MonoBehaviour
{
    void Start()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Vector2 bgSize = sr.sprite.bounds.size;
        float camH = Camera.main.orthographicSize * 2f;
        float camW = camH * Camera.main.aspect;

        transform.localScale = new Vector3(camW / bgSize.x, camH / bgSize.y, 1f);
    }
}

