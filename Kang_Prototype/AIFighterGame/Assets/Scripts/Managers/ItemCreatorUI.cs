using UnityEngine;
using UnityEngine.UI;

public class ItemCreatorUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The root panel of the item creator window (should have opaque background)")]
    public GameObject windowPanel;
    public InputField nameInput;
    public InputField descriptionInput;
    public Button generateButton;
    public Button toggleButton;

    [Header("Scene References")]
    public ItemSpawner itemSpawner;
    public PlayerController playerController;

    private bool isOpen = false;

    void Start()
    {
        // Auto-find references if missing
        if (itemSpawner == null) itemSpawner = FindObjectOfType<ItemSpawner>();
        if (playerController == null) playerController = FindObjectOfType<PlayerController>();

        // Setup Button Listeners
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(OnGenerateClicked);
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleWindow);
        }

        // Initialize state
        if (windowPanel != null)
        {
            windowPanel.SetActive(false);
        }
        isOpen = false;
    }

    void Update()
    {
        // Optional: Close with Escape
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleWindow();
        }
    }

    public void ToggleWindow()
    {
        isOpen = !isOpen;

        if (windowPanel != null)
        {
            windowPanel.SetActive(isOpen);
        }

        // Block/Unblock Player Input
        if (playerController != null)
        {
            playerController.SetInputEnabled(!isOpen);
        }
    }

    public void OnGenerateClicked()
    {
        if (nameInput == null || descriptionInput == null) return;

        string name = nameInput.text;
        string desc = descriptionInput.text;

        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("[ItemCreatorUI] Name is empty!");
            return;
        }

        if (itemSpawner != null)
        {
            itemSpawner.SpawnCustomItem(name, desc);
        }

        // Clear inputs?
        nameInput.text = "";
        descriptionInput.text = "";

        // Keep window open or close? User said "toggle to close", implied manual close.
        // But usually "Generate" might close it. I'll leave it open for repeated spawning unless user wants otherwise.
        // User: "Button to open... input input... generate... toggle to close"

        // I'll assume keeping it open is fine, but focus might need handling.
    }
}
