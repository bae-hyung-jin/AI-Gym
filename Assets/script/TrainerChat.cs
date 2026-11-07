using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 화면 오른쪽 아래 큰 버튼(또는 T키)으로 대화창을 연다.
// 어르신이 글을 쓰지 않아도 되게 자주 묻는 질문을 버튼으로 두고, 그 답은 미리 써 둔 안전한 문장으로 바로 보여 준다.
// 직접 쓴 질문만 Gemini로 보낸다. 어떤 답이든 끝에 "현장 선생님이나 의사와 먼저 확인하세요"를 붙인다.
public class TrainerChat : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern string PromptInput(string message, string defaultText);
#endif
    [SerializeField] Font font;
    [SerializeField] GeminiClient client;
    [SerializeField] TrainerVoice voice;

    const string Disclaimer = "\n※ 현장 선생님이나 의사와 먼저 확인하세요.";

    // 질문 버튼 이름 / 미리 써 둔 답. 어려운 말을 쓰지 않고, 아프면 멈추고 쉬게 한다.
    static readonly string[][] Presets =
    {
        new[] { "무릎이 아파요", "무릎이 아프면 운동을 바로 멈추고 편하게 앉아 쉬세요. 무릎을 많이 굽히는 동작은 피하고, 앉아서 하는 가벼운 동작부터 해 보세요." },
        new[] { "허리가 아파요", "허리가 아프면 하던 동작을 멈추고 천천히 숨을 고르세요. 등을 곧게 펴고 가볍게만 하시고, 아픔이 계속되면 오늘은 쉬세요." },
        new[] { "어깨가 아파요", "어깨가 아프면 팔을 억지로 올리지 마세요. 바를 아주 조금만 당기거나, 오늘은 쉬어 가셔도 괜찮아요." },
        new[] { "숨이 차요", "숨이 차면 바로 멈추고 의자에 앉아 천천히 숨을 쉬세요. 옆 사람과 말을 할 수 있을 정도로만 운동하는 게 좋아요." },
        new[] { "처음인데 어떻게 해요?", "처음이시면 가볍게 10회만 해 보세요. 바를 가슴 쪽으로 천천히 당기고, 천천히 올리세요. 힘들면 쉬었다 하셔도 돼요." },
        new[] { "몇 번 하면 돼요?", "처음에는 10회 정도면 충분해요. 중간에 쉬어도 괜찮고, 아프면 바로 멈추세요." },
    };

    GameObject panel, openBtn;
    InputField input;
    Text answer;
    bool waiting;
    int ticket; // 늦게 도착한 옛 답을 버리기 위한 번호

    void Start()
    {
        var canvas = UiKit.MakeCanvas("ChatCanvas", 50);
        var t = canvas.transform;
        openBtn = UiKit.Btn(t, font, "궁금한 것 물어보기 (T)", 40, new Vector2(560, 110), new Vector2(-310, 80), new Color(0.15f, 0.55f, 0.45f, 0.95f), new Vector2(1, 0)).gameObject;
        openBtn.GetComponent<Button>().onClick.AddListener(Toggle);

        panel = UiKit.Panel(t, new Vector2(1500, 1000), Vector2.zero, new Color(0.1f, 0.13f, 0.2f, 1f)).gameObject; // 뒤가 비쳐 보이지 않게 불투명
        var p = panel.transform;
        UiKit.Label(p, font, "궁금한 것을 눌러 보세요", 52, new Vector2(1000, 90), new Vector2(0, 440));
        UiKit.Btn(p, font, "닫기", 40, new Vector2(200, 90), new Vector2(620, 440), new Color(0.45f, 0.25f, 0.25f)).onClick.AddListener(Toggle);

        for (var i = 0; i < Presets.Length; i++) // 2열 3행의 큰 질문 버튼
        {
            var question = Presets[i][0]; var canned = Presets[i][1];
            var x = i % 2 == 0 ? -365f : 365f; var y = 320f - (i / 2) * 120f;
            UiKit.Btn(p, font, question, 42, new Vector2(700, 100), new Vector2(x, y), new Color(0.2f, 0.45f, 0.75f)).onClick.AddListener(() => ShowAnswer(canned));
        }

        // 세 번째 줄 버튼(아래 끝 y=30)보다 아래에서 시작하게 둔다
        answer = UiKit.Label(p, font, "", 40, new Vector2(1400, 340), new Vector2(0, -160), TextAnchor.UpperLeft);

        var field = UiKit.Panel(p, new Vector2(1000, 100), new Vector2(-190, -400), Color.white);
        var ph = UiKit.Label(field.transform, font, "직접 쓰셔도 돼요", 38, new Vector2(960, 90), Vector2.zero, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.5f));
        var tx = UiKit.Label(field.transform, font, "", 38, new Vector2(960, 90), Vector2.zero, TextAnchor.MiddleLeft, Color.black);
        tx.raycastTarget = true;
        input = field.gameObject.AddComponent<InputField>();
        input.targetGraphic = field; input.textComponent = tx; input.placeholder = ph;
        input.onSubmit.AddListener(_ => Send());
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL의 Unity 입력창은 한글 조합이 안 된다. 눌러서 브라우저 입력창으로 쓰고 바로 보낸다.
        input.interactable = false;
        // 입력칸 자체에는 버튼을 붙일 수 없어서(이미 InputField가 있다) 위에 투명 버튼을 덮는다
        var ask = UiKit.Btn(field.transform, font, "", 1, new Vector2(1000, 100), Vector2.zero, new Color(0, 0, 0, 0));
        ask.onClick.AddListener(() => { input.text = PromptInput("궁금한 것을 써 주세요", input.text); Send(); });
