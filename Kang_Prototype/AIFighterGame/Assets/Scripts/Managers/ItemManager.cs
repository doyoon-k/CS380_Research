using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ItemManager : MonoBehaviour
{
    public static ItemManager Instance;

    [Header("Configuration")]
    public PromptPipelineAsset pipelineAsset;

    [Header("Components")]
    public PlayerStats playerStats;
    public SkillManager skillManager;

    [Header("Inventory")]
    public List<ItemData> inventory = new List<ItemData>();
    public int currentEquipIndex = 0;
    public ItemData currentItem;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Debug.Log("ItemManager initialized!");
        Debug.Log("Controls: [T] Swap Item, [4] Use Item");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            SwapToNextItem();
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (currentItem != null)
            {
                if (pipelineAsset == null) { Debug.LogError("Pipeline Asset missing!"); return; }

                GamePipelineRunner.Instance.StopGeneration();
                StartCoroutine(ApplyItem(currentItem));
            }
            else
            {
                Debug.LogWarning("No item equipped!");
            }
        }
    }

    public void SwapToNextItem()
    {
        if (inventory.Count == 0) return;

        currentEquipIndex = (currentEquipIndex + 1) % inventory.Count;
        currentItem = inventory[currentEquipIndex];

        Debug.Log($"Swapped to: {currentItem.itemName}");
    }

    public void AddItem(ItemData newItem)
    {
        inventory.Add(newItem);

        if (inventory.Count == 1)
        {
            currentEquipIndex = 0;
            currentItem = newItem;
        }

        Debug.Log($"Picked up {newItem.itemName}. Total items: {inventory.Count}");
    }
    IEnumerator ApplyItem(ItemData item)
    {
        Debug.Log($"=== Using Item: {item.itemName} ===");
        GamePipelineRunner.Instance.GenerateItemStats(pipelineAsset, item, (aiResponse) =>
        {
            if (aiResponse == null) return;
            if (aiResponse.stat_model != null) ApplyStatModel(aiResponse.stat_model);
            if (aiResponse.skill_model != null) ApplySkillModel(aiResponse.skill_model);
        });
        yield return null;
    }

    void ApplyStatModel(StatModel statModel)
    {
        if (statModel == null || statModel.stat_changes == null) return;
        Stats statChanges = new Stats
        {
            Speed = statModel.stat_changes.Speed,
            Attack = statModel.stat_changes.Attack,
            Defense = statModel.stat_changes.Defense,
            Jump = statModel.stat_changes.Jump,
            Attack_Speed = statModel.stat_changes.Attack_Speed,
            Range = statModel.stat_changes.Range,
            CooldownHaste = statModel.stat_changes.CooldownHaste
        };
        float duration = statModel.duration_seconds > 0 ? statModel.duration_seconds : 0f;
        playerStats.ModifyStats(statChanges, duration);
    }

    void ApplySkillModel(SkillModel skillModel)
    {
        if (skillModel == null || skillModel.new_skills == null) return;
        if (skillManager != null)
        {
            skillManager.ClearSkills();
            foreach (var skill in skillModel.new_skills) skillManager.AddSkill(skill);
        }
    }
}