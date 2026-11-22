using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.UI
{
    /// <summary>剧情字幕：居中大字 + 一行副标题，淡入淡出，用于攻城波次、事件播报。</summary>
    public class StoryBanner : MonoBehaviour
    {
        static StoryBanner _instance;

        CanvasGroup _group;
        Text _title;
        Text _sub;
        float _life;
        float _hold;

        public static void Show(string title, string sub = "", float hold = 3.6f)
        {
            if (_instance == null)
            {
                var go = new GameObject("StoryBanner");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<StoryBanner>();
                _instance.Build();
            }
            _instance.Push(title, sub, hold);
        }

        void Build()
        {
            var canvasGo = new GameObject("BannerCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = CjkFont.Get();

            _title = MakeText(canvasGo.transform, font, 54, new Vector2(0f, 300f), new Vector2(1400f, 80f),
                new Color(1f, 0.82f, 0.35f));
            _sub = MakeText(canvasGo.transform, font, 28, new Vector2(0f, 240f), new Vector2(1400f, 50f),
                new Color(0.86f, 0.90f, 0.96f));

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
        }

        static Text MakeText(Transform parent, Font font, int size, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = color;
            return t;
        }

        void Push(string title, string sub, float hold)
        {
            if (_title != null) _title.text = title ?? "";
            if (_sub != null) _sub.text = sub ?? "";
            _hold = Mathf.Max(1f, hold);
            _life = _hold;
        }

        void Update()
        {
            if (_group == null) return;
            if (_life > 0f)
            {
                _life -= Time.deltaTime;
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.deltaTime * 4f);
            }
            else
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f, Time.deltaTime * 1.6f);
            }
        }
    }
}
