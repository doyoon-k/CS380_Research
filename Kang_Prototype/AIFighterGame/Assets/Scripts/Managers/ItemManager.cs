using UnityEngine;

public class ItemManager : MonoBehaviour
{
    /// <summary>
    /// Consumes an item immediately. Pipeline/stat/skill application will be added later.
    /// </summary>
    public void UseItem(ItemData item)
    {
        if (item == null)
        {
            Debug.LogWarning("UseItem called with null item.");
            return;
        }

        Debug.Log($"Consumed item: {item.itemName}");
        // TODO: Add pipeline-driven effects here.
    }
}
