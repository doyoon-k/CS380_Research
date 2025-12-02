using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

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
        // Debug Keys
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (playerStats != null) playerStats.ResetToBaseStats();
            if (skillManager != null) skillManager.ClearSkills();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            EnemyAI[] enemies = FindObjectsOfType<EnemyAI>();
            foreach (var enemy in enemies)
            {
                enemy.Respawn();
            }
        }

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

        // Real Stats
        if (playerStats != null)
        {
            state["AttackPower"] = playerStats.currentStats.Attack.ToString();
            state["AttackSpeed"] = playerStats.currentStats.Attack_Speed.ToString();
            state["ProjectileRange"] = playerStats.currentStats.Range.ToString();
            state["MovementSpeed"] = playerStats.currentStats.Speed.ToString();
            state["MaxHealth"] = playerStats.currentStats.MaxHP.ToString();
            state["Defense"] = playerStats.currentStats.Defense.ToString();
            state["JumpPower"] = playerStats.currentStats.Jump.ToString();
            state["CooldownHaste"] = playerStats.currentStats.CooldownHaste.ToString();
        }
        else
        {
            // Fallback if playerStats is missing
            state["AttackPower"] = "50";
            state["AttackSpeed"] = "1.0";
            state["ProjectileRange"] = "10";
            state["MovementSpeed"] = "5";
            state["MaxHealth"] = "100";
            state["Defense"] = "10";
            state["JumpPower"] = "10";
            state["CooldownHaste"] = "0";
        }

        if (playerStats != null)
        {
            state["current_character_description"] = playerStats.characterDescription;
        }
        else
        {
            state["current_character_description"] = "A brave warrior.";
        }

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
            CooldownHaste = statModel.stat_changes.CooldownHaste,
            MaxHP = statModel.stat_changes.MaxHP
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
            // Removed MapDirectStats to prevent applying current state as changes
            // MapDirectStats(state, response.stat_model.stat_changes);

            if (state.ContainsKey("stat") && state.ContainsKey("value"))
                ApplySingleStat(response.stat_model.stat_changes, state["stat"], state["value"]);

            string nestedJson = GetValue(state, "statChanges", "stat_changes", "stats");
            if (!string.IsNullOrEmpty(nestedJson)) ParseNestedStats(nestedJson, response.stat_model.stat_changes);

            // Set permanent duration
            response.stat_model.duration_seconds = 5.0f;

            // --- Description Mapping ---
            string newDesc = GetValue(state, "newDescription", "new_description");
            if (!string.IsNullOrEmpty(newDesc) && playerStats != null)
            {
                playerStats.characterDescription = newDesc;
            }

            // --- Skill Mapping ---
            SkillData skillData = new SkillData();
            bool skillFound = false;

            // 1. Try to parse "NewSkill" as a JSON object first
            string newSkillJson = GetValue(state, "NewSkill", "new_skill");
            if (!string.IsNullOrEmpty(newSkillJson))
            {
                try
                {
                    JObject skillObj = JObject.Parse(newSkillJson);

                    // Extract Name
                    if (skillObj["abilityName"] != null) skillData.name = skillObj["abilityName"].ToString();
                    else if (skillObj["skillName"] != null) skillData.name = skillObj["skillName"].ToString();
                    else skillData.name = "Unknown Skill";

                    // Extract Description
                    if (skillObj["description"] != null) skillData.description = skillObj["description"].ToString();
                    else if (skillObj["abilityDescription"] != null) skillData.description = skillObj["abilityDescription"].ToString();
                    else skillData.description = "Generated by AI";

                    // Extract Primitives
                    skillData.sequence = new List<string>();
                    JToken primitives = skillObj["primitiveActions"] ?? skillObj["primitives"];
                    if (primitives is JArray arr)
                    {
                        foreach (var item in arr)
                        {
                            if (item.Type == JTokenType.String) skillData.sequence.Add(item.ToString());
                            else if (item.Type == JTokenType.Object && item["primitiveId"] != null)
                                skillData.sequence.Add(item["primitiveId"].ToString());
                        }
                    }
                    else
                    {
                        skillData.sequence.Add("Attack");
                    }

                    skillData.cooldown = 3.0f;
                    skillFound = true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ItemManager] Failed to parse NewSkill JSON: {e.Message}");
                }
            }

            // 2. Fallback to flat keys if NewSkill wasn't valid or found
            if (!skillFound)
            {
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
                }
            }

            if (skillFound)
            {
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

    private void ApplySingleStat(StatChanges stats, string statName, string valueStr, string changeType = "additive")
    {
        float value = ParseFloat(valueStr);
        float delta = value;

        string key = statName.ToLower().Replace(" ", "");

        // Identify which stat we are targeting
        bool isAttackSpeed = key.Contains("attackspeed") || (key.Contains("attack") && key.Contains("speed"));
        bool isAttackPower = !isAttackSpeed && (key.Contains("attack") || key.Contains("power")); // AttackPower or Attack
        bool isMovementSpeed = !isAttackSpeed && (key.Contains("speed") || key.Contains("move")); // MovementSpeed or Speed
        bool isRange = key.Contains("range") || key.Contains("projectile");
        bool isDefense = key.Contains("defense");
        bool isJump = key.Contains("jump");
        bool isHaste = key.Contains("cooldown") || key.Contains("haste");
        bool isHealth = key.Contains("health") || key.Contains("hp");

        // Handle Multiplicative
        if (changeType.ToLower() == "multiplicative" && playerStats != null)
        {
            float currentVal = 0f;

            if (isAttackSpeed) currentVal = playerStats.currentStats.Attack_Speed;
            else if (isAttackPower) currentVal = playerStats.currentStats.Attack;
            else if (isMovementSpeed) currentVal = playerStats.currentStats.Speed;
            else if (isRange) currentVal = playerStats.currentStats.Range;
            else if (isDefense) currentVal = playerStats.currentStats.Defense;
            else if (isJump) currentVal = playerStats.currentStats.Jump;
            else if (isHaste) currentVal = playerStats.currentStats.CooldownHaste;
            else if (isHealth) currentVal = playerStats.currentStats.MaxHP;

            delta = currentVal * (value - 1f);
        }

        // Apply Delta
        if (isAttackSpeed) stats.Attack_Speed = delta;
        else if (isAttackPower) stats.Attack = delta;
        else if (isMovementSpeed) stats.Speed = delta;
        else if (isRange) stats.Range = delta;
        else if (isDefense) stats.Defense = delta;
        else if (isJump) stats.Jump = delta;
        else if (isHaste) stats.CooldownHaste = delta;
        else if (isHealth) stats.MaxHP = delta;
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
                    string t = item["changeType"] != null ? (string)item["changeType"] : "additive";
                    ApplySingleStat(stats, s, v, t);
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