using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 기구 가까이에서 E를 누르면 플레이어가 의자에 앉아 직접 운동한다.
// 3인칭 카메라로 앉은 내 모습이 보이고, 화면의 큰 '당기기' 버튼(또는 스페이스)을 누르고 있으면 바가 내려오고 떼면 올라간다.
// 어르신께 무리가 없게 목표는 10회이고, 다 하면 저절로 끝난다.
public class ExerciseStation : MonoBehaviour
{
    [SerializeField] Font font;
    [SerializeField] FPSController player;
    [SerializeField] Animator machine;       // "Pull" 상태가 있는 기구 애니메이션
    [SerializeField] Transform seatPoint;    // 엉덩이 자리
    [SerializeField] Transform facing;       // 이 오브젝트의 앞쪽(+Z)을 보고 앉는다
    [SerializeField] TrainerAppear trainer;
    [SerializeField] string machineName = "랫풀다운";
    [SerializeField] float useRange = 2.2f;
    [SerializeField] float pullSpeed = 1.35f; // 1초에 바가 움직이는 기본 비율
    [SerializeField] PlayerSeatPose pose;   // 앉은 자세와 손 IK
    [SerializeField] float camDistance = 3.2f;

    [SerializeField] float fullPullTime = 0.5f; // 기구 클립에서 바가 가장 내려온 시점(0~1)
    [SerializeField] int targetReps = 10;   // 무리하지 않게 10회

    // 운동 중에는 질문 버튼·T키를 막아 화면 버튼과 겹치지 않게 한다
    public static bool InUse { get; private set; }

    GameObject promptRoot;
    GameObject hudRoot, controlsRoot, gaugeRoot;
    Text hudTitle, hudSubtitle, hudReps;
    RectTransform repsTransform;

    RectTransform gaugeFillRect;
    Image gaugeFillImg;
    Text gaugeStatusText;

    Text floatingText;
    float floatTimer;
    float repPunchTimer;

    RectTransform pullBtnRect;
    Image pullBtnImg;
    readonly Color pullBtnNormalColor = new Color(0.12f, 0.58f, 0.48f, 0.98f);
    readonly Color pullBtnActiveColor = new Color(0.18f, 0.78f, 0.65f, 1f);

    bool holdBtn; // 화면의 '당기기' 버튼을 누르고 있는지
    CharacterController cc;
    bool active, wentDown;
    float rawPull, visualPull;
    int reps;
    Vector3 exitPos;
    float yaw, pitch;
    Quaternion baseRot;
    Camera cam, fpCam;
    Renderer[] headParts;

