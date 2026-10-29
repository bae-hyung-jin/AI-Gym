using UnityEngine;

public class SlidingDoor : MonoBehaviour
{
    [System.Serializable]
    public class Panel
    {
        public Transform door;          // 움직일 문짝
        public Vector3 openOffset;      // 이 문짝이 열릴 때 이동할 거리
        [HideInInspector] public Vector3 closedPos;
        [HideInInspector] public Vector3 openPos;
    }

    public Panel[] panels;
    public float speed = 2f;
    public string playerTag = "Player";
    public bool autoOpen = true;     // 끄면 스위치로만 열림

    bool isOpen;

    public bool IsOpen => isOpen;
    public void Toggle() { isOpen = !isOpen; }

    void Start()
    {
        foreach (var p in panels)
        {
            p.closedPos = p.door.localPosition;
            p.openPos = p.closedPos + p.openOffset;
        }
    }

    void Update()
    {
        foreach (var p in panels)
        {
            Vector3 target = isOpen ? p.openPos : p.closedPos;
            p.door.localPosition = Vector3.MoveTowards(
                p.door.localPosition, target, speed * Time.deltaTime);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (autoOpen && other.CompareTag(playerTag)) isOpen = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (autoOpen && other.CompareTag(playerTag)) isOpen = false;
    }
}