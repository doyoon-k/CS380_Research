# LLM 패키지 가이드

## 1. 개요
Assets/Scripts/LLM 폴더는 로컬에 띄운 Ollama 서버와 통신하여 텍스트 생성, 대화, 임베딩 추출을 처리하는 Unity 래퍼입니다. 모든 네트워크 호출은 `IOllamaService` 인터페이스로 노출되며, 런타임에서는 `OllamaComponent`, 에디터 도구에서는 `OllamaEditorService`가 동일한 `OllamaHttpWorker`를 공유합니다. `OllamaServiceLocator`가 활성 서비스를 제공하고, `OllamaAutoLoader`가 모델별 ollama serve 프로세스를 관리합니다. `StateSequentialChainExecutor`와 `JSONLLMStateChainLink`/`CompletionChainLink`를 조합하면 JSON 상태 병합과 단순 답변 누적을 안전하게 재시도하면서 체인 형태로 묶을 수 있습니다.

## 2. 주요 스크립트
|경로|설명|
|---|---|
|Assets/Scripts/LLM/IOllamaService.cs|런타임/에디터가 공유하는 Ollama 호출 인터페이스. 모든 메서드는 IEnumerator를 반환합니다.|
|Assets/Scripts/LLM/OllamaServiceLocator.cs|활성 `IOllamaService` 등록·조회 헬퍼. 체인 링크들이 여기서 서비스를 가져옵니다.|
|Assets/Scripts/LLM/OllamaComponent.cs|싱글톤 MonoBehaviour. `IOllamaService`를 구현해 HTTP 호출을 구동하고 서비스 로케이터에 자신을 등록합니다.|
|Assets/Scripts/LLM/OllamaHttpWorker.cs|순수 C# HTTP 워커. 런타임 컴포넌트와 에디터 서비스가 공통으로 사용합니다.|
|Assets/Scripts/LLM/OllamaSettings.cs|ScriptableObject. 모델명, 시스템 프롬프트 템플릿, 샘플링 파라미터(ModelParams), JSON Output Fields를 에셋으로 관리합니다.|
|Assets/Scripts/LLM/OllamaAutoLoader.cs|모델별로 ollama serve 프로세스를 자동 기동·종료하며 포트를 할당합니다.|
|Assets/Scripts/LLM/ProcessWrapper.cs|Windows Job Object로 Ollama 하위 프로세스를 부모와 함께 정리합니다.|
|Assets/Scripts/LLM/PromptTemplate.cs|{{varName}} 치환을 수행하여 상태 값을 프롬프트에 삽입합니다.|
|Assets/Scripts/LLM/IStateChainLink.cs|상태 딕셔너리를 입력/출력으로 사용하는 체인 인터페이스입니다.|
|Assets/Scripts/LLM/StateSequentialChainExecutor.cs|여러 IStateChainLink를 코루틴으로 순차 실행해 상태를 전달합니다.|
|Assets/Scripts/LLM/JSONLLMStateChainLink.cs|LLM 응답을 JSON으로 파싱해 상태에 병합하고 실패 시 재시도합니다.|
|Assets/Scripts/LLM/CompletionChainLink.cs|텍스트 완성 호출을 수행해 결과를 `PromptPipelineConstants.AnswerKey`(기본: `response`)에 저장합니다.|
|Assets/Scripts/LLM/PromptPipelineAsset.cs|그래프 에디터가 사용하는 선형 파이프라인 정의(StepKind: Json, Completion, Custom) ScriptableObject입니다.|
|Assets/Scripts/LLM/PromptPipelineConstants.cs|파이프라인에서 예약한 상태 키 모음. 현재는 AnswerKey(`response`)를 제공합니다.|
|Assets/Scripts/LLM/demo.cs|OllamaComponent API 3종(Generate/Chat/Embed) 호출 예제가 주석으로 포함되어 있습니다.|

## 3. 동작 흐름
1. 런타임의 OllamaComponent가 Awake에서 `OllamaServiceLocator`에 자신을 등록하고 내부 `OllamaHttpWorker`를 준비합니다. 에디터 그래프/시뮬레이터는 기본적으로 `OllamaEditorService`를 사용해 동일한 워커를 호출합니다.
2. LLM 호출 시 OllamaAutoLoader.GetServerAddress(model)이 실행되어 해당 모델 전용 ollama serve 프로세스를 자동 기동하고 포트를 할당합니다.
3. OllamaSettings 에셋은 시스템 프롬프트 템플릿과 모델 파라미터, JSON Output Fields를 보관하며 RenderSystemPrompt로 상태 치환을 수행합니다.
4. 게임/툴 코드는 OllamaComponent 인스턴스나 `OllamaServiceLocator.Current`(또는 주입한 IOllamaService)를 통해 GenerateCompletion, ChatCompletion, Embed를 요청합니다.
5. OllamaHttpWorker가 /api/generate, /api/chat, /api/embed 요청을 만들고, 스트리밍 여부에 따라 줄 단위 또는 전체 응답을 콜백에 전달합니다.
6. StateSequentialChainExecutor가 JSONLLMStateChainLink/CompletionChainLink를 순차 실행해 JSON 필드는 상태에 병합하고, 텍스트 응답은 `response` 키에 누적합니다.

