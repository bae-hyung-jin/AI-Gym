using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] string playerTag = "Player";

    static readonly int Open = Animator.StringToHash("Open");
    int inside; // 플레이어 콜라이더가 여러 개여도 마지막이 나갈 때만 닫는다

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        inside++;
        animator.SetBool(Open, true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        inside = Mathf.Max(0, inside - 1);
        if (inside == 0) animator.SetBool(Open, false);
    }
}
