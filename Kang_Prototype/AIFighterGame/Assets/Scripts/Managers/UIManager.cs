using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Text statsText;
    public Text inventoryText;

    [Header("References")]
    public PlayerStats playerStats;
    public PlayerStats enemyStats;
    public SkillManager skillManager;
    public ItemManager itemManager;
    public CombatController combatController;
    public SkillExecutor skillExecutor;

    void Start()
    {
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
        if (skillManager == null) skillManager = FindObjectOfType<SkillManager>();
        if (itemManager == null) itemManager = FindObjectOfType<ItemManager>();
        if (combatController == null) combatController = FindObjectOfType<CombatController>();
        if (skillExecutor == null) skillExecutor = FindObjectOfType<SkillExecutor>();

        if (skillExecutor == null) Debug.LogError("UIManager: SkillExecutor not found!");
    }

    void Update()
    {
        UpdateStatsDisplay();
        UpdateInventoryList();
    }

    void UpdateInventoryList()
    {
        if (inventoryText == null || itemManager == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<color=white><b>=== INVENTORY (T) ===</b></color>");

        if (itemManager.inventory.Count == 0)
        {
            sb.AppendLine("<color=grey>- Empty -</color>");
        }
        else
        {
            for (int i = 0; i < itemManager.inventory.Count; i++)
            {
                ItemData item = itemManager.inventory[i];

                if (i == itemManager.currentEquipIndex)
                {
                    sb.AppendLine($"<size=30><color=yellow><b>{item.itemName}</b></color></size>");
                }
                else
                {
                    sb.AppendLine($"<color=#AAAAAA>{item.itemName}</color>");
                }
            }
        }

        inventoryText.text = sb.ToString();
    }

    void UpdateStatsDisplay()
    {
        if (statsText == null || playerStats == null) return;

        string display = "<color=white><b>=== PLAYER ===</b></color>\n";
        display += $"<color=#CCCCCC><i>{playerStats.characterDescription}</i></color>\n";
        display += $"<color=white>HP:</color> <color=green>{playerStats.CurrentHealth:F0}/{playerStats.GetStat("MaxHealth"):F0}</color>\n";
        display += $"<color=white>Atk:</color> <color=yellow>{playerStats.GetStat("AttackPower"):F0}</color> | <color=white>Def:</color> <color=cyan>{playerStats.GetStat("Defense"):F0}</color>\n";
        display += $"<color=white>Spd:</color> <color=cyan>{playerStats.GetStat("MovementSpeed"):F0}</color> | <color=white>Jump:</color> <color=cyan>{playerStats.GetStat("JumpPower"):F0}</color>\n";
        display += $"<color=white>AtkSpd:</color> <color=cyan>{playerStats.GetStat("AttackSpeed"):F1}</color> | <color=white>Rng:</color> <color=cyan>{playerStats.GetStat("ProjectileRange"):F0}</color>\n";
        display += $"<color=white>Haste:</color> <color=cyan>{playerStats.GetStat("CooldownHaste"):F1}%</color>\n\n";

        if (enemyStats != null)
        {
            display += "<color=white><b>=== ENEMY ===</b></color>\n";
            display += $"<color=white>HP:</color> <color=red>{enemyStats.CurrentHealth:F0}/{enemyStats.GetStat("MaxHealth"):F0}</color>\n\n";
        }

        if (itemManager.currentItem != null)
        {
            display += $"<color=white><b>ITEM:</b></color> <color=yellow>{itemManager.currentItem.itemName}</color>\n\n";
        }
        else
        {
            display += "<color=white><b>ITEM:</b></color> <color=grey>None</color>\n\n";
        }

        if (skillManager != null && skillManager.activeSkills.Count > 0)
        {
            display += "<color=white><b>=== ACTIVE SKILLS ===</b></color>\n";
            for (int i = 0; i < skillManager.activeSkills.Count; i++)
            {
                string key = i == 0 ? "Q" : "E";
                var skill = skillManager.activeSkills[i];
                string skillName = skill.skillData.name;
                string skillDesc = skill.skillData.description;
                string primitives = string.Join(", ", skill.skillData.sequence);

                if (!skill.CanUse())
                {
                    float remaining = skill.skillData.cooldown - (Time.time - skill.lastUsedTime);
                    if (remaining < 0) remaining = 0;
                    display += $"<color=white>{key}:</color> <color=grey>{skillName}</color> <color=red>({remaining:F1}s)</color>\n";
                }
                else
                {
                    display += $"<color=white>{key}:</color> <color=white>{skillName}</color> <color=green>[READY]</color>\n";
                }

                // Detailed Info
                display += $"   <color=#AAAAAA><i>{skillDesc}</i></color>\n";
                display += $"   <color=#888888>Actions: {primitives}</color>\n";
            }
            display += "\n";
        }

        display += "<color=white><b>=== CONTROLS ===</b></color>\n";
        display += "<color=#DDDDDD>Move: A/D | Jump: Space</color>\n";

        // Attack Cooldown Display
        string attackStatus = "<color=green>[READY]</color>";
        if (combatController != null)
        {
            float atkCd = combatController.GetRemainingCooldown();
            if (atkCd > 0)
            {
                attackStatus = $"<color=red>({atkCd:F1}s)</color>";
            }
        }

        // Projectile Cooldown Display
        string shootStatus = "<color=green>[READY]</color>";
        if (skillExecutor != null)
        {
            float projCd = skillExecutor.GetProjectileCooldown();
            if (projCd > 0)
            {
                shootStatus = $"<color=red>({projCd:F1}s)</color>";
            }
        }

        display += $"<color=white>Attack: J</color> {attackStatus} | <color=white>Shoot: K</color> {shootStatus}\n";
        display += $"<color=white>Skill: Q/E</color>\n";
        display += "<color=#DDDDDD>Item: 4 | Swap: T</color>\n";
        display += "<color=#DDDDDD>Reset Stats: R | Respawn Enemy: E</color>\n";
        display += "<color=#DDDDDD>Debug: 1/2/3</color>\n";

        statsText.text = display;
    }
}