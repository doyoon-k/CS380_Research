using UnityEngine;

/// <summary>
/// World item pickup: shows a prompt when the player is in range and consumes on key press.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class ItemPickup : MonoBehaviour
{
    public ItemData itemData;
    public KeyCode pickupKey = KeyCode.I;
    public GameObject promptUI;

    private bool playerInRange;
    private ItemManager itemManager;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = true;
        itemManager = other.GetComponent<ItemManager>();
        SetPromptVisible(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        playerInRange = false;
        itemManager = null;
        SetPromptVisible(false);
    }

    void Update()
    {
        if (!playerInRange) return;
        if (itemData == null) return;

        if (Input.GetKeyDown(pickupKey))
        {
            Consume();
        }
    }

    void Consume()
    {
        if (itemManager != null)
        {
            itemManager.UseItem(itemData);
            Debug.Log($"Picked up item: {itemData.itemName}");
        }
        else
        {
            Debug.LogWarning("Player does not have ItemManager; item not applied.");
        }

        SetPromptVisible(false);
        Destroy(gameObject);
    }

    void SetPromptVisible(bool visible)
    {
        if (promptUI != null)
        {
            promptUI.SetActive(visible);
        }
    }
}
