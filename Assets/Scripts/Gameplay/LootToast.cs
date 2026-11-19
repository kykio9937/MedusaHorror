using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>屏幕中部提示条：拾取道具等短提示。</summary>
    public class LootToast : MonoBehaviour
    {
        static LootToast _instance;

        CanvasGroup _group;
        Text _text;
        float _life;
        const float Hold = 2.8f;

        public static void Show(string text)
        {
            if (_instance == null)
            {
                var go = new GameObject("LootToast");
                _instance = go.AddComponent<LootToast>();
                _instance.Build();
            }
            _instance.Push(text);
        }

        void Build()
        {
            var canvasGo = new GameObject("ToastCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 82;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textGo = new GameObject("ToastText", typeof(RectTransform));
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1100f, 60f);
            rt.anchoredPosition = new Vector2(0f, 210f);
            _text = textGo.AddComponent<Text>();
            var f = MedusaHorror.UI.CjkFont.Get();
            _text.font = f;
            _text.fontSize = 32;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.raycastTarget = false;
            _text.color = new Color(1f, 0.88f, 0.58f, 1f);

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        void Push(string s)
        {
            if (_text != null) _text.text = string.IsNullOrEmpty(s) ? "" : s;
            _life = Hold;
        }

        void Update()
        {
            if (_group == null) return;
            if (_life > 0f)
            {
                _life -= Time.deltaTime;
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.deltaTime * 5f);
            }
            else
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime * 2f);
            }
        }
    }
}
