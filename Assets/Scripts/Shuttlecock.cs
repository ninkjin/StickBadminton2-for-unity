using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class Shuttlecock : MonoBehaviour
{
    [Header("Physics Settings")]
    public float gravityScale = 1.2f;
    public float linearDrag = 2.5f;
    public float angularDrag = 8f;
    public float minVelocity = 0.5f;

    [Header("Ground")]
    public float groundY = -4.5f;
    public float netX = 0f;

    [Header("State")]
    public bool isInPlay = false;
    public string lastHitter = "";

    [Header("Visual (child object)")]
    public Transform visual;

    private Rigidbody2D rb;
    private Vector3 startPosition;

    public System.Action<string> OnLanded;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.mass = 1f;
        rb.gravityScale = gravityScale;
        rb.drag = linearDrag;
        rb.angularDrag = angularDrag;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        startPosition = transform.position;

        // Auto-find visual child
        if (visual == null)
        {
            var found = transform.Find("Visual");
            if (found != null) visual = found;
        }
    }

    void Update()
    {
        if (!isInPlay) return;

        if (rb.velocity.magnitude < minVelocity && rb.velocity.magnitude > 0.01f)
            rb.velocity = Vector2.zero;

        if (transform.position.y < groundY)
            Land();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (col.gameObject.CompareTag("Net"))
        {
            rb.velocity = new Vector2(rb.velocity.x * 0.2f, rb.velocity.y * 0.5f);
        }
        else if (col.gameObject.CompareTag("Wall"))
        {
            rb.velocity = new Vector2(-rb.velocity.x * 0.5f, rb.velocity.y);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ground"))
        {
            Land();
        }
    }

    public void Serve(float directionX, float speedX, float speedY)
    {
        rb.bodyType = RigidbodyType2D.Dynamic;
        transform.position = startPosition;
        rb.velocity = new Vector2(speedX * directionX, speedY);
        isInPlay = true;
        lastHitter = "";
    }

    public void Hit(Vector2 force)
    {
        rb.velocity = force;
    }

    public void ResetTo(Vector3 pos)
    {
        isInPlay = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.velocity = Vector2.zero;
        transform.position = pos;
        startPosition = pos;
    }

    void Land()
    {
        if (!isInPlay) return;
        isInPlay = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.velocity = Vector2.zero;

        string scorer = transform.position.x < netX ? "Right" : "Left";
        OnLanded?.Invoke(scorer);
    }
}
