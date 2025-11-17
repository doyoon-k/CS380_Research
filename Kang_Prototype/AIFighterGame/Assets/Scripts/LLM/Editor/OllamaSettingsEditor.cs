using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OllamaSettings))]
public class OllamaSettingsEditor : Editor
{
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
        EditorGUILayout.HelpBox(
            "Add the keys you expect from the LLM. The Format field sent to Ollama and the analyzer's produced keys are generated from this list.",
            MessageType.Info
        );

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < _jsonFieldsProp.arraySize; i++)
        {
            var element = _jsonFieldsProp.GetArrayElementAtIndex(i);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)), new GUIContent("Field Name"));
                if (GUILayout.Button("X", GUILayout.Width(24f)))
                {
                    _jsonFieldsProp.DeleteArrayElementAtIndex(i);
                    changed = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }
                EditorGUILayout.EndHorizontal();

                var fieldTypeProp = element.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType));
                EditorGUILayout.PropertyField(fieldTypeProp, new GUIContent("Type"));

                if ((JsonFieldType)fieldTypeProp.enumValueIndex == JsonFieldType.Array)
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
            }
        }

        changed |= EditorGUI.EndChangeCheck();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Field", GUILayout.Width(140f)))
            {
                int newIndex = _jsonFieldsProp.arraySize;
                _jsonFieldsProp.InsertArrayElementAtIndex(newIndex);
                var newElement = _jsonFieldsProp.GetArrayElementAtIndex(newIndex);
                newElement.FindPropertyRelative(nameof(JsonFieldDefinition.fieldName)).stringValue = "field";
                newElement.FindPropertyRelative(nameof(JsonFieldDefinition.fieldType)).enumValueIndex = (int)JsonFieldType.String;
                newElement.FindPropertyRelative(nameof(JsonFieldDefinition.example)).stringValue = string.Empty;
                newElement.FindPropertyRelative(nameof(JsonFieldDefinition.description)).stringValue = string.Empty;
                changed = true;
            }
        }

        return changed;
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
