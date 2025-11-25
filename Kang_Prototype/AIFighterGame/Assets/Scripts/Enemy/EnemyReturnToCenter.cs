using UnityEngine;

/// <summary>
/// Keeps the enemy drifting back to a target point (e.g., map center) so it doesn't stay knocked away.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyReturnToCenter : MonoBehaviour
{
    public Vector2 centerPosition = Vector2.zero;
    public float returnSpeed = 3f;
    public float stopDistance = 0.05f;
    public float hitPauseDuration = 0.2f;

    private Rigidbody2D rb;
    private float hitPauseTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void NotifyHit()
    {
        hitPauseTimer = hitPauseDuration;
    }

    void FixedUpdate()
    {
        if (hitPauseTimer > 0f)
        {
            hitPauseTimer -= Time.fixedDeltaTime;
            return;
        }

        Vector2 pos = rb.position;
        Vector2 toCenter = centerPosition - pos;
        float dist = toCenter.magnitude;

        if (dist <= stopDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        Vector2 dir = toCenter.normalized;
        float moveX = dir.x * returnSpeed;
        rb.linearVelocity = new Vector2(moveX, rb.linearVelocity.y);
    }
}
