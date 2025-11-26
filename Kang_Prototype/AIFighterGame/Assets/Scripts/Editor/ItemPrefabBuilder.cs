using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Creates a simple Item prefab with collider + ItemPickup + pickup prompt.
/// </summary>
public static class ItemPrefabBuilder
{
    private const string PrefabPath = "Assets/Prefabs/ItemPickup.prefab";

    [MenuItem("Tools/Item/Create Item Pickup Prefab")]
    public static void CreateItemPrefab()
    {
        GameObject item = new GameObject("ItemPickup");

        // Visual
        SpriteRenderer sr = item.AddComponent<SpriteRenderer>();
        sr.color = new Color(1f, 1f, 0.5f); // light yellow placeholder

        // Collider
        CircleCollider2D col = item.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.5f;

        // Pickup logic
        ItemPickup pickup = item.AddComponent<ItemPickup>();

        // World-space TMP text prompt (no Canvas; uses TextMeshPro component)
        GameObject textGO = new GameObject("PromptText");
        textGO.transform.SetParent(item.transform);
        textGO.transform.localPosition = new Vector3(0f, 0.8f, 0f);
        textGO.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = textGO.AddComponent<TextMeshPro>();
        tmp.text = "Press I to pick up";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontSize = 2f;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 1f;
        tmp.fontSizeMax = 3.5f;

        pickup.promptUI = textGO;

        // Save prefab
        EnsureFolderExists("Assets/Prefabs");
        PrefabUtility.SaveAsPrefabAssetAndConnect(item, PrefabPath, InteractionMode.UserAction);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"Item pickup prefab created at {PrefabPath}");
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
