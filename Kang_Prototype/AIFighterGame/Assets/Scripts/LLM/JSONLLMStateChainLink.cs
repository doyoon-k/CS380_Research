using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// JSONLLMStateChainLink이 호출될 때마다 내부적으로 최대 maxRetries번까지
/// JSON 파싱이 성공할 때까지 LLM 요청을 재시도하도록 동작을 확장한 버전입니다.
/// </summary>
public class JSONLLMStateChainLink : IStateChainLink
{
    private readonly OllamaSettings _settings;
    private readonly int _maxRetries;
    private readonly float _delayBetweenRetries;

    /// <summary>
    /// 기본 생성자: 재시도 횟수와 재시도 간 딜레이를 지정할 수 있습니다.
    /// </summary>
    /// <param name="settings">OllamaSettings</param>
    /// <param name="maxRetries">최대 재시도 횟수 (기본 3회)</param>
    /// <param name="delayBetweenRetries">재시도 전 대기 시간(초, 기본 0.5초)</param>
    public JSONLLMStateChainLink(
        OllamaSettings settings,
        int maxRetries = 3,
        float delayBetweenRetries = 0.1f
    )
    {
        _settings = settings;
        _maxRetries = Mathf.Max(1, maxRetries);
        _delayBetweenRetries = Mathf.Max(0f, delayBetweenRetries);
    }

    public IEnumerator Execute(
        Dictionary<string, string> state,
        Action<Dictionary<string, string>> onDone
    )
    {
        int attempt = 0;
        bool parsedSuccessfully = false;
        JToken root = null;
        string filledPrompt = null;

        while (attempt < _maxRetries && !parsedSuccessfully)
        {
            attempt++;

            // 1) systemPromptTemplate을 state로 치환하여 마지막 렌더링된 프롬프트를 가져옴
            _settings.RenderSystemPrompt(state);
            filledPrompt = _settings.GetLastRenderedPrompt();

            // 2) Debug: 치환된 prompt 출력
            Debug.Log($"[JSONLLMStateChainLink] Attempt {attempt}/{_maxRetries} - Filled Prompt:\n{filledPrompt}");

            // 3) LLM 호출
            string jsonResponse = null;
            OllamaComponent.Instance.GenerateCompletion(
                _settings,
                filledPrompt,
                resp => jsonResponse = resp
            );

            // 응답이 올 때까지 대기
            while (jsonResponse == null)
                yield return null;

            // 3-1) Debug: raw JSON 응답 출력
            Debug.Log($"[JSONLLMStateChainLink] Attempt {attempt}/{_maxRetries} - Raw JSON Response:\n{jsonResponse}");

            // 4) JSON 파싱 시도
            try
            {
                root = JToken.Parse(jsonResponse);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JSONLLMStateChainLink] Attempt {attempt}/{_maxRetries} - JSON 파싱 실패: {e.Message}");
                root = null;
            }

            // 5) 파싱 결과 검사: 루트가 객체인지 확인
            if (root != null && root.Type == JTokenType.Object)
            {
                parsedSuccessfully = true;
            }
            else
            {
                // 파싱이 실패했거나 최상위가 객체가 아니면 재시도
                if (attempt < _maxRetries)
                {
                    Debug.LogWarning($"[JSONLLMStateChainLink] Attempt {attempt}/{_maxRetries} - 최상위 JSON이 객체가 아니거나 파싱 실패. {_delayBetweenRetries}초 후 재시도합니다.");
                    yield return new WaitForSeconds(_delayBetweenRetries);
                }
                else
                {
                    Debug.LogError($"[JSONLLMStateChainLink] 모든 {_maxRetries}회 시도 실패. JSON을 객체로 파싱하지 못했습니다.");
                }
            }
        }

        // 6) 파싱에 성공했다면 state에 병합, 그렇지 않으면 그대로 onDone 호출
        if (parsedSuccessfully && root is JObject obj)
        {
            foreach (var prop in obj)
            {
                state[prop.Key] = prop.Value.ToString();
            }
        }

        // 7) 완료 콜백
        onDone(state);
    }
}
