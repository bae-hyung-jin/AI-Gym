using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 화면 오른쪽 아래 버튼(또는 T키)으로 대화창을 열고, 입력한 글에 '호출 단어'가 있을 때만 Gemini를 부른다.
public class TrainerChat : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string PromptInput(string message, string defaultText);
#endif
    [SerializeField] Font font;
    [SerializeField] GeminiClient client;
    [SerializeField] TrainerVoice voice;
    [SerializeField] string triggerKeyword = "트레이너";

    GameObject panel;
    InputField input;
    Text answer;
    bool waiting;
    int ticket; // 늦게 도착한 옛 답을 버리기 위한 번호

    void Start()
    {
        var canvas = UiKit.MakeCanvas("ChatCanvas", 50);
        var t = canvas.transform;
        var open = UiKit.Btn(t, font, "트레이너에게 질문 (T)", 30, new Vector2(420, 80), new Vector2(-240, 70), new Color(0.15f, 0.55f, 0.45f, 0.9f), new Vector2(1, 0));
        open.onClick.AddListener(Toggle);

        panel = UiKit.Panel(t, new Vector2(1300, 640), Vector2.zero, new Color(0.1f, 0.13f, 0.2f, 0.97f)).gameObject;
        var p = panel.transform;
        UiKit.Label(p, font, $"질문에 '{triggerKeyword}'를 넣어 주세요. 예) {triggerKeyword}, 허리가 아플 땐 뭘 하죠?", 32, new Vector2(1200, 90), new Vector2(0, 260));
        answer = UiKit.Label(p, font, "", 36, new Vector2(1200, 300), new Vector2(0, 60), TextAnchor.UpperLeft);

        var field = UiKit.Panel(p, new Vector2(900, 90), new Vector2(-140, -220), Color.white);
        var ph = UiKit.Label(field.transform, font, "여기에 입력", 34, new Vector2(860, 80), Vector2.zero, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.5f));
        var tx = UiKit.Label(field.transform, font, "", 34, new Vector2(860, 80), Vector2.zero, TextAnchor.MiddleLeft, Color.black);
        tx.raycastTarget = true;
        input = field.gameObject.AddComponent<InputField>();
        input.targetGraphic = field; input.textComponent = tx; input.placeholder = ph;
        input.onSubmit.AddListener(_ => Send());
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL의 Unity 입력창은 한글 조합이 안 된다. 눌러서 브라우저 입력창으로 쓰고 바로 보낸다.
        input.interactable = false;
        // 입력칸 자체에는 버튼을 붙일 수 없어서(이미 InputField가 있다) 위에 투명 버튼을 덮는다
        var ask = UiKit.Btn(field.transform, font, "", 1, new Vector2(900, 90), Vector2.zero, new Color(0, 0, 0, 0));
        ask.onClick.AddListener(() => { input.text = PromptInput($"질문을 써 주세요 ('{triggerKeyword}' 포함)", input.text); Send(); });
#endif

        UiKit.Btn(p, font, "보내기", 36, new Vector2(240, 90), new Vector2(430, -220), new Color(0.2f, 0.45f, 0.75f)).onClick.AddListener(Send);
        UiKit.Btn(p, font, "닫기", 32, new Vector2(160, 70), new Vector2(560, 270), new Color(0.4f, 0.25f, 0.25f)).onClick.AddListener(Toggle);
        panel.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (!panel.activeSelf && !PlayerFreeze.Frozen && kb.tKey.wasPressedThisFrame) Toggle();
        else if (panel.activeSelf && kb.escapeKey.wasPressedThisFrame) Toggle();
    }

    void Toggle()
    {
        var show = !panel.activeSelf;
        panel.SetActive(show);
        if (show) { PlayerFreeze.Push(); input.ActivateInputField(); }
        else PlayerFreeze.Pop();
    }

    void Send()
    {
        var text = input.text.Trim();
        if (waiting || text.Length == 0) return;
        if (!text.Contains(triggerKeyword)) { answer.text = $"'{triggerKeyword}'를 넣어서 물어봐 주세요."; return; } // 호출 단어가 없으면 API를 부르지 않는다

        waiting = true;
        var my = ++ticket;
        answer.text = "생각하는 중...";
        input.text = "";
        StartCoroutine(Watchdog(my));
        // 설문은 참고만 한다. 손님이 지금 쓴 질문이 우선이고, 질문과 상관없으면 설문 내용은 말하지 않게 한다.
        client.Ask($"[참고: 입장할 때 손님이 불편하다고 고른 자세는 '{DoorSurvey.Summary}'. 아래 질문에서 말한 내용이 항상 우선이며, 질문과 직접 관련이 없으면 이 참고는 언급하지 말 것]\n질문: {text}", reply =>
        {
            if (my != ticket) return; // 이미 시간이 지나 포기한 질문의 답이다
            ticket++; answer.text = reply; waiting = false; voice?.Say(reply);
        });
    }

    // 답이 너무 늦으면 계속 "생각하는 중"에 머물지 않고 다시 질문할 수 있게 풀어 준다
    System.Collections.IEnumerator Watchdog(int my)
    {
        yield return new WaitForSecondsRealtime(40f);
        if (my != ticket) yield break;
        ticket++; waiting = false;
        answer.text = "답이 오지 않아요. 잠시 뒤에 다시 물어봐 주세요.";
        Debug.LogWarning("[TrainerChat] 40초 안에 답이 오지 않았습니다");
    }
}
