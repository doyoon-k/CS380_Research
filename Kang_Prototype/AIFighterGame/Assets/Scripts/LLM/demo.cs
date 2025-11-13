using UnityEngine;

public class MyLLMDemo : MonoBehaviour
{
    [Tooltip("씬 내 OllamaComponent가 붙어있는 GameObject 참조")]
    public OllamaComponent ollama;

    private void Start()
    {

        //// 1. GenerateCompletion 예제: 간단 프롬프트에 대한 응답 받기
        //ollama.GenerateCompletion("Breaking News: CEO of Tesla just farted", (response) =>
        //{
        //    Debug.Log("Generate Completion Response:\n" + response);
        //});
        //Debug.Log("GenerateCompletion 요청을 보냈습니다.");

        //// 2. ChatCompletion 예제: 대화형 메시지 전송
        //ChatMessage[] messages = new ChatMessage[]
        //{
        //    new ChatMessage { role = "system", content = "You are a knowledgeable assistant." },
        //    new ChatMessage { role = "user", content = "Explain quantum entanglement in simple terms." }
        //};
        //ollama.ChatCompletion(messages, (chatLine) => {
        //    Debug.Log("Chat Completion Response (line):\n" + chatLine);
        //});

        //// 3. Embed 예제: 여러 텍스트의 임베딩 벡터 받기
        //string[] texts = { "Hello world", "Quantum entanglement is fascinating." };
        //ollama.Embed(texts, (embeddings) => {
        //    Debug.Log("Received " + embeddings.Length + " embedding vectors.");
        //    // 예를 들어, 각 임베딩 벡터의 길이를 출력
        //    for (int i = 0; i < embeddings.Length; i++)
        //    {
        //        Debug.Log($"Embedding[{i}] length = {embeddings[i].Length}");
        //    }
        //});
    }
}
