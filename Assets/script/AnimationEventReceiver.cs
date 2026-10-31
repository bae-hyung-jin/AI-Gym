using UnityEngine;

// 애니메이션 이벤트는 Animator와 같은 오브젝트로만 전달되므로 EagleHuman에 붙인다.
// 받는 함수가 없으면 걷고 뛸 때마다 "has no receiver" 오류가 뜬다.
public class AnimationEventReceiver : MonoBehaviour
{
    void OnFootstep(AnimationEvent animationEvent) { }

    void OnLand(AnimationEvent animationEvent) { }
}
