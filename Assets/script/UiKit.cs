using UnityEngine;
using UnityEngine.UI;

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
