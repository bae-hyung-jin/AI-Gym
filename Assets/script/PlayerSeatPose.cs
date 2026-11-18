using System.Collections.Generic;
using UnityEngine;

// 플레이어(휴머노이드)를 기구 의자에 앉히고 두 손이 바를 따라가게 한다. 직접 운동하는 동안만 켠다.
// 랫풀다운 당김에 맞춰 상체 흉추(Spine/Chest)를 자연스럽게 뒤로 젖히고, 손목/손가락으로 바를 쥐며(Grip), 팔꿈치를 아래로 당긴다.
public class PlayerSeatPose : MonoBehaviour
{
    [SerializeField] Animator animator;      // 휴머노이드 Animator
    [SerializeField] Transform seatPoint;    // 엉덩이 자리
    [SerializeField] Transform facing;       // 이 오브젝트의 앞쪽(+Z)을 보고 앉는다
    [SerializeField] Transform[] handTargets; // 바에 붙은 손 목표 2개
    [SerializeField] Transform[] elbowHints;

    Transform hips, spine, chest, head;
    readonly Transform[] upArm = new Transform[2], loArm = new Transform[2], hand = new Transform[2];
    readonly Transform[] upLeg = new Transform[2], loLeg = new Transform[2], foot = new Transform[2];
    readonly Quaternion[] footRel = new Quaternion[2];
    float footHeight;

    // 손 회전 및 손가락 그립 (현재 자세와 무관하게 뼈 기하 구조로 손바닥 축을 계산)
    readonly Quaternion[] handLocalToGrip = new Quaternion[2];
    readonly bool[] handGripReady = new bool[2];
    readonly Transform[][] finger = new Transform[2][];
    readonly Quaternion[][] fingerRest = new Quaternion[2][];
    readonly Vector3[][] fingerAxis = new Vector3[2][];
    readonly float[][] fingerCurl = new float[2][];
    static readonly string[] fingerNames = { "Index", "Middle", "Ring", "Pinky", "Little", "Thumb" };
    bool handCached;

    float currentPull; // 0 (완전 이완) ~ 1 (최대 수축)

    public void SetPull(float pull) => currentPull = Mathf.Clamp01(pull);

