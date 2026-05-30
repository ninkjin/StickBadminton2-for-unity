using UnityEngine;

public class ConfettiPiece : MonoBehaviour
{
    private float dx, dy, gravity, lifetime, age, rotSpeed;
    private SpriteRenderer sr;

    public static void Spawn(Vector3 pos)
    {
        GameObject go = new GameObject("confetti");
        go.transform.position = pos;
        var cp = go.AddComponent<ConfettiPiece>();
        cp.Init();
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
        sr.color = new Color(Random.value, Random.value, Random.value);
        sr.sortingOrder = 100;
        transform.localScale = Vector3.one * Random.Range(0.06f, 0.16f);

        dx = Random.Range(-1.5f, 1.5f);
        dy = Random.Range(-0.5f, -0.1f);
        gravity = Random.Range(0.3f, 0.7f);
        lifetime = Random.Range(6f, 10f);
        rotSpeed = Random.Range(-40f, 40f);

        Destroy(gameObject, lifetime + 0.5f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / lifetime;
        float alpha = t < 0.15f ? t / 0.15f : (t > 0.6f ? (1f - t) / 0.4f : 1f);
        var c = sr.color;
        c.a = alpha;
        sr.color = c;

        dy -= gravity * Time.deltaTime;
        transform.position += new Vector3(dx * Time.deltaTime, dy * Time.deltaTime, 0);
        transform.Rotate(0, 0, rotSpeed * Time.deltaTime);
    }
}
