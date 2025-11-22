using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(OllamaSettings))]
public class OllamaSettingsEditor : Editor
{
    public static bool JsonFieldsEnabled = true;
    public static string JsonFieldsDisabledMessage;

    // Unity serialization depth limit is 10; we keep a stricter ceiling to avoid hitting it.
    private const int MaxNestingDepth = 6;

    private readonly Dictionary<string, ReorderableList> _listCache = new();
    private readonly Dictionary<string, int> _selectionByPath = new();
    private SerializedProperty _modelProp;
    private SerializedProperty _streamProp;
    private SerializedProperty _keepAliveProp;
    private SerializedProperty _systemPromptProp;
    private SerializedProperty _modelParamsProp;
    private SerializedProperty _jsonFieldsProp;

    private void OnEnable()
    {
        _modelProp = serializedObject.FindProperty("model");
        _streamProp = serializedObject.FindProperty("stream");
        _keepAliveProp = serializedObject.FindProperty("keepAlive");
        _systemPromptProp = serializedObject.FindProperty("systemPromptTemplate");
        _modelParamsProp = serializedObject.FindProperty("modelParams");
        _jsonFieldsProp = serializedObject.FindProperty("jsonFields");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Basic Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_modelProp);
        EditorGUILayout.PropertyField(_streamProp);
        EditorGUILayout.PropertyField(_keepAliveProp);
        EditorGUILayout.PropertyField(_systemPromptProp);
        EditorGUILayout.PropertyField(_modelParamsProp, true);

        EditorGUILayout.Space(12f);

        bool schemaChanged = DrawJsonSchemaBuilder();

        serializedObject.ApplyModifiedProperties();

        if (schemaChanged)
        {
            foreach (var targetObject in targets)
            {
                if (targetObject is OllamaSettings settings)
                {
                    settings.RebuildFormatFromFields();
                    EditorUtility.SetDirty(settings);
                    OllamaSettingsChangeNotifier.RaiseChanged(settings);
                }
            }

            Repaint();
        }