    void Start()
    {
        InUse = false;
        cc = player.GetComponent<CharacterController>();
        fpCam = player.cameraTransform.GetComponent<Camera>();

        var cg = new GameObject("ExerciseCamera");
        cam = cg.AddComponent<Camera>();
        cam.depth = 100f;
        cam.fieldOfView = 50f;
        cg.SetActive(false);

        var c = UiKit.MakeCanvas("ExerciseCanvas", 35);

        // 1. 접근 프롬프트 (라운드 글래스 필 배지)
        promptRoot = UiKit.RoundedPanel(c.transform, new Vector2(620, 80), new Vector2(0, 100), new Color(0.08f, 0.12f, 0.18f, 0.95f), new Vector2(0.5f, 0f)).gameObject;
        UiKit.Label(promptRoot.transform, font, "▶ [ E ] 키를 눌러 랫풀다운 운동하기", 36, new Vector2(600, 70), Vector2.zero, TextAnchor.MiddleCenter, new Color(0.4f, 0.95f, 0.75f));
        promptRoot.SetActive(false);

        // 2. 상단 헤더 HUD 카드 (세련된 글래스모피즘 + 횟수 강조 대시보드)
        hudRoot = UiKit.RoundedPanel(c.transform, new Vector2(1160, 136), new Vector2(0, -85), new Color(0.07f, 0.10f, 0.16f, 0.94f), new Vector2(0.5f, 1f)).gameObject;
        // 상단 에메랄드 액센트 바
        UiKit.RoundedPanel(hudRoot.transform, new Vector2(1140, 5), new Vector2(0, 63), new Color(0.2f, 0.85f, 0.65f));

        hudTitle = UiKit.Label(hudRoot.transform, font, $"<b>{machineName}</b>  <color=#7ED3B2>· 등 근육 강화</color>", 36, new Vector2(700, 48), new Vector2(-180, 22), TextAnchor.MiddleLeft);
        hudSubtitle = UiKit.Label(hudRoot.transform, font, "무리하지 마시고 편안한 속도로 당겨보세요. 아프면 바로 멈추세요.", 26, new Vector2(700, 40), new Vector2(-180, -22), TextAnchor.MiddleLeft, new Color(0.95f, 0.85f, 0.6f));

        hudReps = UiKit.Label(hudRoot.transform, font, "", 30, new Vector2(360, 100), new Vector2(360, 0), TextAnchor.MiddleRight);
        repsTransform = hudReps.rectTransform;
        hudRoot.SetActive(false);

        // 3. 우측 실시간 당김 게이지 (Tension Gauge)
        gaugeRoot = UiKit.RoundedPanel(c.transform, new Vector2(110, 380), new Vector2(-80, 30), new Color(0.05f, 0.08f, 0.14f, 0.92f), new Vector2(1f, 0.5f)).gameObject;
        UiKit.Label(gaugeRoot.transform, font, "당김 강도", 24, new Vector2(100, 36), new Vector2(0, 162), TextAnchor.MiddleCenter, new Color(0.85f, 0.90f, 0.95f));

        var track = UiKit.RoundedPanel(gaugeRoot.transform, new Vector2(34, 250), new Vector2(0, 12), new Color(0.12f, 0.16f, 0.24f, 0.95f));
        var fillGo = new GameObject("GaugeFill", typeof(RectTransform));
        gaugeFillRect = (RectTransform)fillGo.transform;
        gaugeFillRect.SetParent(track.transform, false);
        gaugeFillRect.anchorMin = new Vector2(0f, 0f);
        gaugeFillRect.anchorMax = new Vector2(1f, 0f);
        gaugeFillRect.pivot = new Vector2(0.5f, 0f);
        gaugeFillRect.sizeDelta = new Vector2(-4f, 0f);
        gaugeFillRect.anchoredPosition = new Vector2(0f, 2f);
        gaugeFillImg = fillGo.AddComponent<Image>();
        gaugeFillImg.sprite = UiKit.GetRoundedSprite();
        gaugeFillImg.type = Image.Type.Sliced;
        gaugeFillImg.color = new Color(0.2f, 0.85f, 0.6f);

        // 95% 완료 목표 가이드 라인
        UiKit.Panel(track.transform, new Vector2(40, 3), new Vector2(0, 250f * 0.95f - 125f), new Color(1f, 0.85f, 0.2f, 0.95f));

        gaugeStatusText = UiKit.Label(gaugeRoot.transform, font, "가슴 쪽으로 당기세요", 22, new Vector2(170, 46), new Vector2(0, -150), TextAnchor.MiddleCenter, new Color(0.6f, 0.85f, 1f));
        gaugeRoot.SetActive(false);

        // 4. 1회 완료 축하 텍스트
        floatingText = UiKit.Label(c.transform, font, "", 52, new Vector2(900, 100), new Vector2(0, 180), TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.35f));
        floatingText.gameObject.SetActive(false);

        // 5. 하단 컨트롤 버튼 (누르고 있으면 당겨지는 캡슐형 카드 버튼)
        controlsRoot = new GameObject("Controls", typeof(RectTransform));
        controlsRoot.transform.SetParent(c.transform, false);
        var cr = (RectTransform)controlsRoot.transform;
        cr.anchorMin = Vector2.zero;
        cr.anchorMax = Vector2.one;
        cr.offsetMin = cr.offsetMax = Vector2.zero;
        var bottom = new Vector2(0.5f, 0f);

        var pullBtn = UiKit.RoundedBtn(controlsRoot.transform, font, "<b>▼ 누르고 있으면 당겨져요</b>\n<size=26><color=#D0FFF0>[ 스페이스 바 또는 길게 누름 ]</color></size>", 44, new Vector2(680, 140), new Vector2(-170, 110), pullBtnNormalColor, bottom);
        pullBtnRect = pullBtn.GetComponent<RectTransform>();
        pullBtnImg = pullBtn.GetComponent<Image>();

        var trig = pullBtn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        void On(UnityEngine.EventSystems.EventTriggerType type, bool value)
        {
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
            e.callback.AddListener(_ => holdBtn = value);
            trig.triggers.Add(e);
        }
        On(UnityEngine.EventSystems.EventTriggerType.PointerDown, true);
        On(UnityEngine.EventSystems.EventTriggerType.PointerUp, false);
        On(UnityEngine.EventSystems.EventTriggerType.PointerExit, false);

        var quitBtn = UiKit.RoundedBtn(controlsRoot.transform, font, "<b>운동 마치기</b>\n<size=26><color=#FFD0C8>[ Esc 키 ]</color></size>", 38, new Vector2(300, 140), new Vector2(340, 110), new Color(0.48f, 0.25f, 0.24f, 0.95f), bottom);
        quitBtn.onClick.AddListener(() => { if (active) End(); });
        controlsRoot.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (!active)
        {
            var near = !PlayerFreeze.Frozen && Flat(player.transform.position - seatPoint.position).magnitude < useRange;
            if (promptRoot.activeSelf != near) promptRoot.SetActive(near);
            if (near && kb.eKey.wasPressedThisFrame) Begin();
            return;
        }

