using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Player;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// NPC 对话系统：底部对话框 + 打字机文本，E / 左键推进，播完回调。
    /// 对话期间屏蔽移动与开火，但不打断已有 UI。
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        static DialogueSystem _inst;

        public static bool IsOpen { get { return _inst != null && _inst._open; } }

        public static DialogueSystem Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("DialogueSystem");
            _inst = go.AddComponent<DialogueSystem>();
            return _inst;
        }

        bool _open;
        string _speaker;
        string[] _lines;
        int _index;
        Action _onDone;

        Canvas _canvas;
        Text _speakerText;
        Text _bodyText;
        Text _hintText;
        Image _panel;
        float _typeTimer;
        int _typedChars;
        bool _lineComplete;
        float _lastAdvanceTime;

        public string Speaker { get { return _speaker; } }
        public int LineIndex { get { return _index; } }
        public int LineCount { get { return _lines == null ? 0 : _lines.Length; } }
        public string CurrentLine { get { return _lines != null && _index >= 0 && _index < _lines.Length ? _lines[_index] : ""; } }
        public string TypedText { get { return _bodyText != null ? _bodyText.text : ""; } }

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildUi();
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }

        /// <summary>播放一段对话；lines 为空则直接回调。</summary>
        public void Play(string speaker, string[] lines, Action onDone = null)
        {
            if (lines == null || lines.Length == 0)
            {
                if (onDone != null) onDone();
                return;
            }
            _speaker = speaker;
            _lines = lines;
            _index = 0;
            _onDone = onDone;
            _open = true;
            _lastAdvanceTime = Time.time;
            SetVisible(true);
            StartLine();
        }

        void StartLine()
        {
            _typedChars = 0;
            _typeTimer = 0f;
            _lineComplete = false;
            if (_speakerText != null) _speakerText.text = _speaker;
            if (_bodyText != null) _bodyText.text = "";
            if (_hintText != null) _hintText.text = "";
        }

        void Update()
        {
            if (!_open) return;

            string line = CurrentLine;
            if (!_lineComplete)
            {
                _typeTimer += Time.deltaTime;
                int target = Mathf.Min(line.Length, Mathf.FloorToInt(_typeTimer / 0.028f));
                if (target != _typedChars)
                {
                    _typedChars = target;
                    if (_bodyText != null) _bodyText.text = line.Substring(0, Mathf.Clamp(_typedChars, 0, line.Length));
                }
                if (_typedChars >= line.Length)
                {
                    _lineComplete = true;
                    if (_hintText != null) _hintText.text = _index < _lines.Length - 1 ? "[E] 继续" : "[E] 结束";
                }
            }

            bool advance = false;
            if (Time.time - _lastAdvanceTime > 0.12f)
            {
                advance = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0);
                if (advance) _lastAdvanceTime = Time.time;
            }
            if (!advance) return;

            if (!_lineComplete)
            {
                // 第一次按键：立刻显示整句
                _typedChars = line.Length;
                _lineComplete = true;
                if (_bodyText != null) _bodyText.text = line;
                if (_hintText != null) _hintText.text = _index < _lines.Length - 1 ? "[E] 继续" : "[E] 结束";
                return;
            }

            _index++;
            if (_index >= _lines.Length)
            {
                Close();
                return;
            }
            StartLine();
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            SetVisible(false);
            var cb = _onDone;
            _onDone = null;
            _lines = null;
            _speaker = null;
            if (cb != null) cb();
        }

        /// <summary>测试钩子：跳过打字机直接推进一句。</summary>
        public void DebugAdvance()
        {
            if (!_open) return;
            if (!_lineComplete)
            {
                _typedChars = CurrentLine.Length;
                _lineComplete = true;
                if (_bodyText != null) _bodyText.text = CurrentLine;
                return;
            }
            _index++;
            if (_index >= _lines.Length) { Close(); return; }
            StartLine();
        }

        void SetVisible(bool v)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(v);
            InputReader.blocked = v;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("Panel", typeof(RectTransform));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panelGo.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(1180f, 210f);
            prt.anchoredPosition = new Vector2(0f, 56f);
            _panel = panelGo.AddComponent<Image>();
            _panel.color = new Color(0.04f, 0.05f, 0.07f, 0.92f);
            _panel.raycastTarget = false;

            var barGo = new GameObject("Bar", typeof(RectTransform));
            barGo.transform.SetParent(panelGo.transform, false);
            var brt = (RectTransform)barGo.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.sizeDelta = new Vector2(5f, 0f);
            var bimg = barGo.AddComponent<Image>();
            bimg.color = new Color(0.32f, 0.92f, 1f);
            bimg.raycastTarget = false;

            var spkGo = new GameObject("Speaker", typeof(RectTransform));
            spkGo.transform.SetParent(panelGo.transform, false);
            var srt = (RectTransform)spkGo.transform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0f, 1f);
            srt.sizeDelta = new Vector2(-64f, 40f);
            srt.anchoredPosition = new Vector2(32f, -16f);
            _speakerText = spkGo.AddComponent<Text>();
            _speakerText.font = CjkFont.Get();
            _speakerText.fontSize = 26;
            _speakerText.alignment = TextAnchor.MiddleLeft;
            _speakerText.color = new Color(0.32f, 0.92f, 1f);
            _speakerText.raycastTarget = false;

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var wrt = (RectTransform)bodyGo.transform;
            wrt.anchorMin = new Vector2(0f, 0f);
            wrt.anchorMax = new Vector2(1f, 1f);
            wrt.offsetMin = new Vector2(32f, 44f);
            wrt.offsetMax = new Vector2(-32f, -60f);
            _bodyText = bodyGo.AddComponent<Text>();
            _bodyText.font = CjkFont.Get();
            _bodyText.fontSize = 24;
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.color = new Color(0.93f, 0.94f, 0.97f);
            _bodyText.raycastTarget = false;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _bodyText.lineSpacing = 1.25f;

            var hintGo = new GameObject("Hint", typeof(RectTransform));
            hintGo.transform.SetParent(panelGo.transform, false);
            var hrt = (RectTransform)hintGo.transform;
            hrt.anchorMin = new Vector2(1f, 0f);
            hrt.anchorMax = new Vector2(1f, 0f);
            hrt.pivot = new Vector2(1f, 0f);
            hrt.sizeDelta = new Vector2(240f, 32f);
            hrt.anchoredPosition = new Vector2(-24f, 10f);
            _hintText = hintGo.AddComponent<Text>();
            _hintText.font = CjkFont.Get();
            _hintText.fontSize = 20;
            _hintText.alignment = TextAnchor.MiddleRight;
            _hintText.color = new Color(0.6f, 0.85f, 1f, 0.9f);
            _hintText.raycastTarget = false;
        }
    }
}
