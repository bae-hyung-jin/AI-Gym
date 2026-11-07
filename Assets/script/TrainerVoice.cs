using UnityEngine;

// 트레이너의 말을 Gemini 음성(TTS)으로 읽어 준다. 새로운 말이 들어오면 이전 음성은 즉시 중단하고 최신 요청만 재생한다.
public class TrainerVoice : MonoBehaviour
{
    [SerializeField] GeminiClient geminiClient;
    [SerializeField] AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    int currentTicket = 0; // 최신 요청만 판별하기 위한 티켓 번호

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (geminiClient == null) geminiClient = FindFirstObjectByType<GeminiClient>();
    }

    public void Say(string replyText)
    {
        if (string.IsNullOrEmpty(replyText)) return;

        // 불필요한 라벨 및 오류 문구 필터링
        replyText = replyText.Replace("AI 트레이너:", "").Trim();
        if (replyText.Length == 0 || replyText.StartsWith("[오류]") || replyText.StartsWith("지금 AI")) return;

        // 새 대사가 오면 현재 재생 중인 오디오는 멈추고 새 티켓 발급
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        int requestTicket = ++currentTicket;
        Debug.Log($"[TrainerVoice] 대사 수신 (티켓 #{requestTicket}): {replyText}");

        if (geminiClient != null)
        {
            geminiClient.Synthesize(replyText, clip =>
            {
                // 응답이 도착했을 때 더 새로운 요청이 들어왔다면 폐기
                if (requestTicket != currentTicket)
                {
                    Debug.Log($"[TrainerVoice] 이전 요청(티켓 #{requestTicket}) 폐기됨 (현재 최신: #{currentTicket})");
                    return;
                }

                if (clip != null && audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.volume = volume;
                    audioSource.Play();
                    Debug.Log($"[TrainerVoice] 음성 재생 시작 (티켓 #{requestTicket})");
                }
                else
                {
                    Debug.LogWarning("[TrainerVoice] 음성 생성 실패 또는 AudioSource 누락");
                }
            });
        }
        else
        {
            Debug.LogWarning("[TrainerVoice] 인스펙터 창에서 GeminiClient를 연결해 주세요.");
        }
    }

    // 재생 중인 음성 정지 (외부에서 호출 시)
    public void Stop()
    {
        currentTicket++; // 진행 중이던 비동기 콜백 무효화
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}