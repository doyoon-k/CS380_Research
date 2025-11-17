using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime MonoBehaviour adapter that exposes Ollama requests through <see cref="IOllamaService"/>.
/// It owns the worker that performs actual HTTP calls and keeps the legacy singleton surface alive.
/// </summary>
public class OllamaComponent : MonoBehaviour, IOllamaService
{
    public static OllamaComponent Instance { get; private set; }

    [Header("Debug")]
    [Tooltip("LLM에 보내는 system+user 프롬프트와 원시 요청/응답을 모두 콘솔에 로깅할지 여부")]
    public bool logLLMTraffic = false;

    private OllamaHttpWorker _worker;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureWorker();
        OllamaServiceLocator.Register(this);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            OllamaServiceLocator.Unregister(this);
        }
    }

    /// <summary>
    /// 기존 state 없이 호출하는 버전.
    /// systemPromptTemplate을 그대로 system 필드에 포함합니다.
    /// </summary>
    public void GenerateCompletion(
        OllamaSettings settings,
        string userPrompt,
        Action<string> onResponse
    )
    {
        StartCoroutine(((IOllamaService)this).GenerateCompletion(settings, userPrompt, onResponse));
    }


    /// <summary>
    /// state를 받아서 system prompt을 치환하고, 그 결과를 system 필드로 포함해 LLM 호출
    /// </summary>
    public void GenerateCompletionWithState(
        OllamaSettings settings,
        string userPrompt,
        Dictionary<string, string> state,
        Action<string> onResponse
    )
    {
        StartCoroutine(((IOllamaService)this).GenerateCompletionWithState(settings, userPrompt, state, onResponse));
    }

    // ──────────────────────────────────
    // 2) Chat Completion
    // ──────────────────────────────────
    public void ChatCompletion(
        OllamaSettings settings,
        ChatMessage[] messages,
        Action<string> onResponse)
    {
        StartCoroutine(((IOllamaService)this).ChatCompletion(settings, messages, onResponse));
    }

    // ──────────────────────────────────
    // 3) Embedding
    // ──────────────────────────────────
    public void Embed(
        OllamaSettings settings,
        string[] inputs,
        Action<float[][]> onEmbeddings)
    {
        StartCoroutine(((IOllamaService)this).Embed(settings, inputs, onEmbeddings));
    }

    IEnumerator IOllamaService.GenerateCompletion(
        OllamaSettings settings,
        string userPrompt,
        Action<string> onResponse)
    {
        EnsureWorker();
        return _worker.GenerateCompletion(settings, userPrompt, onResponse, logLLMTraffic);
    }

    IEnumerator IOllamaService.GenerateCompletionWithState(
        OllamaSettings settings,
        string userPrompt,
        Dictionary<string, string> state,
        Action<string> onResponse)
    {
        EnsureWorker();
        return _worker.GenerateCompletionWithState(settings, userPrompt, state, onResponse, logLLMTraffic);
    }

    IEnumerator IOllamaService.ChatCompletion(
        OllamaSettings settings,
        ChatMessage[] messages,
        Action<string> onResponse)
    {
        EnsureWorker();
        return _worker.ChatCompletion(settings, messages, onResponse, logLLMTraffic);
    }

    IEnumerator IOllamaService.Embed(
        OllamaSettings settings,
        string[] inputs,
        Action<float[][]> onEmbeddings)
    {
        EnsureWorker();
        return _worker.Embed(settings, inputs, onEmbeddings, logLLMTraffic);
    }

    private void EnsureWorker()
    {
        _worker ??= new OllamaHttpWorker();
    }
}

/// <summary>ChatMessage struct for chat completions</summary>
[Serializable]
public struct ChatMessage
{
    public string role;     // "system" | "user" | "assistant"
    public string content;
}
