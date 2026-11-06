using UnityEngine;

// 플레이어(휴머노이드)를 기구 의자에 앉히고 두 손이 바를 따라가게 한다. 직접 운동하는 동안만 켠다.
// 켜져 있는 동안 매 프레임 애니메이션 위에 덮어쓰고, 끄면 원래 걷기 자세로 돌아온다.
public class PlayerSeatPose : MonoBehaviour
{
    [SerializeField] Animator animator;      // 휴머노이드 Animator
    [SerializeField] Transform seatPoint;    // 엉덩이 자리
    [SerializeField] Transform facing;       // 이 오브젝트의 앞쪽(+Z)을 보고 앉는다
    [SerializeField] Transform[] handTargets; // 바에 붙은 손 목표 2개
    [SerializeField] Transform[] elbowHints;

    Transform hips;
    readonly Transform[] upArm = new Transform[2], loArm = new Transform[2], hand = new Transform[2];
    readonly Transform[] upLeg = new Transform[2], loLeg = new Transform[2], foot = new Transform[2];
    readonly Quaternion[] footRel = new Quaternion[2];
    float footHeight;

    void OnEnable()
    {
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        upArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); loArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); hand[0] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        upArm[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm); loArm[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm); hand[1] = animator.GetBoneTransform(HumanBodyBones.RightHand);
        upLeg[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); loLeg[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg); foot[0] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        upLeg[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg); loLeg[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg); foot[1] = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        for (var i = 0; i < 2; i++) footRel[i] = Quaternion.Inverse(transform.rotation) * foot[i].rotation; // 서 있을 때 발바닥 방향
        footHeight = foot[0].position.y - transform.position.y;
    }

    void LateUpdate()
    {
        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);
        hips.position = seatPoint.position;

        for (var i = 0; i < 2; i++) // 다리: 발은 바닥에 두고 무릎은 앞쪽 위로
        {
            var footPos = hips.position + f * 0.45f + right * (i == 0 ? -0.12f : 0.12f);
            footPos.y = transform.position.y + footHeight;
            TrainerIK.Solve(upLeg[i], loLeg[i], foot[i], footPos, hips.position + f + Vector3.up * 0.6f);
            foot[i].rotation = transform.rotation * footRel[i];
        }

        // 팔: 오른쪽(+right)에 더 가까운 목표를 오른손에 쓴다
        var rightIdx = Vector3.Dot(handTargets[0].position - hips.position, right) > Vector3.Dot(handTargets[1].position - hips.position, right) ? 0 : 1;
        for (var i = 0; i < 2; i++)
        {
            var k = i == 1 ? rightIdx : 1 - rightIdx;
            TrainerIK.Solve(upArm[i], loArm[i], hand[i], handTargets[k].position, elbowHints[k].position);
        }
    }
}
