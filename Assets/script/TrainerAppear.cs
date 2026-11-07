using UnityEngine;
using UnityEngine.UI;

// 플레이어가 기구 가까이 오면 AI 트레이너가 나타나 시범을 보이고, Gemini가 쓴 안내 글을 화면에 띄운다.
public class TrainerAppear : MonoBehaviour
{
    [SerializeField] Font font;
    [SerializeField] GeminiClient client;
    [SerializeField] GameObject trainer;
    [SerializeField] Transform anchor;      // 거리를 재는 기준(보통 앉는 자리). 비우면 트레이너 위치
    [SerializeField] TrainerVoice voice;
    [SerializeField] Animator trainerAnim;  // 블렌더에서 만든 트레이너 동작(선택). 기구 애니메이션과 같은 때 처음부터 시작한다
    [SerializeField] Animator machine;      // 기구 애니메이션(바가 오르내림)
    [SerializeField] string machineName = "랫풀다운";
    [SerializeField] float range = 5f;
    [SerializeField] string playerTag = "Player";

    Transform player;
    GameObject panel;
    Text label;
    string cached;
    bool near, asking, busy;
    GameObject coachAvatar; // 플레이어가 운동할 때 옆에 서 있는 코치 아바타(플레이어 모델 복제)

    // 플레이어가 직접 운동하는 동안에는 트레이너를 숨긴다
    public void SetBusy(bool value) { busy = value; Refresh(); }

    // 플레이어가 직접 운동할 때: 기구에 앉은 시범용 트레이너는 숨기고, 서 있는 코치 아바타를 플레이어 옆에 세운다.
    // 시범용 트레이너 모델은 '기구에 앉아 당기는 동작'이 통째로 구워져 있어 서 있게 할 수 없다.
    public void StartCoaching(Transform seat, Transform facing, Animator playerAnim)
    {
        busy = true;
        trainer.SetActive(false);
        if (coachAvatar != null) Destroy(coachAvatar);

        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);
        var pos = seat.position + right * 1.2f + f * 0.4f; // 3인칭 카메라(앞-왼쪽)에서 잘 보이는 플레이어 오른쪽 옆
        pos.y = playerAnim.transform.position.y;

        coachAvatar = Instantiate(playerAnim.gameObject, pos, Quaternion.identity);
        coachAvatar.name = "AI_Trainer_Coach";
        foreach (var ev in coachAvatar.GetComponents<AnimationEventReceiver>()) Destroy(ev);
        // 1인칭에서 가려 둔 머리카락·모자도 보이게 한다
        foreach (var r in coachAvatar.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        var look = Vector3.ProjectOnPlane(seat.position - pos, Vector3.up);
        if (look.sqrMagnitude > 0.001f) coachAvatar.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        var an = coachAvatar.GetComponent<Animator>(); // 서 있는 기본 자세
        an.applyRootMotion = false;
        an.SetFloat("Speed", 0f); an.SetFloat("MotionSpeed", 1f);
        an.SetBool("Grounded", true); an.SetBool("Jump", false); an.SetBool("FreeFall", false);

        panel.SetActive(true);
    }

    public void StopCoaching()
    {
        if (coachAvatar != null) Destroy(coachAvatar);
        busy = false;
        near = false; // 시범용 트레이너가 다시 나타나도록 한다
        Refresh();
    }

    public void SayCoaching(string text)
    {
        label.text = "AI 트레이너: " + text;
        voice?.Say(text);
    }

    void Start()
    {
        var c = UiKit.MakeCanvas("TrainerSpeechCanvas", 40);
        panel = UiKit.Panel(c.transform, new Vector2(1500, 190), new Vector2(0, 120), new Color(0.1f, 0.13f, 0.2f, 0.92f), new Vector2(0.5f, 0f)).gameObject;
        label = UiKit.Label(panel.transform, font, "", 38, new Vector2(1420, 170), Vector2.zero, TextAnchor.MiddleLeft);
        // 창 폭에 맞춰 늘어나게 해서, 창이 좁아도 글이 화면 밖으로 잘리지 않게 한다
        var pr = (RectTransform)panel.transform; pr.anchorMin = new Vector2(0, 0); pr.anchorMax = new Vector2(1, 0); pr.pivot = new Vector2(0.5f, 0); pr.offsetMin = new Vector2(30, 100); pr.offsetMax = new Vector2(-30, 290);
        var lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(24, 8); lr.offsetMax = new Vector2(-24, -8);
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
        if (busy) return; // 코칭 중에는 거리 계산 및 Disappear 방지
        var d = Vector2.Distance(new Vector2(player.position.x, player.position.z), new Vector2((anchor != null ? anchor : trainer.transform).position.x, (anchor != null ? anchor : trainer.transform).position.z));
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
        if (trainerAnim != null) { trainerAnim.Rebind(); trainerAnim.Update(0f); }
        machine.enabled = true;
        machine.speed = 1f; // 직접 운동할 때 0으로 멈춰 둔 것을 되돌린다
        machine.Play("Pull", 0, 0f);
        panel.SetActive(true);
        if (cached != null) { label.text = "AI 트레이너: " + cached; voice?.Say(cached); return; }
        if (asking) return;
        asking = true;
        label.text = "AI 트레이너: 안녕하세요, 잠시만요...";
        client.Ask($"[참고: 손님이 입장 전에 불편하다고 고른 자세는 '{DoorSurvey.Summary}'. 해당되면 조심하라는 말을 한 마디만 넣을 것]\n" +
                   $"손님이 {machineName} 기구 앞으로 다가왔다. 지금 내가 시범을 보이고 있다고 말하며, 이 운동이 어디에 좋은지와 천천히 하라는 말을 두 문장으로 안내해 줘.", reply =>
        {
            asking = false;
            if (reply.StartsWith("[오류]") || reply.StartsWith("지금 AI")) { label.text = "AI 트레이너: " + reply; return; } // 실패는 저장하지 않아 다음에 다시 시도한다
            cached = reply;
            if (near) { label.text = "AI 트레이너: " + reply; voice?.Say(reply); }
        });
    }

    void Disappear()
    {
        voice?.Stop();
        trainer.SetActive(false);
        machine.Rebind(); machine.Update(0f); // 바를 처음 자리로 돌려놓고 멈춘다
        machine.enabled = false;
        panel.SetActive(false);
    }
}
