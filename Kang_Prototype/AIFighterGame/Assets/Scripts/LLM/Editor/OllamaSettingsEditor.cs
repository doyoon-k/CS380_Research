using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OllamaSettings))]
public class OllamaSettingsEditor : Editor
{
    public static bool JsonFieldsEnabled = true;
    public static string JsonFieldsDisabledMessage;

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

        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!JsonFieldsEnabled))
        {
            changed |= DrawFieldList(_jsonFieldsProp, 0);
            changed |= DrawAddButton(_jsonFieldsProp, 0, "+ Add Field");
        }

        changed |= EditorGUI.EndChangeCheck();

        return changed;
    }

    private bool DrawFieldList(SerializedProperty listProp, int depth)
    {
        bool changed = false;
        for (int i = 0; i < listProp.arraySize; i++)
        {
            var element = listProp.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)), new GUIContent("Field Name"));
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    listProp.DeleteArrayElementAtIndex(i);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                var fieldTypeProp = element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType));
                EditorGUILayout.PropertyField(fieldTypeProp, new GUIContent("Type"));

                bool isArray = (JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Array;
                bool isObject = (JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Object;

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

                EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.example)), new GUIContent("Example"));
                EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.description)), new GUIContent("Description"));

                bool needsChildren = isObject ||
                                     (isArray && (JsonArrayElementType)element.FindPropertyRelative(nameof(JsonFieldDefinition.arrayElementType)).enumValueIndex == JsonArrayElementType.Object);
                if (needsChildren)
                {
                    var childrenProp = element.FindPropertyRelative(nameof(JsonFieldDefinition.children));
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.LabelField("Child Fields", EditorStyles.boldLabel);
                        changed |= DrawFieldList(childrenProp, depth + 1);
                        changed |= DrawAddButton(childrenProp, depth + 1, "+ Add Child Field");
                    }
                }
            }
        }

        return changed;
    }

    private bool DrawAddButton(SerializedProperty listProp, int depth, string label)
    {
        bool changed = false;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(depth * 12f);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(label, GUILayout.Width(160f)))
            {
                int newIndex = listProp.arraySize;
                listProp.InsertArrayElementAtIndex(newIndex);
                var newElement = listProp.GetArrayElementAtIndex(newIndex);
                InitializeFieldDefaults(newElement);
                changed = true;
            }
        }
        return changed;
    }

    private void InitializeFieldDefaults(SerializedProperty element)
    {
        element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)).stringValue = "field";
        element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType)).enumValueIndex = (int)JsonFieldType.String;
        element.FindPropertyRelative(nameof(JsonFieldDefinition.arrayElementType)).enumValueIndex = (int)JsonArrayElementType.String;
        element.FindPropertyRelative(nameof(JsonFieldDefinition.example)).stringValue = string.Empty;
        element.FindPropertyRelative(nameof(JsonFieldDefinition.description)).stringValue = string.Empty;
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
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextArea(preview, GUILayout.MinHeight(60f));
        }
    }
}
