using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] string playerTag = "Player";

    static readonly int Open = Animator.StringToHash("Open");
    int inside; // 플레이어 콜라이더가 여러 개여도 마지막이 나갈 때만 닫는다
    bool holdOpen; // 버튼으로 열어 둔 상태. 켜져 있으면 센서와 상관없이 열려 있다

    public bool HoldOpen => holdOpen;

    public void ToggleHold()
    {
        holdOpen = !holdOpen;
        Apply();
    }

    void Apply() => animator.SetBool(Open, inside > 0 || holdOpen);

    void OnTriggerEnter(Collider other) // 플레이어가 들어오면 열고 나가면 닫는다
    {
        if (!other.CompareTag(playerTag)) return; // 플레이어 태그가 아니라면
        inside++;
        Apply();
    }

    void OnTriggerExit(Collider other) // 플레이어 나갈때
    {
        if (!other.CompareTag(playerTag)) return;
        inside = Mathf.Max(0, inside - 1);
        Apply();
    }
}
