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
    private bool consumed;

    void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"ItemPickup enter by {other.name}");
        playerInRange = true;
        itemManager = other.GetComponentInParent<ItemManager>();
        SetPromptVisible(true);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"ItemPickup exit by {other.name}");
        playerInRange = false;
        itemManager = null;
        SetPromptVisible(false);
    }

    void Update()
    {
        if (!playerInRange || consumed) return;
        if (itemData == null) return;

        if (Input.GetKeyDown(pickupKey))
        {
            Consume();
        }
    }

    void Consume()
    {
        if (consumed) return;

        if (itemManager == null)
        {
            Debug.LogWarning("Player does not have ItemManager; item not applied.");
            return;
        }
        if (!playerInRange)
        {
            return;
        }
        if (itemData == null)
        {
            Debug.LogWarning("ItemData is null; cannot consume.");
            return;
        }

        Debug.Log("Consume called");
        consumed = true;
        itemManager.UseItem(itemData);
        Debug.Log($"Picked up item: {itemData.itemName}");

        SetPromptVisible(false);
        Destroy(gameObject);
    }

    void OnDisable()
    {
        if (!consumed)
        {
            Debug.Log($"ItemPickup {name} disabled without consume.");
        }
    }

    void SetPromptVisible(bool visible)
    {
        if (promptUI != null)
        {
            promptUI.SetActive(visible);
        }
    }
}
