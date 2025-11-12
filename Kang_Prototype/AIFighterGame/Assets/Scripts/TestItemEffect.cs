using UnityEngine;

public class TestItemEffect : MonoBehaviour
{
    public PlayerStats playerStats;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TestStatBoost();
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TestDamage();
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TestHeal();
        }
    }

    void TestStatBoost()
    {
        Debug.Log("=== Testing Stat Boost (Press 1) ===");

        Stats changes = new Stats
        {
            Speed = 5,
            Attack = 15,
            Attack_Speed = 3
        };

        playerStats.ModifyStats(changes, 5f);
    }

    void TestDamage()
    {
        Debug.Log("=== Testing Damage (Press 2) ===");
        playerStats.TakeDamage(30f);
    }

    void TestHeal()
    {
        Debug.Log("=== Testing Heal (Press 3) ===");
        playerStats.Heal(50f);
    }
}