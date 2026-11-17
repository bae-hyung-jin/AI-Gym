using UnityEngine;

// 랫풀다운 기구 뒤편 웨이트 스택(Plate_1~4, Head_Plate, Selector_Pin/Knob)의 잘못된 90도 회전을 바로잡아
// 가이드 봉 위에 바르게 안착시키고, 시범·직접 운동 중 케이블(Cable_Stack) 하단에 맞춰 무게추가 실시간으로 오르내리게 한다.
public class WeightStackSync : MonoBehaviour
{
    Transform weightStack, headPlate, cableStack;
    Renderer headPlateRend, cableStackRend;
    Vector3 restWeightLocalPos;
    float restHeadTopOffsetY;
    float restHeadTopY;
    bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToAllLatMachines()
    {
        foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (tr.name == "Weight_Stack" && tr.parent != null && FindChild(tr.parent, "Cable_Stack") != null)
            {
                if (tr.parent.GetComponent<WeightStackSync>() == null)
                    tr.parent.gameObject.AddComponent<WeightStackSync>();
            }
        }
    }

    static Transform FindChild(Transform root, string n)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == n) return t;
        return null;
    }

    void Awake()
    {
        weightStack = FindChild(transform, "Weight_Stack");
        cableStack = FindChild(transform, "Cable_Stack");
        if (weightStack == null || cableStack == null) return;

        // 1. 90도 꺾여 의자 안으로 삐져나와 있던 가동 무게추 4장 + 상단 헤드 플레이트 + 선택 핀을 뒷타워 가이드 봉 위로 정위치 정렬
        FixMisalignedStackChildren();

        headPlate = FindChild(weightStack, "Head_Plate");
        headPlateRend = headPlate != null ? headPlate.GetComponent<Renderer>() : null;
        cableStackRend = cableStack.GetComponent<Renderer>();

        restWeightLocalPos = weightStack.localPosition;
        if (headPlate != null)
            restHeadTopY = headPlate.TransformPoint(new Vector3(0f, 0.015f, 0f)).y;

        ready = (headPlateRend != null && cableStackRend != null);
    }

    void FixMisalignedStackChildren()
    {
        // Plate_Fixed_1~4 간격이 0.038 (Weight_Stack y=0.240 기준)
        SetStackChild("Plate_1", new Vector3(0f, 0.002f, 0f));
        SetStackChild("Plate_2", new Vector3(0f, 0.040f, 0f));
        SetStackChild("Plate_3", new Vector3(0f, 0.078f, 0f));
        SetStackChild("Plate_4", new Vector3(0f, 0.116f, 0f));
        SetStackChild("Head_Plate", new Vector3(0f, 0.156f, 0f));
        // 무게추 1번 앞면(+Z 로컬)에 선택 핀과 노브 장착
        SetStackChild("Selector_Pin", new Vector3(0f, 0.002f, 0.120f));
        SetStackChild("Selector_Knob", new Vector3(0f, 0.002f, 0.180f));
    }

    void SetStackChild(string childName, Vector3 localPos)
    {
        var ch = weightStack.Find(childName);
        if (ch == null) return;
        ch.localRotation = Quaternion.identity;
        ch.localPosition = localPos;
    }

    void LateUpdate()
    {
        if (!ready) return;

        // Cable_Stack의 현재 하단 Y에 맞춰 Weight_Stack을 수직 상승시킴 (케이블-헤드플레이트 빈틈 0mm 유지)
        float cableBottomY = cableStackRend.bounds.min.y;
        float liftWorld = Mathf.Clamp(cableBottomY - restHeadTopY - 0.004f, 0f, 1.15f);
        float sy = transform.lossyScale.y > 1e-4f ? transform.lossyScale.y : 1f;
        weightStack.localPosition = restWeightLocalPos + Vector3.up * (liftWorld / sy);
    }
}
