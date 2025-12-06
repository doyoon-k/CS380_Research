#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom Inspector for ItemSpawner with easy-to-use buttons
/// </summary>
[CustomEditor(typeof(ItemSpawner))]
public class ItemSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ItemSpawner spawner = (ItemSpawner)target;

        // Draw default inspector
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        // Large "Spawn Items" button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("▶ Spawn Items Now", GUILayout.Height(40)))
        {
            spawner.SpawnItems();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // "Clear Spawned Items" button
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f); // Light red
        if (GUILayout.Button("✖ Clear All Spawned Items", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog("Clear Spawned Items",
                "Remove all currently spawned item instances?",
                "Yes", "Cancel"))
            {
                spawner.ClearAllSpawnedItems();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // "Load Items from Folder" button
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("📁 Load All Items from Folder", GUILayout.Height(30)))
        {
            spawner.LoadAllItemsFromFolder();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        // Info section
        EditorGUILayout.HelpBox(
            $"Items to spawn: {spawner.itemsToSpawn.Count}\n" +
            $"Currently spawned: {spawner.transform.childCount}\n\n" +
            "Adjust spawn area using Scene Gizmos (yellow box)",
            MessageType.Info);

        // Scene view tip
        if (spawner.itemsToSpawn.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No items loaded! Click 'Load All Items from Folder' first.",
                MessageType.Warning);
        }
    }

    // Draw handles in Scene view for easy position adjustment
    private void OnSceneGUI()
    {
        ItemSpawner spawner = (ItemSpawner)target;

        // Draw spawn area handles
        Vector3 center = new Vector3(spawner.spawnCenter.x, spawner.spawnHeight, spawner.spawnCenter.y);

        // Position handle for spawn center
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(spawner, "Move Spawn Center");
            spawner.spawnCenter = new Vector2(newCenter.x, newCenter.z);
            spawner.spawnHeight = newCenter.y;
            EditorUtility.SetDirty(spawner);
        }

        // Draw labels
        Handles.Label(center + Vector3.up, "Spawn Center", EditorStyles.whiteLargeLabel);

        // Draw spawn area bounds
        Handles.color = Color.yellow;
        Vector3[] corners = new Vector3[5];
        float halfWidth = spawner.spawnAreaSize.x / 2f;
        float halfHeight = spawner.spawnAreaSize.y / 2f;

        corners[0] = center + new Vector3(-halfWidth, 0, -halfHeight);
        corners[1] = center + new Vector3(halfWidth, 0, -halfHeight);
        corners[2] = center + new Vector3(halfWidth, 0, halfHeight);
        corners[3] = center + new Vector3(-halfWidth, 0, halfHeight);
        corners[4] = corners[0]; // Close the loop

        Handles.DrawPolyLine(corners);
    }
}
#endif
