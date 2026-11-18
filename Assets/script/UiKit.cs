using UnityEngine;
using UnityEngine.UI;

// 모든 화면이 같이 쓰는 색과 글자 크기. 색을 바꿀 때는 여기만 고친다.
// 규칙: 행동 버튼은 Accent 한 가지, 멈춤·닫기는 Danger, 고르는 버튼은 Choice, 글자는 FontMin(28) 이상.
public static class Theme
{
    // 어두운 화면(운동 HUD, 질문 창, 안내 팝업, 설문)
    public static readonly Color Dim = new Color(0f, 0f, 0f, 0.60f);            // 팝업 뒤 어둡게
    public static readonly Color Surface = new Color(0.07f, 0.10f, 0.16f, 0.96f); // 카드 배경
    public static readonly Color SurfaceRaised = new Color(0.12f, 0.16f, 0.24f, 0.98f); // 카드 안의 칸, 게이지 홈
    public static readonly Color Choice = new Color(0.20f, 0.26f, 0.36f, 1f);    // 고르는 버튼 (안 고름)

    public static readonly Color Accent = new Color(0.12f, 0.58f, 0.48f, 1f);    // 주 행동 버튼, 고른 상태
    public static readonly Color AccentPressed = new Color(0.18f, 0.78f, 0.65f, 1f);
    public static readonly Color AccentLine = new Color(0.20f, 0.85f, 0.65f, 1f); // 카드 위 띠
    public static readonly Color AccentText = new Color(0.40f, 0.95f, 0.75f, 1f); // 어두운 배경 위 강조 글자

    public static readonly Color Danger = new Color(0.62f, 0.24f, 0.22f, 1f);    // 운동 마치기, 닫기
    public static readonly Color DangerText = new Color(1f, 0.84f, 0.80f, 1f);
    public static readonly Color Info = new Color(0.25f, 0.70f, 0.95f, 1f);      // 준비 상태
    public static readonly Color InfoText = new Color(0.72f, 0.88f, 1f, 1f);
    public static readonly Color Gold = new Color(1f, 0.88f, 0.30f, 1f);         // 목표, 칭찬
    public static readonly Color Caution = new Color(0.98f, 0.86f, 0.62f, 1f);   // 안전 안내 문구

    public static readonly Color Text = Color.white;
    public static readonly Color TextSub = new Color(0.80f, 0.85f, 0.90f, 1f);
    public static readonly Color TextMuted = new Color(0.66f, 0.72f, 0.80f, 1f);  // 어두운 배경에서 대비 4.5:1 이상

    // 밝은 화면(요약 카드: 이미지로 저장해 선생님께 보여 주는 용도)
    public static readonly Color Paper = Color.white;
    public static readonly Color PaperWarm = new Color(0.965f, 0.94f, 0.90f, 1f);
    public static readonly Color PaperAccent = new Color(0.90f, 0.97f, 0.94f, 1f); // Accent를 옅게
    public static readonly Color AccentInk = new Color(0.07f, 0.42f, 0.34f, 1f);   // 밝은 배경 위 강조 글자
    public static readonly Color Ink = new Color(0.10f, 0.10f, 0.10f, 1f);
    public static readonly Color InkSub = new Color(0.25f, 0.25f, 0.25f, 1f);
    public static readonly Color InkMuted = new Color(0.38f, 0.38f, 0.38f, 1f);
    public static readonly Color Neutral = new Color(0.36f, 0.38f, 0.42f, 1f);     // 보조 버튼 (닫기 등)

    // 글자 크기 (1920x1080 기준)
    public const int FontMin = 28;   // 이보다 작게 쓰지 않는다
    public const int FontBody = 34;
    public const int FontButton = 40;
    public const int FontTitle = 48;

    public static Color WithAlpha(Color c, float a) { c.a = a; return c; }
}

// 코드로 UI를 만들 때 쓰는 작은 도구 모음. 글자는 어르신이 읽기 쉽게 크게 쓴다.
public static class UiKit
{
    public static Canvas MakeCanvas(string name, int order)
    {
        var go = new GameObject(name);
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var s = go.AddComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<FitScaler>();
        return c;
    }

