using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class PromptPipelineGraphView : GraphView
{
    private readonly Action _markAssetDirty;
    private readonly Action<string> _recordUndo;
    private PromptPipelineAsset _asset;
    private readonly List<PromptPipelineStepNode> _stepNodes = new();
    private readonly List<Edge> _executionEdges = new();
    private readonly List<Edge> _stateEdges = new();
    private StateBlackboardNode _stateBlackboardNode;
    private PipelineInputNode _inputNode;
    private PipelineOutputNode _outputNode;
    private AnalyzedStateModel _stateModel;
    private readonly Dictionary<int, List<string>> _readsByStep = new();
    private readonly Dictionary<int, List<string>> _writesByStep = new();
    private readonly HashSet<string> _inputKeys = new();
    private readonly HashSet<string> _outputKeys = new();
    private bool _pendingStateRefresh;

    public event Action<AnalyzedStateModel> StateModelChanged;

    public PromptPipelineGraphView(Action markAssetDirty, Action<string> recordUndo)
    {
        _markAssetDirty = markAssetDirty;
        _recordUndo = recordUndo;

        style.flexGrow = 1f;

        SetupZoom(0.05f, 2f);
        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        graphViewChanged = OnGraphViewChanged;
        nodeCreationRequest = ctx =>
        {
            Vector2 graphPosition = contentViewContainer.WorldToLocal(ctx.screenMousePosition);
            CreateStepAt(graphPosition);
        };
    }

    public void SetAsset(PromptPipelineAsset asset)
    {
        _asset = asset;
        Reload();
    }

    public void ApplySimulationResult(Dictionary<string, string> state)
    {
        if (_stateModel == null || state == null)
        {
            return;
        }

        foreach (var key in _stateModel.keys)
        {
            if (state.TryGetValue(key.keyName, out var value))
            {
                key.lastValuePreview = value;
                _stateBlackboardNode?.UpdatePreview(key.keyName, value);
            }
        }
    }

    private void Reload()
    {
        ClearGraph();

        if (_asset == null || _asset.steps == null)
        {
            return;
        }

        BuildStepNodes();
        BuildExecutionEdgesFromAsset();
        RefreshStateAnalysis();
        FrameAll();
    }

    private void ClearGraph()
    {
        foreach (var edge in _executionEdges)
        {
            RemoveElement(edge);
        }
        _executionEdges.Clear();

        foreach (var edge in _stateEdges)
        {
            RemoveElement(edge);
        }
        _stateEdges.Clear();

        foreach (var node in _stepNodes)
        {
            RemoveElement(node);
        }
        _stepNodes.Clear();

        if (_stateBlackboardNode != null)
        {
            RemoveElement(_stateBlackboardNode);
            _stateBlackboardNode = null;
        }

        if (_inputNode != null)
        {
            RemoveElement(_inputNode);
            _inputNode = null;
        }

        if (_outputNode != null)
        {
            RemoveElement(_outputNode);
            _outputNode = null;
        }

        _stateModel = null;
        _readsByStep.Clear();
        _writesByStep.Clear();
        _inputKeys.Clear();
        _outputKeys.Clear();
    }

    private void BuildStepNodes()
    {
        for (int i = 0; i < _asset.steps.Count; i++)
        {
            var step = _asset.steps[i];
            if (step == null)
            {
                continue;
            }

            var node = new PromptPipelineStepNode(
                step,
                i,
                MarkAssetDirty,
                RecordUndo,
                RequestStateRefresh,
                Reload,
                () => _stateModel?.keys?.Select(k => k.keyName) ?? Enumerable.Empty<string>(),
                DisconnectExecPort
            );

            Vector2 desiredPosition = step.editorPosition;
            if (desiredPosition == Vector2.zero)
            {
                desiredPosition = new Vector2(200 + i * 320, 120);
                step.editorPosition = desiredPosition;
            }

            node.SetPosition(new Rect(desiredPosition, new Vector2(320, 360)));
            AddElement(node);
            _stepNodes.Add(node);
        }
    }

    private void BuildExecutionEdgesFromAsset()
    {
        RemoveExecutionEdges();
        if (_stepNodes.Count <= 1)
        {
            return;
        }

        for (int i = 0; i < _stepNodes.Count - 1; i++)
        {
            var edge = _stepNodes[i].ExecOutPort.ConnectTo(_stepNodes[i + 1].ExecInPort);
            ConfigureExecutionEdge(edge);
        }
    }

    private void RemoveExecutionEdges()
    {
        foreach (var edge in _executionEdges)
        {
            RemoveElement(edge);
        }
        _executionEdges.Clear();
    }

    private void ConfigureExecutionEdge(Edge edge)
    {
        if (edge == null)
        {
            return;
        }

        RegisterExecutionEdge(edge);
        AddElement(edge);
    }

    private void RegisterExecutionEdge(Edge edge)
    {
        if (edge == null)
        {
            return;
        }

        edge.userData = EdgeCategory.Execution;
        if (!_executionEdges.Contains(edge))
        {
            _executionEdges.Add(edge);
        }
    }

    private void DisconnectExecPort(Port port)
    {
        if (port?.connections == null)
        {
            return;
        }

        var edges = port.connections.ToList();
        if (edges.Count == 0)
        {
            return;
        }

        foreach (var edge in edges)
        {
            edge.output?.Disconnect(edge);
            edge.input?.Disconnect(edge);
            _executionEdges.Remove(edge);
            RemoveElement(edge);
        }

        EditorApplication.delayCall += ApplyExecutionOrderFromGraph;
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter adapter)
    {
        var compatible = new List<Port>();
        foreach (var port in ports.ToList())
        {
            if (port == startPort || port.node == startPort.node)
            {
                continue;
            }

            if (startPort.userData is StepPortKind startKind &&
                port.userData is StepPortKind targetKind)
            {
                bool execCompatible =
                    (startKind == StepPortKind.ExecOut && targetKind == StepPortKind.ExecIn) ||
                    (startKind == StepPortKind.ExecIn && targetKind == StepPortKind.ExecOut);

                if (execCompatible)
                {
                    compatible.Add(port);
                }

                continue;
            }

            if (startPort.direction != port.direction &&
                startPort.portType == port.portType)
            {
                compatible.Add(port);
            }
        }

        return compatible;
    }

    private void RefreshStateAnalysis()
    {
        if (_asset == null)
        {
            return;
        }

        _stateModel = PipelineStateAnalyzer.Analyze(_asset);
        BuildStateLookups();
        RebuildStateNodes();
        UpdateNodeStateData();
        StateModelChanged?.Invoke(_stateModel);
    }

    private void BuildStateLookups()
    {
        _readsByStep.Clear();
        _writesByStep.Clear();
        _inputKeys.Clear();
        _outputKeys.Clear();

        if (_stateModel == null || _stateModel.keys == null)
        {
            return;
        }

        foreach (AnalyzedStateKey key in _stateModel.keys)
        {
            if (key == null)
                continue;

            if (key.kind == AnalyzedStateKeyKind.Input)
            {
                _inputKeys.Add(key.keyName);
            }
            else if (key.kind == AnalyzedStateKeyKind.Output)
            {
                _outputKeys.Add(key.keyName);
            }

            foreach (int idx in key.consumedByStepIndices)
            {
                AddKeyToMap(_readsByStep, idx, key.keyName);
            }

            foreach (int idx in key.producedByStepIndices)
            {
                AddKeyToMap(_writesByStep, idx, key.keyName);
            }
        }
    }

    private static void AddKeyToMap(Dictionary<int, List<string>> map, int index, string keyName)
    {
        if (!map.TryGetValue(index, out var list))
        {
            list = new List<string>();
            map[index] = list;
        }

        if (!list.Contains(keyName))
        {
            list.Add(keyName);
        }
    }

    private void RebuildStateNodes()
    {
        foreach (var edge in _stateEdges)
        {
            RemoveElement(edge);
        }
        _stateEdges.Clear();

        if (_stateBlackboardNode != null)
        {
            RemoveElement(_stateBlackboardNode);
            _stateBlackboardNode = null;
        }

        if (_inputNode != null)
        {
            RemoveElement(_inputNode);
            _inputNode = null;
        }

        if (_outputNode != null)
        {
            RemoveElement(_outputNode);
            _outputNode = null;
        }

        _stateBlackboardNode = new StateBlackboardNode();
        _stateBlackboardNode.Bind(_stateModel);
        _stateBlackboardNode.SetPosition(new Rect(-300, 360, 420, 360));
        AddElement(_stateBlackboardNode);

        _inputNode = new PipelineInputNode();
        _inputNode.Bind(_stateModel);
        _inputNode.SetPosition(new Rect(-600, 80, 240, 320));
        AddElement(_inputNode);

        _outputNode = new PipelineOutputNode();
        _outputNode.Bind(_stateModel);
        float outputX = 320 * (_stepNodes.Count + 1);
        _outputNode.SetPosition(new Rect(outputX, 80, 240, 320));
        AddElement(_outputNode);

        CreateStateEdges();
    }

    private void CreateStateEdges()
    {
        foreach (var edge in _stateEdges)
        {
            RemoveElement(edge);
        }
        _stateEdges.Clear();

        for (int i = 0; i < _stepNodes.Count; i++)
        {
            var node = _stepNodes[i];
            var reads = GetReads(i);
            var writes = GetWrites(i);

            node.UpdateStateSummary(reads, writes);
            node.UpdateAvailableStateKeys(_stateModel?.keys?.Select(k => k.keyName));

            foreach (string key in reads)
            {
                if (_stateBlackboardNode != null)
                {
                    var readPort = _stateBlackboardNode.GetReadPort(key);
                    if (readPort != null)
                    {
                        var edge = readPort.ConnectTo(node.StateInPort);
                        ConfigureStateEdge(edge);
                    }
                }

                if (_inputKeys.Contains(key) && _inputNode != null)
                {
                    var inputPort = _inputNode.GetPort(key);
                    if (inputPort != null)
                    {
                        var edge = inputPort.ConnectTo(node.StateInPort);
                        ConfigureStateEdge(edge);
                    }
                }
            }

            foreach (string key in writes)
            {
                if (_stateBlackboardNode != null)
                {
                    var writePort = _stateBlackboardNode.GetWritePort(key);
                    if (writePort != null)
                    {
                        var edge = node.StateOutPort.ConnectTo(writePort);
                        ConfigureStateEdge(edge);
                    }
                }

                if (_outputKeys.Contains(key) && _outputNode != null)
                {
                    var outputPort = _outputNode.GetPort(key);
                    if (outputPort != null)
                    {
                        var edge = node.StateOutPort.ConnectTo(outputPort);
                        ConfigureStateEdge(edge);
                    }
                }
            }
        }
    }

    private void ConfigureStateEdge(Edge edge)
    {
        if (edge == null)
        {
            return;
        }

        edge.capabilities &= ~(Capabilities.Deletable | Capabilities.Selectable);
        edge.userData = EdgeCategory.State;
        AddElement(edge);
        _stateEdges.Add(edge);
    }

    private List<string> GetReads(int index)
    {
        return _readsByStep.TryGetValue(index, out var list) ? list : new List<string>();
    }

    private List<string> GetWrites(int index)
    {
        return _writesByStep.TryGetValue(index, out var list) ? list : new List<string>();
    }

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        bool requiresReorder = false;
        bool movedSteps = false;

        if (change.movedElements != null)
        {
            foreach (var element in change.movedElements)
            {
                if (element is PromptPipelineStepNode node)
                {
                    node.PersistPosition();
                    movedSteps = true;
                }
            }
        }

        if (movedSteps)
        {
            MarkAssetDirty();
        }

        if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
        {
            change.edgesToCreate = change.edgesToCreate
                .Where(IsExecutionEdge)
                .ToList();

            foreach (var edge in change.edgesToCreate)
            {
                RegisterExecutionEdge(edge);
            }

            requiresReorder |= change.edgesToCreate.Count > 0;
        }

        if (change.elementsToRemove != null)
        {
            foreach (var element in change.elementsToRemove)
            {
                if (element is Edge edge &&
                    edge.userData is EdgeCategory edgeCategory &&
                    edgeCategory == EdgeCategory.Execution)
                {
                    _executionEdges.Remove(edge);
                    requiresReorder = true;
                }
            }
        }

        if (requiresReorder)
        {
            EditorApplication.delayCall += ApplyExecutionOrderFromGraph;
        }

        return change;
    }

    private static bool IsExecutionEdge(Edge edge)
    {
        return edge.output?.userData is StepPortKind outputKind &&
               outputKind == StepPortKind.ExecOut &&
               edge.input?.userData is StepPortKind inputKind &&
               inputKind == StepPortKind.ExecIn;
    }

    private void ApplyExecutionOrderFromGraph()
    {
        if (_asset == null || _stepNodes.Count == 0)
        {
            return;
        }

        var orderedNodes = BuildExecutionChain();
        if (orderedNodes == null || orderedNodes.Count != _asset.steps.Count)
        {
            Debug.LogWarning("Prompt Pipeline graph must form a single linear chain before order can be updated.");
            return;
        }

        RecordUndo("Reorder Prompt Pipeline");
        _asset.steps.Clear();
        foreach (var node in orderedNodes)
        {
            _asset.steps.Add(node.Step);
        }

        MarkAssetDirty();
        Reload();
    }

    private List<PromptPipelineStepNode> BuildExecutionChain()
    {
        var startNodes = _stepNodes.Where(n => !HasExecInConnection(n)).ToList();
        if (startNodes.Count != 1)
        {
            return null;
        }

        var ordered = new List<PromptPipelineStepNode>();
        var visited = new HashSet<PromptPipelineStepNode>();
        var current = startNodes[0];

        while (current != null)
        {
            if (!visited.Add(current))
            {
                return null;
            }

            ordered.Add(current);
            current = GetNextExecutionNode(current);
        }

        return ordered.Count == _stepNodes.Count ? ordered : null;
    }

    private static bool HasExecInConnection(PromptPipelineStepNode node)
    {
        return node.ExecInPort != null &&
               node.ExecInPort.connections != null &&
               node.ExecInPort.connections.Any();
    }

    private PromptPipelineStepNode GetNextExecutionNode(PromptPipelineStepNode node)
    {
        var connection = node.ExecOutPort?.connections?.FirstOrDefault();
        if (connection?.input?.node is PromptPipelineStepNode stepNode)
        {
            return stepNode;
        }

        return null;
    }

    private void RequestStateRefresh()
    {
        if (_pendingStateRefresh)
        {
            return;
        }

        _pendingStateRefresh = true;
        EditorApplication.delayCall += () =>
        {
            _pendingStateRefresh = false;
            RefreshStateAnalysis();
        };
    }

    private void UpdateNodeStateData()
    {
        for (int i = 0; i < _stepNodes.Count; i++)
        {
            _stepNodes[i].UpdateStateSummary(GetReads(i), GetWrites(i));
            _stepNodes[i].UpdateDisplayIndex(i);
        }
    }

    private void MarkAssetDirty() => _markAssetDirty?.Invoke();
    private void RecordUndo(string label) => _recordUndo?.Invoke(label);

    public void CreateStepAtCenter()
    {
        var rect = contentViewContainer.layout;
        Vector2 graphPosition = rect.width > 0f && rect.height > 0f
            ? rect.center
            : Vector2.zero;
        CreateStepAt(graphPosition);
    }

    private void CreateStepAt(Vector2 graphPosition)
    {
        if (_asset == null)
        {
            return;
        }

        RecordUndo("Add Prompt Step");
        var step = new PromptPipelineStep
        {
            stepName = $"Step {_asset.steps.Count + 1}",
            stepKind = PromptPipelineStepKind.JsonLlm,
            editorPosition = graphPosition
        };

        _asset.steps.Add(step);
        MarkAssetDirty();
        Reload();
    }

    public override EventPropagation DeleteSelection()
    {
        bool removedSteps = false;
        if (_asset != null)
        {
            var stepsToRemove = selection
                .OfType<PromptPipelineStepNode>()
                .Select(n => n.Step)
                .ToList();

            if (stepsToRemove.Count > 0)
            {
                removedSteps = true;
                RecordUndo("Delete Prompt Step");
                foreach (var step in stepsToRemove)
                {
                    _asset.steps.Remove(step);
                }
                MarkAssetDirty();
            }
        }

        var result = base.DeleteSelection();

        if (removedSteps)
        {
            Reload();
        }

        return result;
    }
}

