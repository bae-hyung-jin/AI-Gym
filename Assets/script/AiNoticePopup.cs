using UnityEngine;

// 게임을 시작하면 AI 고지 문구를 띄우고, 확인을 누를 때까지 플레이어를 멈춘다.
public class AiNoticePopup : MonoBehaviour
{
    [SerializeField] Font font;
    [TextArea(3, 8)]
    [SerializeField]
    string message =
        "안내\n\n본 서비스의 AI 가이드는 운동을 돕기 위한 참고용 안내입니다.\nAI의 분석이나 추천을 절대 맹신하지 마시고, 반드시 본인의 오늘 몸 상태와 체력에 맞춰 무리 없이 운동해 주십시오.\n운동 기구를 본격적으로 이용하시기 전에는 반드시 담당 의사나 현장 전문 트레이너의 정확한 소견을 먼저 듣고 안전하게 이용해 주시기를 부탁드립니다.";

    [SerializeField] GameObject root;

    void Start()
    {
        // 게임 플레이 시작 시 팝업 띄우기
        ShowPopup();
    }

    // 인스펙터 컴포넌트 우클릭 메뉴에서 "미리보기 팝업 띄우기" 클릭 가능
    [ContextMenu("미리보기 팝업 띄우기")]
    public void ShowPopup()
    {
        // 이미 띄워져 있으면 중복 생성 방지
        if (root != null)
        {
            ClearPopup();
        }

        var canvas = UiKit.MakeCanvas("AiNoticeCanvas", 100);
        root = canvas.gameObject;

        UiKit.Panel(canvas.transform, new Vector2(2000, 1200), Vector2.zero, new Color(0, 0, 0, 0.55f));
        UiKit.Panel(canvas.transform, new Vector2(1200, 620), Vector2.zero, new Color(0.1f, 0.13f, 0.2f, 0.97f));
        UiKit.Label(canvas.transform, font, message, 42, new Vector2(1100, 400), new Vector2(0, 60));

        var ok = UiKit.Btn(canvas.transform, font, "확인", 44, new Vector2(420, 110), new Vector2(0, -220), new Color(0.15f, 0.55f, 0.45f));
        ok.onClick.AddListener(Close);

        // 플레이 중일 때만 플레이어 조작을 멈춥니다 (에디터 편집 중에는 에러 방지)
        if (Application.isPlaying)
        {
            PlayerFreeze.Push();
        }
    }

    // 인스펙터 컴포넌트 우클릭 메뉴에서 "미리보기 팝업 지우기" 클릭 가능
    [ContextMenu("미리보기 팝업 지우기")]
    public void ClearPopup()
    {
        if (root != null)
        {
            // 에디터 모드에서는 DestroyImmediate를 써야 즉시 지워집니다
            if (Application.isPlaying)
            {
                Destroy(root);
            }
            else
            {
                DestroyImmediate(root);
            }
            root = null;
        }
    }

    void Close()
    {
        if (Application.isPlaying)
        {
            PlayerFreeze.Pop();
        }
        ClearPopup();
    }
}