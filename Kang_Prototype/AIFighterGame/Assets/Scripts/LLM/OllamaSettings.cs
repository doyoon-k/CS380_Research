using System.Collections.Generic;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "OllamaSettings", menuName = "LLM/Ollama Settings")]
public class OllamaSettings : ScriptableObject
{
    [Header("Basic Settings")]
    public string model = "deepseek-r1:7b";
    public string format;
    public bool stream = false;
    public string keepAlive = null;

    [TextArea(3, 20)]
    [Tooltip("System Prompt 템플릿. 변수 치환용 {{varName}} 사용 가능.")]
    public string systemPromptTemplate;

    [Serializable]
    public class ModelParams
    {
        public float temperature = 0.7f;
        public float top_p = 0.9f;
        public float top_k = 40f;
        public int num_predict = 0;
        public float repeat_penalty = 1.1f;
    }

    public ModelParams modelParams = new ModelParams();

    // 치환된 결과를 임시 저장할 프로퍼티 (런타임 중 메모리에만 남음)
    [NonSerialized]
    private string _lastRenderedPrompt;

    /// <summary>
    /// state를 치환해 새로운 문자열을 만들어 두고,
    /// _lastRenderedPrompt에 저장만 한다.
    /// </summary>
    public void RenderSystemPrompt(Dictionary<string, string> state)
    {
        var tmpl = new PromptTemplate(systemPromptTemplate);
        _lastRenderedPrompt = tmpl.Render(state);
    }

    /// <summary>
    /// 마지막으로 치환된 프롬프트를 반환.
    /// GenerateCompletion 호출 시, 이 값을 user/system 메시지로 사용.
    /// </summary>
    public string GetLastRenderedPrompt()
    {
        return _lastRenderedPrompt;
    }
}
