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
        // 1. Setup Endpoint (Using Chat API)
        string url = $"{ollamaUrl}/api/chat";

        // 2. Construct JSON Payload
        // We use the model name defined in the OllamaSettings asset.
        var requestData = new RuntimeChatRequest
        {
            model = settings != null ? settings.model : "llama3",
            messages = new[]
            {
                new RuntimeMessage { role = "user", content = userPrompt }
            },
            stream = false,
            // Force JSON format if the settings require it
            format = (settings != null && !string.IsNullOrEmpty(settings.format)) ? "json" : null
        };

        // Handle System Prompt if it exists in settings
        if (settings != null && !string.IsNullOrEmpty(settings.systemPromptTemplate))
        {
            // Note: In a full implementation, you might want to prepend this to the messages list as a "system" role.
        }

        string jsonData = JsonUtility.ToJson(requestData);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);

        // 3. Send Request
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            yield return request.SendWebRequest();

            // 4. Handle Response
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[RuntimeOllama] Error: {request.error}\nResponse: {request.downloadHandler.text}");
                onResponse?.Invoke(null);
            }
            else
            {
                string responseText = request.downloadHandler.text;
                try
                {
                    // Parse the specific Ollama response format
                    var responseObj = JsonUtility.FromJson<RuntimeChatResponse>(responseText);
                    if (responseObj != null && responseObj.message != null)
                    {
                        onResponse?.Invoke(responseObj.message.content);
                    }
                    else
                    {
                        Debug.LogWarning("[RuntimeOllama] Failed to parse message content. Returning raw text.");
                        onResponse?.Invoke(responseText);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RuntimeOllama] JSON Parse Error: {e.Message}");
                    onResponse?.Invoke(null);
                }
            }
        }
    }

    // --- IOllamaService Interface Stubs (Implement if needed) ---

    public IEnumerator GenerateCompletion(OllamaSettings settings, string userPrompt, Action<string> onResponse)
    {
        // Redirects to the main method without state
        return GenerateCompletionWithState(settings, userPrompt, null, onResponse);
    }

    public IEnumerator ChatCompletion(OllamaSettings settings, ChatMessage[] messages, Action<string> onResponse)
    {
        // Implement if you need full chat history support
        yield break;
    }

    public IEnumerator Embed(OllamaSettings settings, string[] inputs, Action<float[][]> onEmbeddings)
    {
        // Implement if you need embeddings
        yield break;
    }

    // --- Internal DTOs for JSON Serialization ---
    [Serializable]
    private class RuntimeChatRequest
    {
        public string model;
        public RuntimeMessage[] messages;
        public bool stream;
        public string format; // "json" or null
    }

    [Serializable]
    private class RuntimeMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class RuntimeChatResponse
    {
        public string model;
        public RuntimeMessage message;
        public bool done;
    }
}