## 4. 설치 및 준비

### 4.1 Ollama 사전 준비
- 개발 PC에 Ollama를 설치하고 `ollama pull deepseek-r1:7b`처럼 필요한 모델을 내려받습니다.
- GPU 레이어 수 등 공통 환경 변수는 OllamaAutoLoader가 자동 지정(OLLAMA_NUM_GPU_LAYERS=100, OLLAMA_KEEP_ALIVE=-1)하므로 별도 설정이 필요 없습니다.
- Ollama 서버는 런타임/에디터 모두에서 자동 관리되지만, 수동 종료가 필요하면 Unity 종료만으로도 정리됩니다.

### 4.2 Unity 에디터 설정
1. 프로젝트 뷰에서 Create > LLM > Ollama Settings를 선택해 설정 에셋을 생성합니다.
2. 모델명(model), 응답 형식(format, 필요 시 JSON 스키마 문자열), 스트리밍 여부(stream), 샘플링 파라미터(ModelParams)를 지정합니다.
3. 시스템 프롬프트 템플릿에 {{playerName}}, {{planet}}처럼 상태 키를 넣어두면 체인에서 자동 치환됩니다.
4. 씬에 빈 GameObject를 만들고 OllamaComponent를 붙여 싱글톤 인스턴스를 확보합니다. 디버깅이 필요하면 logLLMTraffic을 켜서 요청/응답을 콘솔에 기록합니다.
5. LLM을 호출할 스크립트(MonoBehaviour)에서 `public OllamaComponent ollama;`와 `public OllamaSettings settings;`를 노출한 뒤, 인스펙터에서 레퍼런스를 연결합니다.

## 5. API 사용법
아래 예제는 OllamaComponent를 직접 호출하지만 동일한 시그니처의 `IOllamaService`(예: `OllamaServiceLocator.Current`, `OllamaEditorService`)에도 그대로 적용됩니다.

### 5.1 GenerateCompletion (단일 프롬프트)
- OllamaSettings.systemPromptTemplate의 원본 문자열을 그대로 system prompt로 사용합니다.
- 사용자 프롬프트 문자열과 응답 콜백만 넘기면 됩니다.

```csharp
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
```

### 5.2 GenerateCompletionWithState (상태 치환)
- Dictionary<string, string> 상태를 넘기면 RenderSystemPrompt → _lastRenderedPrompt 순서로 치환된 system prompt가 사용됩니다.

```csharp
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
```

### 5.3 ChatCompletion (멀티 메시지)
- ChatMessage 배열에 system/user/assistant 역할을 명시합니다.
- settings.stream이 true면 줄 단위로 콜백됩니다.

```csharp
var messages = new[]
{
    new ChatMessage { role = "system", content = "You are a diplomatic aide." },
    new ChatMessage { role = "user", content = "How should I negotiate with the Heliox guild?" }
};

ollama.ChatCompletion(settings, messages, chunk =>
{
    Debug.Log("Assistant:\n" + chunk);
});
```

### 5.4 Embed (임베딩 추출)
- 문자열 배열을 넘기면 /api/embed 응답을 float[][]로 파싱하여 콜백에 전달합니다.

```csharp
var texts = new[] { "Helium futures", "Quantum fuel" };
ollama.Embed(settings, texts, vectors =>
{
    Debug.Log($"Received {vectors.Length} embedding(s). First length = {vectors[0].Length}");
});
```

## 6. 상태 체인 활용
StateSequentialChainExecutor는 코루틴으로 작동하므로 호출측에서도 StartCoroutine으로 실행해야 합니다. JSON 파싱과 일반 답변 저장을 함께 엮는 예시는 아래와 같습니다.

