using UnityEngine;

// 플레이어(휴머노이드)를 기구 의자에 앉히고 두 손이 바를 따라가게 한다. 직접 운동하는 동안만 켠다.
// 랫풀다운 당김에 맞춰 상체 흉추(Spine/Chest)를 자연스럽게 뒤로 젖히고 손가락으로 바를 쥐게(Grip) 보정한다.
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

    // 손 회전 및 손가락 그립
    readonly Quaternion[] handRel = new Quaternion[2], handB0Rel = new Quaternion[2];
    readonly Transform[][] finger = new Transform[2][];
    readonly Quaternion[][] fingerRest = new Quaternion[2][];
    readonly Vector3[][] fingerAxis = new Vector3[2][];
    readonly float[][] fingerCurl = new float[2][];
    static readonly string[] fingerNames = { "Index", "Middle", "Ring", "Little", "Thumb" };
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
        var mid = FindBone(animator.transform, side + "HandMiddle2");
        if (mid == null) return;

        var fd = (mid.position - hand[i].position).normalized;
        var palmDown = Vector3.ProjectOnPlane(Vector3.down, fd).normalized;
        var r0 = transform.rotation;
        handRel[i] = Quaternion.Inverse(r0) * hand[i].rotation;
        handB0Rel[i] = Quaternion.Inverse(r0) * Quaternion.LookRotation(fd, palmDown);

        var list = new System.Collections.Generic.List<Transform>();
        var rest = new System.Collections.Generic.List<Quaternion>();
        var axis = new System.Collections.Generic.List<Vector3>();
        var curl = new System.Collections.Generic.List<float>();
        foreach (var fn in fingerNames)
        {
            for (var j = 1; j <= 3; j++)
            {
                var b = FindBone(animator.transform, side + "Hand" + fn + j);
                if (b == null) continue;
                var dir = b.childCount > 0 ? b.GetChild(0).position - b.position : b.position - b.parent.position;
                list.Add(b); rest.Add(b.localRotation);
                axis.Add(Quaternion.Inverse(b.rotation) * Vector3.Cross(dir.normalized, palmDown));
                curl.Add(fn == "Thumb" ? 25f : (j == 1 ? 55f : j == 2 ? 75f : 50f));
            }
        }
        finger[i] = list.ToArray(); fingerRest[i] = rest.ToArray(); fingerAxis[i] = axis.ToArray(); fingerCurl[i] = curl.ToArray();
    }

    void Grip(int i, Vector3 forward)
    {
        if (hand[i] == null) return;
        if (handB0Rel[i] != Quaternion.identity || handRel[i] != Quaternion.identity)
        {
            hand[i].rotation = Quaternion.LookRotation(Vector3.up, forward) * Quaternion.Inverse(handB0Rel[i]) * handRel[i];
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
        hips.position = seatPoint.position;

        // 상체 흉추 신전 (당길 때 상체가 10~13도 뒤로 젖혀지며 가슴이 펴짐)
        float leanAngle = Mathf.Lerp(0f, 12.5f, currentPull);
        if (spine != null) spine.rotation = Quaternion.AngleAxis(leanAngle * 0.45f, -right) * spine.rotation;
        if (chest != null && chest != spine) chest.rotation = Quaternion.AngleAxis(leanAngle * 0.55f, -right) * chest.rotation;
        // 머리는 시선을 거울/바 쪽으로 유지
        if (head != null) head.rotation = Quaternion.AngleAxis(leanAngle * 0.65f, right) * head.rotation;

        for (var i = 0; i < 2; i++) // 다리
        {
            var footPos = hips.position + f * 0.45f + right * (i == 0 ? -0.12f : 0.12f);
            footPos.y = transform.position.y + footHeight;
            TrainerIK.Solve(upLeg[i], loLeg[i], foot[i], footPos, hips.position + f + Vector3.up * 0.6f);
            foot[i].rotation = transform.rotation * footRel[i];
        }

        // 팔 및 손
        if (handTargets != null && handTargets.Length >= 2)
        {
            var rightIdx = Vector3.Dot(handTargets[0].position - hips.position, right) > Vector3.Dot(handTargets[1].position - hips.position, right) ? 0 : 1;
            for (var i = 0; i < 2; i++)
            {
                var k = i == 1 ? rightIdx : 1 - rightIdx;
                TrainerIK.Solve(upArm[i], loArm[i], hand[i], handTargets[k].position, elbowHints[k].position);
                Grip(i, f);
            }
        }
    }
}
