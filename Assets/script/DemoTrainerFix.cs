using UnityEngine;

// 블렌더에서 제작된 TrainerHuman_Demo(LatPulldown_Demo 클립)의 재생 위상을
// 기구 애니메이션(LatPulldown_Pull)과 정확히 동기화하고, 시범 중 바 높이 및 양손 바 그립 밀착을 유지한다.
public class DemoTrainerFix : MonoBehaviour
{
    Transform facing, barSlide;
    Animator machineAnim, trainerAnim;
    readonly Transform[] upArm = new Transform[2], loArm = new Transform[2], hand = new Transform[2];
    readonly Transform[] barGrips = new Transform[2];
    float lastAppliedBarY = float.NaN;
    bool hasBakedDemo, ready;

    static readonly int DemoStateHash = Animator.StringToHash("LatPulldown_Demo");

    public void Init(Transform machineRoot, Transform seat)
    {
        if (machineRoot == null) return;
        facing = machineRoot;
        machineAnim = machineRoot.GetComponentInChildren<Animator>();
        barSlide = FindBone(machineRoot, "Bar_Slide");
        barGrips[0] = FindBone(machineRoot, "Bar_Grip_L");
        barGrips[1] = FindBone(machineRoot, "Bar_Grip_R");

        trainerAnim = GetComponentInChildren<Animator>();
        hasBakedDemo = trainerAnim != null && trainerAnim.HasState(0, DemoStateHash);

        upArm[0] = FindBone(transform, "LeftArm"); upArm[1] = FindBone(transform, "RightArm");
        loArm[0] = FindBone(transform, "LeftForeArm"); loArm[1] = FindBone(transform, "RightForeArm");
        hand[0] = FindBone(transform, "LeftHand"); hand[1] = FindBone(transform, "RightHand");

        ready = (barSlide != null && barGrips[0] != null && barGrips[1] != null);
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
        if (!ready || facing == null) return;

        // 시범용 바 높이 보정 (-0.11m, Blender 제작 기준치와 일치 / 누적 방지)
        if (!Mathf.Approximately(barSlide.position.y, lastAppliedBarY))
        {
            barSlide.position += Vector3.down * 0.11f;
            lastAppliedBarY = barSlide.position.y;
        }

        // 기구 Pull 클립 진행률과 블렌더 LatPulldown_Demo 애니메이션을 매 프레임 정확히 일치시킴
        if (hasBakedDemo && machineAnim != null && machineAnim.enabled)
        {
            var mState = machineAnim.GetCurrentAnimatorStateInfo(0);
            float t = mState.normalizedTime - Mathf.Floor(mState.normalizedTime);
            trainerAnim.speed = 0f;
            trainerAnim.Play(DemoStateHash, 0, t);
            trainerAnim.Update(0f);
        }

        // 각 팔과 같은 좌/우 방향에 있는 바 손잡이(Bar_Grip)를 찾아 손목 위치를 0mm 밀착 보정
        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);
        for (int i = 0; i < 2; i++)
        {
            if (upArm[i] == null || loArm[i] == null || hand[i] == null) continue;
            float armSide = Vector3.Dot(upArm[i].position - transform.position, right);
            float d0 = Vector3.Dot(barGrips[0].position - transform.position, right);
            float d1 = Vector3.Dot(barGrips[1].position - transform.position, right);
            Transform grip = (armSide >= 0f) ? (d0 >= d1 ? barGrips[0] : barGrips[1]) : (d0 < d1 ? barGrips[0] : barGrips[1]);
            float sideSign = armSide >= 0f ? 1f : -1f; // +1 = 기구 오른쪽, -1 = 기구 왼쪽

            var curWrist = hand[i].position;
            var palmDesired = grip.position + Vector3.up * 0.054f - f * 0.058f - right * (sideSign * 0.016f);
            if ((palmDesired - curWrist).sqrMagnitude > 0.0001f)
            {
                var handRot = hand[i].rotation;
                var pole = loArm[i].position; // 블렌더에서 잡은 자연스러운 팔꿈치 방향 유지
                TrainerIK.Solve(upArm[i], loArm[i], hand[i], palmDesired, pole);
                hand[i].rotation = handRot; // 블렌더에서 만든 손바닥·5손가락 그립 각도 보존
            }
        }
    }
}