        if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { End(); return; }

        // 마우스 오른쪽 버튼을 누른 채 회전하여 내 자세 감상
        if (mouse.rightButton.isPressed)
        {
            var look = mouse.delta.ReadValue() * 0.15f;
            yaw = Mathf.Clamp(yaw + look.x, -110f, 110f);
            pitch = Mathf.Clamp(pitch - look.y, 0f, 45f);
        }

        bool pulling = (kb.spaceKey.isPressed || holdBtn);
        float target = pulling ? 1f : 0f;

        // 역학적 가속 및 저항 곡선: 당길 때는 끝으로 갈수록 중량 저항이 생기고, 놓을 때는 무게추 탄성으로 복귀
        float speed = pulling ? pullSpeed : (pullSpeed * 1.25f);
        if (pulling && rawPull > 0.65f)
        {
            float resistance = Mathf.Lerp(1f, 0.68f, (rawPull - 0.65f) / 0.35f);
            speed *= resistance;
        }
        rawPull = Mathf.MoveTowards(rawPull, target, speed * Time.deltaTime);

        // 유기적인 부드러움을 주는 SmoothStep 곡선
        visualPull = Mathf.SmoothStep(0f, 1f, rawPull);

        // 기구 및 플레이어 자세 실시간 갱신
        machine.Play("Pull", 0, visualPull * fullPullTime);
        machine.Update(0f);
        if (pose != null) pose.SetPull(visualPull);

        // 1회 완료 판정 (바를 94% 이상 당긴 후 12% 이하로 다시 올렸을 때 카운트)
        if (rawPull >= 0.94f)
        {
            wentDown = true;
        }
        else if (rawPull <= 0.12f && wentDown)
        {
            wentDown = false;
            reps++;
            repPunchTimer = 0.35f;
            floatTimer = 1.6f;
            if (floatingText != null)
            {
                floatingText.text = $"<b>+1회 완료! ({reps}/{targetReps})</b>";
                floatingText.gameObject.SetActive(true);
            }
            OnRepCompleted(reps);
            if (reps >= targetReps) { End(); return; }
        }

