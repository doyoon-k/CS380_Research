using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Singleton wrapper for Ollama REST endpoints.
/// ✅ 요청마다 <see cref="OllamaSettings"/> 를 파라미터로 전달해
///    내부 공유 상태 / 경합 문제를 완전히 제거했다.
/// </summary>
public class OllamaComponent : MonoBehaviour
{
    public static OllamaComponent Instance { get; private set; }

    [Header("Debug")]
    [Tooltip("LLM에 보내는 system+user 프롬프트와 원시 요청/응답을 모두 콘솔에 로깅할지 여부")]
    public bool logLLMTraffic = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        // settings.systemPromptTemplate(raw)를 system으로 넘김
        string systemPrompt = settings.systemPromptTemplate;
        StartCoroutine(CoGenerateCompletion(settings, userPrompt, systemPrompt, onResponse));
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
        settings.RenderSystemPrompt(state);
        string renderedSystem = settings.GetLastRenderedPrompt();
        StartCoroutine(CoGenerateCompletion(settings, userPrompt, renderedSystem, onResponse));
    }

    private IEnumerator CoGenerateCompletion(
        OllamaSettings s,
        string userPrompt,
        string renderedSystem,
        Action<string> onEachResponseLine
    )
    {
        if (!Validate(s, userPrompt))
            yield break;

        // ─── 로깅: system + user ───
        if (logLLMTraffic)
        {
            Debug.Log($"[Ollama] ► SYSTEM PROMPT:\n{renderedSystem}\n" +
                      $"[Ollama] ► USER PROMPT:\n{userPrompt}");
        }

        // 요청 바디 생성
        var body = new Dictionary<string, object>
        {
            { "model", s.model },
            { "prompt", userPrompt }
        };
        if (!string.IsNullOrEmpty(renderedSystem))
            body["system"] = renderedSystem;
        if (!string.IsNullOrEmpty(s.format))
            body["format"] = ParseFormat(s.format);
        body["stream"] = s.stream;
        if (!string.IsNullOrEmpty(s.keepAlive))
            body["keep_alive"] = s.keepAlive;

        var options = new Dictionary<string, object>
        {
            { "temperature", s.modelParams.temperature },
            { "top_p",       s.modelParams.top_p },
            { "top_k",       s.modelParams.top_k },
            { "num_predict", s.modelParams.num_predict },
            { "repeat_penalty", s.modelParams.repeat_penalty }
        };
        body["options"] = options;

        string url = $"{OllamaAutoLoader.GetServerAddress(s.model)}/api/generate";
        string jsonReq = JsonConvert.SerializeObject(body,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        // ─── 로깅: HTTP 요청 ───
        if (logLLMTraffic)
            Debug.Log($"[Ollama] >>> REQUEST to {url} <<<\n{jsonReq}");

        using var req = new UnityWebRequest(url, "POST");
        byte[] data = Encoding.UTF8.GetBytes(jsonReq);
        req.uploadHandler = new UploadHandlerRaw(data);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Ollama] request error: {req.error}, code={req.responseCode}");
            yield break;
        }

        string resp = req.downloadHandler.text;

        // ─── 로깅: HTTP 응답 ───
        if (logLLMTraffic)
            Debug.Log($"[Ollama] <<< RESPONSE from {url} <<<\n{resp}");

        if (s.stream)
        {
            foreach (string line in resp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                TryInvokeResponse(line, onEachResponseLine);
        }
        else
        {
            TryInvokeResponse(resp, onEachResponseLine);
        }
    }

    // ──────────────────────────────────
    // 2) Chat Completion
    // ──────────────────────────────────
    public void ChatCompletion(
        OllamaSettings settings,
        ChatMessage[] messages,
        Action<string> onResponse)
    {
        StartCoroutine(CoChatCompletion(settings, messages, onResponse));
    }

    private IEnumerator CoChatCompletion(
        OllamaSettings s,
        ChatMessage[] msgs,
        Action<string> onResponse)
    {
        if (!Validate(s, msgs))
            yield break;

        // ─── 로깅: system/user 합친 Chat messages ───
        if (logLLMTraffic)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Ollama] ► CHAT MESSAGES:");
            foreach (var m in msgs)
                sb.AppendLine($"{m.role.ToUpper()}: {m.content}");
            Debug.Log(sb.ToString());
        }

        object body = new
        {
            model = s.model,
            messages = msgs,
            format = ParseFormat(s.format),
            stream = s.stream,
            keep_alive = s.keepAlive,
            options = new
            {
                temperature = s.modelParams.temperature,
                top_p = s.modelParams.top_p,
                top_k = s.modelParams.top_k,
                num_predict = s.modelParams.num_predict,
                repeat_penalty = s.modelParams.repeat_penalty
            }
        };

        string url = $"{OllamaAutoLoader.GetServerAddress(s.model)}/api/chat";
        yield return SendOllamaRequest(url, body, s.stream, onResponse);
    }

    // ──────────────────────────────────
    // 3) Embedding
    // ──────────────────────────────────
    public void Embed(
        OllamaSettings settings,
        string[] inputs,
        Action<float[][]> onEmbeddings)
    {
        StartCoroutine(CoEmbed(settings, inputs, onEmbeddings));
    }

    private IEnumerator CoEmbed(
        OllamaSettings s,
        string[] inputs,
        Action<float[][]> onEmbeddings)
    {
        if (!Validate(s, inputs))
            yield break;

        object body = new
        {
            model = s.model,
            input = (inputs.Length == 1) ? (object)inputs[0] : inputs,
            keep_alive = s.keepAlive,
            truncate = true,
            options = new { temperature = s.modelParams.temperature }
        };

        string url = $"{OllamaAutoLoader.GetServerAddress(s.model)}/api/embed";
        string json = JsonConvert.SerializeObject(body,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        if (logLLMTraffic)
            Debug.Log($"[Ollama] >>> EMBED REQUEST to {url} <<<\n{json}");

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Ollama] embed error: {req.error}, code={req.responseCode}");
            yield break;
        }

        string embedResp = req.downloadHandler.text;
        if (logLLMTraffic)
            Debug.Log($"[Ollama] <<< EMBED RESPONSE <<<\n{embedResp}");

        try
        {
            var embeddings = ParseEmbeddings(embedResp);
            onEmbeddings?.Invoke(embeddings);
        }
        catch (Exception e)
        {
            Debug.LogError("[Ollama] embed parse error: " + e);
        }
    }

    // ──────────────────────────────────
    //  Shared HTTP helper for chat/embed
    // ──────────────────────────────────
    private IEnumerator SendOllamaRequest(
        string url,
        object body,
        bool isStreaming,
        Action<string> onEachResponseLine)
    {
        string json = JsonConvert.SerializeObject(body,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        if (logLLMTraffic)
            Debug.Log($"[Ollama] >>> REQUEST to {url} <<<\n{json}");

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Ollama] request error: {req.error}, code={req.responseCode}");
            yield break;
        }

        string resp = req.downloadHandler.text;
        if (logLLMTraffic)
            Debug.Log($"[Ollama] <<< RESPONSE from {url} <<<\n{resp}");

        if (isStreaming)
        {
            foreach (string line in resp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                TryInvokeResponse(line, onEachResponseLine);
        }
        else
        {
            TryInvokeResponse(resp, onEachResponseLine);
        }
    }

    private static void TryInvokeResponse(string jsonLine, Action<string> cb)
    {
        try
        {
            string content = JObject.Parse(jsonLine)["response"]?.ToString();
            if (!string.IsNullOrEmpty(content))
                cb?.Invoke(content);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Ollama] parse error: " + e);
        }
    }

    private static bool Validate(OllamaSettings s, object payload)
    {
        if (s == null) { Debug.LogError("[Ollama] settings null!"); return false; }
        if (payload == null || (payload is string str && string.IsNullOrEmpty(str)))
        {
            Debug.LogWarning("[Ollama] payload empty.");
            return false;
        }
        return true;
    }

    private static object ParseFormat(string fmt)
    {
        if (string.IsNullOrEmpty(fmt)) return null;
        try { return JToken.Parse(fmt); }
        catch { return fmt; }
    }

    private static float[][] ParseEmbeddings(string json)
    {
        var root = JObject.Parse(json);
        var arr = root["embeddings"];
        var list = new List<float[]>();
        foreach (var item in arr)
        {
            var row = new List<float>();
            foreach (var v in item) row.Add(v.ToObject<float>());
            list.Add(row.ToArray());
        }
        return list.ToArray();
    }
}

/// <summary>ChatMessage struct for chat completions</summary>
[Serializable]
public struct ChatMessage
{
    public string role;     // "system" | "user" | "assistant"
    public string content;
}
