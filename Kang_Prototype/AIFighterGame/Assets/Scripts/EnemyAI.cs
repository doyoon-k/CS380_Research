using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("AI Settings")]
    public Vector3 homePosition;
    public float returnSpeed = 3f;
    public float returnThreshold = 0.05f;
    public float idleTime = 1f;

    private Rigidbody2D rb;
    private PlayerStats stats;
    private float lastHitTime = 0f;
    private bool isReturning = false;

    void Start()
    {
        homePosition = transform.position;
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerStats>();

        if (rb != null)
        {
            rb.linearDamping = 5f;
        }

        Debug.Log($"Enemy home position set to: {homePosition}");
    }

    void Update()
    {
        if (stats != null && stats.isDead) return;

        float distanceFromHome = Vector3.Distance(transform.position, homePosition);

        if (Time.time - lastHitTime > idleTime && distanceFromHome > returnThreshold)
        {
            isReturning = true;
        }
        else if (distanceFromHome <= returnThreshold)
        {
            isReturning = false;
            transform.position = new Vector3(homePosition.x, transform.position.y, homePosition.z);
            if (rb != null)
            {
                rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            }
        }
    }

    void FixedUpdate()
    {
        if (stats != null && stats.isDead) return;
        if (!isReturning) return;
        if (rb == null) return;

        float distanceFromHome = Vector3.Distance(transform.position, homePosition);

        if (distanceFromHome <= returnThreshold)
        {
            transform.position = new Vector3(homePosition.x, transform.position.y, homePosition.z);
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            isReturning = false;
            return;
        }

        Vector2 direction = (homePosition - transform.position).normalized;
        float moveX = direction.x * returnSpeed;
        rb.linearVelocity = new Vector2(moveX, rb.linearVelocity.y);
    }

    public void OnHit()
    {
        lastHitTime = Time.time;
        isReturning = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Vector3 pos = Application.isPlaying ? homePosition : transform.position;
        Gizmos.DrawWireSphere(pos, 0.5f);
        Gizmos.DrawLine(pos + Vector3.left * 0.5f, pos + Vector3.right * 0.5f);
        Gizmos.DrawLine(pos + Vector3.up * 0.5f, pos + Vector3.down * 0.5f);
    }
}