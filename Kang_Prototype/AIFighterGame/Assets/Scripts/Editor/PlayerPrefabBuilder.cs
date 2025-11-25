using UnityEditor;
using UnityEngine;

/// <summary>
/// One-click creator for the requested Player prefab (components, ground check, layers, tag wiring).
/// </summary>
public static class PlayerPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/Player/Create Player Prefab")]
    public static void CreatePlayerPrefab()
    {
        GameObject player = new GameObject("Player");

        // Core components
        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(0.8f, 1.6f);
        collider.direction = CapsuleDirection2D.Vertical;

        SpriteRenderer sr = player.AddComponent<SpriteRenderer>();

        // Gameplay scripts
        PlayerStatsComponent stats = player.AddComponent<PlayerStatsComponent>();
        PlayerMovementController movement = player.AddComponent<PlayerMovementController>();
        PlayerCombatController combat = player.AddComponent<PlayerCombatController>();
        PlayerAbilityController ability = player.AddComponent<PlayerAbilityController>();
        PlayerInputController input = player.AddComponent<PlayerInputController>();

        // Ground check child
        GameObject groundCheck = new GameObject("GroundCheck");
        groundCheck.transform.SetParent(player.transform);
        groundCheck.transform.localPosition = new Vector3(0f, -0.9f, 0f);

        // Wire references
        movement.stats = stats;
        movement.spriteRenderer = sr;
        movement.groundCheck = groundCheck.transform;
        movement.groundLayer = LayerMask.GetMask("Ground");

        combat.stats = stats;
        combat.movement = movement;
        combat.attackOrigin = player.transform;
        combat.enemyLayer = LayerMask.GetMask("Enemy");

        ability.stats = stats;
        ability.movement = movement;
        ability.rb = rb;
        ability.enemyLayer = LayerMask.GetMask("Enemy");

        input.movement = movement;
        input.combat = combat;
        input.ability = ability;

        // Tag and layer
        try
        {
            player.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("Player tag not found. Add a 'Player' tag in the Tag Manager.");
        }

        // Save prefab
        EnsureFolderExists("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAssetAndConnect(player, PrefabPath, InteractionMode.UserAction);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        Debug.Log($"Player prefab created at {PrefabPath}");
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
