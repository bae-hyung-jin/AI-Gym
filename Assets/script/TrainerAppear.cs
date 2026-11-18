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
    [SerializeField] GameObject coachPrefab; // 플레이어가 운동할 때 옆에 서서 코칭할 '서 있는' AI 트레이너 아바타(프리팹/FBX)
    [Header("▼ [고정 대사] 여기에 글을 적으면 AI 대신 이 문구가 나옵니다")]
    [TextArea(3, 6), SerializeField] string customGreetingText; // 비워두면 Gemini AI가 생성, 글을 적으면 고정 출력

    Transform player;
    GameObject panel;
    Text label;
    string cached;
    bool near, asking, busy;
    GameObject coachAvatar; // 운동 중 플레이어 옆에 서 있는 코치 아바타(coachPrefab에서 생성)

    void Awake()
    {
        EnsureCoachPrefab();
    }

    void EnsureCoachPrefab()
    {
        var modern = Resources.Load<GameObject>("TrainerHuman");
        if (modern != null) { coachPrefab = modern; return; }
        if (coachPrefab != null) return;
        coachPrefab = Resources.Load<GameObject>("AI_Trainer");
#if UNITY_EDITOR
        if (coachPrefab == null)
        {
            coachPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gym/AI_Trainer.fbx");
        }
#endif
    }

    // 플레이어가 직접 운동하는 동안에는 트레이너를 숨긴다
    public void SetBusy(bool value) { busy = value; Refresh(); }

    // 플레이어가 직접 운동할 때: 기구에 앉은 시범용 트레이너는 숨기고, coachPrefab 아바타를 플레이어 옆에 세운다.
    // 시범용 트레이너 모델은 '기구에 앉아 당기는 동작'이 통째로 구워져 있어 서 있게 할 수 없다.
    public void StartCoaching(Transform seat, Transform facing, Animator playerAnim)
    {
        busy = true;
        trainer.SetActive(false);
        if (coachAvatar != null) Destroy(coachAvatar);

        EnsureCoachPrefab();

        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);
        var pos = seat.position + right * 1.2f + f * 0.4f; // 3인칭 카메라(앞-왼쪽)에서 잘 보이는 플레이어 오른쪽 옆
        pos.y = playerAnim.transform.position.y;

        if (coachPrefab != null)
        {
            try
            {
                coachAvatar = Instantiate(coachPrefab, pos, Quaternion.identity);
                if (coachAvatar != null)
                {
                    coachAvatar.name = "AI_Trainer_Coach";
                    coachAvatar.SetActive(true);
                    var ca = coachAvatar.GetComponentInChildren<Animator>();
                    if (ca == null || !ca.isHuman || ca.runtimeAnimatorController == null)
                    {
                        if (coachAvatar.GetComponent<CoachStandPose>() == null)
                            coachAvatar.AddComponent<CoachStandPose>(); // 구형 T자 자세 -> 서 있는 자세
                    }
                    else
                    {
                        ca.SetFloat("Speed", 0f);
                        ca.SetBool("Grounded", true);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[TrainerAppear] 아바타 생성 중 오류 발생: {ex}");
            }
        }
        else
        {
            Debug.LogWarning("[TrainerAppear] Coach Prefab(AI_Trainer)을 찾지 못해 자막/음성으로만 코칭합니다.");
        }

        if (coachAvatar != null)
        {
            // 발바닥이 바닥(또는 장판 윗면)에 닿도록 높이를 맞춘다. 모델 기준점이 발보다 낮아 바닥에 묻히던 것을 막는다
            var rs = coachAvatar.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
                var floorY = Mathf.Max(playerAnim.transform.position.y, 0.04f); // 장판 윗면이 약 0.04
                coachAvatar.transform.position += Vector3.up * (floorY - bb.min.y);
            }
        }

        if (coachAvatar != null)
        {
            var look = Vector3.ProjectOnPlane(seat.position - pos, Vector3.up);
            if (look.sqrMagnitude > 0.001f)
                coachAvatar.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

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
        var c = UiKit.MakeCanvas("TrainerSpeechCanvas", 34);
        panel = UiKit.RoundedPanel(c.transform, new Vector2(1140, 76), new Vector2(0, -166), Theme.WithAlpha(Theme.Surface, 0.92f), new Vector2(0.5f, 1f)).gameObject;
        var pr = (RectTransform)panel.transform;
        pr.pivot = new Vector2(0.5f, 1f);
        pr.anchoredPosition = new Vector2(0f, -166f); // 상단 HUD(하단 y=-152)와 여백을 두고 아래에 깔끔하게 배치
        UiKit.RoundedPanel(panel.transform, new Vector2(6, 52), new Vector2(-546, 0), Theme.AccentLine);
        label = UiKit.Label(panel.transform, font, "", 30, new Vector2(1050, 66), new Vector2(12, 0), TextAnchor.MiddleLeft, Theme.Text);
        panel.SetActive(false);
        // 새 블렌더 시범 전용 아바타(TrainerHuman_Demo: 좌석·허벅지 패드·바 그립 포함 150프레임 애니메이션) 로드
        var modernDemo = Resources.Load<GameObject>("TrainerHuman_Demo") ?? Resources.Load<GameObject>("TrainerHuman");
        if (modernDemo != null && machine != null)
        {
            if (trainer != null) trainer.SetActive(false);
            Transform seat = null;
            foreach (var tr in machine.GetComponentsInChildren<Transform>(true))
                if (tr.name == "TrainerSeat") { seat = tr; break; }
            var f = Vector3.ProjectOnPlane(machine.transform.forward, Vector3.up).normalized;
            var pos = seat != null ? seat.position : machine.transform.position;
            pos.y = machine.transform.position.y; // Blender Z=0 == 기구 바닥 높이(0.004)
            var newTrainer = Instantiate(modernDemo, pos, Quaternion.LookRotation(f, Vector3.up));
            newTrainer.name = "TrainerHuman_Demo";
            var anim = newTrainer.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) { anim.SetFloat("Speed", 0f); anim.SetBool("Grounded", true); }
            newTrainer.AddComponent<DemoTrainerFix>().Init(machine.transform, seat);
            trainer = newTrainer;
            trainerAnim = anim;
        }
        else if (trainer != null && machine != null && trainer.GetComponent<DemoTrainerFix>() == null)
        {
            trainer.AddComponent<DemoTrainerFix>().Init(machine.transform, null);
        }
        if (trainer != null) trainer.SetActive(false);
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
        if (trainerAnim != null)
        {
            trainerAnim.Rebind();
            if (trainerAnim.HasState(0, Animator.StringToHash("LatPulldown_Demo")))
                trainerAnim.Play("LatPulldown_Demo", 0, 0f);
            trainerAnim.Update(0f);
        }
        machine.enabled = true;
        machine.speed = 1f; // 직접 운동할 때 0으로 멈춰 둔 것을 되돌린다
        machine.Play("Pull", 0, 0f);
        panel.SetActive(true);
        if (!string.IsNullOrEmpty(customGreetingText))
        {
            label.text = "AI 트레이너: " + customGreetingText;
            voice?.Say(customGreetingText);
            return;
        }
        if (cached != null) { label.text = "AI 트레이너: " + cached; voice?.Say(cached); return; }
        if (asking) return;
        asking = true;
        label.text = "AI 트레이너: 안녕하세요, 잠시만요...";
        client.Ask($"[참고: 손님이 입장 전에 불편하다고 고른 자세는 '{DoorSurvey.Summary}'. 해당되면 조심하라는 말을 한 마디만 넣을 것]\n" +
                   $"손님이 {machineName} 기구 앞으로 다가왔다. 지금 내가 시범을 보이고 있다고 말하며, 이 운동이 어디에 좋은지와 천천히 하라는 말을 두 문장으로 안내해 줘. 영어 단어나 괄호()는 절대 쓰지 말고 순수 한국어로만 답해.", reply =>
        {
            asking = false;
            if (reply.StartsWith("[오류]"))
            {
                reply = $"반갑습니다! {machineName} 운동 시범을 보여드릴게요. 등을 펴고 천천히 당겨보세요.";
            }
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
