using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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

        // 1. Initialize State
        Dictionary<string, string> state = new Dictionary<string, string>();
        state["item_name"] = item.itemName;
        state["item_description"] = item.description;

        // Dummy Stats
        state["AttackPower"] = "50";
        state["AttackSpeed"] = "1.0";
        state["ProjectileRange"] = "10";
        state["MovementSpeed"] = "5";
        state["MaxHealth"] = "100";
        state["Defense"] = "10";
        state["JumpPower"] = "10";
        state["CooldownHaste"] = "0";
        state["current_character_description"] = "A brave warrior.";

        bool pipelineFinished = false;

        GamePipelineRunner.Instance.RunPipeline(pipelineAsset, state, (finalState) =>
        {
            if (finalState == null) return;
            
            AIResponse response = MapStateToResponse(finalState);

            if (response.stat_model != null) ApplyStatModel(response.stat_model);
            if (response.skill_model != null) ApplySkillModel(response.skill_model);
            
            pipelineFinished = true;
        });

        while (!pipelineFinished) yield return null;
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

    // --- Mapping Logic ---

    private AIResponse MapStateToResponse(Dictionary<string, string> state)
    {
        AIResponse response = new AIResponse();
        response.stat_model = new StatModel { stat_changes = new StatChanges() };
        response.skill_model = new SkillModel { new_skills = new List<SkillData>() };

        try
        {
            // --- Stat Mapping ---
            MapDirectStats(state, response.stat_model.stat_changes);
            if (state.ContainsKey("stat") && state.ContainsKey("value"))
                ApplySingleStat(response.stat_model.stat_changes, state["stat"], state["value"]);

            string nestedJson = GetValue(state, "statChanges", "stat_changes", "stats");
            if (!string.IsNullOrEmpty(nestedJson)) ParseNestedStats(nestedJson, response.stat_model.stat_changes);

            // Set permanent duration
            response.stat_model.duration_seconds = 5.0f;

            // --- Skill Mapping ---
            SkillData skillData = new SkillData();
            bool skillFound = false;

            string nameVal = GetValue(state, "Name", "abilityName", "ability_name", "skillName", "skill_name");
            string primitivesJson = GetValue(state, "Primitives", "primitives", "primitiveActions", "primitive_actions");

            if (!string.IsNullOrEmpty(nameVal))
            {
                skillData.name = nameVal;
                skillFound = true;
            }
            else if (!string.IsNullOrEmpty(primitivesJson))
            {
                skillData.name = "Unknown Power";
                skillFound = true;
            }

            if (skillFound)
            {
                string descVal = GetValue(state, "Description", "description", "abilityDescription", "ability_description", "flavor", "Flavor");
                skillData.description = !string.IsNullOrEmpty(descVal) ? descVal : "Generated by AI";
                skillData.cooldown = 3.0f;

                skillData.sequence = new List<string>();

                if (!string.IsNullOrEmpty(primitivesJson))
                {
                    try
                    {
                        JToken token = JToken.Parse(primitivesJson);
                        if (token is JArray array)
                        {
                            foreach (var item in array)
                            {
                                if (item.Type == JTokenType.String)
                                    skillData.sequence.Add(item.ToString());
                                else if (item.Type == JTokenType.Object && item["primitiveId"] != null)
                                    skillData.sequence.Add(item["primitiveId"].ToString());
                            }
                        }
                    }
                    catch { skillData.sequence.Add("Attack"); }
                }
                else { skillData.sequence.Add("Attack"); }

                response.skill_model.new_skills.Add(skillData);
                Debug.Log($"[ItemManager] Mapped skill: {skillData.name}");
            }
            else
            {
                Debug.LogWarning("[ItemManager] AI returned empty skill. Creating fallback.");
                SkillData fallbackSkill = new SkillData
                {
                    name = "Fizzled Magic",
                    description = "The item's power flickered out. (AI Error)",
                    cooldown = 1.0f,
                    sequence = new List<string> { "Attack" }
                };
                response.skill_model.new_skills.Add(fallbackSkill);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[ItemManager] Mapping Error: {e.Message}");
        }

        return response;
    }

    private void MapDirectStats(Dictionary<string, string> state, StatChanges stats)
    {
        if (state.ContainsKey("Attack") || state.ContainsKey("AttackPower"))
            stats.Attack = ParseFloat(GetValue(state, "Attack", "AttackPower"));
        if (state.ContainsKey("Speed") || state.ContainsKey("MovementSpeed"))
            stats.Speed = ParseFloat(GetValue(state, "Speed", "MovementSpeed"));
        if (state.ContainsKey("Defense"))
            stats.Defense = ParseFloat(state["Defense"]);
        if (state.ContainsKey("Jump") || state.ContainsKey("JumpPower"))
            stats.Jump = ParseFloat(GetValue(state, "Jump", "JumpPower"));
        if (state.ContainsKey("Range") || state.ContainsKey("ProjectileRange"))
            stats.Range = ParseFloat(GetValue(state, "Range", "ProjectileRange"));
        if (state.ContainsKey("CooldownHaste") || state.ContainsKey("Haste"))
            stats.CooldownHaste = ParseFloat(GetValue(state, "CooldownHaste", "Haste"));
    }

    private void ApplySingleStat(StatChanges stats, string statName, string valueStr)
    {
        float value = ParseFloat(valueStr);
        string key = statName.ToLower().Replace(" ", "");
        if (key.Contains("attack") && !key.Contains("speed")) stats.Attack = value;
        else if (key.Contains("speed") && !key.Contains("attack")) stats.Speed = value;
        else if (key.Contains("defense")) stats.Defense = value;
        else if (key.Contains("jump")) stats.Jump = value;
        else if (key.Contains("range")) stats.Range = value;
        else if (key.Contains("cooldown") || key.Contains("haste")) stats.CooldownHaste = value;
    }

    private void ParseNestedStats(string json, StatChanges stats)
    {
        try
        {
            JToken token = JToken.Parse(json);
            if (token is JArray array)
            {
                foreach (var item in array)
                {
                    string s = (string)item["stat"];
                    string v = (string)item["value"];
                    ApplySingleStat(stats, s, v);
                }
            }
            else if (token is JObject obj)
            {
                foreach (var prop in obj) ApplySingleStat(stats, prop.Key, prop.Value?.ToString());
            }
        }
        catch (Exception e) { Debug.LogWarning($"[ItemManager] Failed to parse nested stats: {e.Message}"); }
    }

    private float ParseFloat(string val)
    {
        if (float.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float result)) return result;
        return 0f;
    }

    private string GetValue(Dictionary<string, string> dict, params string[] potentialKeys)
    {
        foreach (var key in potentialKeys) if (dict.ContainsKey(key)) return dict[key];
        return null;
    }
}