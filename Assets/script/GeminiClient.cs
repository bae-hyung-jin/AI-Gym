using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Gemini에 질문하고 답을 문자열로 돌려준다. 키는 StreamingAssets/gemini_key.txt 한 줄(Git 제외 파일)에서 읽는다.
public class GeminiClient : MonoBehaviour
{
    // GeminiClient.cs 상단 모델 선언부
    [SerializeField] string model = "gemini-3.6-flash";         // 메인: 빠르고 정확한 3.6 Flash
    [SerializeField] string fallbackModel = "gemini-3.1-pro";     // 대체: 503/429 발생 시 넘어갈 폴백 모델
    [TextArea, SerializeField] string systemPrompt = "너는 어르신을 돕는 헬스장 AI 트레이너다. 두세 문장으로 쉽고 다정하게 한국어로 답한다.";

    string apiKey;

    [Serializable] class Part { public string text; }
    [Serializable] class Content { public Part[] parts; }
    [Serializable] class Request { public Content system_instruction; public Content[] contents; }
    [Serializable] class Candidate { public Content content; }
    [Serializable] class Response { public Candidate[] candidates; }

    // 질문 하나를 보내고 답(또는 오류 문구)을 onDone으로 돌려준다.
    public void Ask(string question, Action<string> onDone) => StartCoroutine(AskRoutine(question, onDone));

