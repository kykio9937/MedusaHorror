using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 右上角信用点显示。任何场景都常驻，钱包变化即时刷新。
    /// </summary>
    public class CurrencyHud : MonoBehaviour
    {
        static CurrencyHud _inst;

        Text _text;
        Canvas _canvas;

        public static CurrencyHud Instance { get { return _inst; } }

        public static CurrencyHud Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("CurrencyHud");
            _inst = go.AddComponent<CurrencyHud>();
            return _inst;
        }

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildUi();
            Wallet.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            Wallet.Changed -= Refresh;
            if (_inst == this) _inst = null;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 55;
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
            prt.sizeDelta = new Vector2(196f, 46f);
            prt.anchoredPosition = new Vector2(-24f, -24f);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.06f, 0.68f);
            bg.raycastTarget = false;

            var txtGo = new GameObject("Coins", typeof(RectTransform));
            txtGo.transform.SetParent(panel.transform, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 0f);
            trt.offsetMax = new Vector2(-10f, 0f);
            _text = txtGo.AddComponent<Text>();
            _text.font = CjkFont.Get();
            _text.fontSize = 24;
            _text.alignment = TextAnchor.MiddleRight;
            _text.color = new Color(0.95f, 0.82f, 0.45f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void Refresh()
        {
            if (_text != null) _text.text = "信用点 " + Wallet.Coins;
        }

        public void SetShown(bool show)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(show);
        }
    }
}
