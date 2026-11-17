using System.Collections.Generic;
using UnityEngine;

// 시범용 AI 트레이너(Latpulldown_ani)의 베이크된 애니메이션 결함(귀까지 솟은 어깨, 뒤틀린 손목/공중에 뜬 손)을
// 매 프레임 Animator 평가 직후(LateUpdate) 해부학적으로 교정하고 실제 기구 바(HandTarget)에 손을 밀착시킨다.
public class DemoTrainerFix : MonoBehaviour
{
    Transform hips, spine, chest, head;
    Quaternion baseSpineLocal, baseChestLocal, baseHeadLocal;
    readonly Transform[] shoulder = new Transform[2];
    readonly Transform[] upArm = new Transform[2], loArm = new Transform[2], hand = new Transform[2];
    readonly Transform[] handTargets = new Transform[2];
    Transform facing;

    readonly Quaternion[] handLocalToGrip = new Quaternion[2];
    readonly bool[] handGripReady = new bool[2];
    readonly Transform[][] finger = new Transform[2][];
    readonly Quaternion[][] fingerRest = new Quaternion[2][];
    readonly Vector3[][] fingerAxis = new Vector3[2][];
    readonly float[][] fingerCurl = new float[2][];
    static readonly string[] fingerNames = { "Index", "Middle", "Ring", "Pinky", "Little", "Thumb" };
    bool ready;

    public void Init(Transform machineRoot)
    {
        if (machineRoot == null) return;
        facing = machineRoot;
        handTargets[0] = FindBone(machineRoot, "HandTarget_L");
        handTargets[1] = FindBone(machineRoot, "HandTarget_R");

        hips = FindBone(transform, "Hips");
        spine = FindBone(transform, "Spine");
        chest = FindBone(transform, "Chest") ?? spine;
        head = FindBone(transform, "Head");

        if (spine != null) baseSpineLocal = spine.localRotation;
        if (chest != null) baseChestLocal = chest.localRotation;
        if (head != null) baseHeadLocal = head.localRotation;

        string[] side = { "Left", "Right" };
        for (int i = 0; i < 2; i++)
        {
            shoulder[i] = FindBone(transform, side[i] + "Shoulder");
            upArm[i] = FindBone(transform, side[i] + "UpperArm");
            loArm[i] = FindBone(transform, side[i] + "LowerArm");
            hand[i] = FindBone(transform, side[i] + "Hand");
            CacheHand(i, side[i]);
        }
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
                curl.Add(fn == "Thumb" ? (j == 1 ? 15f : 25f) : (j == 1 ? 24f : j == 2 ? 32f : 20f));
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
            var targetForward = (forward * 0.82f + Vector3.up * 0.57f).normalized;
            var targetPalm = Vector3.ProjectOnPlane((Vector3.down * 0.85f - forward * 0.52f), targetForward).normalized;
            // 랫풀다운 바 양쪽 끝의 완만한 기울기에 맞춰 살짝 안쪽으로 정렬
            var barTilt = Quaternion.AngleAxis(sideSign * 10f, forward);
            var targetFrame = barTilt * Quaternion.LookRotation(targetForward, targetPalm);
            hand[i].rotation = targetFrame * Quaternion.Inverse(handLocalToGrip[i]);
        }

        // 초기 로컬 회전(fingerRest) 기준으로 적용해 Animator 정지 시에도 회전 누적 방지
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

        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);

        // 현재 바 높이를 기준으로 당김 진행률(0~1) 추정 (상단 ~1.80m -> 하단 ~1.41m)
        float barY = (handTargets[0].position.y + handTargets[1].position.y) * 0.5f;
        float pull = Mathf.Clamp01(Mathf.InverseLerp(1.80f, 1.41f, barY));

        // 1. 흉추 신전 (초기 로컬 회전 기준으로 매 프레임 적용해 회전 누적 원천 차단)
        if (spine != null) spine.localRotation = baseSpineLocal;
        if (chest != null && chest != spine) chest.localRotation = baseChestLocal;
        if (head != null) head.localRotation = baseHeadLocal;

        float leanAngle = Mathf.Lerp(2f, 14f, pull);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(leanAngle * 0.45f, -right) * spine.rotation;
        if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(leanAngle * 0.55f, -right) * chest.rotation;
        if (head != null) head.rotation = Quaternion.AngleAxis(leanAngle * 0.65f, right) * head.rotation;

        // 2. 귀까지 솟구친 쇄골/어깨(44~55도 으쓱거림)를 해부학적으로 편안한 위치로 내림
        for (int i = 0; i < 2; i++)
        {
            if (shoulder[i] == null || upArm[i] == null) continue;
            float sideSign = (i == 0 ? -1f : 1f);
            var desiredClavDir = (right * sideSign + Vector3.up * Mathf.Lerp(0.24f, -0.05f, pull)).normalized;
            var curClavDir = (upArm[i].position - shoulder[i].position).normalized;
            if (curClavDir.sqrMagnitude > 1e-5f)
                shoulder[i].rotation = Quaternion.FromToRotation(curClavDir, desiredClavDir) * shoulder[i].rotation;
        }

        // 3. 두 손을 실제 바 그립 위치에 정확히 밀착시키고 팔꿈치는 옆구리/아래 방향으로 유도
        var rightIdx = Vector3.Dot(handTargets[0].position - hips.position, right) > Vector3.Dot(handTargets[1].position - hips.position, right) ? 0 : 1;
        var shoulderCenter = (upArm[0].position + upArm[1].position) * 0.5f;
        for (int i = 0; i < 2; i++)
        {
            int k = i == 1 ? rightIdx : 1 - rightIdx;
            float sideSign = (i == 0 ? -1f : 1f);
            var dynamicElbowPole = shoulderCenter
                + right * (0.60f * sideSign)
                + Vector3.up * Mathf.Lerp(0.05f, -0.50f, pull)
                + f * 0.08f;

            var gripPos = handTargets[k].position + Vector3.up * 0.045f + f * 0.015f;
            TrainerIK.Solve(upArm[i], loArm[i], hand[i], gripPos, dynamicElbowPole);
            Grip(i, f);
        }
    }
}