internal enum StepPortKind
{
    ExecIn,
    ExecOut,
    StateIn,
    StateOut
}

internal enum EdgeCategory
{
    Execution,
    State
}

internal class PromptPipelineStepNode : Node
{
    private readonly Action _markDirty;
    private readonly Action<string> _recordUndo;
    private readonly Action _requestStateRefresh;
    private readonly Action _onStepKindChanged;
    private readonly Func<IEnumerable<string>> _stateKeyProvider;
    private readonly Action<Port> _disconnectExecPort;

    private readonly TextField _nameField;
    private readonly TextField _titleEditField;
    private readonly ObjectField _settingsField;
    private readonly EnumField _kindField;
    private readonly TextField _userPromptField;
    private readonly IntegerField _maxRetriesField;
    private readonly FloatField _retryDelayField;
    private readonly TextField _customTypeField;
    private readonly Button _insertStateKeyButton;
    private readonly Label _readsLabel;
    private readonly Label _writesLabel;
    private readonly VisualElement _jsonOptionsContainer;
    private readonly VisualElement _customOptionsContainer;

    private List<string> _availableKeys = new();
    private int _displayIndex;

    public PromptPipelineStep Step { get; }
    public Port ExecInPort { get; }
    public Port ExecOutPort { get; }
    public Port StateInPort { get; }
    public Port StateOutPort { get; }

