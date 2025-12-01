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

    void Update()
    {
        UpdateStatsDisplay();
        UpdateInventoryList();
    }

    void UpdateInventoryList()
    {
        if (inventoryText == null || itemManager == null) return;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>=== INVENTORY (T) ===</b>");

        if (itemManager.inventory.Count == 0)
        {
            sb.AppendLine("- Empty -");
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
                    sb.AppendLine($"<color=#808080>{item.itemName}</color>");
                }
            }
        }

        inventoryText.text = sb.ToString();
    }

    void UpdateStatsDisplay()
    {
        if (statsText == null || playerStats == null) return;

        string display = "<b>=== PLAYER ===</b>\n";
        display += $"HP: {playerStats.currentStats.HP:F0}/{playerStats.currentStats.MaxHP:F0}, ";
        display += $"Speed: {playerStats.currentStats.Speed:F0}\n";
        display += $"Attack: {playerStats.currentStats.Attack:F0}, ";
        display += $"Defense: {playerStats.currentStats.Defense:F0}\n";
        display += $"Haste: {playerStats.currentStats.CooldownHaste:F1}%\n\n";

        if (enemyStats != null)
        {
            display += "<b>=== ENEMY ===</b>\n";
            display += $"HP: {enemyStats.currentStats.HP:F0}/{enemyStats.currentStats.MaxHP:F0}\n\n";
        }

        if (itemManager.currentItem != null)
        {
            display += $"<b>ITEM: {itemManager.currentItem.itemName}</b>\n\n";
        }
        else
        {
            display += "<b>ITEM: None</b>\n\n";
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
        display += "Attack: J, Skill: Q\n";
        display += "Use Item: 4, Next Item: T\n";
        display += "1: Heal Player, 2: Damage Player\n";
        display += "3: Heal Enemy\n";


        statsText.text = display;
    }
}