#endif
        UiKit.Btn(p, font, "보내기", 42, new Vector2(260, 100), new Vector2(560, -400), new Color(0.2f, 0.45f, 0.75f)).onClick.AddListener(Send);
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
        openBtn.SetActive(!show); // 창이 열려 있는 동안은 열기 버튼을 숨긴다
        if (show) { PlayerFreeze.Push(); answer.text = ""; }
        else { ticket++; waiting = false; PlayerFreeze.Pop(); } // 닫으면 기다리던 답은 버린다
    }

    // 미리 써 둔 답: 기다림 없이 바로 보여 주고 읽어 준다
    void ShowAnswer(string text)
    {
        ticket++; waiting = false; // 기다리던 Gemini 답이 있으면 버린다
        answer.text = text + Disclaimer;
        voice?.Say(text + " 현장 선생님이나 의사와 먼저 확인하세요.");
    }

    void Send()
    {
        var text = input.text.Trim();
        if (waiting || text.Length == 0) return;

        waiting = true;
        var my = ++ticket;
        answer.text = "생각하는 중...";
        input.text = "";
        StartCoroutine(Watchdog(my));
        // 설문은 참고만 한다. 손님이 지금 쓴 질문이 우선이고, 질문과 상관없으면 설문 내용은 말하지 않게 한다.
        client.Ask($"[참고: 입장할 때 손님이 불편하다고 고른 자세는 '{DoorSurvey.Summary}'. 아래 질문에서 말한 내용이 항상 우선이며, 질문과 직접 관련이 없으면 이 참고는 언급하지 말 것]\n질문: {text}", reply =>
        {
            if (my != ticket) return; // 이미 시간이 지나 포기했거나 닫은 질문의 답이다
            ticket++; waiting = false;
            if (reply.StartsWith("[오류]")) { answer.text = "지금은 답을 가져오지 못했어요. 위의 질문 버튼을 눌러 보세요."; return; }
            answer.text = reply + Disclaimer;
            voice?.Say(reply + " 현장 선생님이나 의사와 먼저 확인하세요.");
        });
    }

    // 답이 너무 늦으면 계속 "생각하는 중"에 머물지 않고 다시 질문할 수 있게 풀어 준다
    System.Collections.IEnumerator Watchdog(int my)
    {
        yield return new WaitForSecondsRealtime(15f);
        if (my != ticket) yield break;
        ticket++; waiting = false;
        answer.text = "답이 늦어지고 있어요. 위의 질문 버튼을 눌러 보세요.";
        Debug.LogWarning("[TrainerChat] 15초 안에 답이 오지 않았습니다");
    }
}
