using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] string playerTag = "Player";

    static readonly int Open = Animator.StringToHash("Open");
    int inside; // 플레이어 콜라이더가 여러 개여도 마지막이 나갈 때만 닫는다

    void OnTriggerEnter(Collider other) // 플레이어가 들어오면 열고 나가면 닫는다
    {
        if (!other.CompareTag(playerTag)) return; // 플레이어 태그가 아니라면
        inside++;
        animator.SetBool(Open, true);
    }

    void OnTriggerExit(Collider other) // 플레이어 나갈때
    {
        if (!other.CompareTag(playerTag)) return;
        inside = Mathf.Max(0, inside - 1);
        if (inside == 0) animator.SetBool(Open, false);
    }
}
