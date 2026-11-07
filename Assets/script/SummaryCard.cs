using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 헬스장 문으로 나가면 Gemini가 네 칸(잘 맞아요 / 선생님께 질문 / 다음에 해 보세요 / 선생님과 먼저 확인)으로 정리한 카드를 보여 준다.
// 카드는 기기에 저장되고(C키로 다시 보기), 이미지로 내려받아 다음 방문 때 선생님께 보여 줄 수 있다.
// 이번 방문에서 한 운동 기록
public static class SessionLog
{
    static readonly System.Collections.Generic.Dictionary<string, int> reps = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => reps.Clear();

    public static void Add(string machine, int n) => reps[machine] = (reps.TryGetValue(machine, out var v) ? v : 0) + n;
    public static int Total { get { var t = 0; foreach (var v in reps.Values) t += v; return t; } }
    public static string Summary()
    {
        if (reps.Count == 0) return "직접 해 본 운동 없음";
        var l = new System.Collections.Generic.List<string>();
        foreach (var kv in reps) l.Add(kv.Key + " " + kv.Value + "회");
        return string.Join(", ", l);
    }
}

public class SummaryCard : MonoBehaviour
{
    [SerializeField] float insideZ = -7f;   // 이보다 안쪽(+z)으로 들어갔다가
    [SerializeField] float outsideZ = -11f; // 이보다 바깥(-z)으로 나오면 카드를 띄운다
    Transform player;
    bool wasInside;

    [SerializeField] Font font;
    [SerializeField] Font fontBold;
    [SerializeField] GeminiClient client;

