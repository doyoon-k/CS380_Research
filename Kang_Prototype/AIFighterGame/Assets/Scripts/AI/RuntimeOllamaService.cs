using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Implementation of IOllamaService that runs during gameplay (Runtime) using UnityWebRequest.
/// This replaces the editor-only service.
/// </summary>
public class RuntimeOllamaService : MonoBehaviour, IOllamaService
{
    [Header("Ollama Server Settings")]
    [Tooltip("Ollama API URL (Default: http://localhost:11434)")]
    public string ollamaUrl = "http://localhost:11434";

    [Tooltip("Request timeout in seconds")]
    public int timeoutSeconds = 60;

    [Tooltip("Log all LLM traffic to console")]
    public bool logTraffic = false;

    private readonly OllamaHttpWorker _worker = new OllamaHttpWorker();

    /// <summary>
    /// Sends a request to the Ollama Chat API.
    /// Used by JSONLLMStateChainLink.
    /// </summary>
    public IEnumerator GenerateCompletionWithState(
        OllamaSettings settings,
        string userPrompt,
        Dictionary<string, string> state,
        Action<string> onResponse)
    {
        // Ensure the worker knows the correct URL if it's configurable
        // Note: OllamaHttpWorker uses OllamaAutoLoader.GetServerAddress internally, 
        // which might need to be updated or we assume standard behavior.
        // For now, we trust OllamaHttpWorker's logic but we should ensure it respects our settings if possible.
        
        return _worker.GenerateCompletionWithState(settings, userPrompt, state, onResponse, logTraffic);
    }

    public IEnumerator GenerateCompletion(OllamaSettings settings, string userPrompt, Action<string> onResponse)
    {
        return _worker.GenerateCompletion(settings, userPrompt, onResponse, logTraffic);
    }

    public IEnumerator ChatCompletion(OllamaSettings settings, ChatMessage[] messages, Action<string> onResponse)
    {
        return _worker.ChatCompletion(settings, messages, onResponse, logTraffic);
    }

    public IEnumerator Embed(OllamaSettings settings, string[] inputs, Action<float[][]> onEmbeddings)
    {
        return _worker.Embed(settings, inputs, onEmbeddings, logTraffic);
    }
}