        DrawFormatPreview();
    }

    private bool DrawJsonSchemaBuilder()
    {
        bool changed = false;

        EditorGUILayout.LabelField("JSON Output Fields", EditorStyles.boldLabel);
        if (!JsonFieldsEnabled && !string.IsNullOrEmpty(JsonFieldsDisabledMessage))
        {
            EditorGUILayout.HelpBox(JsonFieldsDisabledMessage, MessageType.Info);
        }

        EditorGUILayout.HelpBox(
            "Add the keys you expect from the LLM. The Format field sent to Ollama and the analyzer's produced keys are generated from this list.",
            MessageType.Info
        );

        // Clamp excessive nesting to avoid Unity serialization depth limit errors.
        ClampDepth(_jsonFieldsProp, 0);

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!JsonFieldsEnabled))
        {
            var list = GetOrCreateList(_jsonFieldsProp, "JSON Output Fields");
            list.DoLayoutList();
            changed |= DrawSelectedFieldDetails(_jsonFieldsProp, 0);
        }

        changed |= EditorGUI.EndChangeCheck();

        return changed;
    }

    private void InitializeFieldDefaults(SerializedProperty element)
    {
        element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)).stringValue = "field";
        element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType)).enumValueIndex = (int)JsonFieldType.String;
        element.FindPropertyRelative(nameof(JsonFieldDefinition.arrayElementType)).enumValueIndex = (int)JsonArrayElementType.String;
        element.FindPropertyRelative(nameof(JsonFieldDefinition.minValue)).stringValue = string.Empty;
        element.FindPropertyRelative(nameof(JsonFieldDefinition.maxValue)).stringValue = string.Empty;
        var enums = element.FindPropertyRelative(nameof(JsonFieldDefinition.enumOptions));
        if (enums != null)
        {
            while (enums.arraySize > 0)
            {
                enums.DeleteArrayElementAtIndex(enums.arraySize - 1);
            }
        }
        var children = element.FindPropertyRelative(nameof(JsonFieldDefinition.children));
        if (children != null)
        {
            while (children.arraySize > 0)
            {
                children.DeleteArrayElementAtIndex(children.arraySize - 1);
            }
        }
    }

    private void DrawFormatPreview()
    {
        var settings = (OllamaSettings)target;
        string preview = string.IsNullOrWhiteSpace(settings.format) ? "(no fields defined)" : settings.format;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Generated Format (read-only)", EditorStyles.boldLabel);
        var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Preview Window", GUILayout.Width(160f)))
            {
                OllamaFormatPreviewWindow.Show(preview);
            }
        }

        _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUILayout.MinHeight(60f));
        EditorGUILayout.SelectableLabel(preview, style, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void DrawEnumList(SerializedProperty enumProp)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Enum Options (optional)", EditorStyles.boldLabel);
            for (int i = 0; i < enumProp.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                var element = enumProp.GetArrayElementAtIndex(i);
                element.stringValue = EditorGUILayout.TextField(element.stringValue);
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    enumProp.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Option", GUILayout.Width(120f)))
                {
                    int newIdx = enumProp.arraySize;
                    enumProp.InsertArrayElementAtIndex(newIdx);
                    enumProp.GetArrayElementAtIndex(newIdx).stringValue = string.Empty;
                }
            }
        }
    }

    private Vector2 _previewScroll;

    private ReorderableList GetOrCreateList(SerializedProperty listProp, string header)
    {
        string key = listProp.propertyPath;
        if (_listCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var list = new ReorderableList(listProp.serializedObject, listProp, true, true, true, true);
        list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, header);
        list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = listProp.GetArrayElementAtIndex(index);
            string name = element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)).stringValue;
            var typeProp = element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType));
            string typeName = ((JsonFieldType)typeProp.enumValueIndex).ToString();
            EditorGUI.LabelField(rect, $"{name} ({typeName})");
        };
        list.elementHeight = EditorGUIUtility.singleLineHeight + 4f;
        list.onSelectCallback = l => _selectionByPath[key] = l.index;
        list.onAddCallback = l =>
        {
            int newIndex = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(newIndex);
            InitializeFieldDefaults(listProp.GetArrayElementAtIndex(newIndex));
            _selectionByPath[key] = newIndex;
        };
        list.onRemoveCallback = l =>
        {
            int idx = l.index;
            listProp.DeleteArrayElementAtIndex(idx);
            _selectionByPath[key] = Mathf.Clamp(idx - 1, 0, listProp.arraySize - 1);
        };
        list.onReorderCallback = l =>
        {
            _selectionByPath[key] = Mathf.Clamp(GetSelectionIndex(key), 0, l.count - 1);
        };

        _listCache[key] = list;
        return list;
    }

    private int GetSelectionIndex(string key)
    {
        return _selectionByPath.TryGetValue(key, out var idx) ? idx : -1;
    }

    private bool DrawSelectedFieldDetails(SerializedProperty listProp, int depth)
    {
        bool changed = false;
        if (listProp == null || listProp.arraySize == 0)
        {
            return changed;
        }

        int sel = Mathf.Clamp(GetSelectionIndex(listProp.propertyPath), 0, listProp.arraySize - 1);
        _selectionByPath[listProp.propertyPath] = sel;
        var element = listProp.GetArrayElementAtIndex(sel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(depth == 0 ? "Field Details" : "Child Field Details", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)), new GUIContent("Field Name"));

            var fieldTypeProp = element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType));
            EditorGUILayout.PropertyField(fieldTypeProp, new GUIContent("Type"));

            bool isArray = (JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Array;
            bool isObject = (JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Object;
            bool isNumeric = !isArray && !isObject &&
                             ((JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Number ||
                              (JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Integer);

            if (isArray)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative(nameof(JsonFieldDefinition.arrayElementType)),
                        new GUIContent("Element Type")
                    );
                }
            }

            DrawEnumList(element.FindPropertyRelative(nameof(JsonFieldDefinition.enumOptions)));

            if (isNumeric)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.minValue)), new GUIContent("Min"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.maxValue)), new GUIContent("Max"));
                }
            }

            bool needsChildren = isObject ||
                                 (isArray && (JsonArrayElementType)element.FindPropertyRelative(nameof(JsonFieldDefinition.arrayElementType)).enumValueIndex == JsonArrayElementType.Object);
            if (needsChildren)
            {
                var childrenProp = element.FindPropertyRelative(nameof(JsonFieldDefinition.children));
                if (depth >= MaxNestingDepth)
                {
                    EditorGUILayout.HelpBox($"Reached max nesting depth ({MaxNestingDepth}). Further child fields are not serialized.", MessageType.Warning);
                    changed |= ClampDepth(childrenProp, depth + 1);
                }
                else
                {
                    var childList = GetOrCreateList(childrenProp, "Child Fields");
                    childList.DoLayoutList();
                    changed |= DrawSelectedFieldDetails(childrenProp, depth + 1);
                }
            }
        }

        return changed;
    }

    private bool ClampDepth(SerializedProperty listProp, int depth)
    {
        bool changed = false;
        if (listProp == null)
        {
            return changed;
        }

        if (depth > MaxNestingDepth)
        {
            while (listProp.arraySize > 0)
            {
                listProp.DeleteArrayElementAtIndex(listProp.arraySize - 1);
                changed = true;
            }
            return changed;
        }

        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            var children = element.FindPropertyRelative(nameof(JsonFieldDefinition.children));
            changed |= ClampDepth(children, depth + 1);
        }

        return changed;
    }

    private sealed class OllamaFormatPreviewWindow : EditorWindow
    {
        private string _content;
        private Vector2 _scroll;

        public static void Show(string content)
        {
            var window = CreateInstance<OllamaFormatPreviewWindow>();
            window._content = content ?? string.Empty;
            window.titleContent = new GUIContent("Ollama Format");
            window.minSize = new Vector2(360f, 240f);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Generated Format (read-only)", EditorStyles.boldLabel);
            var style = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.SelectableLabel(_content, style, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }
    }
}