    static Transform FindBone(Transform root, string n)
    {
        if (root == null) return null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == n || t.name.EndsWith(":" + n) || t.name.EndsWith(n))
                return t;
        }
        return null;
    }

    void CacheHand(int i, string side)
    {
        if (hand[i] == null) return;
        var root = animator.transform;
        var mid = FindBone(root, side + "HandMiddle1") ?? FindBone(root, side + "HandMiddle2");
        var idx = FindBone(root, side + "HandIndex1");
        var pnk = FindBone(root, side + "HandPinky1") ?? FindBone(root, side + "HandLittle1");
        if (mid == null || idx == null || pnk == null) return;

        // 자세(Idle / T-pose)에 무관하게 뼈 자체 좌표계로 손가락 뻗는 방향(fd)과 손바닥이 향하는 방향(palmOut) 계산 (Unity 왼손 좌표계 기준)
        var fd = (mid.position - hand[i].position).normalized;
        var across = (idx.position - pnk.position).normalized;
        var palmOut = (i == 0 ? Vector3.Cross(across, fd) : Vector3.Cross(fd, across)).normalized;
        if (palmOut.sqrMagnitude < 1e-5f) return;

        // hand[i].rotation * handLocalToGrip[i] == LookRotation(fd, palmOut) 가 되도록 로컬 오프셋 저장
        var desiredFrame = Quaternion.LookRotation(fd, palmOut);
        handLocalToGrip[i] = Quaternion.Inverse(hand[i].rotation) * desiredFrame;
        handGripReady[i] = true;

        var list = new List<Transform>();
        var rest = new List<Quaternion>();
        var axis = new List<Vector3>();
        var curl = new List<float>();

        foreach (var fn in fingerNames)
        {
            for (var j = 1; j <= 3; j++)
            {
                var b = FindBone(root, side + "Hand" + fn + j);
                if (b == null) continue;
                var dir = b.childCount > 0 ? b.GetChild(0).position - b.position : b.position - b.parent.position;
                if (dir.sqrMagnitude < 1e-6f) continue;

                // 양의 회전 각도일 때 손가락 끝(dir)이 손바닥(palmOut) 쪽으로 접히는 로컬 회전축
                var worldAxis = Vector3.Cross(dir.normalized, palmOut);
                if (worldAxis.sqrMagnitude < 1e-6f) continue;

                list.Add(b);
                rest.Add(b.localRotation);
                axis.Add(Quaternion.Inverse(b.rotation) * worldAxis.normalized);
                curl.Add(fn == "Thumb" ? (j == 1 ? 32f : 44f) : (j == 1 ? 56f : j == 2 ? 68f : 52f));
            }
        }
        finger[i] = list.ToArray();
        fingerRest[i] = rest.ToArray();
        fingerAxis[i] = axis.ToArray();
        fingerCurl[i] = curl.ToArray();
    }

    void Grip(int i, Vector3 forward)
    {
        if (hand[i] == null) return;
        if (handGripReady[i])
        {
            float sideSign = (i == 0 ? -1f : 1f);
            // 진짜 오버핸드 그립: 손등이 위/내 쪽을 향하고, 손가락이 바 위를 넘어 앞(+forward)·아래로 감싸 쥠
            var targetForward = (forward * 0.84f + Vector3.down * 0.54f).normalized;
            var targetPalm = Vector3.ProjectOnPlane((Vector3.down * 0.86f - forward * 0.51f), targetForward).normalized;
            var barTilt = Quaternion.AngleAxis(sideSign * 22f, forward);
            var targetFrame = barTilt * Quaternion.LookRotation(targetForward, targetPalm);
            hand[i].rotation = targetFrame * Quaternion.Inverse(handLocalToGrip[i]);
        }

        if (finger[i] != null)
        {
            for (var j = 0; j < finger[i].Length; j++)
            {
                if (finger[i][j] != null)
                    finger[i][j].localRotation = fingerRest[i][j] * Quaternion.AngleAxis(fingerCurl[i][j], fingerAxis[i][j]);
            }
        }
    }

    void OnEnable()
    {
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
        head = animator.GetBoneTransform(HumanBodyBones.Head);

        upArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); loArm[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); hand[0] = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        upArm[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperArm); loArm[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerArm); hand[1] = animator.GetBoneTransform(HumanBodyBones.RightHand);
        upLeg[0] = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); loLeg[0] = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg); foot[0] = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        upLeg[1] = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg); loLeg[1] = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg); foot[1] = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        for (var i = 0; i < 2; i++) footRel[i] = Quaternion.Inverse(transform.rotation) * foot[i].rotation;
        footHeight = foot[0].position.y - transform.position.y;

        if (!handCached)
        {
            string[] side = { "Left", "Right" };
            for (var i = 0; i < 2; i++) CacheHand(i, side[i]);
            handCached = true;
        }

        currentPull = 0f;
    }

    void LateUpdate()
    {
        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);

        // 골반 위치를 좌석 쿠션 윗면 위에 맞추고(바가 모자/얼굴에 걸리지 않게 살짝 뒤로) 정면을 바라보게 정렬
        hips.position = seatPoint.position + Vector3.up * 0.08f - f * 0.05f;
        hips.rotation = Quaternion.LookRotation(f, Vector3.up);

        // 상체 흉추 신전: 준비 시 5도 -> 당길수록 22도까지 뒤로 젖히며 가슴을 열어 등 근육 수축
        float leanAngle = Mathf.Lerp(5f, 22f, currentPull);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(leanAngle * 0.45f, -right) * spine.rotation;
        if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(leanAngle * 0.55f, -right) * chest.rotation;
        // 머리는 천장을 보지 않도록 시선을 앞쪽(기구/거울)으로 유지
        if (head != null) head.rotation = Quaternion.AngleAxis(leanAngle * 0.72f, right) * head.rotation;

        for (var i = 0; i < 2; i++) // 다리
        {
            var footPos = hips.position + f * 0.45f + right * (i == 0 ? -0.12f : 0.12f);
            footPos.y = transform.position.y + footHeight;
            TrainerIK.Solve(upLeg[i], loLeg[i], foot[i], footPos, hips.position + f + Vector3.up * 0.6f);
            foot[i].rotation = transform.rotation * footRel[i];
        }

        // 팔 및 손: 팔꿈치가 귀 뒤로 꺾이지 않고 옆구리/아래로 내려오도록 동적 폴 타겟 계산
        if (handTargets != null && handTargets.Length >= 2)
        {
            var barRoot = handTargets[0].parent;
            Transform gL = null, gR = null;
            if (barRoot != null)
            {
                gL = FindBone(barRoot, "Bar_Grip_L");
                gR = FindBone(barRoot, "Bar_Grip_R");
            }
            var t0 = gL != null ? gL : handTargets[0];
            var t1 = gR != null ? gR : handTargets[1];
            var useExactFoam = (gL != null && gR != null);

            var rightIdx = Vector3.Dot(t0.position - hips.position, right) > Vector3.Dot(t1.position - hips.position, right) ? 0 : 1;
            var shoulderCenter = hips.position + Vector3.up * 0.45f;
            for (var i = 0; i < 2; i++)
            {
                var k = i == 1 ? rightIdx : 1 - rightIdx;
                var targetTr = (k == 0 ? t0 : t1);
                float sideSign = (i == 0 ? -1f : 1f);
                var dynamicElbowPole = shoulderCenter
                    + right * (0.58f * sideSign)
                    + Vector3.up * Mathf.Lerp(0.10f, -0.45f, currentPull)
                    + f * 0.08f;

                var gripPos = useExactFoam
                    ? targetTr.position + Vector3.up * 0.048f - f * 0.036f
                    : targetTr.position + Vector3.up * 0.145f - f * 0.065f;
                TrainerIK.Solve(upArm[i], loArm[i], hand[i], gripPos, dynamicElbowPole);
                Grip(i, f);
            }
        }
    }
}
