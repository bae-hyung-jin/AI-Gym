using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController))]
public class FPSController : MonoBehaviour
{
    public Transform cameraTransform;   // 플레이어 자식 카메라
    public float moveSpeed = 5f;
    public float jumpHeight = 1.2f;
    public float gravity = -20f;
    public float mouseSensitivity = 0.1f;

    // 1인칭 카메라 앞을 가리는 부위. 화면에서는 숨기고 그림자만 남긴다.
    // 머리는 Ch38_Body에 붙어 있어 따로 숨길 수 없다.
    public string[] hiddenInFirstPerson = { "CapDome", "CapBrim", "Hair", "Eyelashes" };

    CharacterController controller;
    Animator animator;
    float pitch;
    float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        animator = GetComponentInChildren<Animator>();
        HideFirstPersonParts();
    }

    void HideFirstPersonParts()
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            foreach (var part in hiddenInFirstPerson)
                if (r.name.Contains(part))
                {
                    r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
                    break;
                }
    }


    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        // 마우스 시점: 좌우는 몸, 위아래는 카메라
        Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;
        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // WASD 이동
        float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
        float z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
        Vector3 move = (transform.right * x + transform.forward * z).normalized * moveSpeed;

        // 점프 + 중력
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (controller.isGrounded && kb.spaceKey.wasPressedThisFrame)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        if (animator != null)
        {
            float speed = new Vector3(move.x, 0f, move.z).magnitude;
            animator.SetFloat("Speed", speed);
            animator.SetFloat("MotionSpeed", 1f);
            animator.SetBool("Grounded", controller.isGrounded);
            animator.SetBool("Jump", !controller.isGrounded && verticalVelocity > 0f);
            animator.SetBool("FreeFall", !controller.isGrounded && verticalVelocity <= 0f);
        }

        // 화면 클릭하면 다시 커서 잠금
        if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ESC로 커서 풀기
        if (kb.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}