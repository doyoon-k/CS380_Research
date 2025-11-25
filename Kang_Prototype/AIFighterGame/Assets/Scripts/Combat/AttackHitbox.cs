using UnityEngine;
using System.Collections.Generic;

public class AttackHitbox : MonoBehaviour
{
    [Header("Attack Properties")]
    public float damage = 10f;
    public float knockbackForce = 0.3f;
    public LayerMask enemyLayer;

    [Header("Hitbox Settings")]
    public Vector2 hitboxSize = new Vector2(1f, 1f);
    public Vector2 hitboxOffset = new Vector2(0.5f, 0f);

    private PlayerStats ownerStats;
    private bool isActive = false;
    private HashSet<Collider2D> hitTargets = new HashSet<Collider2D>();

    public void Initialize(PlayerStats stats)
    {
        ownerStats = stats;
    }

    public void ActivateHitbox(float duration)
    {
        isActive = true;
        hitTargets.Clear();
        Invoke(nameof(DeactivateHitbox), duration);
        Debug.Log("Hitbox activated!");
    }

    void DeactivateHitbox()
    {
        isActive = false;
        hitTargets.Clear();
        Debug.Log("Hitbox deactivated!");
    }

    void FixedUpdate()
    {
        if (!isActive) return;

        Vector2 hitboxPosition = (Vector2)transform.position + hitboxOffset;
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitboxPosition, hitboxSize, 0f, enemyLayer);

        foreach (Collider2D hit in hits)
        {
            if (hitTargets.Contains(hit)) continue;

            hitTargets.Add(hit);
            Debug.Log($"Hit detected: {hit.gameObject.name}");

            EnemyDamageHandler enemyDamage = hit.GetComponent<EnemyDamageHandler>();
            if (enemyDamage != null && ownerStats != null)
            {
                float totalDamage = ownerStats.currentStats.Attack;
                enemyDamage.TakeHit(totalDamage, transform.position, knockbackForce);
                continue;
            }

            PlayerStats enemyStats = hit.GetComponent<PlayerStats>();
            if (enemyStats != null && ownerStats != null)
            {
                float totalDamage = ownerStats.currentStats.Attack;
                enemyStats.TakeDamage(totalDamage);

                Rigidbody2D enemyRb = hit.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    Vector2 knockbackDirection = (hit.transform.position - transform.position).normalized;
                    enemyRb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
                    Debug.Log($"Applied knockback: {knockbackDirection * knockbackForce}");
                }

                EnemyAI enemyAI = hit.GetComponent<EnemyAI>();
                if (enemyAI != null)
                {
                    enemyAI.OnHit();
                }
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isActive ? Color.red : Color.yellow;
        Vector2 hitboxPosition = (Vector2)transform.position + hitboxOffset;
        Gizmos.DrawWireCube(hitboxPosition, hitboxSize);
    }
}
