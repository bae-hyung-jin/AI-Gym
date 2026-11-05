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
        UiKit.Panel(t, new Vector2(2000, 1200), Vector2.zero, new Color(0, 0, 0, 0.55f));
        UiKit.Panel(t, new Vector2(1200, 860), Vector2.zero, new Color(0.1f, 0.13f, 0.2f, 0.97f));
        UiKit.Label(t, font, "어느 자세가 불편하신가요?\n(여러 개 골라도 됩니다)", 46, new Vector2(1100, 160), new Vector2(0, 320));

        var off = new Color(0.25f, 0.3f, 0.4f);
        var on = new Color(0.15f, 0.55f, 0.45f);
        for (var i = 0; i < options.Length; i++)
        {
            var text = options[i];
            var b = UiKit.Btn(t, font, text, 40, new Vector2(900, 100), new Vector2(0, 170 - i * 120), off);
            b.onClick.AddListener(() =>
            {
                if (!picked.Remove(text)) picked.Add(text);
                b.targetGraphic.color = picked.Contains(text) ? on : off;
            });
        }
        var go = UiKit.Btn(t, font, "다 괜찮아요 / 선택 완료", 42, new Vector2(700, 110), new Vector2(0, -330), new Color(0.2f, 0.45f, 0.75f));
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
