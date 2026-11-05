using UnityEngine;

// 애니메이션 클립이 없는 트레이너가 기구에 앉아 바를 잡게 한다.
// 엉덩이는 seatPoint에 두고, 손은 기구(바)에 붙은 목표를 따라가게 팔·다리를 매 프레임 계산한다.
// 기구의 애니메이션(바 이동)이 곧 트레이너의 동작이 되므로 둘이 어긋나지 않는다.
public class TrainerIK : MonoBehaviour
{
    [SerializeField] Transform seatPoint;       // 엉덩이를 둘 자리(기구 안에 있는 빈 오브젝트)
    [SerializeField] Transform facing;          // 이 오브젝트의 앞쪽(+Z)을 보고 앉는다
    [SerializeField] Transform[] handTargets;   // 바에 붙은 손 목표 2개
    [SerializeField] Transform[] elbowHints;    // 팔꿈치가 향할 쪽 2개

    Transform hips;
    readonly Transform[] upArm = new Transform[2], loArm = new Transform[2], hand = new Transform[2];
    readonly Transform[] upLeg = new Transform[2], loLeg = new Transform[2], foot = new Transform[2];
    Quaternion[] footRel = new Quaternion[2];
    float footHeight;
    bool ready;

    // 손: 처음(T자) 자세의 방향을 기억해 두었다가 '바를 쥔 모양'으로 돌린다
    readonly Quaternion[] handRel = new Quaternion[2], handB0Rel = new Quaternion[2];
    readonly Transform[][] finger = new Transform[2][];
    readonly Quaternion[][] fingerRest = new Quaternion[2][];
    readonly Vector3[][] fingerAxis = new Vector3[2][];
    readonly float[][] fingerCurl = new float[2][];
    static readonly string[] fingerNames = { "Index", "Middle", "Ring", "Little", "Thumb" };

    static Transform Find(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
        return null;
    }

    void CacheHand(int i, string side)
    {
        var mid = Find(transform, side + "HandMiddle2");
        var fd = (mid.position - hand[i].position).normalized;          // 손가락이 뻗은 방향
        var palmDown = Vector3.ProjectOnPlane(Vector3.down, fd).normalized; // T자 자세에서 손바닥이 향한 쪽
        var r0 = transform.rotation;
        handRel[i] = Quaternion.Inverse(r0) * hand[i].rotation;
        handB0Rel[i] = Quaternion.Inverse(r0) * Quaternion.LookRotation(fd, palmDown);
        var list = new System.Collections.Generic.List<Transform>();
        var rest = new System.Collections.Generic.List<Quaternion>();
        var axis = new System.Collections.Generic.List<Vector3>();
        var curl = new System.Collections.Generic.List<float>();
        foreach (var fn in fingerNames)
            for (var j = 1; j <= 3; j++)
            {
                var b = Find(transform, side + "Hand" + fn + j);
                if (b == null) continue;
                var dir = b.childCount > 0 ? b.GetChild(0).position - b.position : b.position - b.parent.position;
                list.Add(b); rest.Add(b.localRotation);
                axis.Add(Quaternion.Inverse(b.rotation) * Vector3.Cross(dir.normalized, palmDown)); // 손바닥 쪽으로 접는 축
                curl.Add(fn == "Thumb" ? 25f : (j == 1 ? 55f : j == 2 ? 75f : 50f));
            }
        finger[i] = list.ToArray(); fingerRest[i] = rest.ToArray(); fingerAxis[i] = axis.ToArray(); fingerCurl[i] = curl.ToArray();
    }

    // 손바닥이 앞을 보고 손가락이 위(바)를 향하게 돌리고 손가락을 접는다
    void Grip(int i, Vector3 forward)
    {
        hand[i].rotation = Quaternion.LookRotation(Vector3.up, forward) * Quaternion.Inverse(handB0Rel[i]) * handRel[i];
        for (var j = 0; j < finger[i].Length; j++)
            finger[i][j].localRotation = fingerRest[i][j] * Quaternion.AngleAxis(fingerCurl[i][j], fingerAxis[i][j]);
    }

    void OnEnable()
    {
        if (!ready)
        {
            hips = Find(transform, "Hips");
            string[] side = { "Left", "Right" };
            for (var i = 0; i < 2; i++)
            {
                upArm[i] = Find(transform, side[i] + "UpperArm"); loArm[i] = Find(transform, side[i] + "LowerArm"); hand[i] = Find(transform, side[i] + "Hand");
                upLeg[i] = Find(transform, side[i] + "UpperLeg"); loLeg[i] = Find(transform, side[i] + "LowerLeg"); foot[i] = Find(transform, side[i] + "Foot");
                footRel[i] = Quaternion.Inverse(transform.rotation) * foot[i].rotation; // 발바닥이 평평한 처음 방향
                CacheHand(i, side[i]);
            }
            footHeight = foot[0].position.y - transform.position.y;
            ready = true;
        }
        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        transform.rotation = Quaternion.LookRotation(f, Vector3.up); // 트레이너의 앞쪽은 월드 +Z였다
        transform.position = new Vector3(seatPoint.position.x, transform.position.y, seatPoint.position.z);
        Pose();
    }

    void LateUpdate() => Pose();

    void Pose()
    {
        if (!ready) return;
        var f = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, f);
        hips.position = seatPoint.position;

        // 다리: 발은 바닥, 무릎은 앞쪽 위로
        for (var i = 0; i < 2; i++)
        {
            var sideSign = i == 0 ? -1f : 1f; // 왼쪽=-right
            var footPos = hips.position + f * 0.42f + right * (0.10f * sideSign);
            footPos.y = transform.position.y + footHeight;
            Solve(upLeg[i], loLeg[i], foot[i], footPos, hips.position + f * 1f + Vector3.up * 0.6f);
            foot[i].rotation = transform.rotation * footRel[i];
        }

        // 팔: 오른쪽(+right)에 더 가까운 목표를 오른손에 쓴다
        if (handTargets == null || handTargets.Length < 2) return;
        var rightIdx = Vector3.Dot(handTargets[0].position - hips.position, right) > Vector3.Dot(handTargets[1].position - hips.position, right) ? 0 : 1;
        for (var i = 0; i < 2; i++)
        {
            var k = i == 1 ? rightIdx : 1 - rightIdx; // i=1이 오른팔
            Solve(upArm[i], loArm[i], hand[i], handTargets[k].position, elbowHints[k].position);
            Grip(i, f);
        }
    }

    // 두 마디(어깨-팔꿈치-손목)를 목표에 닿게 구부린다. pole 쪽으로 팔꿈치가 꺾인다.
    static void Solve(Transform a, Transform b, Transform c, Vector3 target, Vector3 pole)
    {
        var pa = a.position;
        var l1 = (b.position - pa).magnitude;
        var l2 = (c.position - b.position).magnitude;
        var to = target - pa;
        var d = Mathf.Clamp(to.magnitude, 0.05f, (l1 + l2) * 0.999f);
        var dir = to.normalized;
        var side = Vector3.ProjectOnPlane(pole - pa, dir);
        side = side.sqrMagnitude < 1e-6f ? Vector3.up : side.normalized;
        var ang = Mathf.Acos(Mathf.Clamp((l1 * l1 + d * d - l2 * l2) / (2f * l1 * d), -1f, 1f));
        var newB = pa + (dir * Mathf.Cos(ang) + side * Mathf.Sin(ang)) * l1;
        a.rotation = Quaternion.FromToRotation(b.position - pa, newB - pa) * a.rotation;
        var tip = pa + dir * d;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, tip - b.position) * b.rotation;
    }
}
