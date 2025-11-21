using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that stores a linear prompt pipeline definition used by the GraphView editor.
/// </summary>
[CreateAssetMenu(fileName = "PromptPipeline", menuName = "LLM/Prompt Pipeline", order = 0)]
public class PromptPipelineAsset : ScriptableObject
{
    [Tooltip("Human readable pipeline name that shows up in GraphView toolbars.")]
    public string displayName = "New Prompt Pipeline";

    [TextArea(2, 5)]
    public string description;

    [SerializeField]
    public List<PromptPipelineStep> steps = new();

    [SerializeField]
    public PromptPipelineLayoutSettings layoutSettings = new();

    /// <summary>
    /// Ensures we always have a valid step list when the asset is created or loaded.
    /// </summary>
    private void OnValidate()
    {
        if (steps == null)
        {
            steps = new List<PromptPipelineStep>();
        }

        layoutSettings ??= new PromptPipelineLayoutSettings();
    }

    /// <summary>
    /// Builds a runnable executor for this pipeline using the provided IOllamaService.
    /// </summary>
    public StateSequentialChainExecutor BuildExecutor(IOllamaService service)
    {
        if (service == null)
        {
            throw new InvalidOperationException("IOllamaService is required to build a pipeline executor.");
        }

        var executor = new StateSequentialChainExecutor();

        if (steps == null)
        {
            return executor;
        }

        foreach (var step in steps)
        {
            if (step == null)
            {
                continue;
            }

            executor.AddLink(CreateLink(step, service));
        }

        return executor;
    }

    private static IStateChainLink CreateLink(PromptPipelineStep step, IOllamaService service)
    {
        switch (step.stepKind)
        {
            case PromptPipelineStepKind.JsonLlm:
                EnsureSettings(step);
                return new JSONLLMStateChainLink(
                    service,
                    step.ollamaSettings,
                    step.userPromptTemplate,
                    step.jsonMaxRetries,
                    step.jsonRetryDelaySeconds
                );
            case PromptPipelineStepKind.CompletionLlm:
                EnsureSettings(step);
                return new CompletionChainLink(
                    service,
                    step.ollamaSettings,
                    step.userPromptTemplate
                );
            case PromptPipelineStepKind.CustomLink:
                return InstantiateCustomLink(step);
            default:
                throw new InvalidOperationException($"Unsupported PromptPipelineStepKind: {step.stepKind}");
        }
    }

    private static void EnsureSettings(PromptPipelineStep step)
    {
        if (step.ollamaSettings == null)
        {
            throw new InvalidOperationException($"Step '{step.stepName}' requires OllamaSettings.");
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
}

/// <summary>
/// Serialized data for a single pipeline step (LLM or custom link).
/// </summary>
[Serializable]
public class PromptPipelineStep
{
    public string stepName = "Step";

    public PromptPipelineStepKind stepKind = PromptPipelineStepKind.JsonLlm;

    [Header("Shared LLM Settings")]
    public OllamaSettings ollamaSettings;

    [TextArea(4, 12)]
    public string userPromptTemplate;

    [Header("JSON LLM Options")]
    [Min(1)]
    public int jsonMaxRetries = 3;

    [Min(0f)]
    public float jsonRetryDelaySeconds = 0.1f;

    [Header("Custom Link Options")]
    [Tooltip("Full type name implementing IStateChainLink (Type.GetType resolvable).")]
    public string customLinkTypeName;

    [HideInInspector]
    public Vector2 editorPosition;
}

/// <summary>
/// Identifies which kind of step a PromptPipelineStep represents.
/// </summary>
public enum PromptPipelineStepKind
{
    JsonLlm = 0,
    CompletionLlm = 1,
    CustomLink = 2
}

[Serializable]
public class PromptPipelineLayoutSettings
{
    public Vector2 inputNodePosition = new(-600f, 80f);
    public Vector2 outputNodePosition = Vector2.zero;
    public bool inputPositionInitialized;
    public bool outputPositionInitialized;
    public Vector3 viewPosition = Vector3.zero;
    public Vector3 viewScale = Vector3.one;
    public bool viewInitialized;
}
