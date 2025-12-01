using UnityEngine;

[System.Serializable]
public class Stats
{
    public float Speed = 10f;
    public float Attack = 50f;
    public float Defense = 30f;
    public float HP = 200f;
    public float MaxHP = 200f;
    public float Jump = 10f;
    public float Attack_Speed = 5f;
    public float Range = 100f;
    public float CooldownHaste = 0f;

    public Stats Clone()
    {
        return new Stats
        {
            Speed = this.Speed,
            Attack = this.Attack,
            Defense = this.Defense,
            HP = this.HP,
            MaxHP = this.MaxHP,
            Jump = this.Jump,
            Attack_Speed = this.Attack_Speed,
            Range = this.Range,
            CooldownHaste = this.CooldownHaste
        };
    }
}

public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    public Stats baseStats = new Stats();

    [Header("Current Stats (Runtime)")]
    public Stats currentStats = new Stats();

    [Header("Death Settings")]
    public bool isDead = false;

    [Header("UI")]
    public UnityEngine.UI.Slider hpBar;
    public GameObject damagePopupPrefab;
    public Transform popupSpawnPoint;

    void Start()
    {
        InitializeStats();

        if (hpBar != null)
        {
            hpBar.maxValue = baseStats.MaxHP;
            hpBar.value = currentStats.HP;
        }

        Debug.Log($"PlayerStats initialized - HP: {currentStats.HP}, Attack: {currentStats.Attack}");
    }

    public void InitializeStats()
    {
        currentStats = baseStats.Clone();
        isDead = false;
    }

    public void ModifyStats(Stats statChanges, float duration)
    {
        Debug.Log($"Modifying stats for {duration} seconds");

        currentStats.Speed += statChanges.Speed;
        currentStats.Attack += statChanges.Attack;
        currentStats.Defense += statChanges.Defense;
        currentStats.Jump += statChanges.Jump;
        currentStats.Attack_Speed += statChanges.Attack_Speed;
        currentStats.Range += statChanges.Range;
        currentStats.CooldownHaste += statChanges.CooldownHaste;

        currentStats.Speed = Mathf.Max(1f, currentStats.Speed);
        currentStats.Attack = Mathf.Max(1f, currentStats.Attack);
        currentStats.Defense = Mathf.Max(0f, currentStats.Defense);
        currentStats.Jump = Mathf.Max(1f, currentStats.Jump);
        currentStats.Attack_Speed = Mathf.Max(0.1f, currentStats.Attack_Speed);
        currentStats.Range = Mathf.Max(1f, currentStats.Range);

        LogCurrentStats();

        if (duration > 0)
        {
            Invoke(nameof(ResetToBaseStats), duration);
        }
    }

    public void ResetToBaseStats()
    {
        Debug.Log("Resetting stats to base values");
        currentStats = baseStats.Clone();
        currentStats.HP = Mathf.Min(currentStats.HP, currentStats.MaxHP);
        LogCurrentStats();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        float actualDamage = Mathf.Max(0, damage - currentStats.Defense);
        currentStats.HP -= actualDamage;
        currentStats.HP = Mathf.Max(0, currentStats.HP);

        if (hpBar != null)
        {
            hpBar.value = currentStats.HP;
        }

        if (damagePopupPrefab != null && popupSpawnPoint != null)
        {
            Vector3 spawnPos = popupSpawnPoint.position;
            GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity, GameObject.Find("Canvas").transform);
            DamagePopup popupScript = popup.GetComponent<DamagePopup>();
            if (popupScript != null)
            {
                popupScript.Initialize(actualDamage, actualDamage > 50);
            }
        }

        Debug.Log($"Took {actualDamage} damage! HP: {currentStats.HP}/{currentStats.MaxHP}");

        if (currentStats.HP <= 0)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentStats.HP += amount;
        currentStats.HP = Mathf.Min(currentStats.HP, currentStats.MaxHP);

        if (hpBar != null)
        {
            hpBar.value = currentStats.HP;
        }

        Debug.Log($"Healed {amount}! HP: {currentStats.HP}/{currentStats.MaxHP}");
    }

    void Die()
    {
        isDead = true;
        Debug.Log($"[DEATH] {gameObject.name} has died!");

        GetComponent<SpriteRenderer>().color = Color.gray;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Static;
        }

        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts)
        {
            if (script != this)
            {
                script.enabled = false;
            }
        }
    }

    void LogCurrentStats()
    {
        Debug.Log($"Current Stats - Speed: {currentStats.Speed}, Attack: {currentStats.Attack}, " +
                  $"Defense: {currentStats.Defense}, HP: {currentStats.HP}/{currentStats.MaxHP}, " +
                  $"Jump: {currentStats.Jump}, AttackSpeed: {currentStats.Attack_Speed}, Range: {currentStats.Range}");
    }
}