using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using MedusaHorror.Core;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 右侧短暂显示当前目标与完成进度，更新进度时再次显示。
    /// 不依赖场景序列化，场景加载后自动挂载。
    /// </summary>
    public class ObjectiveHud : MonoBehaviour
    {
        static ObjectiveHud _instance;

        public static ObjectiveHud Instance { get { return _instance; } }

        Canvas _canvas;
        CanvasGroup _group;
        Text _titleText;
        Text _progressText;
        Image _barBg;
        Image _cardBackground;
        float _visibleUntil;
        Image _barFill;
        Text _levelText;

        string _title = "";
        int _current;
        int _total;
        bool _completed;
        float _flashTimer;
        Color _barColor = new Color(0.85f, 0.62f, 0.28f, 1f);

        public static ObjectiveHud Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ObjectiveHudRuntime");
            DontDestroyOnLoad(go);
            return go.AddComponent<ObjectiveHud>();
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildUi();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("ObjectiveCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 120;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var cardRt = NewRect("ObjectiveCard", canvasGo.transform);
            cardRt.anchorMin = new Vector2(0.73f, 0.3f);
            cardRt.anchorMax = new Vector2(0.73f, 0.3f);
            cardRt.pivot = new Vector2(0.5f, 1f);
            cardRt.sizeDelta = new Vector2(440f, 132f);
            cardRt.anchoredPosition = new Vector2(0f, -14f);
            var bg = cardRt.gameObject.AddComponent<Image>();
            _cardBackground = bg;
            bg.color = new Color(0.06f, 0.06f, 0.07f, 0.72f);
            bg.raycastTarget = false;

            Font font = CjkFont.Get();

            _levelText = NewText("ObjectiveLevel", cardRt, font, 20, new Vector2(20f, -18f), new Vector2(380f, 26f));
            _levelText.alignment = TextAnchor.MiddleLeft;
            _levelText.color = new Color(0.62f, 0.60f, 0.58f, 0.9f);
            _levelText.text = "";

            _titleText = NewText("ObjectiveTitle", cardRt, font, 26, new Vector2(20f, -46f), new Vector2(380f, 34f));
            _titleText.alignment = TextAnchor.MiddleLeft;
            _titleText.color = new Color(0.94f, 0.90f, 0.84f, 1f);
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.text = "";

            // 进度条
            var barBgRt = NewRect("ObjectiveBarBg", cardRt);
            barBgRt.anchorMin = new Vector2(0f, 1f);
            barBgRt.anchorMax = new Vector2(0f, 1f);
            barBgRt.pivot = new Vector2(0f, 1f);
            barBgRt.sizeDelta = new Vector2(380f, 8f);
            barBgRt.anchoredPosition = new Vector2(20f, -92f);
            _barBg = barBgRt.gameObject.AddComponent<Image>();
            _barBg.color = new Color(0.22f, 0.21f, 0.20f, 0.9f);
            _barBg.raycastTarget = false;

            var fillRt = NewRect("ObjectiveBarFill", barBgRt);
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fillRt.pivot = new Vector2(0f, 0.5f);
            _barFill = fillRt.gameObject.AddComponent<Image>();
            _barFill.color = _barColor;
            _barFill.raycastTarget = false;
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillAmount = 0f;

            _progressText = NewText("ObjectiveProgress", cardRt, font, 20, new Vector2(20f, -104f), new Vector2(380f, 26f));
            _progressText.alignment = TextAnchor.MiddleLeft;
            _progressText.color = new Color(0.86f, 0.82f, 0.76f, 0.95f);
            _progressText.text = "";
        }

        Text NewText(string name, Transform parent, Font font, int size, Vector2 pos, Vector2 sizeDelta)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.resizeTextForBestFit = true; txt.resizeTextMinSize = 18; txt.resizeTextMaxSize = size;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        /// <summary>显示目标。total<=0 表示不显示进度条（例如「前往出口」）。</summary>
        public void Show(string title, int current, int total)
        {
            _title = title ?? "";
            _current = current;
            _total = total;
            _completed = false;
            _flashTimer = 0f;

            var scene = SceneManager.GetActiveScene();
            var theme = SceneNoticeTheme.For(scene.name);
            _levelText.text = theme.category;
            _levelText.color = theme.accent;
            _cardBackground.sprite = theme.Background;
            _cardBackground.color = new Color(0.12f, 0.14f, 0.15f, 0.96f);
            _titleText.text = _title;
            _titleText.color = new Color(0.94f, 0.90f, 0.84f, 1f);
            _barColor = theme.accent;
            _barFill.color = _barColor;
            RefreshProgress();
            _group.alpha = 1f;
            _visibleUntil = Time.unscaledTime + 6f;
        }

        public void SetProgress(int current, int total)
        {
            _visibleUntil = Time.unscaledTime + 6f;
            _group.alpha = 1f;
            _current = current;
            if (total >= 0) _total = total;
            RefreshProgress();
        }

        public void Complete()
        {
            if (_completed) return;
            _completed = true;
            if (_total > 0) _current = _total;
            _titleText.text = _title + "　✔";
            _titleText.color = new Color(0.55f, 0.85f, 0.55f, 1f);
            _barColor = new Color(0.45f, 0.80f, 0.45f, 1f);
            _barFill.color = _barColor;
            RefreshProgress();
            _flashTimer = 1.6f;
            _visibleUntil = Time.unscaledTime + 6f;
            _group.alpha = 1f;

            var audio = AudioDirector.Instance;
            if (audio != null) audio.Play(SfxId.Pickup, 0.9f, 1.15f);
        }

        public void Hide()
        {
            _group.alpha = 0f;
            _visibleUntil = 0f;
        }

        void RefreshProgress()
        {
            if (_total > 0)
            {
                _progressText.text = _current + " / " + _total;
                _barFill.fillAmount = Mathf.Clamp01((float)_current / _total);
                _barBg.gameObject.SetActive(true);
                _progressText.gameObject.SetActive(true);
            }
            else
            {
                _progressText.text = "";
                _barBg.gameObject.SetActive(false);
                _progressText.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if(MedusaHorror.Player.InputReader.blocked || MedusaHorror.Core.SceneLoader.IsLoading){_group.alpha=0;return;}
            if(_visibleUntil>Time.unscaledTime)_group.alpha=1;
            if (_visibleUntil > 0 && Time.unscaledTime > _visibleUntil)
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0, Time.unscaledDeltaTime * 2f);
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.unscaledDeltaTime;
                if (_flashTimer <= 0f && _group != null) _group.alpha = 0.85f;
            }
        }
    }
}
