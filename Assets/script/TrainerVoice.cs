using UnityEngine;

// 트레이너의 말을 Gemini 음성(TTS)으로 읽어 준다. 새로 말하면 앞서 하던 말은 멈춘다.
[RequireComponent(typeof(AudioSource))]
public class TrainerVoice : MonoBehaviour
{
    [SerializeField] GeminiClient client;
    [SerializeField, Range(0f, 1f)] float volume = 1f;

    AudioSource source;
    int ticket; // 늦게 도착한 옛 음성을 버리기 위한 번호

    void Awake() { source = GetComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0f; }

    public void Say(string text)
    {
        text = text.Replace("AI 트레이너:", "").Trim();
        if (text.Length == 0 || text.StartsWith("[오류]") || text.StartsWith("지금 AI")) return; // 오류 문구는 읽지 않는다
        var my = ++ticket;
        source.Stop();
        client.Synthesize(text, clip =>
        {
            if (clip == null || my != ticket) return;
            source.clip = clip; source.volume = volume; source.Play();
        });
    }

    public void Stop() { ticket++; source.Stop(); }
}