```csharp
public class QuestBriefingChain : MonoBehaviour
{
    public OllamaSettings settings;

    private IEnumerator Start()
    {
        var executor = new StateSequentialChainExecutor();
        executor.AddLink(new JSONLLMStateChainLink(
            settings,
            "Return { \"mission_summary\": \"...\", \"threat_level\": \"...\" }",
            maxRetries: 3,
            delayBetweenRetries: 0.2f));
        executor.AddLink(new CompletionChainLink(
            settings,
            "Write a radio message about {{mission_summary}} (threat: {{threat_level}})."));

        var initial = new Dictionary<string, string>
        {
            { "playerName", "Astra" },
            { "sector", "Gamma-12" }
        };

        yield return StartCoroutine(executor.Execute(initial, state =>
        {
            Debug.Log($"미션 요약: {state.GetValueOrDefault("mission_summary")}");
            Debug.Log($"위협도: {state.GetValueOrDefault("threat_level")}");
            Debug.Log($"최종 답변: {state.GetValueOrDefault(PromptPipelineConstants.AnswerKey)}");
        }));
    }
}
```

- JSONLLMStateChainLink는 정의된 JSON Output Fields를 기준으로 응답을 파싱하며, 실패 시 설정한 최대 횟수만큼 재시도합니다.
- CompletionChainLink는 한 번의 텍스트 완성 결과를 `PromptPipelineConstants.AnswerKey`(response)로 저장해 이후 단계나 출력에서 일관되게 접근할 수 있게 합니다.
- 두 링크 모두 현재 등록된 `IOllamaService`(OllamaComponent 또는 명시적으로 전달한 서비스)를 사용하므로, 서비스가 등록되지 않았다면 예외가 발생합니다.

### 6.1 PromptPipelineAsset 런타임 실행
에디터에서 만든 PromptPipelineAsset은 `BuildExecutor(IOllamaService service)`로 바로 실행 준비를 할 수 있습니다. 씬에 OllamaComponent만 배치해 두면 됩니다.

```csharp
public class MissionPipelineRunner : MonoBehaviour
{
    public PromptPipelineAsset pipeline;
    public OllamaComponent ollama;

    private IEnumerator Start()
    {
        ollama ??= OllamaComponent.Instance;
        if (pipeline == null || ollama == null) yield break;

        var executor = pipeline.BuildExecutor(ollama);
        var state = new Dictionary<string, string>
        {
            { "playerName", "Astra" },
            { "sector", "Gamma-12" }
        };

        yield return StartCoroutine(executor.Execute(state, final =>
        {
            Debug.Log($"최종 답변: {final.GetValueOrDefault(PromptPipelineConstants.AnswerKey)}");
        }));
    }
}
```

## 7. 디버깅 & 트러블슈팅
- **요청 로깅**: OllamaComponent.logLLMTraffic을 켜면 system/user prompt와 HTTP Request/Response 원문을 모두 확인할 수 있습니다.
- **JSON 파싱 실패**: 모델이 JSON을 잘못 반환하면 경고와 함께 재시도하므로, 프롬프트에 "반드시 유효한 JSON 객체로만 답하라" 같은 지시를 넣어 안정성을 높입니다.
- **서버 충돌**: 모델별 Ollama 프로세스가 응답하지 않으면 OllamaAutoLoader.StopAllServers()를 호출하거나 Unity를 재시작해 프로세스를 재기동하세요.
- **네트워크/리소스**: Ollama가 GPU를 사용할 수 없을 때는 OLLAMA_NUM_GPU_LAYERS 값을 수정하거나 모델을 더 작은 버전으로 교체하세요.
- **스트리밍 파싱**: settings.stream == true일 때는 응답이 줄 단위로 들어오므로 UI에 누적하거나 마지막 줄에서 마무리 로직을 수행해야 합니다.
- **서비스 미등록 오류**: 체인 링크는 `OllamaServiceLocator.Require()`를 호출하므로, 런타임에는 OllamaComponent가 씬에 존재하거나 `OllamaServiceLocator.Register(customService)`를 직접 호출해야 합니다. 에디터 시뮬레이터는 기본적으로 OllamaEditorService를 사용합니다.

## 8. 확장 아이디어
- IStateChainLink를 상속해 커스텀 링크(예: 벡터 임베딩 생성 후 DB 저장, 함수 호출 결과를 상태에 주입)를 추가합니다.
- PromptTemplate를 대체/확장하여 조건부 블록, 반복 구문 등 더 풍부한 템플릿 기능을 구현할 수 있습니다.
- OllamaSettings 에셋을 여러 개 만들어 모델별 또는 용도별(서사, 경제, 전투) 파라미터를 분리합니다.
- 임베딩 API를 Vector DB(예: SQLite + cosine)와 결합하면 게임 내 지식 베이스 검색이나 NPC 기억 시스템을 구축할 수 있습니다.

## 9. Prompt Pipeline 그래프 에디터 사용법
새로운 그래프 에디터는 ScriptableObject 기반의 PromptPipelineAsset을 시각적으로 설계하고, 상태 키 흐름을 검증하며, 즉시 시뮬레이션할 수 있도록 돕는 도구입니다. 아래 순서를 참고해 활용하세요.