    const string Key = "summary_card";
    [Serializable] class Data { public string date, machine, ok, ask, next, check; public int reps; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void DownloadFile(string name, byte[] data, int size);
#endif

    GameObject root, buttons;
    Text title, sub;
    readonly Text[] body = new Text[4];
    Data current;
    bool open;

    void Start()
    {
        var c = UiKit.MakeCanvas("SummaryCardCanvas", 80);
        root = new GameObject("Card", typeof(RectTransform));
        root.transform.SetParent(c.transform, false);
        var t = root.transform;
        UiKit.Panel(t, new Vector2(2000, 1200), Vector2.zero, new Color(0, 0, 0, 0.6f));
        UiKit.Panel(t, new Vector2(1100, 980), Vector2.zero, Color.white);
        UiKit.Panel(t, new Vector2(1100, 520), new Vector2(0, 230), new Color(0.965f, 0.94f, 0.9f));

        string[] head = { "✓ 잘 맞아요", "? 선생님께 질문", "+ 다음에 해 보세요", "! 선생님과 먼저 확인" };
        for (var i = 0; i < 4; i++)
        {
            var x = i % 2 == 0 ? -235f : 235f; var y = i < 2 ? 345f : 115f;
            var green = i % 2 == 1;
            UiKit.Panel(t, new Vector2(440, 220), new Vector2(x, y), green ? new Color(0.93f, 0.96f, 0.88f) : Color.white);
            UiKit.Label(t, fontBold, head[i], 38, new Vector2(400, 56), new Vector2(x, y + 68), TextAnchor.MiddleLeft, new Color(0.1f, 0.1f, 0.1f));
            body[i] = UiKit.Label(t, font, "", 30, new Vector2(400, 130), new Vector2(x, y - 22), TextAnchor.UpperLeft, new Color(0.25f, 0.25f, 0.25f));
        }
        title = UiKit.Label(t, fontBold, "4칸 요약 카드", 58, new Vector2(960, 80), new Vector2(0, -75), TextAnchor.MiddleLeft, new Color(0.1f, 0.1f, 0.1f));
        sub = UiKit.Label(t, font, "", 32, new Vector2(960, 120), new Vector2(0, -170), TextAnchor.UpperLeft, new Color(0.45f, 0.45f, 0.45f));

        buttons = new GameObject("Buttons", typeof(RectTransform));
        buttons.transform.SetParent(t, false);
        UiKit.Btn(buttons.transform, fontBold, "이미지로 저장", 36, new Vector2(340, 90), new Vector2(-200, -390), new Color(0.12f, 0.4f, 0.3f)).onClick.AddListener(() => StartCoroutine(SaveImage()));
        UiKit.Btn(buttons.transform, fontBold, "닫기", 36, new Vector2(240, 90), new Vector2(150, -390), new Color(0.4f, 0.4f, 0.4f)).onClick.AddListener(Close);
        root.SetActive(false);
    }

    void Update()
    {
        if (root == null) return; // Start가 끝나 카드 UI가 만들어진 뒤부터 동작한다
        if (player == null) { var p = GameObject.FindGameObjectWithTag("Player"); if (p != null) player = p.transform; }
        if (player != null && !open)
        {
            var z = player.position.z;
            if (z > insideZ) wasInside = true;
            else if (z < outsideZ && wasInside) { wasInside = false; Make(SessionLog.Summary(), SessionLog.Total); } // 헬스장을 나왔다
        }
        var kb = Keyboard.current;
        if (kb != null && !open && !PlayerFreeze.Frozen && kb.cKey.wasPressedThisFrame && PlayerPrefs.HasKey(Key))
            Display(JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)));
    }

    // 헬스장을 나올 때 호출한다. Gemini 답이 올 때까지 "정리하는 중"을 보여 준다.
    public void Make(string activity, int reps)
    {
        var d = new Data { date = DateTime.Now.ToString("yyyy.MM.dd"), machine = activity, reps = reps, ok = "정리하는 중...", ask = "정리하는 중...", next = "정리하는 중...", check = "정리하는 중..." };
        Display(d);
        client.Ask($"[손님 정보: 불편한 자세 '{DoorSurvey.Summary}', 한 운동 '{activity}']\n어르신용 운동 체험 요약을 아래 JSON 한 줄만 출력해(설명·코드블록 금지). " +
                   "ok=몸에 잘 맞았을 것 같은 점, ask=현장 선생님께 물어볼 것, next=다음에 해 볼 것, check=선생님과 먼저 확인할 것. 값은 쉬운 한국어 30자 이내.\n" +
                   "{\"ok\":\"\",\"ask\":\"\",\"next\":\"\",\"check\":\"\"}", reply =>
        {
            var a = reply.IndexOf('{'); var b = reply.LastIndexOf('}');
            Data parsed = null;
            if (a >= 0 && b > a) try { parsed = JsonUtility.FromJson<Data>(reply.Substring(a, b - a + 1)); } catch (ArgumentException) { }
            // 못 읽으면 기본 문구. 체험 내용을 잃지 않게 카드는 항상 채운다
            d.ok = Pick(parsed?.ok, activity);
            d.ask = Pick(parsed?.ask, "알맞은 무게와 횟수가 궁금해요");
            d.next = Pick(parsed?.next, "천천히 횟수를 늘려 보세요");
            d.check = Pick(parsed?.check, $"불편한 곳: {DoorSurvey.Summary}");
            Fill(d); // 이미 닫았어도 카드를 다시 띄우지는 않고 저장만 한다
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(d)); PlayerPrefs.Save();
        });
    }

    static string Pick(string v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();

    void Display(Data d)
    {
        if (d == null) return;
        Fill(d);
        if (open) return;
        open = true; root.SetActive(true); PlayerFreeze.Push();
    }

    void Fill(Data d)
    {
        if (d == null || body[0] == null) return; // 저장된 글을 못 읽었거나 UI가 아직 없으면 건너뛴다
        current = d;
        body[0].text = d.ok; body[1].text = d.ask; body[2].text = d.next; body[3].text = d.check;
        sub.text = "체험이 끝나면 다음에 무엇을 할지 네 칸으로 정리해\n현장 선생님께 이어 줍니다.   " + d.date + " · " + d.machine;
    }

    void Close()
    {
        if (!open) return;
        open = false; root.SetActive(false); PlayerFreeze.Pop();
    }

    IEnumerator SaveImage()
    {
        buttons.SetActive(false); // 버튼은 이미지에 안 나오게 한다
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        buttons.SetActive(true);
        var png = tex.EncodeToPNG(); Destroy(tex);
        var name = $"요약카드_{DateTime.Now:yyyyMMdd_HHmm}.png";
#if UNITY_WEBGL && !UNITY_EDITOR
        DownloadFile(name, png, png.Length);
#else
        var path = Path.Combine(Application.persistentDataPath, name);
        File.WriteAllBytes(path, png);
        Debug.Log("[요약카드] 저장: " + path);
#endif
    }
}
