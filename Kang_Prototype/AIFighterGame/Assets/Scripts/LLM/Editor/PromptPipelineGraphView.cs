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
    private readonly List<StateSnapshotNode> _snapshotNodes = new();
    private readonly List<Edge> _executionEdges = new();
    private readonly List<Edge> _stateEdges = new();
    private PipelineInputNode _inputNode;
    private PipelineOutputNode _outputNode;
    private AnalyzedStateModel _stateModel;
    private readonly Dictionary<int, List<string>> _readsByStep = new();
    private readonly Dictionary<int, List<string>> _writesByStep = new();
    private readonly HashSet<string> _inputKeys = new();
    private bool _pendingStateRefresh;
    private Vector3 _lastViewPosition;
    private Vector3 _lastViewScale = Vector3.one;

    public event Action<AnalyzedStateModel> StateModelChanged;
    public AnalyzedStateModel CurrentStateModel => _stateModel;

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

        OllamaSettingsChangeNotifier.SettingsChanged += OnOllamaSettingsChanged;
        _lastViewPosition = GetCurrentViewPosition();
        _lastViewScale = GetCurrentViewScale();
        schedule.Execute(TrackViewTransform).Every(200);
    }

    public void Dispose()
    {
        OllamaSettingsChangeNotifier.SettingsChanged -= OnOllamaSettingsChanged;
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
            }
        }
    }

    private void Reload()
    {
        bool restoredView = TryRestoreViewTransform();
        ClearGraph();

        if (_asset == null || _asset.steps == null)
        {
            return;
        }

        BuildStepNodes();
        BuildExecutionEdgesFromAsset();
        RefreshStateAnalysis();

        if (!restoredView)
        {
            FrameAll();
            PersistCurrentViewTransform();
        }
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

        foreach (var snap in _snapshotNodes)
        {
            RemoveElement(snap);
        }
        _snapshotNodes.Clear();

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

        foreach (var snap in _snapshotNodes)
        {
            RemoveElement(snap);
        }
        _snapshotNodes.Clear();

        _inputNode = new PipelineInputNode();
        _inputNode.Bind(_stateModel);
        Vector2 inputPosition = GetInputNodePosition();
        _inputNode.SetPosition(new Rect(inputPosition, new Vector2(240, 320)));
        AddElement(_inputNode);

        _outputNode = new PipelineOutputNode();
        _outputNode.Bind(_stateModel?.finalStateKeys);
        Vector2 outputPosition = GetOutputNodePosition();
        _outputNode.SetPosition(new Rect(outputPosition, new Vector2(240, 320)));
        AddElement(_outputNode);

        BuildSnapshotNodes();

        CreateStateEdges();
    }

    private void BuildSnapshotNodes()
    {
        _snapshotNodes.Clear();

        if (_stateModel?.stepStates == null || _stateModel.stepStates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _stepNodes.Count && i < _stateModel.stepStates.Count; i++)
        {
            var stepNode = _stepNodes[i];
            var state = _stateModel.stepStates[i];

            var snapshot = new StateSnapshotNode(state);
            Vector2 position = CalculateSnapshotPosition(i);
            snapshot.SetPosition(new Rect(position, new Vector2(220, 280)));
            AddElement(snapshot);
            _snapshotNodes.Add(snapshot);
        }
    }

    private Vector2 CalculateSnapshotPosition(int index)
    {
        var current = GetStepNode(index);
        var next = GetStepNode(index + 1);
        if (current == null)
        {
            return new Vector2(200f + index * 240f, 400f);
        }

        Vector2 currentPos = current.GetPosition().position;
        if (next != null)
        {
            Vector2 nextPos = next.GetPosition().position;
            return (currentPos + nextPos) * 0.5f + new Vector2(0f, 60f);
        }

        return currentPos + new Vector2(240f, 40f);
    }

    private void CreateStateEdges()
    {
        foreach (var edge in _stateEdges)
        {
            RemoveElement(edge);
        }
        _stateEdges.Clear();

        var allKeyNames = _stateModel?.keys?.Select(k => k.keyName).ToList() ?? new List<string>();

        for (int i = 0; i < _stepNodes.Count; i++)
        {
            var node = _stepNodes[i];
            var reads = GetReads(i);
            var writes = GetWrites(i);

            node.UpdateStateSummary(reads, writes);
            node.UpdateAvailableStateKeys(allKeyNames);

            ConnectExternalInputs(node, reads);
            ConnectSnapshots(i);
        }

        ConnectOutputToFinalSnapshot();
    }

    private void ConnectExternalInputs(PromptPipelineStepNode node, List<string> reads)
    {
        if (_inputNode == null || reads == null || reads.Count == 0)
        {
            return;
        }

        var connectedKeys = new HashSet<string>();
        foreach (string key in reads)
        {
            if (!_inputKeys.Contains(key) || !connectedKeys.Add(key))
            {
                continue;
            }

            var inputPort = _inputNode.GetPort(key);
            TryConnectStateEdge(inputPort, node.StateInPort);
        }
    }

    private void ConnectSnapshots(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= _snapshotNodes.Count)
        {
            return;
        }

        var stepNode = GetStepNode(stepIndex);
        var snapshot = _snapshotNodes[stepIndex];
        TryConnectStateEdge(stepNode?.StateOutPort, snapshot.StateInPort);

        if (stepIndex + 1 < _stepNodes.Count)
        {
            var nextStep = GetStepNode(stepIndex + 1);
            TryConnectStateEdge(snapshot.StateOutPort, nextStep?.StateInPort);
        }
    }

    private void ConnectOutputToFinalSnapshot()
    {
        if (_outputNode == null)
        {
            return;
        }

        var finalSnapshot = _snapshotNodes.LastOrDefault();
        if (finalSnapshot == null)
        {
            return;
        }

        var finalKeys = _stateModel?.finalStateKeys ?? new List<string>();
        foreach (string key in finalKeys)
        {
            var port = _outputNode.GetPort(key);
            TryConnectStateEdge(finalSnapshot.StateOutPort, port);
        }
    }

    private PromptPipelineStepNode GetStepNode(int index) =>
        index >= 0 && index < _stepNodes.Count ? _stepNodes[index] : null;

    private void TryConnectStateEdge(Port from, Port to)
    {
        if (from == null || to == null)
        {
            return;
        }

        var edge = from.ConnectTo(to);
        ConfigureStateEdge(edge);
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

        bool movedUtilityNodes = false;
        if (change.movedElements != null)
        {
            foreach (var element in change.movedElements)
            {
                if (element is PromptPipelineStepNode node)
                {
                    node.PersistPosition();
                    movedSteps = true;
                }
                else if (element is PipelineInputNode || element is PipelineOutputNode)
                {
                    movedUtilityNodes = true;
                }
            }
        }

        if (movedSteps)
        {
            MarkAssetDirty();
        }

        if (movedUtilityNodes)
        {
            PersistUtilityNodePositions();
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

    private void OnOllamaSettingsChanged(OllamaSettings settings)
    {
        if (settings == null || _asset?.steps == null)
        {
            return;
        }

        foreach (var step in _asset.steps)
        {
            if (step?.ollamaSettings == settings)
            {
                RefreshStateAnalysis();
                break;
            }
        }
    }

    private void TrackViewTransform()
    {
        if (_asset?.layoutSettings == null)
        {
            return;
        }

        Vector3 position = GetCurrentViewPosition();
        Vector3 scale = GetCurrentViewScale();

        if (HasSignificantDifference(position, _lastViewPosition) ||
            HasSignificantDifference(scale, _lastViewScale))
        {
            _lastViewPosition = position;
            _lastViewScale = scale;
            _asset.layoutSettings.viewPosition = position;
            _asset.layoutSettings.viewScale = scale;
            _asset.layoutSettings.viewInitialized = true;
            MarkAssetDirty();
        }
    }

    private bool TryRestoreViewTransform()
    {
        if (_asset?.layoutSettings == null || !_asset.layoutSettings.viewInitialized)
        {
            return false;
        }

        UpdateViewTransform(_asset.layoutSettings.viewPosition, _asset.layoutSettings.viewScale);
        _lastViewPosition = _asset.layoutSettings.viewPosition;
        _lastViewScale = _asset.layoutSettings.viewScale;
        return true;
    }

    private void PersistCurrentViewTransform()
    {
        _lastViewPosition = GetCurrentViewPosition();
        _lastViewScale = GetCurrentViewScale();

        if (_asset?.layoutSettings == null)
        {
            return;
        }

        _asset.layoutSettings.viewPosition = _lastViewPosition;
        _asset.layoutSettings.viewScale = _lastViewScale;
        _asset.layoutSettings.viewInitialized = true;
        MarkAssetDirty();
    }

    private static bool HasSignificantDifference(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude > 0.0001f;
    }

    private Vector3 GetCurrentViewPosition()
    {
        var translate = contentViewContainer.resolvedStyle.translate;
        return new Vector3(
            translate.x,
            translate.y,
            translate.z
        );
    }

    private Vector3 GetCurrentViewScale()
    {
        var scale = contentViewContainer.resolvedStyle.scale;
        return scale.value;
    }

    private Vector2 GetInputNodePosition()
    {
        if (_asset?.layoutSettings == null)
        {
            return new Vector2(-600f, 80f);
        }

        if (!_asset.layoutSettings.inputPositionInitialized)
        {
            _asset.layoutSettings.inputNodePosition = new Vector2(-600f, 80f);
            _asset.layoutSettings.inputPositionInitialized = true;
            MarkAssetDirty();
        }

        return _asset.layoutSettings.inputNodePosition;
    }

    private Vector2 GetOutputNodePosition()
    {
        if (_asset?.layoutSettings == null)
        {
            return CalculateDefaultOutputNodePosition();
        }

        if (!_asset.layoutSettings.outputPositionInitialized)
        {
            _asset.layoutSettings.outputNodePosition = CalculateDefaultOutputNodePosition();
            _asset.layoutSettings.outputPositionInitialized = true;
            MarkAssetDirty();
        }

        return _asset.layoutSettings.outputNodePosition;
    }

    private Vector2 CalculateDefaultOutputNodePosition()
    {
        float x = 320f * Mathf.Max(1, _stepNodes.Count + 1);
        return new Vector2(x, 80f);
    }

    private void PersistUtilityNodePositions()
    {
        if (_asset?.layoutSettings == null)
        {
            return;
        }

        if (_inputNode != null)
        {
            _asset.layoutSettings.inputNodePosition = _inputNode.GetPosition().position;
            _asset.layoutSettings.inputPositionInitialized = true;
        }

        if (_outputNode != null)
        {
            _asset.layoutSettings.outputNodePosition = _outputNode.GetPosition().position;
            _asset.layoutSettings.outputPositionInitialized = true;
        }

        MarkAssetDirty();
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
    private readonly Foldout _settingsFoldout;
    private readonly IMGUIContainer _settingsInspector;
    private UnityEditor.Editor _settingsEditor;
    private bool _lastExpandedState;
    private IVisualElementScheduledItem _expandedMonitor;
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
            UpdateSettingsInspector();
        });
        extensionContainer.Add(_settingsField);

        _settingsFoldout = new Foldout
        {
            text = "Inline Ollama Settings",
            value = false
        };
        _settingsFoldout.RegisterValueChangedCallback(evt =>
        {
            if (evt.newValue)
            {
                BringNodeToFront();
            }
        });
        _settingsInspector = new IMGUIContainer(DrawSettingsInspector)
        {
            style =
            {
                marginLeft = 4,
                marginBottom = 4
            }
        };
        _settingsFoldout.Add(_settingsInspector);
        extensionContainer.Add(_settingsFoldout);

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
        UpdateSettingsInspector();
        RefreshExpandedState();
        if (expanded)
        {
            BringNodeToFront();
        }
        RegisterCallback<DetachFromPanelEvent>(_ =>
        {
            DisposeSettingsEditor();
            _expandedMonitor?.Pause();
        });
        RegisterCallback<AttachToPanelEvent>(_ =>
        {
            _expandedMonitor?.Resume();
            _lastExpandedState = expanded;
        });
        ApplyBackgroundStyles();
        _lastExpandedState = expanded;
        _expandedMonitor = schedule.Execute(MonitorExpandedState).Every(200);
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

    private void UpdateSettingsInspector()
    {
        if (_settingsFoldout == null || _settingsInspector == null)
        {
            return;
        }

        var target = Step.ollamaSettings;
        if (target == null)
        {
            _settingsFoldout.style.display = DisplayStyle.None;
            DisposeSettingsEditor();
            return;
        }

        _settingsFoldout.style.display = DisplayStyle.Flex;
        UnityEditor.Editor.CreateCachedEditor(target, null, ref _settingsEditor);
        _settingsInspector.MarkDirtyRepaint();
    }

    private void DrawSettingsInspector()
    {
        if (_settingsEditor == null)
        {
            EditorGUILayout.HelpBox("Assign OllamaSettings to edit inline.", MessageType.Info);
            return;
        }

        EditorGUI.BeginChangeCheck();
        _settingsEditor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            var settings = Step.ollamaSettings;
            if (settings != null)
            {
                Undo.RecordObject(settings, "Edit Ollama Settings");
                EditorUtility.SetDirty(settings);
                OllamaSettingsChangeNotifier.RaiseChanged(settings);
            }
        }
    }

    private void DisposeSettingsEditor()
    {
        if (_settingsEditor != null)
        {
            UnityEngine.Object.DestroyImmediate(_settingsEditor);
            _settingsEditor = null;
        }
    }

    private void ApplyBackgroundStyles()
    {
        var panelColor = new Color(0.11f, 0.11f, 0.11f, 0.98f);
        var bodyColor = new Color(0.16f, 0.16f, 0.16f, 0.98f);
        style.backgroundColor = new StyleColor(panelColor);
        style.opacity = 1f;
        mainContainer.style.backgroundColor = new StyleColor(panelColor);
        mainContainer.style.opacity = 1f;
        extensionContainer.style.backgroundColor = new StyleColor(bodyColor);
        extensionContainer.style.opacity = 1f;
        extensionContainer.style.paddingLeft = 6;
        extensionContainer.style.paddingRight = 6;
        extensionContainer.style.paddingBottom = 6;
    }

    private void BringNodeToFront()
    {
        BringToFront();
        parent?.BringToFront();
    }

    private void MonitorExpandedState()
    {
        if (expanded != _lastExpandedState)
        {
            if (expanded)
            {
                BringNodeToFront();
            }
            _lastExpandedState = expanded;
        }
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

internal class PipelineInputNode : Node
{
    private readonly Dictionary<string, Port> _ports = new();

    public PipelineInputNode()
    {
        title = "Pipeline Input";
        capabilities |= Capabilities.Movable;
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
        capabilities |= Capabilities.Movable;
    }

    public void Bind(IEnumerable<string> outputKeys)
    {
        _ports.Clear();
        inputContainer.Clear();
        outputContainer.Clear();

        if (outputKeys != null)
        {
            foreach (string keyName in outputKeys)
            {
                var port = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
                port.portName = keyName;
                port.pickingMode = PickingMode.Ignore;
                inputContainer.Add(port);
                _ports[keyName] = port;
            }
        }

        RefreshExpandedState();
        RefreshPorts();
    }

    public Port GetPort(string keyName) =>
        _ports.TryGetValue(keyName, out var port) ? port : null;
}

internal class StateSnapshotNode : Node
{
    public Port StateInPort { get; }
    public Port StateOutPort { get; }

    public StateSnapshotNode(AnalyzedStepState state)
    {
        int displayIndex = state != null ? state.stepIndex + 1 : 0;
        title = $"State after Step {displayIndex}";
        capabilities &= ~Capabilities.Deletable;
        capabilities |= Capabilities.Movable | Capabilities.Selectable | Capabilities.Copiable;

        StateInPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(string));
        StateInPort.portName = "State In";
        StateInPort.pickingMode = PickingMode.Ignore;
        inputContainer.Add(StateInPort);

        StateOutPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(string));
        StateOutPort.portName = "State Out";
        StateOutPort.pickingMode = PickingMode.Ignore;
        outputContainer.Add(StateOutPort);

        BuildBody(state);
        RefreshExpandedState();
        RefreshPorts();
    }

    private void BuildBody(AnalyzedStepState state)
    {
        extensionContainer.Clear();
        var scroll = new ScrollView();
        scroll.style.maxHeight = 200f;

        if (state?.stateKeys == null || state.stateKeys.Count == 0)
        {
            scroll.Add(new Label("No state keys detected."));
        }
        else
        {
            var newSet = new HashSet<string>(state.newKeys ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            foreach (string key in state.stateKeys)
            {
                var label = new Label(key);
                if (newSet.Contains(key))
                {
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                scroll.Add(label);
            }
        }

        extensionContainer.Add(scroll);
    }
}
