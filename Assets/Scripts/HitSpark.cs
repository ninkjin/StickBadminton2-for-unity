using UnityEngine;

public class HitSpark : MonoBehaviour
{
    private float dx, dy, lifetime, age;
    private SpriteRenderer sr;

    public static void Burst(Vector3 pos, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("spark");
            go.transform.position = pos;
            var hs = go.AddComponent<HitSpark>();
            hs.Init();
        }
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
        sr.color = new Color(1f, Random.Range(0.65f, 0.95f), Random.Range(0f, 0.2f));
        sr.sortingOrder = 95;
        transform.localScale = Vector3.one * Random.Range(0.04f, 0.1f);

        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float spd = Random.Range(3f, 10f);
        dx = Mathf.Cos(angle) * spd;
        dy = Mathf.Sin(angle) * spd;
        lifetime = Random.Range(0.3f, 0.7f);

        Destroy(gameObject, lifetime + 0.1f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / lifetime;
        float alpha = 1f - t;
        var c = sr.color;
        c.a = alpha;
        sr.color = c;

        transform.position += new Vector3(dx * Time.deltaTime, dy * Time.deltaTime, 0);
    }
}
