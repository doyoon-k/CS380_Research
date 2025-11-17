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
    private readonly IOllamaService _ollamaService;
    private readonly Action<string> _log;

    public CompletionChainLink(OllamaSettings settings, string userPromptTemplate, Action<string> log = null)
        : this(OllamaServiceLocator.Require(), settings, userPromptTemplate, log)
    {
    }

    public CompletionChainLink(
        IOllamaService service,
        OllamaSettings settings,
        string userPromptTemplate,
        Action<string> log = null)
    {
        _ollamaService = service;
        _log = log;
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

        if (_ollamaService == null)
        {
            Debug.LogError("[CompletionChainLink] IOllamaService is missing.");
            onDone?.Invoke(state);
            yield break;
        }

        string userPrompt = _userPromptTemplate.Render(state);
        string response = null;

        Log($"[CompletionChainLink] User Prompt:\n{userPrompt}");

        yield return _ollamaService.GenerateCompletionWithState(
            _settings,
            userPrompt,
            state,
            text => response = text
        );

        Log($"[CompletionChainLink] Raw Response:\n{response}");

        state[PromptPipelineConstants.AnswerKey] = response;
        onDone?.Invoke(state);
    }

    private void Log(string message)
    {
        _log?.Invoke(message);
        Debug.Log(message);
    }
}
