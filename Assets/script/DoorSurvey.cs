using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 문 앞 영역(트리거)에 플레이어가 들어오면 불편한 자세를 묻고, 끝나면 문의 잠금을 푼다.
[RequireComponent(typeof(Collider))]
public class DoorSurvey : MonoBehaviour
{
    [SerializeField] Font font;
    [SerializeField] DoorTrigger door;
    [SerializeField] string playerTag = "Player";
    [SerializeField] string[] options = { "허리를 숙이기 불편해요", "무릎을 굽히기 불편해요", "팔을 들어올리기 불편해요", "오래 서 있기 힘들어요" };

    // 트레이너에게 질문할 때 같이 보낸다.
    public static string Summary { get; private set; } = "없음";

    readonly List<string> picked = new();
    bool done;
    GameObject root;

    void Start() => door.SetLocked(true); // 설문 전에는 문이 열리지 않는다

    void OnTriggerEnter(Collider other)
    {
        if (done || root != null || !other.CompareTag(playerTag)) return;
        Build();
    }

    void Build()
    {
        var canvas = UiKit.MakeCanvas("SurveyCanvas", 90);
        root = canvas.gameObject;
        var t = canvas.transform;
        UiKit.Panel(t, new Vector2(2000, 1200), Vector2.zero, Theme.Dim);
        var card = UiKit.Card(t, new Vector2(1200, 860), Vector2.zero, Theme.Surface);
        UiKit.AccentStripe(card.transform, 1180, 424);
        UiKit.Label(t, font, "어느 자세가 불편하신가요?\n(여러 개 골라도 됩니다)", 44, new Vector2(1100, 160), new Vector2(0, 320));

        var off = Theme.Choice;
        var on = Theme.Accent;
        for (var i = 0; i < options.Length; i++)
        {
            var text = options[i];
            var b = UiKit.RoundedBtn(t, font, "○  " + text, 40, new Vector2(900, 100), new Vector2(0, 170 - i * 120), off);
            var label = b.GetComponentInChildren<Text>();
            b.onClick.AddListener(() =>
            {
                if (!picked.Remove(text)) picked.Add(text);
                var on_ = picked.Contains(text);
                b.targetGraphic.color = on_ ? on : off;
                label.text = (on_ ? "✓  " : "○  ") + text; // 색만이 아니라 표시로도 고른 것을 보여 준다
            });
        }
        var go = UiKit.RoundedBtn(t, font, "선택 완료 ▶", 42, new Vector2(700, 110), new Vector2(0, -330), Theme.Accent);
        go.onClick.AddListener(Finish);
        PlayerFreeze.Push();
    }

    void Finish()
    {
        Summary = picked.Count == 0 ? "없음" : string.Join(", ", picked);
        done = true;
        PlayerFreeze.Pop();
        Destroy(root);
        door.SetLocked(false);
        Debug.Log("[설문] 불편한 자세: " + Summary);
    }
}
