# LLM 패키지 가이드

## 1. 개요
Assets/Scripts/LLM 폴더는 로컬에 띄운 Ollama 서버와 통신하여 텍스트 생성, 대화, 임베딩 추출을 처리하는 Unity 래퍼입니다. OllamaComponent가 HTTP 요청·응답을 관리하고, OllamaSettings가 모델/프롬프트/샘플링 파라미터를 묶어 씬 전반에서 재사용할 수 있게 해 줍니다. 여기에 StateSequentialChainExecutor와 JSONLLMStateChainLink를 더하면 LLM이 반환한 JSON 상태를 안전하게 재시도·병합하는 파이프라인을 구성할 수 있습니다.

## 2. 주요 스크립트
|경로|설명|
|---|---|
|Assets/Scripts/LLM/OllamaComponent.cs|싱글톤 MonoBehaviour. /api/generate, /api/chat, /api/embed 요청을 만들고, 응답 스트리밍과 로깅을 처리합니다.|
|Assets/Scripts/LLM/OllamaSettings.cs|ScriptableObject. 모델명, 시스템 프롬프트 템플릿, 샘플링 파라미터(ModelParams)를 에셋으로 보관합니다.|
|Assets/Scripts/LLM/OllamaAutoLoader.cs|모델별로 ollama serve 프로세스를 자동 기동하고 포트를 할당합니다. Unity 종료 시 모든 서버를 정리합니다.|
|Assets/Scripts/LLM/ProcessWrapper.cs|Windows Job Object를 이용해 Ollama 하위 프로세스를 부모가 죽으면 함께 종료시키는 헬퍼입니다.|
|Assets/Scripts/LLM/PromptTemplate.cs|{{varName}} 치환을 수행하여 상태 값을 시스템 프롬프트에 삽입합니다.|
|Assets/Scripts/LLM/IStateChainLink.cs|상태 딕셔너리를 입력/출력으로 사용하는 체인 인터페이스입니다.|
|Assets/Scripts/LLM/StateSequentialChainExecutor.cs|여러 IStateChainLink를 코루틴으로 순차 실행해 상태를 흘려보냅니다.|
|Assets/Scripts/LLM/JSONLLMStateChainLink.cs|LLM 응답을 JSON으로 파싱하여 상태에 병합합니다. 파싱 실패 시 재시도와 딜레이를 지원합니다.|
|Assets/Scripts/LLM/demo.cs|OllamaComponent API 3종(Generate/Chat/Embed)을 호출하는 기본 예제가 주석으로 포함되어 있습니다.|

## 3. 동작 흐름
1. 게임 플레이 코드가 OllamaAutoLoader.GetServerAddress(model)을 호출하면 해당 모델 전용 ollama serve 프로세스가 자동 구동되고 고유 포트를 부여받습니다.
2. OllamaSettings 에셋은 시스템 프롬프트 템플릿과 모델 파라미터를 갖고 있으며, 필요 시 RenderSystemPrompt가 상태 값을 템플릿에 치환합니다.
3. 씬 어딘가에 존재하는 OllamaComponent가 GenerateCompletion, ChatCompletion, Embed 중 하나를 호출받아 HTTP 요청 본문을 조립합니다.
4. UnityWebRequest가 Ollama REST 엔드포인트(/api/generate, /api/chat, /api/embed)에 POST를 보낸 뒤, 응답을 줄 단위 혹은 통째로 콜백에 전달합니다.
5. StateSequentialChainExecutor와 JSONLLMStateChainLink를 활용하면 JSON 응답을 상태 딕셔너리에 병합하면서 체이닝할 수 있습니다.

## 4. 설치 및 준비

### 4.1 Ollama 사전 준비
- 개발 PC에 Ollama를 설치하고 ollama pull deepseek-r1:7b처럼 필요한 모델을 내려받습니다.
- GPU 레이어 수 등 공통 환경 변수는 OllamaAutoLoader가 자동 지정(OLLAMA_NUM_GPU_LAYERS=100, OLLAMA_KEEP_ALIVE=-1)하므로 별도 설정이 필요 없습니다.
- Ollama 서버는 런타임/에디터 모두에서 자동 관리되지만, 수동으로 종료해야 할 경우 Unity 메뉴/에디터 종료만으로도 정리됩니다.

### 4.2 Unity 에디터 설정
1. 프로젝트 뷰에서 Create > LLM > Ollama Settings를 선택해 설정 에셋을 생성합니다.
2. 모델명(model), 응답 형식(ormat, 필요 시 JSON 스키마 문자열), 스트리밍 여부(stream), 샘플링 파라미터(ModelParams)를 지정합니다.
3. 시스템 프롬프트 템플릿에 {{playerName}}, {{planet}}처럼 상태 키를 넣어두면 체인에서 자동 치환됩니다.
4. 씬에 빈 GameObject를 만들고 OllamaComponent를 붙여 싱글톤 인스턴스를 확보합니다. 디버깅이 필요하면 logLLMTraffic을 켜서 요청/응답을 콘솔에 기록합니다.
5. LLM을 호출할 스크립트(MonoBehaviour)에서 public OllamaComponent ollama;와 public OllamaSettings settings;를 노출한 뒤, 인스펙터에서 레퍼런스를 연결합니다.

