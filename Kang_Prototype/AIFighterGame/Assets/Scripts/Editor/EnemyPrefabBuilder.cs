using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click Enemy prefab creator (damage handler + knockback ready).
/// </summary>
public static class EnemyPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Enemy.prefab";

    [MenuItem("Tools/Enemy/Create Enemy Prefab")]
    public static void CreateEnemyPrefab()
    {
        GameObject enemy = new GameObject("Enemy");

        // Layer & Tag
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer == -1)
        {
            Debug.LogWarning("Layer 'Enemy' not found. Please add it in Project Settings > Tags and Layers.");
        }
        else
        {
            enemy.layer = enemyLayer;
        }

        try
        {
            enemy.tag = "Enemy";
        }
        catch (UnityException)
        {
            Debug.LogWarning("Tag 'Enemy' not found. Please add it in the Tag Manager.");
        }

        // Core components
        Rigidbody2D rb = enemy.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        CapsuleCollider2D col = enemy.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Vertical;
        col.size = new Vector2(0.8f, 1.6f);

        SpriteRenderer sr = enemy.AddComponent<SpriteRenderer>();

        // Stats & damage handling
        PlayerStatsComponent stats = enemy.AddComponent<PlayerStatsComponent>();
        EnemyDamageHandler damageHandler = enemy.AddComponent<EnemyDamageHandler>();
        damageHandler.stats = stats;
        damageHandler.rb = rb;
        damageHandler.spriteRenderer = sr;

        // Save prefab
        EnsureFolderExists("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAssetAndConnect(enemy, PrefabPath, InteractionMode.UserAction);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"Enemy prefab created at {PrefabPath}");
    }

    static void EnsureFolderExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = "Assets";
            foreach (string part in path.Substring("Assets/".Length).Split('/'))
            {
                string current = $"{parent}/{part}";
                if (!AssetDatabase.IsValidFolder(current))
                {
                    AssetDatabase.CreateFolder(parent, part);
                }

                parent = current;
            }
        }
    }
}
