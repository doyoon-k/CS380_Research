using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic health bar that can follow either a player (PlayerStatsComponent) or an enemy (EnemyDamageHandler).
/// Assign a Slider (UI) and the target component.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    public Slider slider;
    public PlayerStatsComponent playerStats;
    public EnemyDamageHandler enemy;

    [Header("Behavior")]
    public bool hideWhenFull = false;

    void Awake()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }
    }

    void Update()
    {
        if (slider == null) return;

        float current = 0f;
        float max = 0f;

        if (playerStats != null)
        {
            current = playerStats.CurrentHealth;
            max = playerStats.GetStat(PlayerStatType.MaxHealth);
        }
        else if (enemy != null)
        {
            current = enemy.CurrentHealth;
            max = enemy.MaxHealth;
        }
        else
        {
            return;
        }

        max = Mathf.Max(1f, max);
        current = Mathf.Clamp(current, 0f, max);

        slider.maxValue = max;
        slider.value = current;

        if (hideWhenFull && current >= max)
        {
            if (slider.gameObject.activeSelf) slider.gameObject.SetActive(false);
        }
        else
        {
            if (!slider.gameObject.activeSelf) slider.gameObject.SetActive(true);
        }
    }
}