## 5. API 사용법

### 5.1 GenerateCompletion (단일 프롬프트)
- OllamaSettings.systemPromptTemplate의 원본 문자열을 그대로 system prompt로 사용합니다.
- 사용자 프롬프트 문자열과 응답 콜백만 넘기면 됩니다.

`csharp
public class NewsHeadlineGenerator : MonoBehaviour
{
    public OllamaComponent ollama;
    public OllamaSettings settings;

    void Start()
    {
        ollama.GenerateCompletion(
            settings,
            "List two trade routes worth scouting this week.",
            response => Debug.Log($"Headline:\n{response}")
        );
    }
}
`

### 5.2 GenerateCompletionWithState (상태 치환)
- Dictionary<string, string> 상태를 넘기면 RenderSystemPrompt → _lastRenderedPrompt 순서로 치환된 system prompt가 사용됩니다.

`csharp
var state = new Dictionary<string, string>
{
    { "playerName", "Astra" },
    { "planet", "Sagan-3" }
};

ollama.GenerateCompletionWithState(
    settings,
    "Give me a concise recon briefing.",
    state,
    response => Debug.Log(response)
);
`

### 5.3 ChatCompletion (멀티 메시지)
- ChatMessage 배열에 system/user/assistant 역할을 명시합니다.
- settings.stream이 	rue면 줄 단위로 콜백됩니다.

`csharp
var messages = new[]
{
    new ChatMessage { role = "system", content = "You are a diplomatic aide." },
    new ChatMessage { role = "user", content = "How should I negotiate with the Heliox guild?" }
};

ollama.ChatCompletion(settings, messages, chunk =>
{
    Debug.Log("Assistant:\n" + chunk);
});
`

### 5.4 Embed (임베딩 추출)
- 문자열 배열을 넘기면 /api/embed 응답을 loat[][]로 파싱하여 콜백에 전달합니다.

`csharp
var texts = new[] { "Helium futures", "Quantum fuel" };
ollama.Embed(settings, texts, vectors =>
{
    Debug.Log($"Received {vectors.Length} embedding(s). First length = {vectors[0].Length}");
});
`

## 6. 상태 체인 활용

StateSequentialChainExecutor는 코루틴으로 작동하므로 호출측에서도 StartCoroutine으로 실행해야 합니다.

`csharp
public class QuestBriefingChain : MonoBehaviour
{
    public OllamaSettings settings;

    private IEnumerator Start()
    {
        var executor = new StateSequentialChainExecutor();
        executor.AddLink(new JSONLLMStateChainLink(settings, maxRetries: 3, delayBetweenRetries: 0.2f));

        var initial = new Dictionary<string, string>
        {
            { "playerName", "Astra" },
            { "sector", "Gamma-12" }
        };

        yield return StartCoroutine(executor.Execute(initial, state =>
        {
            Debug.Log($"미션 요약: {state.GetValueOrDefault("mission_summary")}");
            Debug.Log($"위협도: {state.GetValueOrDefault("threat_level")}");
        }));
    }
}
`

- JSONLLMStateChainLink는 JToken.Parse에 실패하거나 객체 타입이 아닐 때 _maxRetries만큼 재시도합니다.
- 체인 링크를 더 추가해 멀티스텝 워크플로우(예: 요약 → 임무 생성 → 대사 생성)를 구성할 수 있습니다.

## 7. 디버깅 & 트러블슈팅
- **요청 로깅**: OllamaComponent.logLLMTraffic을 켜면 system/user prompt와 HTTP Request/Response 원문을 모두 확인할 수 있습니다.
- **JSON 파싱 실패**: 모델이 JSON을 잘못 반환하면 경고와 함께 재시도하므로, 프롬프트에 "반드시 유효한 JSON 객체로만 답하라" 같은 지시를 넣어 안정성을 높입니다.
- **서버 충돌**: 모델별 Ollama 프로세스가 응답하지 않으면 OllamaAutoLoader.StopAllServers()를 호출하거나 Unity를 재시작해 프로세스를 재기동하세요.
- **네트워크/리소스**: Ollama가 GPU를 사용할 수 없을 때는 OLLAMA_NUM_GPU_LAYERS 값을 수정하거나 모델을 더 작은 버전으로 교체하세요.
- **스트리밍 파싱**: settings.stream == true일 때는 응답이 줄 단위로 들어오므로 UI에 누적하거나 마지막 줄에서 마무리 로직을 수행해야 합니다.

## 8. 확장 아이디어
- IStateChainLink를 상속해 커스텀 링크(예: 벡터 임베딩 생성 후 DB 저장, 함수 호출 결과를 상태에 주입)를 추가합니다.
- PromptTemplate를 대체/확장하여 조건부 블록, 반복 구문 등 더 풍부한 템플릿 기능을 구현할 수 있습니다.
- OllamaSettings 에셋을 여러 개 만들어 모델별 또는 용도별(서사, 경제, 전투) 파라미터를 분리합니다.
- 임베딩 API를 Vector DB(예: SQLite + cosine)와 결합하면 게임 내 지식 베이스 검색이나 NPC 기억 시스템을 구축할 수 있습니다.
