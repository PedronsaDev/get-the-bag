using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PedronsaDev.Console.Components
{
    /// <summary>
    /// Floating right-click context menu for console output items.
    /// Provides 1-click clipboard copying (clean message, full formatted log, stack trace)
    /// with screen boundary clamping, near-mouse toast notification, and auto-dismissal.
    /// </summary>
    public class ConsoleContextMenu : MonoBehaviour
    {
        public static ConsoleContextMenu Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private ConsolePreferences m_Preferences;

        private Canvas _parentCanvas;
        private RectTransform _canvasRect;
        private RectTransform _menuPanelRect;
        private CanvasGroup _menuCanvasGroup;

        // Context Menu Buttons
        private Button _btnCopyMessage;
        private Button _btnCopyFullLog;
        private Button _btnCopyStackTrace;
        private TextMeshProUGUI _copyMessageLabel;
        private TextMeshProUGUI _copyFullLogLabel;
        private TextMeshProUGUI _copyStackTraceLabel;

        // Toast Notification
        private RectTransform _toastRect;
        private CanvasGroup _toastCanvasGroup;
        private TextMeshProUGUI _toastText;
        private Coroutine _toastCoroutine;

        private LogMessageSetup _targetLogSetup;
        private bool _isOpen;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas)
            {
                _canvasRect = _parentCanvas.GetComponent<RectTransform>();
            }

            BuildMenuUI();
            BuildToastUI();
            Hide();
            DismissToastImmediate();
        }

        private void OnEnable()
        {
            Console.OnToggle += HandleConsoleToggle;
        }

        private void OnDisable()
        {
            Console.OnToggle -= HandleConsoleToggle;
            Hide();
            DismissToastImmediate();
        }

        private void OnDestroy()
        {
            Console.OnToggle -= HandleConsoleToggle;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void HandleConsoleToggle(bool isOpen)
        {
            if (!isOpen)
            {
                Hide();
                DismissToastImmediate();
            }
        }

        private void Update()
        {
            if (!_isOpen) return;

            Vector2 mousePos = GetCurrentMousePosition();
            bool mouseClicked = false;

            if (Mouse.current != null)
            {
                mouseClicked = Mouse.current.leftButton.wasPressedThisFrame ||
                               Mouse.current.rightButton.wasPressedThisFrame ||
                               Mouse.current.middleButton.wasPressedThisFrame;
            }

            // Dismiss menu if user clicks anywhere outside the menu panel
            if (mouseClicked)
            {
                if (_menuPanelRect && !RectTransformUtility.RectangleContainsScreenPoint(_menuPanelRect, mousePos))
                {
                    Hide();
                }
            }
            else
            {
                bool escapePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
                if (escapePressed)
                {
                    Hide();
                }
            }
        }

        private Vector2 GetCurrentMousePosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }
            return Input.mousePosition;
        }

        /// <summary>
        /// Displays the context menu anchored to the given screen position for the targeted log message.
        /// </summary>
        public static void Show(Vector2 screenPosition, LogMessageSetup logSetup)
        {
            if (Instance == null && logSetup != null)
            {
                var canvas = logSetup.GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    canvas = FindAnyObjectByType<Canvas>();
                }

                if (canvas != null)
                {
                    var go = new GameObject("ConsoleContextMenu", typeof(RectTransform));
                    go.transform.SetParent(canvas.transform, false);
                    Instance = go.AddComponent<ConsoleContextMenu>();
                }
            }

            if (Instance != null)
            {
                if (Instance.m_Preferences == null)
                {
                    var prefs = Resources.FindObjectsOfTypeAll<ConsolePreferences>();
                    if (prefs != null && prefs.Length > 0)
                    {
                        Instance.m_Preferences = prefs[0];
                    }
                }

                Instance.OpenMenu(screenPosition, logSetup);
            }
        }

        private void OpenMenu(Vector2 screenPosition, LogMessageSetup logSetup)
        {
            _targetLogSetup = logSetup;
            if (_targetLogSetup == null) return;

            bool hasStackTrace = _targetLogSetup.LogMessage != null && !string.IsNullOrWhiteSpace(_targetLogSetup.LogMessage.AltText);
            if (_btnCopyStackTrace != null)
            {
                _btnCopyStackTrace.interactable = hasStackTrace;
            }

            PositionMenu(screenPosition);

            _menuCanvasGroup.alpha = 1f;
            _menuCanvasGroup.interactable = true;
            _menuCanvasGroup.blocksRaycasts = true;
            _menuPanelRect.gameObject.SetActive(true);
            _isOpen = true;
        }

        public void Hide()
        {
            _isOpen = false;
            if (_menuCanvasGroup)
            {
                _menuCanvasGroup.alpha = 0f;
                _menuCanvasGroup.interactable = false;
                _menuCanvasGroup.blocksRaycasts = false;
            }

            if (_menuPanelRect)
            {
                _menuPanelRect.gameObject.SetActive(false);
            }

            _targetLogSetup = null;
        }

        private void PositionMenu(Vector2 screenPos)
        {
            if (_canvasRect == null || _menuPanelRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPos,
                _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _parentCanvas.worldCamera : null,
                out Vector2 localPos
            );

            float menuWidth = 180f;
            float menuHeight = (_btnCopyStackTrace != null && _btnCopyStackTrace.gameObject.activeSelf) ? 105f : 72f;

            Rect canvasBounds = _canvasRect.rect;

            // Clamp X: if overflowing right edge, flip to left
            if (localPos.x + menuWidth > canvasBounds.xMax)
            {
                localPos.x -= menuWidth;
            }

            // Clamp Y: if overflowing bottom edge, flip upwards
            if (localPos.y - menuHeight < canvasBounds.yMin)
            {
                localPos.y += menuHeight;
            }

            _menuPanelRect.anchoredPosition = localPos;
            _menuPanelRect.SetAsLastSibling();
        }

        private void HandleCopyMessage()
        {
            if (_targetLogSetup == null) return;

            string clean = _targetLogSetup.GetCleanText();
            GUIUtility.systemCopyBuffer = clean;
            Vector2 mousePos = GetCurrentMousePosition();
            Hide();
            ShowToast("Message copied!", mousePos);
        }

        private void HandleCopyFullLog()
        {
            if (_targetLogSetup == null) return;

            string full = _targetLogSetup.GetFullFormattedLog();
            GUIUtility.systemCopyBuffer = full;
            Vector2 mousePos = GetCurrentMousePosition();
            Hide();
            ShowToast("Full log copied!", mousePos);
        }

        private void HandleCopyStackTrace()
        {
            if (_targetLogSetup == null) return;

            string stack = _targetLogSetup.GetCleanAltText();
            if (!string.IsNullOrWhiteSpace(stack))
            {
                GUIUtility.systemCopyBuffer = stack;
                Vector2 mousePos = GetCurrentMousePosition();
                Hide();
                ShowToast("Stack trace copied!", mousePos);
            }
            else
            {
                Hide();
            }
        }

        public void ShowToast(string message, Vector2 screenPos)
        {
            if (_toastRect == null || _toastText == null || _toastCanvasGroup == null) return;

            _toastText.text = message;

            // Position toast right near the mouse cursor
            PositionToast(screenPos, out Vector2 localStartPos);

            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }

            _toastCoroutine = StartCoroutine(ToastLifecycleCoroutine(localStartPos));
        }

        private void PositionToast(Vector2 screenPos, out Vector2 clampedLocalPos)
        {
            clampedLocalPos = Vector2.zero;
            if (_canvasRect == null || _toastRect == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPos,
                _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _parentCanvas.worldCamera : null,
                out Vector2 localPos
            );

            // Offset slightly above and to the right of cursor so cursor doesn't obscure it
            localPos += new Vector2(10f, 15f);

            float halfW = _toastRect.sizeDelta.x * 0.5f;
            float h = _toastRect.sizeDelta.y;
            Rect canvasBounds = _canvasRect.rect;

            localPos.x = Mathf.Clamp(localPos.x, canvasBounds.xMin + halfW + 8f, canvasBounds.xMax - halfW - 8f);
            localPos.y = Mathf.Clamp(localPos.y, canvasBounds.yMin + 8f, canvasBounds.yMax - h - 8f);

            clampedLocalPos = localPos;
            _toastRect.anchoredPosition = clampedLocalPos;
            _toastRect.SetAsLastSibling();
        }

        private void DismissToastImmediate()
        {
            if (_toastCoroutine != null)
            {
                StopCoroutine(_toastCoroutine);
                _toastCoroutine = null;
            }

            if (_toastCanvasGroup != null)
            {
                _toastCanvasGroup.alpha = 0f;
            }

            if (_toastRect != null)
            {
                _toastRect.gameObject.SetActive(false);
            }
        }

        private IEnumerator ToastLifecycleCoroutine(Vector2 startLocalPos)
        {
            _toastRect.gameObject.SetActive(true);
            _toastCanvasGroup.alpha = 0f;
            _toastRect.anchoredPosition = startLocalPos;

            const float totalDuration = 0.85f;
            const float fadeInDuration = 0.12f;
            const float fadeOutDuration = 0.20f;
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                float alpha;
                if (elapsed < fadeInDuration)
                {
                    alpha = elapsed / fadeInDuration;
                }
                else if (elapsed > totalDuration - fadeOutDuration)
                {
                    alpha = (totalDuration - elapsed) / fadeOutDuration;
                }
                else
                {
                    alpha = 1f;
                }

                _toastCanvasGroup.alpha = Mathf.Clamp01(alpha);

                // Gentle upward float: 18px over the entire duration
                float floatProgress = Mathf.Clamp01(elapsed / totalDuration);
                _toastRect.anchoredPosition = startLocalPos + new Vector2(0f, floatProgress * 18f);

                yield return null;
            }

            _toastCanvasGroup.alpha = 0f;
            _toastRect.gameObject.SetActive(false);
            _toastCoroutine = null;
        }

        #region Programmatic UI Construction

        private void BuildMenuUI()
        {
            var menuGO = new GameObject("ContextMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            menuGO.transform.SetParent(transform, false);

            _menuPanelRect = menuGO.GetComponent<RectTransform>();
            _menuPanelRect.sizeDelta = new Vector2(180, 105);
            _menuPanelRect.pivot = new Vector2(0, 1);

            var bg = menuGO.GetComponent<Image>();
            bg.color = new Color(0.10f, 0.12f, 0.16f, 0.98f); // Modern dark slate

            var outline = menuGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.85f);
            outline.effectDistance = new Vector2(1, -1);

            _menuCanvasGroup = menuGO.GetComponent<CanvasGroup>();

            var layout = menuGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 3;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var font = m_Preferences && m_Preferences.GlobalFont ? m_Preferences.GlobalFont : null;

            _btnCopyMessage = CreateMenuButton(menuGO.transform, "Copy Message", font, HandleCopyMessage, out _copyMessageLabel);
            _btnCopyFullLog = CreateMenuButton(menuGO.transform, "Copy Full Log", font, HandleCopyFullLog, out _copyFullLogLabel);
            _btnCopyStackTrace = CreateMenuButton(menuGO.transform, "Copy Stack Trace", font, HandleCopyStackTrace, out _copyStackTraceLabel);
        }

        private Button CreateMenuButton(Transform parent, string labelText, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick, out TextMeshProUGUI labelTmp)
        {
            var btnGO = new GameObject($"Btn_{labelText.Replace(" ", "")}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);

            var rt = btnGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(172, 28);

            var img = btnGO.GetComponent<Image>();
            img.color = new Color(0.15f, 0.18f, 0.24f, 0.9f);

            var btn = btnGO.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.18f, 0.24f, 0.9f);
            colors.highlightedColor = new Color(0.22f, 0.32f, 0.45f, 1.0f);
            colors.pressedColor = new Color(0.10f, 0.40f, 0.70f, 1.0f);
            colors.disabledColor = new Color(0.15f, 0.18f, 0.24f, 0.5f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);

            var textGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(btnGO.transform, false);

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            textRT.offsetMin = new Vector2(8, 0);
            textRT.offsetMax = new Vector2(-8, 0);

            labelTmp = textGO.GetComponent<TextMeshProUGUI>();
            labelTmp.text = labelText;
            labelTmp.fontSize = 12;
            labelTmp.color = new Color(0.92f, 0.94f, 0.98f);
            labelTmp.alignment = TextAlignmentOptions.Left;
            if (font) labelTmp.font = font;

            return btn;
        }

        private void BuildToastUI()
        {
            var toastGO = new GameObject("ToastNotification", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            toastGO.transform.SetParent(transform, false);

            _toastRect = toastGO.GetComponent<RectTransform>();
            _toastRect.pivot = new Vector2(0.5f, 0.5f);
            _toastRect.sizeDelta = new Vector2(160, 26);

            var bg = toastGO.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.22f, 0.16f, 0.96f); // Sleek emerald dark pill

            var outline = toastGO.AddComponent<Outline>();
            outline.effectColor = new Color(0.29f, 0.87f, 0.50f, 0.95f);
            outline.effectDistance = new Vector2(1, -1);

            _toastCanvasGroup = toastGO.GetComponent<CanvasGroup>();
            _toastCanvasGroup.alpha = 0f;
            _toastCanvasGroup.blocksRaycasts = false;

            var textGO = new GameObject("ToastText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(toastGO.transform, false);

            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;

            _toastText = textGO.GetComponent<TextMeshProUGUI>();
            _toastText.text = "Copied!";
            _toastText.fontSize = 11;
            _toastText.color = new Color(0.88f, 1f, 0.92f);
            _toastText.alignment = TextAlignmentOptions.Center;

            if (m_Preferences && m_Preferences.GlobalFont)
            {
                _toastText.font = m_Preferences.GlobalFont;
            }

            toastGO.SetActive(false);
        }

        #endregion
    }
}
