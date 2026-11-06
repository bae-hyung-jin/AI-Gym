using UnityEngine;
using UnityEngine.UI;

// 플레이어가 기구 가까이 오면 AI 트레이너가 나타나 시범을 보이고, Gemini가 쓴 안내 글을 화면에 띄운다.
public class TrainerAppear : MonoBehaviour
{
    [SerializeField] Font font;
    [SerializeField] GeminiClient client;
    [SerializeField] GameObject trainer;
    [SerializeField] Animator machine;      // 기구 애니메이션(바가 오르내림)
    [SerializeField] string machineName = "랫풀다운";
    [SerializeField] float range = 5f;
    [SerializeField] string playerTag = "Player";

    Transform player;
    GameObject panel;
    Text label;
    string cached;
    bool near, asking, busy;

    // 플레이어가 직접 운동하는 동안에는 트레이너를 숨긴다
    public void SetBusy(bool value) { busy = value; Refresh(); }

    void Start()
    {
        var c = UiKit.MakeCanvas("TrainerSpeechCanvas", 40);
        panel = UiKit.Panel(c.transform, new Vector2(1500, 190), new Vector2(0, 120), new Color(0.1f, 0.13f, 0.2f, 0.92f), new Vector2(0.5f, 0f)).gameObject;
        label = UiKit.Label(panel.transform, font, "", 38, new Vector2(1420, 170), Vector2.zero, TextAnchor.MiddleLeft);
        panel.SetActive(false);
        trainer.SetActive(false);
        machine.enabled = false;
    }

    void Update()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag(playerTag);
            if (p == null) return;
            player = p.transform;
        }
        var d = Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2(trainer.transform.position.x, trainer.transform.position.z));
        dist = d;
        Refresh();
    }

    float dist = 999f;

    void Refresh()
    {
        var now = !busy && dist < range;
        if (now == near) return;
        near = now;
        if (near) Appear(); else Disappear();
    }

    void Appear()
    {
        trainer.SetActive(true);
        machine.enabled = true;
        machine.speed = 1f; // 직접 운동할 때 0으로 멈춰 둔 것을 되돌린다
        machine.Play("Pull", 0, 0f);
        panel.SetActive(true);
        if (cached != null) { label.text = "AI 트레이너: " + cached; return; }
        if (asking) return;
        asking = true;
        label.text = "AI 트레이너: 안녕하세요, 잠시만요...";
        client.Ask($"[참고: 손님이 입장 전에 불편하다고 고른 자세는 '{DoorSurvey.Summary}'. 해당되면 조심하라는 말을 한 마디만 넣을 것]\n" +
                   $"손님이 {machineName} 기구 앞으로 다가왔다. 지금 내가 시범을 보이고 있다고 말하며, 이 운동이 어디에 좋은지와 천천히 하라는 말을 두 문장으로 안내해 줘.", reply =>
        {
            asking = false;
            if (reply.StartsWith("[오류]") || reply.StartsWith("지금 AI")) { label.text = "AI 트레이너: " + reply; return; } // 실패는 저장하지 않아 다음에 다시 시도한다
            cached = reply;
            if (near) label.text = "AI 트레이너: " + reply;
        });
    }

    void Disappear()
    {
        trainer.SetActive(false);
        machine.Rebind(); machine.Update(0f); // 바를 처음 자리로 돌려놓고 멈춘다
        machine.enabled = false;
        panel.SetActive(false);
    }
}
