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

    /// <summary>
    /// Ensures we always have a valid step list when the asset is created or loaded.
    /// </summary>
    private void OnValidate()
    {
        if (steps == null)
        {
            steps = new List<PromptPipelineStep>();
        }
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
