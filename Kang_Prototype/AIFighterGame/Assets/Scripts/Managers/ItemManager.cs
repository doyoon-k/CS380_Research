using UnityEngine;
using System.Collections;

public class ItemManager : MonoBehaviour
{
    [Header("Configuration")]
    public PromptPipelineAsset pipelineAsset;

    [Header("Components")]
    public PlayerStats playerStats;

    [Header("Test Items")]
    public ItemData[] testItems;
    public int currentItemIndex = 0;

    [Header("Skill System")]
    public SkillManager skillManager;

    void Start()
    {
        Debug.Log("ItemManager initialized!");
        Debug.Log("Press '4' to use current item (Run Pipeline)");
        Debug.Log("Press '5' to switch to next item");
    }

    void Update()
    {
        // Key 4: Generate
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            if (testItems != null && testItems.Length > 0 && testItems[currentItemIndex] != null)
            {
                if (pipelineAsset == null)
                {
                    Debug.LogError("Pipeline Asset is missing!");
                    return;
                }

                // Stop any previous run before starting new one
                GamePipelineRunner.Instance.StopGeneration();
                StartCoroutine(ApplyItem(testItems[currentItemIndex]));
            }
            else
            {
                Debug.LogError("No test items assigned!");
            }
        }

        // Key 5: Switch Item
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            if (testItems != null && testItems.Length > 0)
            {
                // Stop generation immediately when switching items!
                GamePipelineRunner.Instance.StopGeneration();
                StopAllCoroutines(); // Stop local coroutine as well

                currentItemIndex = (currentItemIndex + 1) % testItems.Length;
                Debug.Log($"Switched to item: {testItems[currentItemIndex].itemName}");
            }
        }
    }

    IEnumerator ApplyItem(ItemData item)
    {
        Debug.Log($"=== Applying Item: {item.itemName} via Pipeline ===");

        GamePipelineRunner.Instance.GenerateItemStats(pipelineAsset, item, (aiResponse) =>
        {
            if (aiResponse == null)
            {
                Debug.LogError("AI Response came back null!");
                return;
            }

            Debug.Log("Pipeline finished! Applying results...");

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
        if (skillModel == null || skillModel.new_skills == null || skillModel.new_skills.Count == 0)
        {
            return;
        }

        if (skillManager != null)
        {
            skillManager.ClearSkills();
            foreach (var skill in skillModel.new_skills)
            {
                skillManager.AddSkill(skill);
            }
        }
    }
}