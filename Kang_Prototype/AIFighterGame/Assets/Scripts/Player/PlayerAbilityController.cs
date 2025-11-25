using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ActiveSkill
{
    public string id = "dash_attack";
    public float baseCooldownSeconds = 5f;
    public float dashDistance = 4f;
    public float dashDuration = 0.2f;
    public float damageMultiplier = 1.5f;
}

/// <summary>
/// Handles equipped active skills and cooldowns. Default skill: dash forward and hit enemies in the path.
/// </summary>
public class PlayerAbilityController : MonoBehaviour
{
    [Header("Components")]
    public PlayerStatsComponent stats;
    public PlayerMovementController movement;
    public Rigidbody2D rb;

    [Header("Skill Settings")]
    public LayerMask enemyLayer;
    public List<ActiveSkill> equippedSkills = new List<ActiveSkill>();

    private readonly List<float> cooldownTimers = new List<float>();
    private bool isCasting;

    void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerStatsComponent>();
        if (movement == null) movement = GetComponent<PlayerMovementController>();
        if (rb == null) rb = GetComponent<Rigidbody2D>();

        if (equippedSkills.Count == 0)
        {
            equippedSkills.Add(new ActiveSkill());
        }

        SyncCooldownList();
    }

    void Update()
    {
        TickCooldowns();
    }

    public bool TryUseActiveSkill(int slotIndex = 0)
    {
        if (slotIndex < 0 || slotIndex >= equippedSkills.Count) return false;
        if (cooldownTimers[slotIndex] > 0f) return false;
        if (isCasting) return false;

        ActiveSkill skill = equippedSkills[slotIndex];
        float cooldown = CalculateCooldown(skill.baseCooldownSeconds);
        cooldownTimers[slotIndex] = cooldown;

        StartCoroutine(ExecuteDashAttack(skill));
        return true;
    }

    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= cooldownTimers.Count) return -1f;
        return Mathf.Max(0f, cooldownTimers[slotIndex]);
    }

    IEnumerator ExecuteDashAttack(ActiveSkill skill)
    {
        isCasting = true;

        int facing = movement != null ? movement.FacingSign : (int)Mathf.Sign(transform.localScale.x);
        if (facing == 0) facing = 1;

        Vector2 start = transform.position;
        Vector2 end = start + Vector2.right * facing * skill.dashDistance;

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, skill.dashDuration);

        while (elapsed < duration)
        {
            Vector2 target = Vector2.Lerp(start, end, elapsed / duration);
            MoveCharacter(target);
            elapsed += Time.deltaTime;
            yield return null;
        }

        MoveCharacter(end);
        DoSkillHit(end, skill.damageMultiplier);

        isCasting = false;
    }

    void DoSkillHit(Vector2 center, float damageMultiplier)
    {
        float baseRadius = 0.6f;
        float rangeBonus = stats != null ? stats.GetStat(PlayerStatType.ProjectileRange) * 0.2f : 0f;
        float radius = Mathf.Max(baseRadius, baseRadius + rangeBonus);

        float damage = stats != null ? stats.GetStat(PlayerStatType.AttackPower) * damageMultiplier : damageMultiplier;

        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius, enemyLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject) continue;

            PlayerStats legacyStats = hit.GetComponent<PlayerStats>();
            if (legacyStats != null)
            {
                legacyStats.TakeDamage(damage);
                continue;
            }

            PlayerStatsComponent newStats = hit.GetComponent<PlayerStatsComponent>();
            if (newStats != null)
            {
                newStats.ApplyDamage(damage);
            }
        }
    }

    void MoveCharacter(Vector2 target)
    {
        if (rb != null)
        {
            rb.MovePosition(target);
        }
        else
        {
            transform.position = target;
        }
    }

    float CalculateCooldown(float baseCooldown)
    {
        float haste = stats != null ? stats.GetStat(PlayerStatType.CooldownHaste) : 0f;
        return baseCooldown / Mathf.Max(0.1f, 1f + haste);
    }

    void TickCooldowns()
    {
        for (int i = 0; i < cooldownTimers.Count; i++)
        {
            if (cooldownTimers[i] <= 0f) continue;
            cooldownTimers[i] -= Time.deltaTime;
            if (cooldownTimers[i] < 0f) cooldownTimers[i] = 0f;
        }
    }

    void SyncCooldownList()
    {
        cooldownTimers.Clear();
        for (int i = 0; i < equippedSkills.Count; i++)
        {
            cooldownTimers.Add(0f);
        }
    }
}
