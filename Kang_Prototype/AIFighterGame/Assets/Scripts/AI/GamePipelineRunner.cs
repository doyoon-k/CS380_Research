using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class GamePipelineRunner : MonoBehaviour
{
    public static GamePipelineRunner Instance;

    [Header("Dependencies")]
    [SerializeField] private RuntimeOllamaService _runtimeService;

    private Coroutine _currentRoutine;

    private void Awake()
    {
        Instance = this;
        if (_runtimeService == null) _runtimeService = GetComponent<RuntimeOllamaService>();
    }
    public void StopGeneration()
    {
        if (_currentRoutine != null)
        {
            StopCoroutine(_currentRoutine);
            _currentRoutine = null;
            Debug.Log("[GamePipelineRunner] Generation stopped by user.");
        }
    }

    public void GenerateItemStats(PromptPipelineAsset asset, ItemData itemData, Action<AIResponse> onComplete)
    {
        // Stop any existing routine before starting a new one
        StopGeneration();

        // Start and store the reference
        _currentRoutine = StartCoroutine(RunRoutine(asset, itemData, onComplete));
    }

    private IEnumerator RunRoutine(PromptPipelineAsset asset, ItemData itemData, Action<AIResponse> onComplete)
    {
        if (asset == null || asset.steps == null)
        {
            Debug.LogError("Asset is null or empty!");
            yield break;
        }

        // 1. Initialize State
        Dictionary<string, string> state = new Dictionary<string, string>();
        state["itemName"] = itemData.itemName;
        state["item_name"] = itemData.itemName;
        state["description"] = itemData.description;
        state["item_description"] = itemData.description;

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

        // 2. Setup Executor
        StateSequentialChainExecutor executor = new StateSequentialChainExecutor();

        foreach (var step in asset.steps)
        {
            if (step.stepKind == PromptPipelineStepKind.JsonLlm)
            {
                var link = new JSONLLMStateChainLink(
                    _runtimeService,
                    step.ollamaSettings,
                    step.userPromptTemplate,
                    step.jsonMaxRetries,
                    step.jsonRetryDelaySeconds,
                    Debug.Log
                );
                executor.AddLink(link);
            }
        }

        // 3. Execute Pipeline
        Dictionary<string, string> finalState = null;
        yield return executor.Execute(state, result => finalState = result);

        // 4. Map Results
        if (finalState != null)
        {
            AIResponse response = MapStateToResponse(finalState);
            onComplete?.Invoke(response);
        }
        else
        {
            Debug.LogError("Pipeline execution failed.");
        }

        // Clear routine reference when done
        _currentRoutine = null;
    }

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
                Debug.Log($"[GamePipelineRunner] Mapped skill: {skillData.name}");
            }
            else
            {
                Debug.LogWarning("[GamePipelineRunner] AI returned empty skill. Creating fallback.");
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
            Debug.LogError($"[GamePipelineRunner] Mapping Error: {e.Message}");
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
        catch (Exception e) { Debug.LogWarning($"[GamePipelineRunner] Failed to parse nested stats: {e.Message}"); }
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