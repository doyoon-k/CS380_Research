using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class OllamaClient : MonoBehaviour
{
    [Header("Ollama Settings")]
    public string ollamaUrl = "http://localhost:11434/api/chat";
    public string modelName = "gemma2:2b";

    [Header("Atomic Skills Reference")]
    [TextArea(5, 15)]
    public string atomicSkillsReference = @"
MOVE: FORWARD, BACK, LEFT, RIGHT, JUMP, LAND
ATTACK: HIT, HEAVY_HIT, LOW, MID, HIGH
POSE: CROUCH, STAND, ROLL, DODGE
STATE: INVINCIBLE, GUARD, SUPERARMOR, STUN_ENEMY
EFFECT: HEAL, BUFF_SELF, DEBUFF_ENEMY, TAUNT
TARGET: AIM, MARK, TRACK
";

    public IEnumerator GenerateSkillsAndStats(ItemData item, Action<AIResponse> onSuccess, Action<string> onError)
    {
        Debug.Log($"=== Generating AI content for: {item.itemName} ===");

        if (item.isCached)
        {
            Debug.Log("Using cached data!");
            AIResponse cached = new AIResponse
            {
                stat_model = JsonUtility.FromJson<StatModel>(item.cachedStatModelJson),
                skill_model = JsonUtility.FromJson<SkillModel>(item.cachedSkillModelJson)
            };
            onSuccess?.Invoke(cached);
            yield break;
        }

        string prompt = BuildPrompt(item);
        Debug.Log($"Prompt:\n{prompt}");

        yield return StartCoroutine(SendOllamaRequest(prompt, response =>
        {
            try
            {
                Debug.Log($"Raw AI Response:\n{response}");

                AIResponse aiResponse = ParseAIResponse(response);

                item.isCached = true;
                item.cachedStatModelJson = JsonUtility.ToJson(aiResponse.stat_model);
                item.cachedSkillModelJson = JsonUtility.ToJson(aiResponse.skill_model);

                Debug.Log("Successfully parsed AI response!");
                onSuccess?.Invoke(aiResponse);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing AI response: {e.Message}");
                onError?.Invoke(e.Message);
            }
        }, onError));
    }

    string BuildPrompt(ItemData item)
    {
        return $@"You are a game AI. Generate combat stats AND skills for a 2D fighting game item.

Item: {item.itemName}
Description: {item.description}

Available Atomic Skills:

{atomicSkillsReference}

CRITICAL REQUIREMENT: You MUST generate BOTH stat_model AND skill_model in EVERY response!
If you generate only stat_model without skill_model, the response is INVALID!

Generate a complete JSON with this EXACT structure:

{{
  ""stat_model"": {{
    ""stat_changes"": {{
      ""Speed"": 5,
      ""Attack"": 10,
      ""Defense"": 0,
      ""Jump"": 3,
      ""Attack_Speed"": 2,
      ""Range"": 0
    }},
    ""duration_seconds"": 5
  }},
  ""skill_model"": {{
    ""new_skills"": [
      {{
        ""name"": ""FIRE_DASH"",
        ""sequence"": [""FORWARD"", ""HIT"", ""BUFF_SELF""],
        ""description"": ""Dash forward with fire and boost attack"",
        ""cooldown"": 3.0,
        ""duration"": 0.5
      }},
      {{
        ""name"": ""FLAME_STRIKE"",
        ""sequence"": [""JUMP"", ""HEAVY_HIT""],
        ""description"": ""Jump and strike with burning power"",
        ""cooldown"": 4.0,
        ""duration"": 0.6
      }}
    ]
  }}
}}

MANDATORY RULES (MUST FOLLOW ALL):
1. stat_changes must match item theme
2. Use POSITIVE numbers for stat increases
3. stat_model duration_seconds: 3-7 seconds ONLY
4. YOU MUST CREATE EXACTLY 2 SKILLS - NOT 0, NOT 1, EXACTLY 2!
5. Each skill needs 2-4 atomic skills from the list above
6. Skill duration: 0.3-1.0 seconds ONLY
7. Skill cooldown: 2.0-5.0 seconds
8. Return COMPLETE JSON with BOTH stat_model AND skill_model
9. NO markdown (```), NO extra text, ONLY valid JSON

REMEMBER: If you don't include skill_model with 2 skills, your response is WRONG!";
    }

    IEnumerator SendOllamaRequest(string prompt, Action<string> onSuccess, Action<string> onError)
    {
        var message = new OllamaMessage
        {
            role = "user",
            content = prompt
        };

        var requestData = new OllamaChatRequest
        {
            model = modelName,
            messages = new OllamaMessage[] { message },
            stream = false,
            format = "json"
        };

        string jsonData = JsonUtility.ToJson(requestData);
        Debug.Log($"Request JSON:\n{jsonData}");

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(ollamaUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"Sending request to {ollamaUrl}...");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Ollama request failed: {request.error}");
                Debug.LogError($"Response Code: {request.responseCode}");
                Debug.LogError($"Response: {request.downloadHandler.text}");
                onError?.Invoke(request.error);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                Debug.Log($"Full Response:\n{responseText}");

                var ollamaResponse = JsonUtility.FromJson<OllamaChatResponse>(responseText);
                onSuccess?.Invoke(ollamaResponse.message.content);
            }
        }
    }

    AIResponse ParseAIResponse(string jsonResponse)
    {
        string cleaned = jsonResponse.Trim();

        if (cleaned.StartsWith("```json"))
        {
            cleaned = cleaned.Substring(7);
        }
        if (cleaned.StartsWith("```"))
        {
            cleaned = cleaned.Substring(3);
        }
        if (cleaned.EndsWith("```"))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 3);
        }

        cleaned = cleaned.Trim();

        AIResponse response = JsonUtility.FromJson<AIResponse>(cleaned);

        if (response.skill_model == null || response.skill_model.new_skills == null || response.skill_model.new_skills.Count == 0)
        {
            Debug.LogWarning("AI did not generate skills! Using default skill.");
            response.skill_model = new SkillModel
            {
                new_skills = new System.Collections.Generic.List<SkillData>
            {
                new SkillData
                {
                    name = "Basic Combo",
                    sequence = new System.Collections.Generic.List<string> { "FORWARD", "HIT", "HIGH" },
                    description = "A simple forward dash and attack combo",
                    cooldown = 5.0f,
                    duration = 30.0f
                }
            }
            };
        }

        return response;
    }
}

[Serializable]
public class OllamaMessage
{
    public string role;
    public string content;
}

[Serializable]
public class OllamaChatRequest
{
    public string model;
    public OllamaMessage[] messages;
    public bool stream;
    public string format;
}

[Serializable]
public class OllamaChatResponse
{
    public string model;
    public OllamaMessage message;
    public bool done;
}