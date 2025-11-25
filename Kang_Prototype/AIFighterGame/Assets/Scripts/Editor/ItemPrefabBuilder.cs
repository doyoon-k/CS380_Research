using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        // Prompt UI (world space)
        GameObject canvasGO = new GameObject("PromptCanvas");
        canvasGO.transform.SetParent(item.transform);
        canvasGO.transform.localPosition = new Vector3(0f, 0.8f, 0f);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2.5f, 0.6f);

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 30f;
        canvasGO.AddComponent<GraphicRaycaster>();

        GameObject textGO = new GameObject("PromptText");
        textGO.transform.SetParent(canvasGO.transform);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text promptText = textGO.AddComponent<Text>();
        promptText.text = "Press I to pick up";
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        promptText.color = Color.white;
        promptText.resizeTextForBestFit = true;
        promptText.resizeTextMinSize = 10;
        promptText.resizeTextMaxSize = 24;

        pickup.promptUI = canvasGO;

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