    public PromptPipelineStepNode(
        PromptPipelineStep step,
        int index,
        Action markDirty,
        Action<string> recordUndo,
        Action requestStateRefresh,
        Action onStepKindChanged,
        Func<IEnumerable<string>> stateKeyProvider,
        Action<Port> disconnectExecPort
    )
    {
        Step = step;
        _markDirty = markDirty;
        _recordUndo = recordUndo;
        _requestStateRefresh = requestStateRefresh;
        _onStepKindChanged = onStepKindChanged;
        _stateKeyProvider = stateKeyProvider;
        _disconnectExecPort = disconnectExecPort;

        title = step.stepName;
        UpdateDisplayIndex(index);
        titleContainer.RegisterCallback<MouseDownEvent>(evt =>
        {
            if (evt.clickCount == 2 && evt.button == 0)
            {
                BeginInlineTitleEdit();
                evt.StopPropagation();
            }
        });

        _titleEditField = new TextField
        {
            style =
            {
                flexGrow = 1f,
                display = DisplayStyle.None
            }
        };
        _titleEditField.RegisterCallback<FocusOutEvent>(_ => CommitInlineTitleEdit());
        _titleEditField.RegisterCallback<KeyDownEvent>(OnTitleEditKeyDown);
        titleContainer.Add(_titleEditField);

        ExecInPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));
        ExecInPort.portName = "Exec In";
        ExecInPort.userData = StepPortKind.ExecIn;
        AttachExecPortContextMenu(ExecInPort);
        inputContainer.Add(ExecInPort);

        ExecOutPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
        ExecOutPort.portName = "Exec Out";
        ExecOutPort.userData = StepPortKind.ExecOut;
        AttachExecPortContextMenu(ExecOutPort);
        outputContainer.Add(ExecOutPort);

        StateInPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
        StateInPort.portName = "State In";
        StateInPort.userData = StepPortKind.StateIn;
        StateInPort.pickingMode = PickingMode.Ignore;
        inputContainer.Add(StateInPort);

        StateOutPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
        StateOutPort.portName = "State Out";
        StateOutPort.userData = StepPortKind.StateOut;
        StateOutPort.pickingMode = PickingMode.Ignore;
        outputContainer.Add(StateOutPort);

        _nameField = new TextField("Step Name") { value = step.stepName };
        _nameField.RegisterValueChangedCallback(evt =>
        {
            ApplyChange("Rename Step", () =>
            {
                step.stepName = evt.newValue;
                UpdateDisplayIndex(index);
            }, refreshState: false);
        });
        extensionContainer.Add(_nameField);

        _kindField = new EnumField("Step Kind", step.stepKind);
        _kindField.RegisterValueChangedCallback(evt =>
        {
            var newKind = (PromptPipelineStepKind)evt.newValue;
            if (newKind == step.stepKind)
            {
                return;
            }

            ApplyChange("Change Step Kind", () => step.stepKind = newKind, refreshState: false);
            _onStepKindChanged?.Invoke();
        });
        extensionContainer.Add(_kindField);

        _settingsField = new ObjectField("Ollama Settings")
        {
            objectType = typeof(OllamaSettings),
            value = step.ollamaSettings
        };
        _settingsField.RegisterValueChangedCallback(evt =>
        {
            ApplyChange("Assign Ollama Settings", () => step.ollamaSettings = evt.newValue as OllamaSettings);
        });
        extensionContainer.Add(_settingsField);

        _userPromptField = new TextField("User Prompt Template")
        {
            multiline = true,
            value = step.userPromptTemplate,
            style = { minHeight = 80 }
        };
        _userPromptField.RegisterValueChangedCallback(evt =>
        {
            ApplyChange("Edit User Prompt", () => step.userPromptTemplate = evt.newValue);
        });
        extensionContainer.Add(_userPromptField);

        _insertStateKeyButton = new Button(OnInsertStateKeyClicked)
        {
            text = "Insert State Key"
        };
        extensionContainer.Add(_insertStateKeyButton);

        _jsonOptionsContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
        _jsonOptionsContainer.Add(new Label("JSON Options"));
        _maxRetriesField = new IntegerField("Max Retries") { value = step.jsonMaxRetries };
        _maxRetriesField.RegisterValueChangedCallback(evt =>
        {
            ApplyChange("Edit JSON Retries", () => step.jsonMaxRetries = Mathf.Max(1, evt.newValue));
        });
        _jsonOptionsContainer.Add(_maxRetriesField);

        _retryDelayField = new FloatField("Retry Delay (s)") { value = step.jsonRetryDelaySeconds };
        _retryDelayField.RegisterValueChangedCallback(evt =>
        {
            ApplyChange("Edit JSON Retry Delay", () => step.jsonRetryDelaySeconds = Mathf.Max(0f, evt.newValue));
        });
        _jsonOptionsContainer.Add(_retryDelayField);
        extensionContainer.Add(_jsonOptionsContainer);

        _customOptionsContainer = new VisualElement { style = { flexDirection = FlexDirection.Column } };
        _customOptionsContainer.Add(new Label("Custom Link Options"));
        _customTypeField = new TextField("Type Name") { value = step.customLinkTypeName };
        _customTypeField.RegisterValueChangedCallback(evt =>
        {
            ApplyChange("Edit Custom Link Type", () => step.customLinkTypeName = evt.newValue, refreshState: false);
        });
        _customOptionsContainer.Add(_customTypeField);
        extensionContainer.Add(_customOptionsContainer);

        _readsLabel = new Label("Reads: -");
        _writesLabel = new Label("Writes: -");
        extensionContainer.Add(_readsLabel);
        extensionContainer.Add(_writesLabel);

        RefreshSections();
        RefreshExpandedState();
    }

    public void UpdateStateSummary(IReadOnlyCollection<string> reads, IReadOnlyCollection<string> writes)
    {
        _readsLabel.text = $"Reads: {(reads == null || reads.Count == 0 ? "-" : string.Join(", ", reads))}";
        _writesLabel.text = $"Writes: {(writes == null || writes.Count == 0 ? "-" : string.Join(", ", writes))}";
    }

    public void UpdateAvailableStateKeys(IEnumerable<string> keys)
    {
        _availableKeys = keys?.Distinct().ToList() ?? new List<string>();
        _insertStateKeyButton.SetEnabled(_availableKeys.Count > 0);
    }

    public void UpdateDisplayIndex(int index)
    {
        _displayIndex = index;
        title = $"{index + 1}. {Step.stepName} ({Step.stepKind})";
        UpdateHeaderStyle();
    }

    public void PersistPosition()
    {
        Step.editorPosition = GetPosition().position;
    }

    private void ApplyChange(string undoLabel, Action mutate, bool refreshState = true)
    {
        _recordUndo?.Invoke(undoLabel);
        mutate?.Invoke();
        _markDirty?.Invoke();
        if (refreshState)
        {
            _requestStateRefresh?.Invoke();
        }
    }

    private void RefreshSections()
    {
        bool isJson = Step.stepKind == PromptPipelineStepKind.JsonLlm;
        bool isCustom = Step.stepKind == PromptPipelineStepKind.CustomLink;

        _jsonOptionsContainer.style.display = isJson ? DisplayStyle.Flex : DisplayStyle.None;
        _customOptionsContainer.style.display = isCustom ? DisplayStyle.Flex : DisplayStyle.None;
        UpdateHeaderStyle();
    }

    private void OnInsertStateKeyClicked()
    {
        if (_availableKeys == null || _availableKeys.Count == 0)
        {
            return;
        }

        var menu = new GenericMenu();
        foreach (string key in _availableKeys)
        {
            menu.AddItem(new GUIContent(key), false, () =>
            {
                string insertion = $"{{{{{key}}}}}";
                ApplyChange("Insert State Key", () =>
                {
                    Step.userPromptTemplate = (Step.userPromptTemplate ?? string.Empty) + insertion;
                    _userPromptField.value = Step.userPromptTemplate;
                });
            });
        }
        menu.DropDown(_insertStateKeyButton.worldBound);
    }

    private void UpdateHeaderStyle()
    {
        var color = GetColorForKind(Step.stepKind);
        titleContainer.style.backgroundColor = new StyleColor(color);
    }

    private static Color GetColorForKind(PromptPipelineStepKind kind)
    {
        return kind switch
        {
            PromptPipelineStepKind.JsonLlm => new Color(0.18f, 0.5f, 0.82f),
            PromptPipelineStepKind.CompletionLlm => new Color(0.25f, 0.7f, 0.45f),
            PromptPipelineStepKind.CustomLink => new Color(0.8f, 0.55f, 0.2f),
            _ => new Color(0.3f, 0.3f, 0.3f)
        };
    }

    private void AttachExecPortContextMenu(Port port)
    {
        if (port == null)
        {
            return;
        }

        port.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction(
                "Disconnect",
                _ => _disconnectExecPort?.Invoke(port),
                _ => port.connections != null && port.connections.Any()
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled
            );
        }));
    }

    private void BeginInlineTitleEdit()
    {
        _titleEditField.value = Step.stepName;
        _titleEditField.style.display = DisplayStyle.Flex;
        _titleEditField.Focus();
        _titleEditField.SelectAll();
    }

    private void CommitInlineTitleEdit()
    {
        if (_titleEditField.style.display == DisplayStyle.None)
        {
            return;
        }

        string newName = _titleEditField.value?.Trim();
        _titleEditField.style.display = DisplayStyle.None;

        if (string.IsNullOrEmpty(newName) || newName == Step.stepName)
        {
            return;
        }

        ApplyChange("Rename Step", () =>
        {
            Step.stepName = newName;
            UpdateDisplayIndex(_displayIndex);
            _nameField.value = newName;
        }, refreshState: false);
    }

    private void CancelInlineTitleEdit()
    {
        _titleEditField.style.display = DisplayStyle.None;
    }

    private void OnTitleEditKeyDown(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            CommitInlineTitleEdit();
            evt.StopPropagation();
        }
        else if (evt.keyCode == KeyCode.Escape)
        {
            CancelInlineTitleEdit();
            evt.StopPropagation();
        }
    }
}