    IEnumerator AskRoutine(string question, Action<string> onDone)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            using var keyReq = UnityWebRequest.Get(Application.streamingAssetsPath + "/gemini_key.txt");
            yield return keyReq.SendWebRequest();
            if (keyReq.result != UnityWebRequest.Result.Success) { onDone("[오류] StreamingAssets/gemini_key.txt 를 읽지 못했습니다."); yield break; }
            apiKey = keyReq.downloadHandler.text.Trim();
        }

        var body = new Request
        {
            system_instruction = new Content { parts = new[] { new Part { text = systemPrompt } } },
            contents = new[] { new Content { parts = new[] { new Part { text = question } } } }
        };
        var json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
        long lastCode = 0;
        // 서버가 붐비면(503·429) 잠깐 쉬고 다시 시도하고, 그래도 안 되면 대체 모델로 넘어간다.
        foreach (var m in new[] { model, fallbackModel })
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var req = new UnityWebRequest($"https://generativelanguage.googleapis.com/v1beta/models/{m}:generateContent", "POST");
                req.uploadHandler = new UploadHandlerRaw(json);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-goog-api-key", apiKey);
                Debug.Log($"[Gemini] 질문 보냄 {m} (시도 {attempt + 1})");
                yield return req.SendWebRequest();
                Debug.Log($"[Gemini] 응답 {m} {req.responseCode} {req.result}");

                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<Response>(req.downloadHandler.text);
                    var ok = res?.candidates != null && res.candidates.Length > 0 && res.candidates[0].content?.parts?.Length > 0;
                    onDone(ok ? res.candidates[0].content.parts[0].text : "[오류] 빈 응답: " + req.downloadHandler.text);
                    yield break;
                }
                lastCode = req.responseCode;
                Debug.LogWarning($"[Gemini] {m} 실패 {lastCode} {req.error} {req.downloadHandler.text}"); // 원인 확인용
                if (lastCode != 503 && lastCode != 429) break; // 붐벼서 생긴 오류가 아니면 같은 모델로 다시 해도 소용없다
                yield return new WaitForSeconds(1.5f);
            }
        }
        onDone(lastCode == 503 || lastCode == 429
            ? "지금 AI 서버가 붐벼서 답을 받지 못했어요. 잠시 뒤에 다시 물어봐 주세요."
            : $"[오류] {lastCode}");
    }

    // ---- 음성(TTS) ----
    [SerializeField] string ttsModel = "gemini-3.8-flash-tts";
    [SerializeField] string ttsFallbackModel = "gemini-3.8-flash-lite-tts";
    [SerializeField] string voiceName = "Kore";
    [SerializeField] bool preferGoogleTts = false;
    [SerializeField] bool useGoogleTtsFallback = true;

    [Serializable] class TtsPrebuilt { public string voiceName; }
    [Serializable] class TtsVoiceCfg { public TtsPrebuilt prebuiltVoiceConfig; }
    [Serializable] class TtsSpeechCfg { public TtsVoiceCfg voiceConfig; }
    [Serializable] class TtsGenCfg { public string[] responseModalities; public TtsSpeechCfg speechConfig; }
    [Serializable] class TtsRequest { public Content[] contents; public TtsGenCfg generationConfig; }
    [Serializable] class InlineData { public string mimeType; public string data; }
    [Serializable] class AudioPart { public InlineData inlineData; }
    [Serializable] class AudioContent { public AudioPart[] parts; }
    [Serializable] class AudioCandidate { public AudioContent content; }
    [Serializable] class AudioResponse { public AudioCandidate[] candidates; }

    // Interactions API schema
    [Serializable] class TtsInterContent { public string type = "text"; public string text; }
    [Serializable] class TtsInterInput { public string type = "user_input"; public TtsInterContent[] content; }
    [Serializable] class TtsInterRespFormat { public string type = "audio"; public string mime_type = "audio/wav"; public int sample_rate = 24000; }
    [Serializable] class TtsInterVoice { public string voice; }
    [Serializable] class TtsInterGenCfg { public TtsInterVoice[] speech_config; }
    [Serializable] class TtsInterReq
    {
        public string model;
        public TtsInterInput[] input;
        public TtsInterRespFormat response_format;
        public TtsInterGenCfg generation_config;
    }
    [Serializable] class TtsInterAudio { public string data; public string mime_type; }
    [Serializable] class TtsInterRes { public TtsInterAudio output_audio; }

    // 글을 음성으로 바꿔 AudioClip으로 돌려준다. 실패하면 null.
    public void Synthesize(string text, Action<AudioClip> onDone) => StartCoroutine(SynthRoutine(text, onDone));

    IEnumerator SynthRoutine(string text, Action<AudioClip> onDone)
    {
        if (preferGoogleTts)
        {
            yield return StartCoroutine(GoogleTtsRoutine(text, onDone));
            yield break;
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            using var keyReq = UnityWebRequest.Get(Application.streamingAssetsPath + "/gemini_key.txt");
            yield return keyReq.SendWebRequest();
            if (keyReq.result == UnityWebRequest.Result.Success)
            {
                apiKey = keyReq.downloadHandler.text.Trim();
            }
        }

        if (!string.IsNullOrEmpty(apiKey))
        {
            // 1. Interactions API 시도 (gemini-3.8-flash-tts 공식 규격)
            var interBody = new TtsInterReq
            {
                model = ttsModel,
                input = new[] { new TtsInterInput { content = new[] { new TtsInterContent { text = text } } } },
                response_format = new TtsInterRespFormat(),
                generation_config = new TtsInterGenCfg { speech_config = new[] { new TtsInterVoice { voice = voiceName } } }
            };
            var interJson = Encoding.UTF8.GetBytes(JsonUtility.ToJson(interBody));
            using (var req = new UnityWebRequest("https://generativelanguage.googleapis.com/v1beta/interactions", "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(interJson);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-goog-api-key", apiKey);
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<TtsInterRes>(req.downloadHandler.text);
                    if (res?.output_audio != null && !string.IsNullOrEmpty(res.output_audio.data))
                    {
                        var clip = ToClip(Convert.FromBase64String(res.output_audio.data), res.output_audio.mime_type);
                        if (clip != null) { onDone(clip); yield break; }
                    }
                }
                else
                {
                    Debug.LogWarning($"[Gemini Interactions TTS] 실패 {req.responseCode} {req.error}");
                }
            }

            // 2. generateContent API 시도 (대체)
            var body = new TtsRequest
            {
                contents = new[] { new Content { parts = new[] { new Part { text = text } } } },
                generationConfig = new TtsGenCfg
                {
                    responseModalities = new[] { "AUDIO" },
                    speechConfig = new TtsSpeechCfg { voiceConfig = new TtsVoiceCfg { prebuiltVoiceConfig = new TtsPrebuilt { voiceName = voiceName } } }
                }
            };
            var json = Encoding.UTF8.GetBytes(JsonUtility.ToJson(body));
            foreach (var m in new[] { ttsModel, ttsFallbackModel })
            {
                using var req = new UnityWebRequest($"https://generativelanguage.googleapis.com/v1beta/models/{m}:generateContent", "POST");
                req.uploadHandler = new UploadHandlerRaw(json);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-goog-api-key", apiKey);
                req.timeout = 5;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<AudioResponse>(req.downloadHandler.text);
                    var d = res?.candidates != null && res.candidates.Length > 0 ? res.candidates[0].content?.parts?[0]?.inlineData : null;
                    if (d != null && !string.IsNullOrEmpty(d.data))
                    {
                        var clip = ToClip(Convert.FromBase64String(d.data), d.mimeType);
                        if (clip != null) { onDone(clip); yield break; }
                    }
                }
            }
        }

        // 3. Google Translate TTS 폴백 (지연 없이 안정적인 음성 출력)
        if (useGoogleTtsFallback)
        {
            Debug.Log($"[GeminiClient] Google TTS로 전환하여 음성을 합성합니다: \"{text}\"");
            yield return StartCoroutine(GoogleTtsRoutine(text, onDone));
            yield break;
        }

        onDone(null);
    }

    IEnumerator GoogleTtsRoutine(string text, Action<AudioClip> onDone)
    {
        string encoded = UnityWebRequest.EscapeURL(text);
        string url = "https://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&client=tw-ob&tl=ko&q=" + encoded;
        using var www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
        yield return www.SendWebRequest();
        if (www.result == UnityWebRequest.Result.Success)
        {
            var clip = DownloadHandlerAudioClip.GetContent(www);
            if (clip != null)
            {
                onDone(clip);
                yield break;
            }
        }
        Debug.LogWarning($"[Google TTS 실패] {www.error} (HTTP {www.responseCode})");
        onDone(null);
    }

    // WAV 또는 24kHz 16비트 PCM을 AudioClip으로 바꾼다
    static AudioClip ToClip(byte[] b, string mime)
    {
        int rate = 24000, ch = 1, start = 0;
        int dataLength = b.Length;

        // RIFF WAV 헤더 분석 시 data 크기만큼만 정확히 자름 (후미 잡음 방지)
        if (b.Length > 44 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F')
        {
            ch = BitConverter.ToInt16(b, 22);
            rate = BitConverter.ToInt32(b, 24);
            for (var i = 12; i + 8 < b.Length;)
            {
                var size = BitConverter.ToInt32(b, i + 4);
                if (b[i] == 'd' && b[i + 1] == 'a' && b[i + 2] == 't' && b[i + 3] == 'a')
                {
                    start = i + 8;
                    dataLength = Mathf.Min(b.Length, start + size);
                    break;
                }
                i += 8 + size;
            }
        }
        else if (mime != null && mime.Contains("rate="))
        {
            int.TryParse(mime.Substring(mime.IndexOf("rate=") + 5).Split(';')[0], out rate);
        }

        var n = (dataLength - start) / 2;
        if (n <= 0) return null;

        var f = new float[n];
        for (var i = 0; i < n; i++)
        {
            f[i] = BitConverter.ToInt16(b, start + i * 2) / 32768f;
        }

        // 끝부분 0.05초 페이드아웃 (화이트 노이즈 제거)
        int fadeLength = Mathf.Min(f.Length, 1200);
        for (int i = 0; i < fadeLength; i++)
        {
            int index = f.Length - 1 - i;
            float fadeRatio = (float)i / fadeLength;
            f[index] *= fadeRatio;
        }

        var clip = AudioClip.Create("trainer_voice", n / ch, ch, rate, false);
        clip.SetData(f, 0);
        return clip;
    }
}