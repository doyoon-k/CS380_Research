using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// Executes an LLM call that is expected to return JSON and merges the parsed keys into the shared state dictionary.
/// </summary>
public class JSONLLMStateChainLink : IStateChainLink
{
    private readonly OllamaSettings _settings;
    private readonly int _maxRetries;
    private readonly float _delayBetweenRetries;
    private readonly PromptTemplate _userPromptTemplate;

    public JSONLLMStateChainLink(
        OllamaSettings settings,
        string userPromptTemplate,
        int maxRetries = 3,
        float delayBetweenRetries = 0.1f
    )
    {
        _settings = settings;
        _userPromptTemplate = new PromptTemplate(userPromptTemplate ?? string.Empty);
        _maxRetries = Mathf.Max(1, maxRetries);
        _delayBetweenRetries = Mathf.Max(0f, delayBetweenRetries);
    }

    public IEnumerator Execute(
        Dictionary<string, string> state,
        Action<Dictionary<string, string>> onDone
    )
    {
        state ??= new Dictionary<string, string>();
        if (_settings == null)
        {
            Debug.LogError("[JSONLLMStateChainLink] OllamaSettings is missing.");
            onDone?.Invoke(state);
            yield break;
        }

        int attempt = 0;
        bool parsedSuccessfully = false;
        JObject parsedObject = null;

        while (attempt < _maxRetries && !parsedSuccessfully)
        {
            attempt++;

            string userPrompt = RenderUserPrompt(state);
            Debug.Log($"[JSONLLMStateChainLink] Attempt {attempt}/{_maxRetries} - User Prompt:\n{userPrompt}");

            string jsonResponse = null;
            OllamaComponent.Instance.GenerateCompletionWithState(
                _settings,
                userPrompt,
                state,
                resp => jsonResponse = resp
            );

            while (jsonResponse == null)
            {
                yield return null;
            }

            Debug.Log($"[JSONLLMStateChainLink] Attempt {attempt}/{_maxRetries} - Raw Response:\n{jsonResponse}");

            try
            {
                parsedObject = JObject.Parse(jsonResponse);
                parsedSuccessfully = parsedObject.Type == JTokenType.Object;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JSONLLMStateChainLink] JSON parse failed: {e.Message}");
                parsedObject = null;
            }

            if (!parsedSuccessfully && attempt < _maxRetries && _delayBetweenRetries > 0f)
            {
                yield return new WaitForSeconds(_delayBetweenRetries);
            }
        }

        if (parsedSuccessfully && parsedObject != null)
        {
            foreach (var property in parsedObject)
            {
                state[property.Key] = property.Value?.ToString();
            }
        }

        onDone?.Invoke(state);
    }

    private string RenderUserPrompt(Dictionary<string, string> state)
    {
        if (_userPromptTemplate == null)
        {
            return string.Empty;
        }

        return _userPromptTemplate.Render(state);
    }
}
