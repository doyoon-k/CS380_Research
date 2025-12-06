using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("Components")]
    public PlayerStats playerStats;
    public AttackHitbox attackHitbox;
    private PlayerController playerController;

    [Header("Attack Settings")]
    public float attackDuration = 0.2f;
    public float attackCooldown = 0.5f;

    private float lastAttackTime = 0f;
    private bool canAttack = true;

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();

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
        if (playerController != null && !playerController.IsInputEnabled) return;

        if (Input.GetKeyDown(KeyCode.J) && canAttack)
        {
            PerformAttack();
        }
    }

    void PerformAttack()
    {
        Debug.Log("=== ATTACK! ===");

        canAttack = false;
        lastAttackTime = Time.time;

        attackHitbox.gameObject.SetActive(true);
        attackHitbox.ActivateHitbox(attackDuration);

        Invoke(nameof(ResetAttack), attackCooldown);
        Invoke(nameof(DeactivateHitboxObject), attackDuration);
    }

    void DeactivateHitboxObject()
    {
        if (attackHitbox != null)
        {
            attackHitbox.gameObject.SetActive(false);
        }
    }

    void ResetAttack()
    {
        canAttack = true;
        Debug.Log("Attack ready!");
    }

    public float GetRemainingCooldown()
    {
        if (canAttack) return 0f;
        return Mathf.Max(0f, attackCooldown - (Time.time - lastAttackTime));
    }
}