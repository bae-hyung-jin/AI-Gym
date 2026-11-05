using UnityEngine;

// 키가 제대로 동작하는지 확인하는 임시 스크립트. 플레이를 시작하면 질문을 한 번 보내고 콘솔에 답을 찍는다.
public class GeminiTest : MonoBehaviour
{
    [SerializeField] GeminiClient client;
    [SerializeField] string question = "안녕하세요, 첫 운동으로 뭘 하면 좋을까요?";

    void Start() => client.Ask(question, answer => Debug.Log("[Gemini] " + answer));
}
