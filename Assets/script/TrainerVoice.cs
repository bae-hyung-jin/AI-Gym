using UnityEngine;

// 트레이너의 말을 Gemini 음성(TTS)으로 읽어 준다. 새로운 말이 들어오면 이전 음성은 즉시 중단하고 최신 요청만 재생한다.
public class TrainerVoice : MonoBehaviour
{
    [SerializeField] GeminiClient client;
    [SerializeField] AudioSource audioSource;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    int currentTicket = 0; // 최신 요청만 판별하기 위한 티켓 번호

    void Awake()
    {
        EnsureAudioSource();
        if (client == null) client = FindFirstObjectByType<GeminiClient>();
    }

    void EnsureAudioSource()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void Say(string replyText)
    {
        if (string.IsNullOrEmpty(replyText)) return;

        // 불필요한 라벨 및 오류 문구 필터링
        replyText = replyText.Replace("AI 트레이너:", "").Trim();
        if (replyText.Length == 0 || replyText.StartsWith("[오류]") || replyText.StartsWith("지금 AI")) return;

        // () 또는 （） 괄호와 괄호 안의 단어 제거 (예: 바(Bar) -> 바, 덤벨(Dumbbell) -> 덤벨)
        replyText = System.Text.RegularExpressions.Regex.Replace(replyText, @"[\(（][^)）]*[\)）]", "");
        replyText = System.Text.RegularExpressions.Regex.Replace(replyText, @"\s+", " ").Trim();
        if (replyText.Length == 0) return;

        EnsureAudioSource();

        // 새 대사가 오면 현재 재생 중인 오디오는 멈추고 새 티켓 발급
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        int requestTicket = ++currentTicket;
        Debug.Log($"[TrainerVoice] 대사 수신 (티켓 #{requestTicket}): {replyText}");

        if (client == null) client = FindFirstObjectByType<GeminiClient>();

        if (client != null)
        {
            client.Synthesize(replyText, clip =>
            {
                // 응답이 도착했을 때 더 새로운 요청이 들어왔다면 폐기
                if (requestTicket != currentTicket)
                {
                    Debug.Log($"[TrainerVoice] 이전 요청(티켓 #{requestTicket}) 폐기됨 (현재 최신: #{currentTicket})");
                    return;
                }

                if (clip != null)
                {
                    EnsureAudioSource();
                    audioSource.clip = clip;
                    audioSource.volume = volume;
                    audioSource.Play();
                    Debug.Log($"[TrainerVoice] 음성 재생 시작 (티켓 #{requestTicket})");
                }
                else
                {
                    Debug.LogWarning($"[TrainerVoice] '{replyText}' 음성을 받지 못해 직접 재생을 시도합니다.");
                    StartCoroutine(DirectGoogleTts(replyText, requestTicket));
                }
            });
        }
        else
        {
            StartCoroutine(DirectGoogleTts(replyText, requestTicket));
        }
    }

    System.Collections.IEnumerator DirectGoogleTts(string text, int ticket)
    {
        string encoded = UnityEngine.Networking.UnityWebRequest.EscapeURL(text);
        string url = "https://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&client=tw-ob&tl=ko&q=" + encoded;
        using var www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
        yield return www.SendWebRequest();
        if (ticket == currentTicket && www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            var clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
            if (clip != null)
            {
                EnsureAudioSource();
                audioSource.clip = clip;
                audioSource.volume = volume;
                audioSource.Play();
                Debug.Log($"[TrainerVoice] 직접 음성 재생 완료 (티켓 #{ticket})");
            }
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