#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Editor-safe implementation of <see cref="IOllamaService"/> that runs entirely in C#.
/// Editor coroutines (like <see cref="EditorCoroutineRunner"/>) should drive the returned enumerators.
/// </summary>
public class OllamaEditorService : IOllamaService
{
    private readonly OllamaHttpWorker _worker = new();
    private readonly bool _logTraffic;

    public OllamaEditorService(bool logTraffic = false)
    {
        _logTraffic = logTraffic;
    }

    public IEnumerator GenerateCompletion(
        OllamaSettings settings,
        string userPrompt,
        Action<string> onResponse)
    {
        return _worker.GenerateCompletion(settings, userPrompt, onResponse, _logTraffic);
    }

    public IEnumerator GenerateCompletionWithState(
        OllamaSettings settings,
        string userPrompt,
        Dictionary<string, string> state,
        Action<string> onResponse)
    {
        return _worker.GenerateCompletionWithState(settings, userPrompt, state, onResponse, _logTraffic);
    }

    public IEnumerator ChatCompletion(
        OllamaSettings settings,
        ChatMessage[] messages,
        Action<string> onResponse)
    {
        return _worker.ChatCompletion(settings, messages, onResponse, _logTraffic);
    }

    public IEnumerator Embed(
        OllamaSettings settings,
        string[] inputs,
        Action<float[][]> onEmbeddings)
    {
        return _worker.Embed(settings, inputs, onEmbeddings, _logTraffic);
    }
}
#endif
