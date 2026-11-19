using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectInfinity.UI
{
    /// <summary>
    /// Builds the interface from code so the prototype has real, working widgets without shipping a
    /// fragile hand-authored scene or depending on font/TMP assets. Replaced by authored prefabs
    /// in the UI polish pass; the hierarchy it produces is the same one those prefabs will use.
    /// </summary>
    public static class UiFactory
    {
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                                    typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text Label(Transform parent, string name, string text, int size,
                                 TextAnchor anchor, Color color)
        {
            var rt = Rect(parent, name);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.alignment = anchor;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string name, string label, Color tint,
                                    UnityAction onClick, int fontSize = 22)
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = tint;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);

            var text = Label(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return button;
        }

        /// <summary>Creates a background bar and returns its background; the fill is the out parameter.</summary>
        public static Image Bar(Transform parent, string name, Color fillColor, out Image fill)
        {
            var background = Panel(parent, name, new Color(0f, 0f, 0f, 0.55f));
            fill = Panel(background.transform, "Fill", fillColor);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            return background;
        }

        public static void SetFill(Image fill, float fraction)
        {
            if (fill == null) return;
            var rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rt.offsetMin = new Vector2(2f, 2f);
            rt.offsetMax = new Vector2(-2f, -2f);
        }
    }
}
