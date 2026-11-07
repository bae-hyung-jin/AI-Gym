using UnityEngine;

// 애니메이션이 없는 AI 트레이너 모델(T자 자세)이 팔을 내리고 편하게 서 있게 한다.
// 코치 아바타가 생성될 때 TrainerAppear가 붙인다.
public class CoachStandPose : MonoBehaviour
{
    [SerializeField] float outward = 0.18f; // 팔을 몸에서 살짝 벌리는 정도

    Transform[] upper = new Transform[2];
    Transform[] lower = new Transform[2];
    Transform[] hand = new Transform[2];

    Quaternion[] baseUpperLocal = new Quaternion[2];
    Quaternion[] baseLowerLocal = new Quaternion[2];
    bool inited;

    static Transform Find(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    void Awake() => Init();
    void OnEnable() => Init();

    void Init()
    {
        if (inited) return;

        // Animator가 켜져 있으면 기본 T자 자세와 간섭할 수 있으므로 비활성화
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;

        upper[0] = Find(transform, "LeftUpperArm");
        lower[0] = Find(transform, "LeftLowerArm");
        hand[0]  = Find(transform, "LeftHand");

        upper[1] = Find(transform, "RightUpperArm");
        lower[1] = Find(transform, "RightLowerArm");
        hand[1]  = Find(transform, "RightHand");

        for (var i = 0; i < 2; i++)
        {
            if (upper[i] != null) baseUpperLocal[i] = upper[i].localRotation;
            if (lower[i] != null) baseLowerLocal[i] = lower[i].localRotation;
        }

        inited = (upper[0] != null && upper[1] != null);
    }

    void LateUpdate()
    {
        if (!inited) return;

        for (var i = 0; i < 2; i++)
        {
            if (upper[i] == null || lower[i] == null) continue;

            // 매 프레임 기본 바인드 포즈 로컬 회전에서 출발(오차 누적 방지)
            upper[i].localRotation = baseUpperLocal[i];
            lower[i].localRotation = baseLowerLocal[i];

            var side = (lower[i].position - upper[i].position);
            side.y = 0f;
            side = side.sqrMagnitude > 1e-6f ? side.normalized : (i == 0 ? -transform.right : transform.right);

            var target = (Vector3.down + side * outward).normalized;
            var cur = (lower[i].position - upper[i].position).normalized;
            upper[i].rotation = Quaternion.FromToRotation(cur, target) * upper[i].rotation;

            // 팔꿈치 & 전완: 아래쪽 + 약간 앞쪽으로
            var handT = hand[i] != null ? hand[i] : (lower[i].childCount > 0 ? lower[i].GetChild(0) : null);
            if (handT != null)
            {
                var forearm = (handT.position - lower[i].position).normalized;
                var want = (Vector3.down + transform.forward * 0.15f + side * outward).normalized;
                lower[i].rotation = Quaternion.FromToRotation(forearm, want) * lower[i].rotation;
            }
        }
    }
}
