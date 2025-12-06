using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Pure C# worker that performs Ollama REST requests. Shared by runtime and editor services.
/// </summary>
public class OllamaHttpWorker
{
    public IEnumerator GenerateCompletion(
        OllamaSettings settings,
        string userPrompt,
        Action<string> onResponse,
        bool logLLMTraffic)
    {
        string systemPrompt = settings?.systemPromptTemplate;
        yield return GenerateCompletionInternal(settings, userPrompt, systemPrompt, onResponse, logLLMTraffic);
    }

    public IEnumerator GenerateCompletionWithState(
        OllamaSettings settings,
        string userPrompt,
        Dictionary<string, string> state,
        Action<string> onResponse,
        bool logLLMTraffic)
    {
        if (settings != null)
        {
            settings.RenderSystemPrompt(state);
        }

        string renderedSystem = settings != null ? settings.GetLastRenderedPrompt() : null;
        yield return GenerateCompletionInternal(settings, userPrompt, renderedSystem, onResponse, logLLMTraffic);
    }

    public IEnumerator ChatCompletion(
        OllamaSettings settings,
        ChatMessage[] messages,
        Action<string> onResponse,
        bool logLLMTraffic)
    {
        if (!Validate(settings, messages))
            yield break;

        if (logLLMTraffic)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Ollama] ► CHAT MESSAGES:");
            if (messages != null)
            {
                foreach (var m in messages)
                {
                    sb.AppendLine($"{m.role.ToUpper()}: {m.content}");
                }
            }
            Debug.Log(sb.ToString());
        }

        object body = new
        {
            model = settings.model,
            messages,
            format = ParseFormat(settings.format),
            stream = settings.stream,
            keep_alive = settings.keepAlive,
            options = new
            {
                temperature = settings.modelParams.temperature,
                top_p = settings.modelParams.top_p,
                top_k = settings.modelParams.top_k,
                num_predict = settings.modelParams.num_predict,
                repeat_penalty = settings.modelParams.repeat_penalty
            }
        };

        string url = $"{OllamaAutoLoader.GetServerAddress(settings.model)}/api/chat";
        yield return SendOllamaRequest(url, body, settings.stream, onResponse, logLLMTraffic);
    }

    public IEnumerator Embed(
        OllamaSettings settings,
        string[] inputs,
        Action<float[][]> onEmbeddings,
        bool logLLMTraffic)
    {
        if (!Validate(settings, inputs))
            yield break;

        object body = new
        {
            model = settings.model,
            input = (inputs.Length == 1) ? (object)inputs[0] : inputs,
            keep_alive = settings.keepAlive,
            truncate = true,
            options = new { temperature = settings.modelParams.temperature }
        };

        string url = $"{OllamaAutoLoader.GetServerAddress(settings.model)}/api/embed";
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

    private IEnumerator GenerateCompletionInternal(
        OllamaSettings settings,
        string userPrompt,
        string renderedSystem,
        Action<string> onEachResponseLine,
        bool logLLMTraffic)
    {
        if (!Validate(settings, userPrompt))
            yield break;

        if (logLLMTraffic)
        {
            Debug.Log($"[Ollama] ► SYSTEM PROMPT:\n{renderedSystem}\n" +
                      $"[Ollama] ► USER PROMPT:\n{userPrompt}");
        }

        var body = new Dictionary<string, object>
        {
            { "model", settings.model },
            { "prompt", userPrompt }
        };
        if (!string.IsNullOrEmpty(renderedSystem))
            body["system"] = renderedSystem;
        if (!string.IsNullOrEmpty(settings.format))
            body["format"] = ParseFormat(settings.format);
        body["stream"] = settings.stream;
        if (!string.IsNullOrEmpty(settings.keepAlive))
            body["keep_alive"] = settings.keepAlive;

        var options = new Dictionary<string, object>
        {
            { "temperature", settings.modelParams.temperature },
            { "top_p",       settings.modelParams.top_p },
            { "top_k",       settings.modelParams.top_k },
            { "num_predict", settings.modelParams.num_predict },
            { "repeat_penalty", settings.modelParams.repeat_penalty }
        };
        body["options"] = options;

        string url = $"{OllamaAutoLoader.GetServerAddress(settings.model)}/api/generate";
        string jsonReq = JsonConvert.SerializeObject(body,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

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

        if (logLLMTraffic)
            Debug.Log($"[Ollama] <<< RESPONSE from {url} <<<\n{resp}");

        if (settings.stream)
        {
            foreach (string line in resp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                TryInvokeResponse(line, onEachResponseLine);
        }
        else
        {
            TryInvokeResponse(resp, onEachResponseLine);
        }
    }

    private IEnumerator SendOllamaRequest(
        string url,
        object body,
        bool isStreaming,
        Action<string> onEachResponseLine,
        bool logLLMTraffic)
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

    private static bool Validate(OllamaSettings settings, object payload)
    {
        if (settings == null)
        {
            Debug.LogError("[Ollama] settings null!");
            return false;
        }

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
