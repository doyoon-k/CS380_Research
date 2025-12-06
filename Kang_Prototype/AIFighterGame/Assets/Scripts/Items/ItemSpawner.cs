using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns item pickups randomly across the map at game start
/// Attach this to a GameObject in your scene
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Prefab with ItemPickup component")]
    public GameObject itemPickupPrefab;

    [Tooltip("All items to spawn (auto-populated or manually assigned")]
    public List<ItemData> itemsToSpawn = new List<ItemData>();

    [Header("Spawn Area")]
    [Tooltip("Center of spawn area")]
    public Vector2 spawnCenter = Vector2.zero;

    [Tooltip("Size of spawn area (width, height)")]
    public Vector2 spawnAreaSize = new Vector2(20f, 10f);

    [Tooltip("Minimum distance between items")]
    public float minDistanceBetweenItems = 2f;

    [Header("Options")]
    [Tooltip("Spawn all items or random subset")]
    public bool spawnAllItems = true;

    [Tooltip("Number of items to spawn if not spawning all")]
    public int numberOfItemsToSpawn = 5;

    [Tooltip("Y position for spawned items")]
    public float spawnHeight = 0f;

    private List<Vector2> spawnedPositions = new List<Vector2>();

    // Items will only spawn when manually triggered from editor
    // void Start() removed - use Inspector button or context menu instead

    [ContextMenu("Spawn Items Now")]
    public void SpawnItems()
    {
        if (itemPickupPrefab == null)
        {
            Debug.LogError("[ItemSpawner] Item pickup prefab is not assigned!");
            return;
        }

        if (itemsToSpawn == null || itemsToSpawn.Count == 0)
        {
            Debug.LogWarning("[ItemSpawner] No items to spawn! Load items first.");
            return;
        }

        spawnedPositions.Clear();

        List<ItemData> itemsToActuallySpawn;

        if (spawnAllItems)
        {
            itemsToActuallySpawn = new List<ItemData>(itemsToSpawn);
        }
        else
        {
            itemsToActuallySpawn = new List<ItemData>();
            int count = Mathf.Min(numberOfItemsToSpawn, itemsToSpawn.Count);

            // Random selection without duplicates
            List<ItemData> temp = new List<ItemData>(itemsToSpawn);
            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(0, temp.Count);
                itemsToActuallySpawn.Add(temp[randomIndex]);
                temp.RemoveAt(randomIndex);
            }
        }

        foreach (var itemData in itemsToActuallySpawn)
        {
            if (itemData == null) continue;

            Vector2 spawnPos = FindValidSpawnPosition();
            if (spawnPos != Vector2.zero)
            {
                SpawnItem(itemData, spawnPos);
            }
        }

        Debug.Log($"<color=cyan>[ItemSpawner] Spawned {itemsToActuallySpawn.Count} items</color>");
    }

    private Vector2 FindValidSpawnPosition()
    {
        int maxAttempts = 30;
        for (int i = 0; i < maxAttempts; i++)
        {
            float randomX = spawnCenter.x + Random.Range(-spawnAreaSize.x / 2f, spawnAreaSize.x / 2f);
            float randomY = spawnCenter.y + Random.Range(-spawnAreaSize.y / 2f, spawnAreaSize.y / 2f);
            Vector2 candidatePos = new Vector2(randomX, randomY);

            bool validPosition = true;
            foreach (var existingPos in spawnedPositions)
            {
                if (Vector2.Distance(candidatePos, existingPos) < minDistanceBetweenItems)
                {
                    validPosition = false;
                    break;
                }
            }

            if (validPosition)
            {
                spawnedPositions.Add(candidatePos);
                return candidatePos;
            }
        }

        Debug.LogWarning("[ItemSpawner] Could not find valid spawn position after max attempts");
        return spawnCenter; // Fallback to center
    }

    private void SpawnItem(ItemData itemData, Vector2 position)
    {
        Vector3 spawnPos = new Vector3(position.x, spawnHeight, position.y);
        GameObject itemObj = Instantiate(itemPickupPrefab, spawnPos, Quaternion.identity, transform);

        ItemPickup pickup = itemObj.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            pickup.itemData = itemData;
        }

        itemObj.name = $"Item_{itemData.itemName.Replace(" ", "")}";
        Debug.Log($"[ItemSpawner] Spawned {itemData.itemName} at {spawnPos}");
    }

    public void SpawnCustomItem(string name, string description)
    {
        if (string.IsNullOrEmpty(name)) return;

        ItemData newItem = ScriptableObject.CreateInstance<ItemData>();
        newItem.itemName = name;
        newItem.description = description;

        Vector2 spawnPos = FindValidSpawnPosition();
        if (spawnPos == Vector2.zero) spawnPos = spawnCenter;

        SpawnItem(newItem, spawnPos);
    }

    [ContextMenu("Spawn Demo Items (10 Examples)")]
    public void SpawnDemoItems()
    {
        var demos = new List<(string name, string desc)>
        {
            ("Rocket Boots",
             "Heavy boots equipped with experimental thrusters. They allow the wearer to leap high into the air and crash back down with enough force to shatter the ground."),

            ("Quantum Stopwatch",
             "A strange time-keeping device. When activated, it freezes the flow of time around enemies, making them sluggish, while allowing the user to slip through space to a new position instantly."),

            ("Berserker's Helm",
             "A helmet stained with old blood. It drives the wearer into a frenzy, ignoring all pain and shrugging off death itself, while driving them to relentlessly batter their foes up close."),

            ("Sniper's Goggles",
             "High-tech eyewear that highlights enemy weak points. It analyzes the target to stop them in their tracks with fear, creating the perfect opening for a single, penetrating shot through the heart."),

            ("Phoenix Feather",
             "A warm, glowing feather. It grants the agility to rush forward leaving a trail of embers, unleash a wave of cleansing fire, and restore the user's vitality from the ashes."),

            ("Gravity Reversal Mine",
             "A device that manipulates gravity to launch nearby enemies helplessly into the air. While they float, the user relocates to a vantage point and fires upon the helpless targets."),

            ("Shadow Assassin's Dagger",
             "A blade for those who strike from the shadows. It urges the wielder to rush the enemy in a blink, deliver a fatal cut, and vanish instantly before retaliation."),

            ("Staff of Eternal Frost",
             "A staff cold to the touch. It freezes enemies in place, numbing their limbs with biting cold before shattering them with a lance of ice that pierces through everything."),

            ("Spiked Greatshield",
             "A massive shield that deploys a temporary force field to absorb incoming damage, while empowering the wielder to crush any foe foolish enough to come within arm's reach."),

            ("Vampiric Chalice",
             "This cursed goblet drains the vitality of its victims, making them sluggish. It tears into their flesh and drinks their life essence to restore the wielder.")
        };

        foreach (var (name, desc) in demos)
        {
            SpawnCustomItem(name, desc);
        }
    }

    [ContextMenu("Load All Items from Folder")]
    public void LoadAllItemsFromFolder()
    {
#if UNITY_EDITOR
        itemsToSpawn.Clear();
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/ScriptableObjects/Items" });

        foreach (string guid in guids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            ItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (item != null)
            {
                itemsToSpawn.Add(item);
            }
        }

        Debug.Log($"<color=green>[ItemSpawner] Loaded {itemsToSpawn.Count} items from Assets/ScriptableObjects/Items</color>");
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Clear All Spawned Items")]
    public void ClearAllSpawnedItems()
    {
        int count = transform.childCount;
        for (int i = count - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
            else
            {
#if UNITY_EDITOR
                DestroyImmediate(transform.GetChild(i).gameObject);
#endif
            }
        }

        spawnedPositions.Clear();
        Debug.Log($"[ItemSpawner] Cleared {count} spawned items");
    }

    private void OnDrawGizmosSelected()
    {
        // Draw spawn area
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(spawnCenter.x, spawnHeight, spawnCenter.y);
        Vector3 size = new Vector3(spawnAreaSize.x, 0.1f, spawnAreaSize.y);
        Gizmos.DrawWireCube(center, size);

        // Draw spawned positions
        Gizmos.color = Color.cyan;
        foreach (var pos in spawnedPositions)
        {
            Vector3 worldPos = new Vector3(pos.x, spawnHeight, pos.y);
            Gizmos.DrawWireSphere(worldPos, minDistanceBetweenItems / 2f);
        }
    }
}
