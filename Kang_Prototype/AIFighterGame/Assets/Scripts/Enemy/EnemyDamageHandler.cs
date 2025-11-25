using UnityEngine;

/// <summary>
/// Basic enemy damage receiver: applies damage (with optional stats), knockback, and simple death handling.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class EnemyDamageHandler : MonoBehaviour
{
    [Header("Components")]
    public PlayerStatsComponent stats;
    public Rigidbody2D rb;
    public SpriteRenderer spriteRenderer;

    [Header("State")]
    public bool IsDead { get; private set; }

    public float CurrentHealth => stats != null ? stats.CurrentHealth : 0f;
    public float MaxHealth => stats != null ? stats.GetStat(PlayerStatType.MaxHealth) : 0f;

    void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStatsComponent>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// Apply damage from a source position; returns damage actually applied after defense.
    /// </summary>
    public float TakeHit(float rawDamage, Vector2 hitOrigin, float knockbackForce)
    {
        if (IsDead) return 0f;

        float appliedDamage = 0f;

        if (stats == null)
        {
            Debug.LogWarning($"EnemyDamageHandler on {name} requires PlayerStatsComponent.");
            return 0f;
        }

        appliedDamage = stats.ApplyDamage(rawDamage);
        IsDead = stats.IsDead;

        ApplyKnockback(hitOrigin, knockbackForce);

        EnemyReturnToCenter returner = GetComponent<EnemyReturnToCenter>();
        if (returner != null)
        {
            returner.NotifyHit();
        }

        if (IsDead)
        {
            OnDeath();
        }

        return appliedDamage;
    }

    void ApplyKnockback(Vector2 hitOrigin, float knockbackForce)
    {
        if (rb == null) return;
        if (knockbackForce <= 0f) return;

        Vector2 dir = ((Vector2)transform.position - hitOrigin).normalized;
        rb.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
    }

    void OnDeath()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.gray;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        // Disable other scripts except this
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (mb != this)
            {
                mb.enabled = false;
            }
        }
    }
}
