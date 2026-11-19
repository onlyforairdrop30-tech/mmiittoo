using ProjectInfinity.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectInfinity.UI
{
    /// <summary>
    /// On-screen controls for Android/iOS: virtual stick, look pad and combat buttons. Everything is
    /// written into the same <see cref="InputRouter"/> the keyboard uses, so gameplay code has no
    /// device branches. Hidden on desktop unless <see cref="showOnDesktop"/> is set for testing.
    /// </summary>
    public class TouchControls : MonoBehaviour
    {
        [SerializeField] private InputRouter router;
        [SerializeField] private bool showOnDesktop;
        [SerializeField] private float lookSensitivity = 0.22f;
        [SerializeField] private float stickRadius = 92f;

        public void Bind(InputRouter target) => router = target;

        private void Start()
        {
            if (router == null) router = InputRouter.Instance;

            if (!Application.isMobilePlatform && !showOnDesktop)
            {
                gameObject.SetActive(false);
                return;
            }

            Build();
        }

        private void Build()
        {
            var canvas = UiFactory.CreateCanvas("TouchControls", 5);
            canvas.transform.SetParent(transform, false);

            // Full-screen look pad, behind the HUD canvas so HUD buttons still receive taps first.
            var look = UiFactory.Panel(canvas.transform, "LookPad", new Color(0f, 0f, 0f, 0.001f));
            var lookRect = look.rectTransform;
            lookRect.anchorMin = Vector2.zero;
            lookRect.anchorMax = Vector2.one;
            lookRect.offsetMin = Vector2.zero;
            lookRect.offsetMax = Vector2.zero;
            var lookPad = look.gameObject.AddComponent<LookPad>();
            lookPad.Bind(router, lookSensitivity);

            BuildStick(canvas.transform);
            BuildActionButtons(canvas.transform);
        }

        private void BuildStick(Transform parent)
        {
            var baseImage = UiFactory.Panel(parent, "Stick", new Color(1f, 1f, 1f, 0.16f));
            UiFactory.Place(baseImage.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                            new Vector2(190f, 190f), new Vector2(stickRadius * 2f, stickRadius * 2f));

            var knob = UiFactory.Panel(baseImage.transform, "Knob", new Color(1f, 1f, 1f, 0.38f));
            UiFactory.Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(stickRadius, stickRadius));

            var stick = baseImage.gameObject.AddComponent<VirtualStick>();
            stick.Bind(router, knob.rectTransform, stickRadius);
        }

        private void BuildActionButtons(Transform parent)
        {
            AddButton(parent, "Light", "Strike", new Vector2(-120f, 300f), new Vector2(120f, 120f),
                      () => router.Light.Press(), null);
            AddButton(parent, "Heavy", "Heavy", new Vector2(-250f, 220f), new Vector2(104f, 104f),
                      () => router.Heavy.Press(), null);
            AddButton(parent, "Dodge", "Dodge", new Vector2(-120f, 430f), new Vector2(104f, 104f),
                      () => router.Dodge.Press(), null);
            AddButton(parent, "Sprint", "Sprint", new Vector2(-330f, 330f), new Vector2(96f, 96f),
                      () => router.TouchSprint = true, () => router.TouchSprint = false);
            AddButton(parent, "Block", "Block", new Vector2(-360f, 190f), new Vector2(104f, 104f),
                      () => router.TouchBlock = true, () => router.TouchBlock = false);
        }

        private void AddButton(Transform parent, string name, string label, Vector2 position, Vector2 size,
                               System.Action onDown, System.Action onUp)
        {
            var image = UiFactory.Panel(parent, name, new Color(0.35f, 0.45f, 0.7f, 0.45f));
            UiFactory.Place(image.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), position, size);

            var text = UiFactory.Label(image.transform, "Label", label, 20, TextAnchor.MiddleCenter, Color.white);
            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var button = image.gameObject.AddComponent<HoldButton>();
            button.Bind(onDown, onUp);
        }
    }

    /// <summary>Writes drag/press state into <see cref="InputRouter"/>.</summary>
    internal class LookPad : MonoBehaviour, IDragHandler, IPointerDownHandler
    {
        private InputRouter _router;
        private float _sensitivity = 0.22f;

        public void Bind(InputRouter router, float sensitivity)
        {
            _router = router;
            _sensitivity = sensitivity;
        }

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_router != null) _router.AddLook(eventData.delta * _sensitivity);
        }
    }

    internal class VirtualStick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private InputRouter _router;
        private RectTransform _knob;
        private float _radius = 92f;

        public void Bind(InputRouter router, RectTransform knob, float radius)
        {
            _router = router;
            _knob = knob;
            _radius = radius;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
            if (_knob != null) _knob.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_router == null || _knob == null) return;

            Vector2 center = _knob.parent.position;
            Vector2 offset = Vector2.ClampMagnitude(eventData.position - center, _radius);

            _knob.position = center + offset;
            _router.TouchMove = offset / _radius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_router != null) _router.TouchMove = Vector2.zero;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
        }
    }

    internal class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private System.Action _onDown;
        private System.Action _onUp;

        public void Bind(System.Action onDown, System.Action onUp)
        {
            _onDown = onDown;
            _onUp = onUp;
        }

        public void OnPointerDown(PointerEventData eventData) => _onDown?.Invoke();
        public void OnPointerUp(PointerEventData eventData) => _onUp?.Invoke();
    }
}
