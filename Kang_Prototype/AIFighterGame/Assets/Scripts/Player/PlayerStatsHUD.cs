using UnityEngine;
using UnityEngine.UI;
using System.Text;
using TMPro;

/// <summary>
/// Displays key player stats in the top-left UI text.
/// </summary>
public class PlayerStatsHUD : MonoBehaviour
{
    [Header("References")]
    public PlayerStatsComponent stats;
    public PlayerCombatController combat;
    public PlayerAbilityController ability;
    public TMP_Text hudText;

    StringBuilder _builder = new StringBuilder(256);

    void Awake()
    {
        if (stats == null)
        {
            stats = GetComponentInParent<PlayerStatsComponent>();
        }

        if (combat == null)
        {
            combat = GetComponentInParent<PlayerCombatController>();
        }

        if (ability == null)
        {
            ability = GetComponentInParent<PlayerAbilityController>();
        }
    }

    void Update()
    {
        if (hudText == null || stats == null) return;
        UpdateText();
    }

    void UpdateText()
    {
        _builder.Length = 0;
        _builder.AppendLine("=== PLAYER ===");
        _builder.AppendFormat("HP: {0:0}/{1:0}\n", stats.CurrentHealth, stats.GetStat(PlayerStatType.MaxHealth));
        _builder.AppendFormat("Attack: {0:0.0}  AS: {1:0.00}\n", stats.GetStat(PlayerStatType.AttackPower), stats.GetStat(PlayerStatType.AttackSpeed));
        if (combat != null)
        {
            _builder.AppendFormat("Atk CD: {0:0.00}s\n", combat.AttackCooldownRemaining);
        }
        _builder.AppendFormat("Move: {0:0.0}  Jump: {1:0.0}\n", stats.GetStat(PlayerStatType.MovementSpeed), stats.GetStat(PlayerStatType.JumpPower));
        _builder.AppendFormat("Range: {0:0.0}  CD Haste: {1:0.00}\n", stats.GetStat(PlayerStatType.ProjectileRange), stats.GetStat(PlayerStatType.CooldownHaste));
        _builder.AppendFormat("Defense: {0:0.0}\n", stats.GetStat(PlayerStatType.Defense));

        if (ability != null && ability.equippedSkills != null && ability.equippedSkills.Count > 0)
        {
            _builder.AppendLine("\n=== SKILLS ===");
            for (int i = 0; i < ability.equippedSkills.Count; i++)
            {
                var skill = ability.equippedSkills[i];
                float cd = ability.GetCooldownRemaining(i);
                _builder.AppendFormat("{0}: {1}  CD: {2:0.00}s\n", i, skill.id, cd);
            }
        }

        hudText.text = _builder.ToString();
    }
}
