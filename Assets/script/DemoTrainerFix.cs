using System.Collections.Generic;
using UnityEngine;

// 시범용 AI 트레이너(TrainerHuman / Mixamo Humanoid)를 기구 좌석에 앉히고
// 매 프레임 기구 애니메이션(HandTarget_L/R)에 맞춰 어깨·상체·양팔·15개 손가락 관절을 자연스럽게 동기화한다.
public class DemoTrainerFix : MonoBehaviour
{
    Transform hips, spine, chest, head, seatPoint, facing, barSlide;
    Quaternion baseSpineLocal, baseChestLocal, baseHeadLocal;
    readonly Quaternion[] baseShoulderLocal = new Quaternion[2];
    readonly Transform[] shoulder = new Transform[2];
    readonly Transform[] upArm = new Transform[2], loArm = new Transform[2], hand = new Transform[2];
    readonly Transform[] upLeg = new Transform[2], loLeg = new Transform[2], foot = new Transform[2];
    readonly Quaternion[] footRel = new Quaternion[2];
    float footHeight;
    float lastAppliedBarY = float.NaN;
    readonly Transform[] handTargets = new Transform[2];

    readonly Quaternion[] handLocalToGrip = new Quaternion[2];
    readonly bool[] handGripReady = new bool[2];
    readonly Transform[][] finger = new Transform[2][];
    readonly Quaternion[][] fingerRest = new Quaternion[2][];
    readonly Vector3[][] fingerAxis = new Vector3[2][];
    readonly float[][] fingerCurl = new float[2][];
    static readonly string[] fingerNames = { "Index", "Middle", "Ring", "Pinky", "Little", "Thumb" };
    bool ready;

