using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Executes a single completion style LLM call and stores the answer text into the shared state dictionary.
/// </summary>
public class CompletionChainLink : IStateChainLink
{
    private readonly OllamaSettings _settings;
    private readonly PromptTemplate _userPromptTemplate;

    public CompletionChainLink(OllamaSettings settings, string userPromptTemplate)
    {
        _settings = settings;
        _userPromptTemplate = new PromptTemplate(userPromptTemplate ?? string.Empty);
    }

    public IEnumerator Execute(
        Dictionary<string, string> state,
        Action<Dictionary<string, string>> onDone
    )
    {
        state ??= new Dictionary<string, string>();
        if (_settings == null)
        {
            Debug.LogError("[CompletionChainLink] OllamaSettings is missing.");
            onDone?.Invoke(state);
            yield break;
        }

        string userPrompt = _userPromptTemplate.Render(state);
        string response = null;

        OllamaComponent.Instance.GenerateCompletionWithState(
            _settings,
            userPrompt,
            state,
            text => response = text
        );

        while (response == null)
        {
            yield return null;
        }

        state[PromptPipelineConstants.AnswerKey] = response;
        onDone?.Invoke(state);
    }
}
