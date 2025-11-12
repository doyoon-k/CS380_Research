using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("Components")]
    public PlayerStats playerStats;
    public AttackHitbox attackHitbox;

    [Header("Attack Settings")]
    public float attackDuration = 0.2f;
    public float attackCooldown = 0.5f;

    private float lastAttackTime = 0f;
    private bool canAttack = true;

    void Start()
    {
        if (attackHitbox != null && playerStats != null)
        {
            attackHitbox.Initialize(playerStats);
            Debug.Log("CombatController initialized!");
        }
        else
        {
            Debug.LogError("AttackHitbox or PlayerStats not assigned!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J) && canAttack)
        {
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        Debug.Log("=== ATTACK! ===");

        canAttack = false;
        attackHitbox.ActivateHitbox(attackDuration);

        Invoke(nameof(ResetAttack), attackCooldown);
    }

    void ResetAttack()
    {
        canAttack = true;
        Debug.Log("Attack ready!");
    }
}