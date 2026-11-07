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
    [SerializeField] float pullSpeed = 1.4f; // 1초에 바가 움직이는 비율
    [SerializeField] PlayerSeatPose pose;   // 앉은 자세와 손 IK
    [SerializeField] float camDistance = 3.2f;

    [SerializeField] float fullPullTime = 0.4f; // 기구 클립에서 바가 가장 내려온 시점(0~1)

    [SerializeField] int targetReps = 10;   // 무리하지 않게 10회

    // 운동 중에는 질문 버튼·T키를 막아 화면 버튼과 겹치지 않게 한다
    public static bool InUse { get; private set; }

    Text prompt, hud;
    GameObject hudRoot, controlsRoot;
    bool holdBtn; // 화면의 '당기기' 버튼을 누르고 있는지
    CharacterController cc;
    bool active, wentDown;
    float pull;
    int reps;
    Vector3 exitPos;
    float yaw, pitch;
    Quaternion baseRot;
    Camera cam, fpCam;
    Renderer[] headParts;

    void Start()
    {
        InUse = false; cc = player.GetComponent<CharacterController>();
        fpCam = player.cameraTransform.GetComponent<Camera>();
        var cg = new GameObject("ExerciseCamera"); cam = cg.AddComponent<Camera>(); cam.depth = 100f; cam.fieldOfView = 50f; cg.SetActive(false);
        var c = UiKit.MakeCanvas("ExerciseCanvas", 30);
        prompt = UiKit.Label(c.transform, font, "E : 랫풀다운 운동하기", 44, new Vector2(900, 90), new Vector2(0, -30)); // 아래 자막 패널 위
        prompt.gameObject.SetActive(false);
        hudRoot = UiKit.Panel(c.transform, new Vector2(1000, 150), new Vector2(0, -90), new Color(0.1f, 0.13f, 0.2f, 0.85f), new Vector2(0.5f, 1f)).gameObject;
        hud = UiKit.Label(hudRoot.transform, font, "", 40, new Vector2(960, 140), Vector2.zero);
        hudRoot.SetActive(false);

        // 키보드를 못 쓰셔도 되게 큰 버튼을 둔다
        controlsRoot = new GameObject("Controls", typeof(RectTransform));
        controlsRoot.transform.SetParent(c.transform, false);
        var cr = (RectTransform)controlsRoot.transform; // 화면 전체로 늘려야 아래 기준 배치가 화면 아래에 붙는다
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
        var bottom = new Vector2(0.5f, 0f);
        var pullBtn = UiKit.Btn(controlsRoot.transform, font, "누르고 있으면 당겨져요", 54, new Vector2(820, 180), new Vector2(-250, 150), new Color(0.15f, 0.55f, 0.45f), bottom);
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
        UiKit.Btn(controlsRoot.transform, font, "그만하기", 54, new Vector2(400, 180), new Vector2(370, 150), new Color(0.6f, 0.3f, 0.25f), bottom).onClick.AddListener(() => { if (active) End(); });
        controlsRoot.SetActive(false);
    }

    void Update()
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (!active)
        {
            var near = !PlayerFreeze.Frozen && Flat(player.transform.position - seatPoint.position).magnitude < useRange;
            prompt.gameObject.SetActive(near);
            if (near && kb.eKey.wasPressedThisFrame) Begin();
            return;
        }

        if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { End(); return; }

        // 마우스 오른쪽 버튼을 누른 채 움직이면 카메라를 돌려 앉은 모습을 여러 각도에서 본다(왼쪽 버튼은 화면 버튼용)
        if (mouse.rightButton.isPressed)
        {
            var look = mouse.delta.ReadValue() * 0.15f;
            yaw = Mathf.Clamp(yaw + look.x, -110f, 110f);
            pitch = Mathf.Clamp(pitch - look.y, 0f, 45f);
        }

        var target = (kb.spaceKey.isPressed || holdBtn) ? 1f : 0f;
        pull = Mathf.MoveTowards(pull, target, pullSpeed * Time.deltaTime);
        machine.Play("Pull", 0, pull * fullPullTime);
        machine.Update(0f);

        if (pull > 0.95f) wentDown = true;
        else if (pull < 0.1f && wentDown)
        {
            wentDown = false;
            reps++;
            OnRepCompleted(reps);
            if (reps >= targetReps) { End(); return; } // 목표만큼 하면 저절로 끝낸다
        }
        hud.text = $"{machineName}   {reps} / {targetReps}회\n아프면 바로 멈추세요. 쉬었다 하셔도 돼요.";
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
            // 목표 횟수를 채우면 End()가 마무리 멘트를 한다
        }

        if (!string.IsNullOrEmpty(coachMent) && trainer != null)
            trainer.SayCoaching(coachMent);
    }

    void LateUpdate()
    {
        if (!active) return;
        var pivot = seatPoint.position + Vector3.up * 0.55f;
        var rot = baseRot * Quaternion.Euler(-pitch, yaw, 0f); // 위쪽에서 내려다본다
        cam.transform.position = pivot + rot * new Vector3(0f, 0f, camDistance);
        cam.transform.LookAt(pivot);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

    void Begin()
    {
        active = true; InUse = true; pull = 0; reps = 0; wentDown = false;
        prompt.gameObject.SetActive(false); hudRoot.SetActive(true);
        controlsRoot.SetActive(true); holdBtn = false;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true; // 화면의 큰 버튼을 누를 수 있게 한다

        machine.enabled = true; machine.speed = 0f;

        var f = Flat(facing.forward).normalized;
        exitPos = seatPoint.position + f * 1.7f; exitPos.y = player.transform.position.y;
        player.enabled = false; cc.enabled = false;
        baseRot = Quaternion.LookRotation(f, Vector3.up); yaw = -50f; pitch = 15f; // 앞쪽 옆에서 비스듬히 본다
        player.transform.SetPositionAndRotation(new Vector3(seatPoint.position.x, player.transform.position.y, seatPoint.position.z), baseRot);
        var an = player.GetComponentInChildren<Animator>(); an.SetFloat("Speed", 0f); an.SetBool("Jump", false);
        // 3인칭이라 1인칭에서 숨겨 둔 머리카락·모자를 다시 보이게 한다
        var parts = new System.Collections.Generic.List<Renderer>();
        foreach (var r in player.GetComponentsInChildren<Renderer>(true)) foreach (var n in player.hiddenInFirstPerson) if (r.name.Contains(n)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; parts.Add(r); break; }
        headParts = parts.ToArray();
        pose.enabled = true;
        fpCam.enabled = false; cam.gameObject.SetActive(true);

        // AI 트레이너 코치 아바타가 플레이어 옆에 서서 코칭을 시작한다
        if (trainer != null)
        {
            trainer.StartCoaching(seatPoint, facing, an);
            trainer.SayCoaching($"좋습니다! {machineName} 운동 시작합니다. 바를 가슴 쪽으로 천천히 당겨보세요.");
        }
    }

    void End()
    {
        active = false; InUse = false; hudRoot.SetActive(false); controlsRoot.SetActive(false); holdBtn = false;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        pose.enabled = false; cam.gameObject.SetActive(false); fpCam.enabled = true;
        foreach (var r in headParts) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; // 다시 1인칭용으로
        player.transform.position = exitPos;
        player.cameraTransform.localRotation = Quaternion.identity;
        cc.enabled = true; player.enabled = true;
        machine.Play("Pull", 0, 0f); machine.Update(0f); machine.speed = 1f; machine.enabled = false;

        // 코치 아바타를 치우고(시범용 트레이너가 다시 나타남) 마무리 멘트를 한다
        if (trainer != null)
        {
            trainer.StopCoaching();
            if (reps > 0)
                trainer.SayCoaching(reps >= targetReps
                    ? $"{reps}회 모두 하셨어요! 정말 잘하셨습니다. 이제 편하게 쉬세요. 불편한 곳이 있으면 현장 선생님과 먼저 이야기하세요."
                    : $"{reps}회 하셨어요. 수고하셨습니다! 불편한 곳이 있으면 현장 선생님과 먼저 이야기하세요.");
        }

        if (reps > 0) SessionLog.Add(machineName, reps); // 나갈 때 요약 카드에 쓴다
    }
}
