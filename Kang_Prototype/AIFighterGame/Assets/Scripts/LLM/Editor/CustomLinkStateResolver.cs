using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Editor-only helper that resolves declared read/write keys for custom link types.
/// </summary>
internal static class CustomLinkStateResolver
{
    public static bool TryResolve(string typeName, out List<string> writes)
    {
        writes = new List<string>();

        var type = CustomLinkTypeProvider.ResolveType(typeName);
        if (type == null)
        {
            return false;
        }

        // Avoid expensive activations; rely on parameterless constructor (required for graph selection).
        try
        {
            if (Activator.CreateInstance(type) is not ICustomLinkStateProvider provider)
            {
                return false;
            }

            writes = Normalize(provider.GetWrites());
            return writes.Count > 0;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"CustomLinkStateResolver: Failed to resolve state for '{typeName}': {ex.Message}");
            return false;
        }
    }

    private static List<string> Normalize(IEnumerable<string> source)
    {
        if (source == null)
        {
            return new List<string>();
        }

        return source
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }
}
