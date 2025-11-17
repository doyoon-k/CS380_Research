using System;

/// <summary>
/// Central registration point so systems can access the active <see cref="IOllamaService"/>.
/// </summary>
public static class OllamaServiceLocator
{
    public static IOllamaService Current { get; private set; }

    public static void Register(IOllamaService service)
    {
        Current = service;
    }

    public static void Unregister(IOllamaService service)
    {
        if (Current == service)
        {
            Current = null;
        }
    }

    public static IOllamaService Require()
    {
        if (Current == null)
        {
            throw new InvalidOperationException("No IOllamaService is registered. Ensure a runtime adapter or editor service is initialized before running pipelines.");
        }
        return Current;
    }
}
