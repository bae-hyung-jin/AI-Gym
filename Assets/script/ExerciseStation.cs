using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 기구 가까이에서 E를 누르면 플레이어가 의자에 앉아 직접 운동한다.
// 3인칭 카메라로 앉은 내 모습이 보이고, 스페이스(또는 마우스 왼쪽)를 누르고 있으면 바가 내려오고 떼면 올라간다.
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

    Text prompt, hud, speech;
    GameObject hudRoot;
    CharacterController cc;
    bool active, wentDown;
    float pull;
    int reps;
    Vector3 exitPos;
    float speechUntil, yaw, pitch;
    Quaternion baseRot;
    Camera cam, fpCam;
    Renderer[] headParts;

    void Start()
    {
        cc = player.GetComponent<CharacterController>();
        fpCam = player.cameraTransform.GetComponent<Camera>();
        var cg = new GameObject("ExerciseCamera"); cam = cg.AddComponent<Camera>(); cam.depth = 100f; cam.fieldOfView = 50f; cg.SetActive(false);
        var c = UiKit.MakeCanvas("ExerciseCanvas", 30);
        prompt = UiKit.Label(c.transform, font, "E : 랫풀다운 운동하기", 44, new Vector2(900, 90), new Vector2(0, -200));
        prompt.gameObject.SetActive(false);
        hudRoot = UiKit.Panel(c.transform, new Vector2(1000, 150), new Vector2(0, -90), new Color(0.1f, 0.13f, 0.2f, 0.85f), new Vector2(0.5f, 1f)).gameObject;
        hud = UiKit.Label(hudRoot.transform, font, "", 40, new Vector2(960, 140), Vector2.zero);
        hudRoot.SetActive(false);
        speech = UiKit.Label(c.transform, font, "", 38, new Vector2(1500, 170), new Vector2(0, 150), TextAnchor.MiddleCenter);
        speech.rectTransform.anchorMin = speech.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        speech.gameObject.AddComponent<Shadow>();
    }

    void Update()
    {
        var kb = Keyboard.current; var mouse = Mouse.current;
        if (kb == null || mouse == null) return;
        if (speech.text != "" && Time.time > speechUntil) speech.text = "";

        if (!active)
        {
            var near = !PlayerFreeze.Frozen && Flat(player.transform.position - seatPoint.position).magnitude < useRange;
            prompt.gameObject.SetActive(near);
            if (near && kb.eKey.wasPressedThisFrame) Begin();
            return;
        }

        if (kb.eKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) { End(); return; }

        // 마우스로 카메라를 돌려 앉은 모습을 여러 각도에서 본다
        var look = mouse.delta.ReadValue() * 0.15f;
        yaw = Mathf.Clamp(yaw + look.x, -110f, 110f);
        pitch = Mathf.Clamp(pitch - look.y, 0f, 45f);

        var target = (kb.spaceKey.isPressed || mouse.leftButton.isPressed) ? 1f : 0f;
        pull = Mathf.MoveTowards(pull, target, pullSpeed * Time.deltaTime);
        machine.Play("Pull", 0, pull * fullPullTime);
        machine.Update(0f);

        if (pull > 0.95f) wentDown = true;
        else if (pull < 0.1f && wentDown) { wentDown = false; reps++; }
        hud.text = $"{machineName}   횟수 {reps}회\n스페이스·마우스 왼쪽: 당기기   E: 그만하기";
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
        active = true; pull = 0; reps = 0; wentDown = false;
        prompt.gameObject.SetActive(false); hudRoot.SetActive(true); speech.text = "";
        trainer.SetBusy(true);
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
    }

    void End()
    {
        active = false; hudRoot.SetActive(false);
        pose.enabled = false; cam.gameObject.SetActive(false); fpCam.enabled = true;
        foreach (var r in headParts) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; // 다시 1인칭용으로
        player.transform.position = exitPos;
        player.cameraTransform.localRotation = Quaternion.identity;
        cc.enabled = true; player.enabled = true;
        machine.Play("Pull", 0, 0f); machine.Update(0f); machine.speed = 1f; machine.enabled = false;
        trainer.SetBusy(false);

        if (reps > 0) SessionLog.Add(machineName, reps); // 나갈 때 요약 카드에 쓴다
    }

    void say(string text, float seconds) { speech.text = text; speechUntil = Time.time + seconds; }
}
