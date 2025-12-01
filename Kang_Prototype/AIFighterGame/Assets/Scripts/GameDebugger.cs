using UnityEngine;

public class GameDebugger : MonoBehaviour
{
    [Header("Targets")]
    public PlayerStats playerStats;
    public PlayerStats enemyStats;

    [Header("Debug Settings")]
    public float testDamage = 50f; // Set higher than Defense (30) to ensure damage
    public float testHeal = 50f;

    void Update()
    {
        // Key 1: Heal Player
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (playerStats != null)
            {
                Debug.Log($"=== [Debug] 1: Heal Player ({testHeal}) ===");
                playerStats.Heal(testHeal);
            }
            else
            {
                Debug.LogWarning("Player stats not assigned in GameDebugger!");
            }
        }

        // Key 2: Damage Player (Self Harm)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (playerStats != null)
            {
                Debug.Log($"=== [Debug] 2: Damage Player ({testDamage}) ===");
                // Damage calculation in PlayerStats handles defense subtraction
                playerStats.TakeDamage(testDamage);
            }
        }

        // Key 3: Heal Enemy (New Request)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            if (enemyStats != null)
            {
                Debug.Log($"=== [Debug] 3: Heal Enemy ({testHeal}) ===");
                enemyStats.Heal(testHeal);
            }
            else
            {
                Debug.LogWarning("Enemy stats not assigned in GameDebugger!");
            }
        }

        // Key 8: Kill Player (Optional)
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            if (playerStats != null) playerStats.TakeDamage(9999f);
        }

        // Key 9: Full Heal Player (Optional)
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            if (playerStats != null) playerStats.Heal(9999f);
        }
    }
}