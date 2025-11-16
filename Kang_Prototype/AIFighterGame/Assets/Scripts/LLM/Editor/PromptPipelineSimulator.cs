using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds and runs PromptPipelineAssets directly inside the editor for quick experimentation.
/// </summary>
public static class PromptPipelineSimulator
{
    public static void Run(
        PromptPipelineAsset asset,
        Dictionary<string, string> initialState,
        Action<Dictionary<string, string>> onSuccess,
        Action<string> onError
    )
    {
        if (asset == null)
        {
            onError?.Invoke("No PromptPipelineAsset selected.");
            return;
        }

        try
        {
            var executor = BuildExecutor(asset);
            var startingState = CloneOrCreate(initialState);
            EditorCoroutineRunner.Start(RunExecutor(executor, startingState, onSuccess));
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex.Message);
        }
    }

    private static StateSequentialChainExecutor BuildExecutor(PromptPipelineAsset asset)
    {
        var executor = new StateSequentialChainExecutor();
        foreach (var step in asset.steps)
        {
            if (step == null)
            {
                continue;
            }

            var link = CreateLink(step);
            if (link == null)
            {
                throw new InvalidOperationException($"Step '{step.stepName}' failed to create IStateChainLink.");
            }

            executor.AddLink(link);
        }

        return executor;
    }

    private static IStateChainLink CreateLink(PromptPipelineStep step)
    {
        switch (step.stepKind)
        {
            case PromptPipelineStepKind.JsonLlm:
                EnsureSettings(step);
                return new JSONLLMStateChainLink(
                    step.ollamaSettings,
                    step.userPromptTemplate,
                    step.jsonMaxRetries,
                    step.jsonRetryDelaySeconds
                );
            case PromptPipelineStepKind.CompletionLlm:
                EnsureSettings(step);
                return new CompletionChainLink(
                    step.ollamaSettings,
                    step.userPromptTemplate
                );
            case PromptPipelineStepKind.CustomLink:
                return InstantiateCustomLink(step);
            default:
                return null;
        }
    }

    private static IStateChainLink InstantiateCustomLink(PromptPipelineStep step)
    {
        if (string.IsNullOrEmpty(step.customLinkTypeName))
        {
            throw new InvalidOperationException($"Custom link step '{step.stepName}' is missing a type name.");
        }

        var type = Type.GetType(step.customLinkTypeName);
        if (type == null)
        {
            throw new InvalidOperationException($"Could not resolve custom link type '{step.customLinkTypeName}'.");
        }

        if (!typeof(IStateChainLink).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"Type '{step.customLinkTypeName}' does not implement IStateChainLink.");
        }

        if (Activator.CreateInstance(type) is not IStateChainLink instance)
        {
            throw new InvalidOperationException($"Failed to instantiate custom link '{step.customLinkTypeName}'.");
        }

        return instance;
    }

    private static IEnumerator RunExecutor(
        StateSequentialChainExecutor executor,
        Dictionary<string, string> state,
        Action<Dictionary<string, string>> onSuccess
    )
    {
        Dictionary<string, string> finalState = null;
        yield return executor.Execute(state, s => finalState = s);
        onSuccess?.Invoke(finalState ?? state);
    }

    private static Dictionary<string, string> CloneOrCreate(Dictionary<string, string> source)
    {
        return source != null
            ? new Dictionary<string, string>(source)
            : new Dictionary<string, string>();
    }

    private static void EnsureSettings(PromptPipelineStep step)
    {
        if (step.ollamaSettings == null)
        {
            throw new InvalidOperationException($"Step '{step.stepName}' requires OllamaSettings.");
        }
    }
}
