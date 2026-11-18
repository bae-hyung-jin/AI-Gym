using UnityEngine;

// 블렌더에서 제작된 TrainerHuman_Demo(LatPulldown_Demo 클립)의 재생 위상을
// 기구 애니메이션(LatPulldown_Pull)의 실제 바 높이 커브와 1:1 프레임 동기화하고 시범용 바 높이를 맞춘다.
public class DemoTrainerFix : MonoBehaviour
{
    Transform barSlide;
    Animator machineAnim, trainerAnim;
    float lastAppliedBarY = float.NaN;
    bool hasBakedDemo, ready;

    static readonly int DemoStateHash = Animator.StringToHash("LatPulldown_Demo");

    public void Init(Transform machineRoot, Transform seat)
    {
        if (machineRoot == null) return;
        machineAnim = machineRoot.GetComponentInChildren<Animator>();
        barSlide = FindBone(machineRoot, "Bar_Slide");
        trainerAnim = GetComponentInChildren<Animator>();
        hasBakedDemo = trainerAnim != null && trainerAnim.HasState(0, DemoStateHash);
        ready = (barSlide != null);
    }

    static Transform FindBone(Transform root, string n)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == n || t.name.EndsWith(":" + n)) return t;
        return null;
    }

    void LateUpdate()
    {
        if (!ready) return;

        // 시범용 바 높이 보정 (-0.11m, Blender 제작 기준치와 일치 / 누적 방지)
        if (!Mathf.Approximately(barSlide.position.y, lastAppliedBarY))
        {
            barSlide.position += Vector3.down * 0.11f;
            lastAppliedBarY = barSlide.position.y;
        }

        // 기구 Pull 클립 진행률과 블렌더 LatPulldown_Demo 애니메이션을 매 프레임 정확히 일치시킴
        // (Blender에서 추출한 동일 31키 Bar_Slide 곡선으로 제작되어 손목·손가락 굽힘을 덮어쓰지 않고 0mm 밀착)
        if (hasBakedDemo && machineAnim != null && machineAnim.enabled)
        {
            var mState = machineAnim.GetCurrentAnimatorStateInfo(0);
            float t = mState.normalizedTime - Mathf.Floor(mState.normalizedTime);
            trainerAnim.speed = 0f;
            trainerAnim.Play(DemoStateHash, 0, t);
            trainerAnim.Update(0f);
        }
    }
}
