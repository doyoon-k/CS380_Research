using System;
using UnityEngine;

public static class OllamaSettingsChangeNotifier
{
    public static event Action<OllamaSettings> SettingsChanged;

    public static void RaiseChanged(OllamaSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        SettingsChanged?.Invoke(settings);
    }
}
