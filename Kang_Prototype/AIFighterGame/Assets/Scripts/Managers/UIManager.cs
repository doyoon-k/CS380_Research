using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    public Text statsText;

    [Header("References")]
    public PlayerStats playerStats;
    public PlayerStats enemyStats;
    public SkillManager skillManager;
    public ItemManager itemManager;

    void Update()
    {
        UpdateStatsDisplay();
    }

    void UpdateStatsDisplay()
    {
        if (statsText == null || playerStats == null) return;

        string display = "<b>=== PLAYER ===</b>\n";
        display += $"HP: {playerStats.currentStats.HP:F0}/{playerStats.currentStats.MaxHP:F0}\n";
        display += $"Speed: {playerStats.currentStats.Speed:F0}\n";
        display += $"Attack: {playerStats.currentStats.Attack:F0}\n";
        display += $"Defense: {playerStats.currentStats.Defense:F0}\n\n";

        if (enemyStats != null)
        {
            display += "<b>=== ENEMY ===</b>\n";
            display += $"HP: {enemyStats.currentStats.HP:F0}/{enemyStats.currentStats.MaxHP:F0}\n\n";
        }

        if (itemManager != null && itemManager.testItems != null && itemManager.testItems.Length > 0)
        {
            display += "<b>=== CURRENT ITEM ===</b>\n";
            display += $"{itemManager.testItems[itemManager.currentItemIndex].itemName}\n\n";
        }

        if (skillManager != null && skillManager.activeSkills.Count > 0)
        {
            display += "<b>=== ACTIVE SKILLS ===</b>\n";
            for (int i = 0; i < skillManager.activeSkills.Count; i++)
            {
                string key = i == 0 ? "Q" : "E";
                display += $"{key}: {skillManager.activeSkills[i].skillData.name}\n";
            }
            display += "\n";
        }

        display += "<b>=== CONTROLS ===</b>\n";
        display += "Move: A/D\n";
        display += "Jump: Space\n";
        display += "Attack: J\n";
        display += "Use Item: 4\n";
        display += "Next Item: 5\n";
        display += "Clear All Cache: 6\n";
        display += "Clear Current: 7\n";
        display += "Skill 1: Q | Skill 2: E";

        statsText.text = display;
    }
}