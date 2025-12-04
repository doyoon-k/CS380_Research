using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class StatTierEvaluationLink : IStateChainLink, ICustomLinkStateProvider
{
    private readonly StatConfigSO _config;

    public StatTierEvaluationLink(StatConfigSO asset)
    {
        _config = asset;
        if (_config == null)
        {
            Debug.LogError("StatTierEvaluationLink requires a StatConfigSO asset.");
        }
    }

    public System.Collections.IEnumerator Execute(Dictionary<string, string> state, Action<Dictionary<string, string>> onDone)
    {
        if (_config == null)
        {
            onDone?.Invoke(new Dictionary<string, string>());
            yield break;
        }

        var result = new Dictionary<string, string>(state);
        var stats = _config.GetStats();

        foreach (var kvp in stats)
        {
            string statName = kvp.Key;
            var definition = kvp.Value;

            if (state.TryGetValue(statName, out string valueStr) && float.TryParse(valueStr, out float value))
            {
                string tier = EvaluateTier(value, definition.MinValue, definition.MaxValue);
                result[$"{statName}Tier"] = tier;
            }
        }

        if (!string.IsNullOrEmpty(_config.CharacterDescription) && !result.ContainsKey("character_description"))
        {
            result["character_description"] = _config.CharacterDescription;
        }

        onDone?.Invoke(result);
        yield break;
    }

    private string EvaluateTier(float value, float min, float max)
    {
        if (max <= min) return "medium"; // Avoid division by zero

        float t = Mathf.Clamp01((value - min) / (max - min));

        if (t < 0.2f) return "lowest";
        if (t < 0.4f) return "low";
        if (t < 0.6f) return "medium";
        if (t < 0.8f) return "high";
        return "highest";
    }

    public IEnumerable<string> GetWrites()
    {
        if (_config == null)
        {
            return Enumerable.Empty<string>();
        }

        var keys = _config.GetStats().Keys.Select(statName => $"{statName}Tier").ToList();
        if (!string.IsNullOrEmpty(_config.CharacterDescription))
        {
            keys.Add("character_description");
        }
        return keys;
    }

    public IEnumerable<string> GetRequiredInputKeys()
    {
        if (_config == null)
        {
            return Enumerable.Empty<string>();
        }

        return _config.GetStats().Keys;
    }
}