internal class StateBlackboardNode : Node
{
    private readonly Dictionary<string, Port> _readPorts = new();
    private readonly Dictionary<string, Port> _writePorts = new();
    private readonly Dictionary<string, Label> _previewLabels = new();

    public StateBlackboardNode()
    {
        title = "State Blackboard";
    }

    public void Bind(AnalyzedStateModel model)
    {
        _readPorts.Clear();
        _writePorts.Clear();
        _previewLabels.Clear();
        extensionContainer.Clear();

        var scroll = new ScrollView();
        if (model == null || model.keys == null || model.keys.Count == 0)
        {
            scroll.Add(new Label("No state keys detected."));
        }
        else
        {
            foreach (AnalyzedStateKey key in model.keys)
            {
                var row = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 4,
                        marginTop = 2
                    }
                };

                var writePort = CreatePort(Direction.Input);
                writePort.style.marginRight = 6;
                _writePorts[key.keyName] = writePort;
                row.Add(writePort);

                var keyLabel = new Label($"{key.keyName} ({key.kind})")
                {
                    style = { minWidth = 150 }
                };
                row.Add(keyLabel);

                var preview = new Label(string.IsNullOrEmpty(key.lastValuePreview) ? "-" : key.lastValuePreview)
                {
                    style = { flexGrow = 1f }
                };
                _previewLabels[key.keyName] = preview;
                row.Add(preview);

                var readPort = CreatePort(Direction.Output);
                readPort.style.marginLeft = 6;
                _readPorts[key.keyName] = readPort;
                row.Add(readPort);

                scroll.Add(row);
            }
        }

        extensionContainer.Add(scroll);
        RefreshExpandedState();
        RefreshPorts();
    }

    public Port GetReadPort(string keyName) =>
        _readPorts.TryGetValue(keyName, out var port) ? port : null;

    public Port GetWritePort(string keyName) =>
        _writePorts.TryGetValue(keyName, out var port) ? port : null;

    public void UpdatePreview(string keyName, string value)
    {
        if (_previewLabels.TryGetValue(keyName, out var label))
        {
            label.text = string.IsNullOrEmpty(value) ? "-" : value;
        }
    }

    private Port CreatePort(Direction direction)
    {
        var port = InstantiatePort(Orientation.Horizontal, direction, Port.Capacity.Multi, typeof(string));
        port.portName = string.Empty;
        port.pickingMode = PickingMode.Ignore;
        return port;
    }
}

