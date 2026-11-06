using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 기구 가까이에서 E를 누르면 플레이어가 의자에 앉아 직접 운동한다.
// 스페이스(또는 마우스 왼쪽)를 누르고 있으면 바가 내려오고, 떼면 올라간다. 끝나면 Gemini가 한마디 해 준다.
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
    [SerializeField] float eyeAboveSeat = 0.73f;

    [SerializeField] float fullPullTime = 0.4f; // 기구 클립에서 바가 가장 내려온 시점(0~1)

    Text prompt, hud, speech;
    GameObject hudRoot;
    CharacterController cc;
    Renderer[] bodyRenderers;
    bool active, wentDown;
    float pull;
    int reps;
    Vector3 exitPos;
    float speechUntil, yaw, pitch;
    Quaternion baseRot;

    void Start()
    {
        cc = player.GetComponent<CharacterController>();
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

        // 앉은 채로 고개만 조금 돌릴 수 있다
        var look = mouse.delta.ReadValue() * 0.1f;
        yaw = Mathf.Clamp(yaw + look.x, -40f, 40f);
        pitch = Mathf.Clamp(pitch - look.y, -30f, 25f);
        player.transform.rotation = baseRot * Quaternion.Euler(0f, yaw, 0f);
        player.cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        var target = (kb.spaceKey.isPressed || mouse.leftButton.isPressed) ? 1f : 0f;
        pull = Mathf.MoveTowards(pull, target, pullSpeed * Time.deltaTime);
        machine.Play("Pull", 0, pull * fullPullTime);
        machine.Update(0f);

        if (pull > 0.95f) wentDown = true;
        else if (pull < 0.1f && wentDown) { wentDown = false; reps++; }
        hud.text = $"{machineName}   횟수 {reps}회\n스페이스·마우스 왼쪽: 당기기   E: 그만하기";
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
        baseRot = Quaternion.LookRotation(f, Vector3.up); yaw = 0f; pitch = 5f;
        player.transform.rotation = baseRot;
        var eye = player.cameraTransform;
        var dy = seatPoint.position.y - 0.07f + eyeAboveSeat - eye.position.y; // 앉은 눈높이로 낮춘다
        player.transform.position = new Vector3(seatPoint.position.x, player.transform.position.y + dy, seatPoint.position.z);
        eye.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        bodyRenderers = player.GetComponentsInChildren<Renderer>();
        foreach (var r in bodyRenderers) r.enabled = false; // 내 몸이 화면을 가리지 않게 한다
    }

    void End()
    {
        active = false; hudRoot.SetActive(false);
        foreach (var r in bodyRenderers) r.enabled = true;
        player.transform.position = exitPos;
        player.cameraTransform.localRotation = Quaternion.identity;
        cc.enabled = true; player.enabled = true;
        machine.Play("Pull", 0, 0f); machine.Update(0f); machine.speed = 1f; machine.enabled = false;
        trainer.SetBusy(false);

        if (reps > 0) SessionLog.Add(machineName, reps); // 나갈 때 요약 카드에 쓴다
    }

    void say(string text, float seconds) { speech.text = text; speechUntil = Time.time + seconds; }
}
