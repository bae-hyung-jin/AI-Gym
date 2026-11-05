using UnityEngine;

// 화면에 창이 떠 있는 동안 플레이어를 멈추고 마우스 커서를 풀어 준다. 창마다 Push/Pop을 짝으로 부른다.
public static class PlayerFreeze
{
    static int count;
    public static bool Frozen => count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => count = 0; // 도메인 리로드를 꺼 둬도 플레이마다 초기화

    public static void Push() { count++; Apply(); }
    public static void Pop() { count = Mathf.Max(0, count - 1); Apply(); }

    static void Apply()
    {
        Cursor.lockState = Frozen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = Frozen;
    }
}
