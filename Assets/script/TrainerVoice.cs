using UnityEngine;

public class TrainerVoice : MonoBehaviour
{
    [SerializeField] GeminiClient geminiClient;
    [SerializeField] AudioSource audioSource;

    void Awake()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void Say(string replyText)
    {
        if (string.IsNullOrEmpty(replyText)) return;

        Debug.Log($"[TrainerVoice] 대사 수신: {replyText}");

        if (geminiClient != null)
        {
            geminiClient.Synthesize(replyText, clip =>
            {
                if (clip != null && audioSource != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            });
        }
        else
        {
            Debug.LogWarning("[TrainerVoice] 인스펙터 창에서 GeminiClient를 연결해 주세요.");
        }
    }

    // TrainerAppear.cs의 Disappear()에서 호출하는 음성 정지 함수
    public void Stop()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}