internal class PipelineInputNode : Node
{
    private readonly Dictionary<string, Port> _ports = new();

    public PipelineInputNode()
    {
        title = "Pipeline Input";
    }

    public void Bind(AnalyzedStateModel model)
    {
        _ports.Clear();
        inputContainer.Clear();
        outputContainer.Clear();

        if (model != null && model.keys != null)
        {
            foreach (var key in model.keys.Where(k => k.kind == AnalyzedStateKeyKind.Input))
            {
                var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
                port.portName = key.keyName;
                port.pickingMode = PickingMode.Ignore;
                outputContainer.Add(port);
                _ports[key.keyName] = port;
            }
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    public Port GetPort(string keyName) =>
        _ports.TryGetValue(keyName, out var port) ? port : null;
}

internal class PipelineOutputNode : Node
{
    private readonly Dictionary<string, Port> _ports = new();

    public PipelineOutputNode()
    {
        title = "Pipeline Output";
    }

    public void Bind(AnalyzedStateModel model)
    {
        _ports.Clear();
        inputContainer.Clear();
        outputContainer.Clear();

        if (model != null && model.keys != null)
        {
            foreach (var key in model.keys.Where(k => k.kind == AnalyzedStateKeyKind.Output))
            {
                var port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
                port.portName = key.keyName;
                port.pickingMode = PickingMode.Ignore;
                inputContainer.Add(port);
                _ports[key.keyName] = port;
            }
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    public Port GetPort(string keyName) =>
        _ports.TryGetValue(keyName, out var port) ? port : null;
}
