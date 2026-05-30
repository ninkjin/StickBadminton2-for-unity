using UnityEngine;

public class FeatherTrail : MonoBehaviour
{
    private float dx, dy, spinSpeed, lifetime, age;
    private SpriteRenderer sr;

    public static void Spawn(Vector3 pos)
    {
        GameObject go = new GameObject("feather");
        go.transform.position = pos;
        var ft = go.AddComponent<FeatherTrail>();
        ft.Init();
    }

    static Sprite squareSprite;

    void Init()
    {
        if (squareSprite == null)
        {
            Texture2D tex = new Texture2D(4, 4);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            squareSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
        }

        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = squareSprite;
        sr.color = new Color(1f, 1f, 1f, 0.7f);
        sr.sortingOrder = 90;
        transform.localScale = Vector3.one * Random.Range(0.03f, 0.06f);

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float spd = Random.Range(0.5f, 2f);
        dx = Mathf.Cos(angle) * spd;
        dy = Mathf.Sin(angle) * spd;
        spinSpeed = Random.Range(100f, 300f) * (Random.value > 0.5f ? 1f : -1f);
        lifetime = Random.Range(0.5f, 1.2f);

        Destroy(gameObject, lifetime + 0.1f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / lifetime;
        // Fade and shrink
        var c = sr.color;
        c.a = (1f - t) * 0.6f;
        sr.color = c;
        transform.localScale *= 1f - Time.deltaTime * 2f;

        transform.position += new Vector3(dx * Time.deltaTime, dy * Time.deltaTime, 0);
        transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
    }
}
