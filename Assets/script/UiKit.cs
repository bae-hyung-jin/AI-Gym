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
}
