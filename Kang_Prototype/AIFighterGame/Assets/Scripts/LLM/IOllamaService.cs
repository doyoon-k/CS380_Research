using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Abstraction over Ollama network calls so editor tools and runtime code can share the same logic.
/// Implementations should execute the returned enumerators within the caller's coroutine runner.
/// </summary>
public interface IOllamaService
{
    IEnumerator GenerateCompletion(
        OllamaSettings settings,
        string userPrompt,
        Action<string> onResponse);

    IEnumerator GenerateCompletionWithState(
        OllamaSettings settings,
        string userPrompt,
        Dictionary<string, string> state,
        Action<string> onResponse);

    IEnumerator ChatCompletion(
        OllamaSettings settings,
        ChatMessage[] messages,
        Action<string> onResponse);

    IEnumerator Embed(
        OllamaSettings settings,
        string[] inputs,
        Action<float[][]> onEmbeddings);
}