    // 가운데 기준으로 크기와 위치를 정하는 사각형
    public static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 pos, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var r = (RectTransform)go.transform;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = anchor;
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        return r;
    }

    public static Image Panel(Transform parent, Vector2 size, Vector2 pos, Color color, Vector2? anchor = null)
    {
        var r = Rect("Panel", parent, size, pos, anchor ?? new Vector2(0.5f, 0.5f));
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text Label(Transform parent, Font font, string text, int size, Vector2 boxSize, Vector2 pos, TextAnchor align = TextAnchor.MiddleCenter, Color? color = null)
    {
        var r = Rect("Text", parent, boxSize, pos, new Vector2(0.5f, 0.5f));
        var t = r.gameObject.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.alignment = align;
        t.color = color ?? Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    public static Button Btn(Transform parent, Font font, string label, int size, Vector2 boxSize, Vector2 pos, Color color, Vector2? anchor = null)
    {
        var r = Rect("Button", parent, boxSize, pos, anchor ?? new Vector2(0.5f, 0.5f));
        var img = r.gameObject.AddComponent<Image>();
        img.color = color;
        var b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        // 누르면 확실히 어두워져서 눌렸다는 것을 알 수 있게 한다
        var cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.90f, 0.90f, 0.90f, 1f);
        cb.selectedColor = Color.white;
        cb.pressedColor = new Color(0.70f, 0.70f, 0.70f, 1f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        var t = Label(r, font, label, size, boxSize, Vector2.zero);
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        return b;
    }

    static Sprite _roundedSprite;
    public static Sprite GetRoundedSprite(int radius = 24, int size = 96)
    {
        if (_roundedSprite != null) return _roundedSprite;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x < r) ? r - x : (x >= size - r ? x - (size - 1 - r) : 0);
                float dy = (y < r) ? r - y : (y >= size - r ? y - (size - 1 - r) : 0);
                if (dx > 0 && dy > 0)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > r)
                        tex.SetPixel(x, y, new Color(1, 1, 1, 0));
                    else
                    {
                        float alpha = Mathf.Clamp01(r - dist + 0.5f);
                        tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                }
                else
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
        }
        tex.Apply();
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return _roundedSprite;
    }

    public static Image RoundedPanel(Transform parent, Vector2 size, Vector2 pos, Color color, Vector2? anchor = null)
    {
        var img = Panel(parent, size, pos, color, anchor);
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        return img;
    }

    // 팝업·HUD 카드: 둥근 판 + 아래로 떨어지는 그림자로 3D 배경과 떨어져 보이게 한다
    public static Image Card(Transform parent, Vector2 size, Vector2 pos, Color color, Vector2? anchor = null)
    {
        var img = RoundedPanel(parent, size, pos, color, anchor);
        var sh = img.gameObject.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.45f);
        sh.effectDistance = new Vector2(0f, -8f);
        return img;
    }

    // 카드 맨 위의 얇은 강조 띠
    public static Image AccentStripe(Transform card, float width, float y)
        => RoundedPanel(card, new Vector2(width, 5), new Vector2(0, y), Theme.AccentLine);

    public static Button RoundedBtn(Transform parent, Font font, string label, int size, Vector2 boxSize, Vector2 pos, Color color, Vector2? anchor = null)
    {
        var b = Btn(parent, font, label, size, boxSize, pos, color, anchor);
        var img = b.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = GetRoundedSprite();
            img.type = Image.Type.Sliced;
        }
        return b;
    }

    public static (Image bg, Image fill, RectTransform fillRect) ProgressBar(Transform parent, Vector2 size, Vector2 pos, Color bgColor, Color fillColor, bool vertical = false, Vector2? anchor = null)
    {
        var root = Rect("ProgressBar", parent, size, pos, anchor ?? new Vector2(0.5f, 0.5f));
        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = GetRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = bgColor;

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        var fillRect = (RectTransform)fillGo.transform;
        fillRect.SetParent(root, false);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = GetRoundedSprite();
        fillImg.type = Image.Type.Sliced;
        fillImg.color = fillColor;

        if (vertical)
        {
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 0f);
            fillRect.pivot = new Vector2(0.5f, 0f);
            fillRect.sizeDelta = new Vector2(-6f, 0f); // 3px margin
            fillRect.anchoredPosition = new Vector2(0f, 3f);
        }
        else
        {
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, -6f);
            fillRect.anchoredPosition = new Vector2(3f, 0f);
        }

        return (bg, fillImg, fillRect);
    }
}

// 창이 16:9보다 좁아도 UI가 화면 밖으로 잘리지 않게, 더 좁은 쪽에 맞춰 크기를 줄인다.
public class FitScaler : MonoBehaviour
{
    CanvasScaler scaler;
    void Awake() => scaler = GetComponent<CanvasScaler>();
    void Update() => scaler.matchWidthOrHeight = Screen.width * 1080f < Screen.height * 1920f ? 0f : 1f;
}
