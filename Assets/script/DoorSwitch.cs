using UnityEngine;
using UnityEngine.InputSystem;

public class DoorSwitch : MonoBehaviour
{
    public SlidingDoor door;                       // 조작할 문
    public string binding = "<Keyboard>/f";        // 누를 키 (Input System 바인딩 경로)
    public Renderer indicator;                     // 상태 램프 (선택)
    public Color closedColor = Color.red;
    public Color openColor = Color.green;
    public string playerTag = "Player";

    InputAction toggleAction;
    bool playerNear;

    void OnEnable()
    {
        toggleAction = new InputAction("ToggleDoor", InputActionType.Button, binding);
        toggleAction.performed += OnToggle;
        toggleAction.Enable();                     // PlayerInput을 꺼 두었으므로 직접 켬
    }

    void OnDisable()
    {
        toggleAction.performed -= OnToggle;
        toggleAction.Dispose();
    }

    void OnToggle(InputAction.CallbackContext ctx)
    {
        if (playerNear) door.Toggle();
    }

    // UI 버튼의 OnClick 등에서 호출할 수 있음 (범위와 상관없이 토글)
    public void Press() { door.Toggle(); }

    void Update()
    {
        if (indicator != null)
            indicator.material.color = door.IsOpen ? openColor : closedColor;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) playerNear = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) playerNear = false;
    }
}
