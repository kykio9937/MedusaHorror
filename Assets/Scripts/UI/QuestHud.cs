using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 右上角任务追踪：列出已接、未交付的任务与进度。任务状态变化即时刷新。
    /// </summary>
    public class QuestHud : MonoBehaviour
    {
        static QuestHud _inst;

        Text _text;
        Canvas _canvas;
        bool _hidden;
        readonly StringBuilder _sb = new StringBuilder(256);

        public static QuestHud Instance { get { return _inst; } }

        public static QuestHud Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("QuestHud");
            _inst = go.AddComponent<QuestHud>();
            return _inst;
        }

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildUi();
            QuestSystem.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            QuestSystem.Changed -= Refresh;
            if (_inst == this) _inst = null;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 54;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(1f, 1f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.sizeDelta = new Vector2(320f, 200f);
            prt.anchoredPosition = new Vector2(-24f, -80f);

            var txtGo = new GameObject("Quests", typeof(RectTransform));
            txtGo.transform.SetParent(panel.transform, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6f, 6f);
            trt.offsetMax = new Vector2(-6f, -6f);
            _text = txtGo.AddComponent<Text>();
            _text.font = CjkFont.Get();
            _text.fontSize = 19;
            _text.alignment = TextAnchor.UpperRight;
            _text.color = new Color(0.88f, 0.86f, 0.80f, 0.95f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void Refresh()
        {
            if (_text == null) return;
            if (_hidden) { if (_canvas != null) _canvas.gameObject.SetActive(false); return; }
            _sb.Length = 0;

            var quests = SaveSystem.Data.quests;
            int shown = 0;
            for (int i = 0; i < quests.Count && shown < 4; i++)
            {
                var s = quests[i];
                if (!s.accepted || s.claimed) continue;
                var d = QuestSystem.Get(s.id);
                if (d == null) continue;

                if (shown > 0) _sb.Append('\n');
                bool ready = s.progress >= d.need;
                _sb.Append(ready ? "● " : "○ ").Append(d.title);
                _sb.Append("  ").Append(Mathf.Min(s.progress, d.need)).Append('/').Append(d.need);
                if (ready) _sb.Append("  可交付");
                shown++;
            }

            _text.text = _sb.Length == 0 ? "" : _sb.ToString();
            if (_canvas != null) _canvas.gameObject.SetActive(shown > 0);
        }

        public void SetShown(bool show)
        {
            _hidden = !show;
            if (show) Refresh();
            else if (_canvas != null) _canvas.gameObject.SetActive(false);
        }
    }
}