    public void Init(Transform machineRoot, Transform seat)
    {
        if (machineRoot == null) return;
        facing = machineRoot;
        seatPoint = seat != null ? seat : (FindBone(machineRoot, "TrainerSeat") ?? machineRoot);
        barSlide = FindBone(machineRoot, "Bar_Slide");
        handTargets[0] = FindBone(machineRoot, "HandTarget_L");
        handTargets[1] = FindBone(machineRoot, "HandTarget_R");

        var anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.isHuman)
        {
            hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            spine = anim.GetBoneTransform(HumanBodyBones.Spine);
            chest = anim.GetBoneTransform(HumanBodyBones.Chest) ?? spine;
            head = anim.GetBoneTransform(HumanBodyBones.Head);

            shoulder[0] = anim.GetBoneTransform(HumanBodyBones.LeftShoulder);
            shoulder[1] = anim.GetBoneTransform(HumanBodyBones.RightShoulder);
            upArm[0] = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); loArm[0] = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm); hand[0] = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            upArm[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperArm); loArm[1] = anim.GetBoneTransform(HumanBodyBones.RightLowerArm); hand[1] = anim.GetBoneTransform(HumanBodyBones.RightHand);
            upLeg[0] = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg); loLeg[0] = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg); foot[0] = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            upLeg[1] = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg); loLeg[1] = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg); foot[1] = anim.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        else
        {
            hips = FindBone(transform, "Hips");
            spine = FindBone(transform, "Spine");
            chest = FindBone(transform, "Chest") ?? spine;
            head = FindBone(transform, "Head");
            string[] s = { "Left", "Right" };
            for (int i = 0; i < 2; i++)
            {
                shoulder[i] = FindBone(transform, s[i] + "Shoulder");
                upArm[i] = FindBone(transform, s[i] + "UpperArm");
                loArm[i] = FindBone(transform, s[i] + "LowerArm");
                hand[i] = FindBone(transform, s[i] + "Hand");
            }
        }

        for (int i = 0; i < 2; i++)
        {
            if (foot[i] != null) footRel[i] = Quaternion.Inverse(transform.rotation) * foot[i].rotation;
            if (shoulder[i] != null) baseShoulderLocal[i] = shoulder[i].localRotation;
        }
        if (spine != null) baseSpineLocal = spine.localRotation;
        if (chest != null) baseChestLocal = chest.localRotation;
        if (head != null) baseHeadLocal = head.localRotation;
        if (foot[0] != null) footHeight = foot[0].position.y - transform.position.y;

        string[] side = { "Left", "Right" };
        for (int i = 0; i < 2; i++) CacheHand(i, side[i]);

        ready = (hips != null && upArm[0] != null && upArm[1] != null && handTargets[0] != null && handTargets[1] != null);
    }

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
        var mid = FindBone(transform, side + "HandMiddle1") ?? FindBone(transform, side + "HandMiddle2");
        var idx = FindBone(transform, side + "HandIndex1");
        var pnk = FindBone(transform, side + "HandPinky1") ?? FindBone(transform, side + "HandLittle1");
        if (mid == null || idx == null || pnk == null) return;

        var fd = (mid.position - hand[i].position).normalized;
        var across = (idx.position - pnk.position).normalized;
        var palmOut = (i == 0 ? Vector3.Cross(across, fd) : Vector3.Cross(fd, across)).normalized;
        if (palmOut.sqrMagnitude < 1e-5f) return;

        handLocalToGrip[i] = Quaternion.Inverse(hand[i].rotation) * Quaternion.LookRotation(fd, palmOut);
        handGripReady[i] = true;

        var list = new List<Transform>();
        var rest = new List<Quaternion>();
        var axis = new List<Vector3>();
        var curl = new List<float>();

        foreach (var fn in fingerNames)
        {
            for (int j = 1; j <= 3; j++)
            {
                var b = FindBone(transform, side + "Hand" + fn + j);
                if (b == null) continue;
                var dir = b.childCount > 0 ? b.GetChild(0).position - b.position : b.position - b.parent.position;
                if (dir.sqrMagnitude < 1e-6f) continue;

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
            for (int j = 0; j < finger[i].Length; j++)
            {
                if (finger[i][j] != null)
                    finger[i][j].localRotation = fingerRest[i][j] * Quaternion.AngleAxis(fingerCurl[i][j], fingerAxis[i][j]);
            }
        }
    }

    void LateUpdate()
    {
        if (!ready || facing == null) return;

        // 기구 바가 너무 높아 팔이 닿지 않는 것을 방지하기 위해 시범 중 기본 높이를 1회/프레임당 정확히 -0.11m 보정(누적 방지)
        if (barSlide != null && !Mathf.Approximately(barSlide.position.y, lastAppliedBarY))
        {
            barSlide.position += Vector3.down * 0.11f;
            lastAppliedBarY = barSlide.position.y;
        }

        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);

        // 골반 좌석 안착
        hips.position = seatPoint.position + Vector3.up * 0.08f - f * 0.05f;
        hips.rotation = Quaternion.LookRotation(f, Vector3.up);

        float barY = (handTargets[0].position.y + handTargets[1].position.y) * 0.5f;
        float pull = Mathf.Clamp01(Mathf.InverseLerp(1.69f, 1.30f, barY));

        // 1. 흉추 신전 및 시선 유지 (초기 로컬 회전에서 출발해 누적 방지)
        if (spine != null) spine.localRotation = baseSpineLocal;
        if (chest != null && chest != spine) chest.localRotation = baseChestLocal;
        if (head != null) head.localRotation = baseHeadLocal;
        float leanAngle = Mathf.Lerp(4f, 16f, pull);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(leanAngle * 0.45f, -right) * spine.rotation;
        if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(leanAngle * 0.55f, -right) * chest.rotation;
        if (head != null) head.rotation = Quaternion.AngleAxis(leanAngle * 0.50f, right) * head.rotation;

        // 2. 다리 착석 포즈
        for (int i = 0; i < 2; i++)
        {
            if (upLeg[i] == null || loLeg[i] == null || foot[i] == null) continue;
            var footPos = hips.position + f * 0.45f + right * (i == 0 ? -0.12f : 0.12f);
            footPos.y = transform.position.y + footHeight;
            TrainerIK.Solve(upLeg[i], loLeg[i], foot[i], footPos, hips.position + f + Vector3.up * 0.6f);
            foot[i].rotation = transform.rotation * footRel[i];
        }

        // 3. 쇄골/어깨의 과도한 으쓱거림 방지
        for (int i = 0; i < 2; i++)
        {
            if (shoulder[i] == null || upArm[i] == null) continue;
            shoulder[i].localRotation = baseShoulderLocal[i];
            float sideSign = (i == 0 ? -1f : 1f);
            var desiredClavDir = (right * sideSign + Vector3.up * Mathf.Lerp(0.18f, -0.04f, pull)).normalized;
            var curClavDir = (upArm[i].position - shoulder[i].position).normalized;
            if (curClavDir.sqrMagnitude > 1e-5f)
                shoulder[i].rotation = Quaternion.FromToRotation(curClavDir, desiredClavDir) * shoulder[i].rotation;
        }

        // 4. 두 손을 바 그립 위치에 정확히 밀착
        var rightIdx = Vector3.Dot(handTargets[0].position - hips.position, right) > Vector3.Dot(handTargets[1].position - hips.position, right) ? 0 : 1;
        var shoulderCenter = (upArm[0].position + upArm[1].position) * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            int k = i == 1 ? rightIdx : 1 - rightIdx;
            float sideSign = (i == 0 ? -1f : 1f);
            var dynamicElbowPole = shoulderCenter
                + right * (0.58f * sideSign)
                + Vector3.up * Mathf.Lerp(0.10f, -0.45f, pull)
                + f * 0.08f;

            var gripPos = handTargets[k].position + Vector3.up * 0.056f - f * 0.058f;
            TrainerIK.Solve(upArm[i], loArm[i], hand[i], gripPos, dynamicElbowPole);
            Grip(i, f);
        }
    }
}
