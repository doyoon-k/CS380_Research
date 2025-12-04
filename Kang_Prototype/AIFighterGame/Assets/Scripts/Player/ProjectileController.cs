using UnityEngine;

public class ProjectileController : MonoBehaviour
{
    [Header("Settings")]
    public float speed = 10f;
    public float lifeTime = 3f;
    public float damage = 10f;
    public string element = "Physical"; // Fire, Ice, Lightning

    private Rigidbody2D rb;
    private Vector2 moveDir;

    public void Initialize(Vector2 direction, float dmg, string elem, Color color)
    {
        moveDir = direction;
        damage = dmg;
        element = elem;

        // Change color based on element
        GetComponent<SpriteRenderer>().color = color;

        // Auto destroy after lifetime
        Destroy(gameObject, lifeTime);
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = moveDir * speed;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Enemy"))
        {
            // Apply Damage (Assumes Enemy has PlayerStats too)
            PlayerStats enemyStats = collision.GetComponent<PlayerStats>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(damage);
                Debug.Log($"[Projectile] Hit Enemy! Dealt {damage} {element} damage.");
            }

            Destroy(gameObject); // Destroy bullet on hit
        }
        else if (collision.CompareTag("Ground"))
        {
            Destroy(gameObject); // Destroy bullet on wall hit
        }
    }
}