        UpdateHUD(pulling);
    }

    void UpdateHUD(bool pulling)
    {
        // 1. 완료 횟수 카운터 텍스트 및 펀치 애니메이션
        hudReps.text = $"<size=24><color=#8FA4B8>완료 횟수</color></size>\n<size=64><color=#3DFFB5><b>{reps}</b></size></color> <size=34><color=#C8D4E0>/ {targetReps}회</color></size>";
        if (repPunchTimer > 0f)
        {
            repPunchTimer -= Time.deltaTime;
            float scale = 1f + Mathf.Sin(Mathf.Clamp01(1f - repPunchTimer / 0.35f) * Mathf.PI) * 0.28f;
            repsTransform.localScale = new Vector3(scale, scale, 1f);
        }
        else
        {
            repsTransform.localScale = Vector3.one;
        }

        // 2. 1회 완료 시 축하 플로팅 텍스트
        if (floatTimer > 0f)
        {
            floatTimer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(floatTimer / 0.4f);
            floatingText.color = new Color(1f, 0.92f, 0.35f, alpha);
            floatingText.rectTransform.anchoredPosition = new Vector2(0f, 180f + (1.6f - floatTimer) * 35f);
            if (floatTimer <= 0f) floatingText.gameObject.SetActive(false);
        }

        // 3. 우측 실시간 당김 게이지 높이 및 상태 문구
        float maxH = 246f;
        gaugeFillRect.sizeDelta = new Vector2(-4f, visualPull * maxH);

        if (rawPull >= 0.94f)
        {
            gaugeFillImg.color = new Color(1f, 0.88f, 0.25f); // 최대 수축 골드 하이라이트
            gaugeStatusText.text = "★ 완료! 천천히 놓으세요";
            gaugeStatusText.color = new Color(1f, 0.9f, 0.3f);
        }
        else if (wentDown)
        {
            gaugeFillImg.color = new Color(0.4f, 0.95f, 0.7f);
            gaugeStatusText.text = "▲ 천천히 팔을 올리세요";
            gaugeStatusText.color = new Color(0.6f, 1f, 0.8f);
        }
        else if (pulling)
        {
            gaugeFillImg.color = Color.Lerp(new Color(0.2f, 0.75f, 1f), new Color(0.2f, 0.95f, 0.6f), rawPull);
            gaugeStatusText.text = "▼ 계속 당겨주세요...";
            gaugeStatusText.color = new Color(0.3f, 0.9f, 0.7f);
        }
        else
        {
            gaugeFillImg.color = new Color(0.2f, 0.7f, 0.95f);
            gaugeStatusText.text = "가슴 쪽으로 당기세요";
            gaugeStatusText.color = new Color(0.7f, 0.85f, 1f);
        }

        // 4. 하단 당기기 버튼 상호작용 피드백
        if (pullBtnRect != null)
        {
            float btnTargetScale = pulling ? 0.96f : 1.0f;
            pullBtnRect.localScale = Vector3.Lerp(pullBtnRect.localScale, new Vector3(btnTargetScale, btnTargetScale, 1f), 15f * Time.deltaTime);
            pullBtnImg.color = pulling ? pullBtnActiveColor : pullBtnNormalColor;
        }
    }

    void OnRepCompleted(int count)
    {
        string coachMent = "";
        switch (count)
        {
            case 1:
                coachMent = "좋아요! 등 근육이 쓰이는 느낌이 드시나요? 아프면 바로 멈추세요.";
                break;
            case 3:
                coachMent = "숨을 내쉬면서 당기고, 마시면서 올라가세요. 힘들면 쉬었다 하셔도 돼요.";
                break;
            case 5:
                coachMent = "벌써 반이에요! 무리하지 마세요. 쉬었다 하셔도 돼요.";
                break;
            case 7:
                coachMent = "잘하고 계세요. 불편하면 바로 멈추셔도 괜찮아요.";
                break;
        }

        if (!string.IsNullOrEmpty(coachMent) && trainer != null)
            trainer.SayCoaching(coachMent);
    }

    void LateUpdate()
    {
        if (!active) return;
        var pivot = seatPoint.position + Vector3.up * 0.72f;
        var rot = baseRot * Quaternion.Euler(-pitch, yaw, 0f);
        cam.transform.position = pivot + rot * new Vector3(0f, 0f, camDistance);
        cam.transform.LookAt(pivot);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    void Begin()
    {
        active = true;
        InUse = true;
        rawPull = 0f;
        visualPull = 0f;
        reps = 0;
        wentDown = false;
        repPunchTimer = 0f;
        floatTimer = 0f;

        promptRoot.SetActive(false);
        hudRoot.SetActive(true);
        gaugeRoot.SetActive(true);
        controlsRoot.SetActive(true);
        holdBtn = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        machine.enabled = true;
        machine.speed = 0f;

        var f = Flat(facing.forward).normalized;
        exitPos = seatPoint.position + f * 1.7f;
        exitPos.y = player.transform.position.y;
        player.enabled = false;
        cc.enabled = false;

        baseRot = Quaternion.LookRotation(f, Vector3.up);
        yaw = -50f;
        pitch = 15f;
        player.transform.SetPositionAndRotation(new Vector3(seatPoint.position.x, player.transform.position.y, seatPoint.position.z), baseRot);

        var an = player.GetComponentInChildren<Animator>();
        an.SetFloat("Speed", 0f);
        an.SetBool("Jump", false);

        var parts = new System.Collections.Generic.List<Renderer>();
        foreach (var r in player.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var n in player.hiddenInFirstPerson)
            {
                if (r.name.Contains(n))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    parts.Add(r);
                    break;
                }
            }
        }
        headParts = parts.ToArray();

        pose.enabled = true;
        pose.SetPull(0f);
        fpCam.enabled = false;
        cam.gameObject.SetActive(true);

        if (trainer != null)
        {
            trainer.StartCoaching(seatPoint, facing, an);
            trainer.SayCoaching($"좋습니다! {machineName} 운동 시작합니다. 바를 가슴 쪽으로 천천히 당겨보세요.");
        }
    }

    void End()
    {
        active = false;
        InUse = false;
        hudRoot.SetActive(false);
        gaugeRoot.SetActive(false);
        controlsRoot.SetActive(false);
        if (floatingText != null) floatingText.gameObject.SetActive(false);
        holdBtn = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        pose.SetPull(0f);
        pose.enabled = false;
        cam.gameObject.SetActive(false);
        fpCam.enabled = true;

        foreach (var r in headParts)
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        player.transform.position = exitPos;
        player.cameraTransform.localRotation = Quaternion.identity;
        cc.enabled = true;
        player.enabled = true;

        machine.Play("Pull", 0, 0f);
        machine.Update(0f);
        machine.speed = 1f;
        machine.enabled = false;

        if (trainer != null)
        {
            trainer.StopCoaching();
            if (reps > 0)
                trainer.SayCoaching(reps >= targetReps
                    ? $"{reps}회 모두 하셨어요! 정말 잘하셨습니다. 이제 편하게 쉬세요. 불편한 곳이 있으면 현장 선생님과 먼저 이야기하세요."
                    : $"{reps}회 하셨어요. 수고하셨습니다! 불편한 곳이 있으면 현장 선생님과 먼저 이야기하세요.");
        }

        if (reps > 0) SessionLog.Add(machineName, reps);
    }
}
