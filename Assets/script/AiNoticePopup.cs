using UnityEngine;

// 게임을 시작하면 AI 고지 문구를 띄우고, 확인을 누를 때까지 플레이어를 멈춘다.
public class AiNoticePopup : MonoBehaviour
{
    [SerializeField] Font font;
    [TextArea(3, 8)]
    [SerializeField] string message =
        "안내\n\n이 헬스장의 트레이너는 인공지능(AI)입니다.\nAI의 답은 틀릴 수 있고, 의사의 진단이나 처방을 대신하지 않습니다.\n운동 중 통증이 있으면 바로 멈추고 전문가와 상담해 주세요.";

    GameObject root;

    void Start()
    {
        var canvas = UiKit.MakeCanvas("AiNoticeCanvas", 100);
        root = canvas.gameObject;
        UiKit.Panel(canvas.transform, new Vector2(2000, 1200), Vector2.zero, new Color(0, 0, 0, 0.55f));
        UiKit.Panel(canvas.transform, new Vector2(1200, 620), Vector2.zero, new Color(0.1f, 0.13f, 0.2f, 0.97f));
        UiKit.Label(canvas.transform, font, message, 42, new Vector2(1100, 400), new Vector2(0, 60));
        var ok = UiKit.Btn(canvas.transform, font, "확인했어요", 44, new Vector2(420, 110), new Vector2(0, -220), new Color(0.15f, 0.55f, 0.45f));
        ok.onClick.AddListener(Close);
        PlayerFreeze.Push();
    }

    void Close()
    {
        PlayerFreeze.Pop();
        Destroy(root);
    }
}
