using UnityEngine;
using UnityEngine.InputSystem;

// 버튼 가까이에서 키를 누르면 문을 열어 둔 채로 고정하고, 한 번 더 누르면 풀어 준다. 센서는 그대로 동작한다.
[RequireComponent(typeof(Collider))]
public class DoorSwitch : MonoBehaviour
{
    [SerializeField] DoorTrigger door;
    [SerializeField] Renderer lamp;
    [SerializeField] Color offColor = Color.red;
    [SerializeField] Color onColor = Color.green;
    [SerializeField] string playerTag = "Player";
    [SerializeField] string binding = "<Keyboard>/f";

    InputAction press;
    int near;

    void OnEnable()
    {
        // PlayerInput과 따로 쓰려고 이 스크립트가 직접 입력을 만든다
        press = new InputAction("DoorSwitch", InputActionType.Button, binding);
        press.performed += OnPress;
        press.Enable();
        Refresh();
    }

    void OnDisable()
    {
        press.performed -= OnPress;
        press.Dispose();
    }

    void OnPress(InputAction.CallbackContext ctx)
    {
        if (near == 0) return; // 버튼 근처에 있을 때만
        door.ToggleHold();
        Refresh();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) near++;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag)) near = Mathf.Max(0, near - 1);
    }

    void Refresh()
    {
        if (lamp && door) lamp.material.color = door.HoldOpen ? onColor : offColor;
    }
}
