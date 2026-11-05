using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// Gemini에 질문하고 답을 문자열로 돌려준다. 키는 StreamingAssets/gemini_key.txt 한 줄(Git 제외 파일)에서 읽는다.
public class GeminiClient : MonoBehaviour
{
    [SerializeField] string model = "gemini-3.1-flash-lite"; // 가볍고 덜 붐빈다. 1.5·2.5 계열은 신규 키로 못 쓴다(404)
    [SerializeField] string fallbackModel = "gemini-3.5-flash"; // 기본 모델이 붐빌 때 쓰는 모델
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
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var req = new UnityWebRequest($"https://generativelanguage.googleapis.com/v1beta/models/{m}:generateContent", "POST");
                req.uploadHandler = new UploadHandlerRaw(json);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-goog-api-key", apiKey);
                yield return req.SendWebRequest();

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
}
