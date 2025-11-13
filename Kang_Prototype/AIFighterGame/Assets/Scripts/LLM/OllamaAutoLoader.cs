#if UNITY_EDITOR
using UnityEditor; // for EditorApplication events
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Manages per-model Ollama servers. Each model is served by a dedicated
/// Ollama process listening on its own port.
/// </summary>
[InitializeOnLoad]
public static class OllamaAutoLoader
{
    private class ServerInfo
    {
        public Process Process;
        public ProcessWrapper Wrapper;
        public string Address;
    }

    private static readonly Dictionary<string, ServerInfo> servers = new();
    private static int nextPort = 11434;

    private const string CMD_EXE = "cmd.exe";
    private const string OLLAMA_ARGS = "/c ollama serve";

    static OllamaAutoLoader()
    {
        Environment.SetEnvironmentVariable("OLLAMA_NUM_GPU_LAYERS", "100");
#if UNITY_EDITOR
        EditorApplication.quitting += OnEditorQuitting;
#endif
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void OnRuntimeStart()
    {
#if !UNITY_EDITOR
        Application.quitting += OnApplicationQuit;
#endif
    }

    public static string GetServerAddress(string model)
    {
        if (!servers.TryGetValue(model, out var info))
        {
            info = LaunchServer(model);
            servers[model] = info;
        }
        return info.Address;
    }

    private static ServerInfo LaunchServer(string model)
    {
        int port = nextPort++;
        string address = $"http://localhost:{port}";

        var wrapper = new ProcessWrapper();
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            FileName = CMD_EXE,
            Arguments = OLLAMA_ARGS,
            UseShellExecute = false
        };
        startInfo.EnvironmentVariables["OLLAMA_HOST"] = address;
        startInfo.EnvironmentVariables["OLLAMA_KEEP_ALIVE"] = "-1";

        var process = new Process { StartInfo = startInfo };
        if (!process.Start() || process.HasExited)
            throw new Exception($"Failed to start ollama process for {model}");

        wrapper.AddProcess(process.Id);

        UnityEngine.Debug.Log($"[OllamaAutoLoader] Started server for {model} at {address}");

        return new ServerInfo
        {
            Process = process,
            Wrapper = wrapper,
            Address = address
        };
    }

    public static void StopAllServers()
    {
        foreach (var kv in servers)
        {
            var info = kv.Value;
            if (info.Process != null && !info.Process.HasExited)
            {
                try
                {
                    info.Process.Kill();
                    info.Process.WaitForExit(2000);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[OllamaAutoLoader] kill error: {ex}");
                }
            }
            info.Wrapper?.Dispose();
        }
        servers.Clear();
        UnityEngine.Debug.Log("[OllamaAutoLoader] All Ollama servers stopped.");
    }

#if UNITY_EDITOR
    private static void OnEditorQuitting()
    {
        StopAllServers();
    }
#endif

#if !UNITY_EDITOR
    private static void OnApplicationQuit()
    {
        StopAllServers();
    }
#endif
}
