using System.Collections;
using UnityEngine;

public enum PlayerStatType
{
    AttackPower,
    AttackSpeed,
    ProjectileRange,
    MovementSpeed,
    MaxHealth,
    Defense,
    JumpPower,
    CooldownHaste
}

[System.Serializable]
public class PlayerStatBlock
{
    [Header("Info")]
    public string Description = "Player";

    public float AttackPower = 50f;
    public float AttackSpeed = 1f;
    public float ProjectileRange = 4f;
    public float MovementSpeed = 8f;
    public float MaxHealth = 200f;
    public float Defense = 10f;
    public float JumpPower = 12f;
    public float CooldownHaste = 0f;

    public PlayerStatBlock Clone()
    {
        return (PlayerStatBlock)MemberwiseClone();
    }
}

/// <summary>
/// Central player stat container. Other controllers query here for runtime stat values and apply buffs/debuffs.
/// </summary>
public class PlayerStatsComponent : MonoBehaviour
{
    [Header("Base Stats")]
    public PlayerStatBlock baseStats = new PlayerStatBlock();

    [Header("Max Stats (modified)")]
    public PlayerStatBlock maxStats = new PlayerStatBlock();

    [Header("Runtime Stats (current)")]
    public PlayerStatBlock currentStats = new PlayerStatBlock();
    [SerializeField] private float currentHealth;

    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0f;

    void Awake()
    {
        ResetStatsToBase();
    }

    public void ResetStatsToBase()
    {
        maxStats = baseStats.Clone();
        currentStats = maxStats.Clone();
        currentHealth = Mathf.Clamp(currentHealth <= 0f ? maxStats.MaxHealth : currentHealth, 0f, maxStats.MaxHealth);
    }

    public float GetStat(PlayerStatType stat)
    {
        switch (stat)
        {
            case PlayerStatType.AttackPower: return currentStats.AttackPower;
            case PlayerStatType.AttackSpeed: return currentStats.AttackSpeed;
            case PlayerStatType.ProjectileRange: return currentStats.ProjectileRange;
            case PlayerStatType.MovementSpeed: return currentStats.MovementSpeed;
            case PlayerStatType.MaxHealth: return currentStats.MaxHealth;
            case PlayerStatType.Defense: return currentStats.Defense;
            case PlayerStatType.JumpPower: return currentStats.JumpPower;
            case PlayerStatType.CooldownHaste: return currentStats.CooldownHaste;
            default: return 0f;
        }
    }

    public void SetBaseStat(PlayerStatType stat, float newValue, bool alsoResetCurrent = false)
    {
        SetStatInternal(baseStats, stat, newValue);

        if (alsoResetCurrent)
        {
            ResetStatsToBase();
        }
        else
        {
            // Update the corresponding max/current stat while keeping other modifiers intact.
            SetStatInternal(maxStats, stat, newValue);
            SetStatInternal(currentStats, stat, newValue);
            ClampHealthToMax();
        }
    }

    public void ApplyAdditive(PlayerStatType stat, float delta, float duration = 0f)
    {
        float previousMax = GetMaxStat(stat);
        float previousCurrent = GetStat(stat);
        float ratio = previousMax > 0f ? previousCurrent / previousMax : 1f;

        float updatedMax = previousMax + delta;
        float updatedCurrent = Mathf.Min(updatedMax, updatedMax * ratio);

        SetStatInternal(maxStats, stat, updatedMax);
        SetStatInternal(currentStats, stat, updatedCurrent);
        ClampHealthToMax();

        if (duration > 0f)
        {
            StartCoroutine(RevertAfterDuration(stat, previousMax, previousCurrent, duration));
        }
    }

    public void ApplyMultiplier(PlayerStatType stat, float multiplier, float duration = 0f)
    {
        multiplier = Mathf.Max(0.01f, multiplier);
        float previousMax = GetMaxStat(stat);
        float previousCurrent = GetStat(stat);
        float ratio = previousMax > 0f ? previousCurrent / previousMax : 1f;

        float updatedMax = previousMax * multiplier;
        float updatedCurrent = Mathf.Min(updatedMax, updatedMax * ratio);

        SetStatInternal(maxStats, stat, updatedMax);
        SetStatInternal(currentStats, stat, updatedCurrent);
        ClampHealthToMax();

        if (duration > 0f)
        {
            StartCoroutine(RevertAfterDuration(stat, previousMax, previousCurrent, duration));
        }
    }

    IEnumerator RevertAfterDuration(PlayerStatType stat, float previousMax, float previousCurrent, float duration)
    {
        yield return new WaitForSeconds(duration);
        SetStatInternal(maxStats, stat, previousMax);
        SetStatInternal(currentStats, stat, Mathf.Min(previousCurrent, previousMax));
        ClampHealthToMax();
    }

    public float ApplyDamage(float rawDamage)
    {
        float mitigated = Mathf.Max(0f, rawDamage - maxStats.Defense);
        currentHealth = Mathf.Max(0f, currentHealth - mitigated);
        return mitigated;
    }

    public float Heal(float amount)
    {
        amount = Mathf.Max(0f, amount);
        currentHealth = Mathf.Min(maxStats.MaxHealth, currentHealth + amount);
        return currentHealth;
    }

    public void SetCurrentHealth(float newValue)
    {
        currentHealth = Mathf.Clamp(newValue, 0f, maxStats.MaxHealth);
    }

    void SetStatInternal(PlayerStatBlock target, PlayerStatType stat, float value)
    {
        float clamped = value;

        switch (stat)
        {
            case PlayerStatType.AttackPower:
                clamped = Mathf.Max(0.1f, value);
                target.AttackPower = clamped;
                break;
            case PlayerStatType.AttackSpeed:
                clamped = Mathf.Max(0.05f, value);
                target.AttackSpeed = clamped;
                break;
            case PlayerStatType.ProjectileRange:
                clamped = Mathf.Max(0.1f, value);
                target.ProjectileRange = clamped;
                break;
            case PlayerStatType.MovementSpeed:
                clamped = Mathf.Max(0.1f, value);
                target.MovementSpeed = clamped;
                break;
            case PlayerStatType.MaxHealth:
                clamped = Mathf.Max(1f, value);
                target.MaxHealth = clamped;
                break;
            case PlayerStatType.Defense:
                clamped = Mathf.Max(0f, value);
                target.Defense = clamped;
                break;
            case PlayerStatType.JumpPower:
                clamped = Mathf.Max(0.1f, value);
                target.JumpPower = clamped;
                break;
            case PlayerStatType.CooldownHaste:
                target.CooldownHaste = value;
                break;
        }
    }

    void ClampHealthToMax()
    {
        currentHealth = Mathf.Min(currentHealth, maxStats.MaxHealth);
    }

    float GetMaxStat(PlayerStatType stat)
    {
        switch (stat)
        {
            case PlayerStatType.AttackPower: return maxStats.AttackPower;
            case PlayerStatType.AttackSpeed: return maxStats.AttackSpeed;
            case PlayerStatType.ProjectileRange: return maxStats.ProjectileRange;
            case PlayerStatType.MovementSpeed: return maxStats.MovementSpeed;
            case PlayerStatType.MaxHealth: return maxStats.MaxHealth;
            case PlayerStatType.Defense: return maxStats.Defense;
            case PlayerStatType.JumpPower: return maxStats.JumpPower;
            case PlayerStatType.CooldownHaste: return maxStats.CooldownHaste;
            default: return 0f;
        }
    }
}
