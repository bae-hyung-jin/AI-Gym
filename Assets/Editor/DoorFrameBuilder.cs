using System.Text;
using UnityEditor;
using UnityEngine;

// 읽기 전용: DoorNew 아래 오브젝트들의 위치/크기/콜라이더를 파일로 저장한다. 일회용, 끝나면 지워도 됨.
public static class DoorFrameBuilder
{
    const string OutPath = @"C:\Users\hyung\AppData\Local\Temp\claude\G--Unity\0cf7b579-84a2-48f3-9218-4c98dd7e1a31\scratchpad\door_info.txt";

    [MenuItem("Tools/Door/Dump Info")]
    static void Dump()
    {
        var root = GameObject.Find("DoorNew");
        if (root == null) { Debug.LogError("[DoorDump] DoorNew 를 찾지 못함"); return; }

        var sb = new StringBuilder();
        var t = root.transform;
        sb.AppendLine($"DoorNew pos={t.position} rot={t.eulerAngles} scale={t.lossyScale}");
        foreach (var c in root.GetComponents<Component>()) sb.AppendLine($"  component: {c.GetType().Name}");

        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var b = r.bounds;
            var tr = r.transform;
            var col = r.GetComponent<Collider>();
            sb.AppendLine($"{Path(tr, t)} | world min={b.min} max={b.max} size={b.size} | localPos={tr.localPosition} localScale={tr.localScale} | mat={(r.sharedMaterial ? r.sharedMaterial.name : "none")} | collider={(col ? col.GetType().Name + (col.isTrigger ? "(trigger)" : "") : "none")}");
        }
        System.IO.File.WriteAllText(OutPath, sb.ToString());
        Debug.Log("[DoorDump] 저장 완료");
    }

    static string Path(Transform tr, Transform root)
    {
        return AnimationUtility.CalculateTransformPath(tr, root);
    }
}