### 9.1 에디터 열기와 자산 선택
1. Unity 메뉴에서 Window > LLM > Prompt Pipeline Editor를 클릭해 창을 엽니다.
2. 상단 툴바의 Pipeline Asset 필드에 기존 자산을 지정하거나, Project 창에서 Create > LLM > Prompt Pipeline을 통해 새 자산을 만든 뒤 드래그해 넣습니다.
3. 툴바 버튼:
   - Save: 현재 자산을 저장합니다.
   - Validate: Analyzer 결과(단계 수, 상태 키 수)를 팝업으로 확인합니다.
   - Run: 입력 패널 값으로 즉시 시뮬레이션을 수행합니다.
   - Ping Asset: Project 창에서 해당 자산을 하이라이트합니다.

### 9.2 그래프 구성 요소 이해
- Step Node: 각 PromptPipelineStep을 나타내며, 타이틀에는 순번/이름/StepKind가 표시되고 색상으로 종류를 구분합니다. 상단 Exec 포트는 실행 순서를 나타내며 하나의 선형 체인만 허용됩니다. 하단 State 포트는 Analyzer가 자동으로 상태 키를 연결하는 시각화 전용 포트입니다.
- Step Kind: Json LLM은 JSON Output Fields 기반으로 키를 생산하고 재시도 옵션을 사용합니다. Completion LLM은 단순 텍스트 완성으로 `response` 키를 출력하며 JSON Output Fields는 무시됩니다. Custom Link는 IStateChainLink 구현체명을 String으로 입력해 실행합니다.
- Node 본문에서 OllamaSettings, User Prompt Template, JSON 재시도 옵션, Custom Link 타입명을 바로 편집할 수 있습니다. Insert State Key 버튼은 Analyzer가 찾아낸 키를 드롭다운으로 보여 주고 템플릿에 {{keyName}} 형식으로 삽입합니다.
- State Flow Connections: Analyzer가 감지한 키를 기준으로 Pipeline Input/Output 노드와 Step 노드가 직접 연결됩니다. 각 Step의 Reads/Writes 라벨만 봐도 전·후 단계에서 어떤 값이 오가는지 한눈에 파악할 수 있습니다.
- Pipeline Input/Output Node: 외부 입력이 필요한 키와 최종으로 노출되는 키를 집약하고 Step Node와 자동 연결합니다.
- OllamaSettings Inspector: Format 필드는 직접 수정하지 않고 JSON Output Fields 빌더를 통해 관리합니다. + Add Field 버튼으로 키를 추가하고 Field Name/Type/Example/Description을 입력하면 JSON Schema가 자동 생성되어 Analyzer와 Runtime Format에 동시에 반영됩니다. Array 타입을 선택하면 Element Type도 함께 지정해 배열 응답 구조를 모델링할 수 있습니다.

### 9.3 실행 순서 변경
1. Step Node의 Exec In/Out 포트를 드래그해 새 연결을 만들면 그래프가 유효한 선형 체인인지 검사합니다.
2. 체인이 올바르면 PromptPipelineAsset.steps 리스트가 해당 순서로 재정렬되고 Analyzer가 재실행됩니다.
3. 분기나 루프가 생기면 콘솔에 경고가 찍히며 기존 순서가 유지되므로, 단일 체인이 되도록 다시 연결해야 합니다.

### 9.4 시뮬레이션 패널
1. Analyzer가 Input으로 분류한 키들이 우측 패널 텍스트 필드로 표시됩니다. 테스트 값, JSON 조각 등을 입력하세요.
2. Run Pipeline 버튼을 누르면 EditorCoroutineRunner가 StateSequentialChainExecutor를 구성해 모든 Step을 순차 실행합니다. OllamaComponent가 없을 때는 자동으로 OllamaEditorService가 사용됩니다.
3. 완료되면 상태 라벨에 시간이 갱신되고, Pipeline Output 노드를 통해 어떤 키가 외부로 노출되는지 확인할 수 있습니다. 실패하면 라벨과 Console 모두에 오류 메시지가 출력됩니다.

### 9.5 팁
- Completion 단계의 최종 답변은 항상 `PromptPipelineConstants.AnswerKey`(`response`)에 저장되므로 후속 단계나 출력 노드에서 이 키를 참조하세요.
- Custom Link 단계는 Analyzer가 읽기/쓰기 키를 추적하지 않으므로 필요한 키를 문서로 별도 기록하는 것이 좋습니다.
- 그래프 창을 닫았다가 다시 열면 Analyzer가 재실행되어 상태가 최신으로 유지됩니다. Undo/Redo도 자동 기록되므로 자유롭게 편집해도 됩니다.
