using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastMatch.View
{
    /// <summary>Small helpers for building the legacy uGUI screens in code.</summary>
    public static class UiKit
    {
        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static Canvas MakeCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(540, 960); sc.matchWidthOrHeight = .5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void AnchorTop(RectTransform rt, float height, float l = 0, float r = 0, float t = 0)
        {
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(.5f, 1);
            rt.offsetMin = new Vector2(l, -height - t); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void AnchorBottom(RectTransform rt, float height, float l = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(.5f, 0);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, height + b);
        }

        public static Image Panel(Transform parent, Color bg, string name = "Panel", Sprite sprite = null)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg; img.sprite = sprite; img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            if (sprite != null) img.pixelsPerUnitMultiplier = 1;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Bold, string name = "Label")
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor; t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button MakeButton(Transform parent, string text, Color bg, Color fg, int size, Action onClick, float w, float h, string name = "Button")
        {
            var img = Panel(parent, bg, name, SpriteFactory.Panel9);
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 3f;
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors; colors.highlightedColor = new Color(1, 1, 1, .9f); colors.pressedColor = new Color(.8f, .8f, .8f, 1); colors.disabledColor = new Color(1, 1, 1, .35f); b.colors = colors;
            b.onClick.AddListener(() => onClick?.Invoke());
            var le = img.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h; le.minWidth = w; le.minHeight = h;
            var lbl = Label(img.transform, text, size, fg);
            Stretch(lbl.rectTransform, 4, 4, 2, 2);
            return b;
        }

        public static VerticalLayoutGroup VStack(Transform parent, float spacing, TextAnchor align = TextAnchor.UpperCenter, int pad = 0, string name = "VStack")
        {
            var rt = Rect(parent, name);
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing; g.childAlignment = align; g.padding = new RectOffset(pad, pad, pad, pad);
            g.childControlWidth = true; g.childControlHeight = true; g.childForceExpandWidth = false; g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup HStack(Transform parent, float spacing, TextAnchor align = TextAnchor.MiddleCenter, int pad = 0, string name = "HStack")
        {
            var rt = Rect(parent, name);
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing; g.childAlignment = align; g.padding = new RectOffset(pad, pad, pad, pad);
            g.childControlWidth = true; g.childControlHeight = true; g.childForceExpandWidth = false; g.childForceExpandHeight = false;
            return g;
        }

        public static GridLayoutGroup Grid(Transform parent, Vector2 cell, Vector2 spacing, int cols, string name = "Grid")
        {
            var rt = Rect(parent, name);
            var g = rt.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = cell; g.spacing = spacing; g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols; g.childAlignment = TextAnchor.UpperCenter;
            return g;
        }

        public static LayoutElement Size(Component c, float w, float h)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w; le.preferredHeight = h; le.minHeight = h;
            if (w > 0) le.minWidth = w;
            return le;
        }

        public static ContentSizeFitter Fit(Component c)
        {
            var f = c.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return f;
        }
    }
}
