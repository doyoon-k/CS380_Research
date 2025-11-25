using UnityEngine;

public class PlayerCombatController : MonoBehaviour
{
    [Header("Components")]
    public PlayerStatsComponent stats;
    public PlayerMovementController movement;

    [Header("Attack Settings")]
    public Transform attackOrigin;
    public LayerMask enemyLayer;
    public float attackRange = 0.75f;
    public float baseAttackInterval = 0.6f;
    public float attackDamageMultiplier = 1f;
    public float knockbackForce = 5f;

    private float nextAttackTime;

    public float AttackCooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);

    void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStatsComponent>();
        if (movement == null) movement = GetComponent<PlayerMovementController>();
    }

    public bool TryAttack()
    {
        if (stats == null) return false;
        if (Time.time < nextAttackTime) return false;

        float attackSpeed = Mathf.Max(0.05f, stats.GetStat(PlayerStatType.AttackSpeed));
        float cooldown = baseAttackInterval / attackSpeed;
        nextAttackTime = Time.time + cooldown;

        PerformAttack();
        return true;
    }

    void PerformAttack()
    {
        Vector2 origin = attackOrigin != null ? (Vector2)attackOrigin.position : (Vector2)transform.position;
        int facing = movement != null ? movement.FacingSign : (int)Mathf.Sign(transform.localScale.x);
        if (facing == 0) facing = 1;

        float reach = Mathf.Max(0.1f, attackRange);
        Vector2 center = origin + Vector2.right * facing * reach * 0.5f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, reach, enemyLayer);

        float damage = stats.GetStat(PlayerStatType.AttackPower) * attackDamageMultiplier;

        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;
            ApplyDamage(hit.gameObject, damage);
        }
    }

    void ApplyDamage(GameObject target, float amount)
    {
        EnemyDamageHandler enemyDamage = target.GetComponent<EnemyDamageHandler>();
        if (enemyDamage != null)
        {
            enemyDamage.TakeHit(amount, transform.position, knockbackForce);
            return;
        }

        PlayerStats legacyStats = target.GetComponent<PlayerStats>();
        if (legacyStats != null)
        {
            legacyStats.TakeDamage(amount);
            return;
        }

        PlayerStatsComponent newStats = target.GetComponent<PlayerStatsComponent>();
        if (newStats != null)
        {
            newStats.ApplyDamage(amount);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector2 origin = attackOrigin != null ? (Vector2)attackOrigin.position : (Vector2)transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, attackRange);
    }
}
