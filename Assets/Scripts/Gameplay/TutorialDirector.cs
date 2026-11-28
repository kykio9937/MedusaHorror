using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 新手引导：首次进入 W1 时逐条播放操作提示；之后按 H 可随时重看。
    /// 播放期间不锁输入，玩家可以边看边操作。
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        static TutorialDirector _inst;

        public static bool Playing { get { return _inst != null && _inst._playing; } }
        public static int CurrentStep { get { return _inst != null ? _inst._step : -1; } }
        public static int StepCount { get { return Tips.Length; } }

        static readonly string[] Tips =
        {
            "WASD 移动　·　Shift 奔跑　·　空格跳跃",
            "鼠标左键 攻击　·　1 / 2 / 3 切换剑与枪械　·　R 换弹",
            "E 交互　·　F 裂空斩　·　V 石化震荡　·　G 召唤坐骑",
            "别让美杜莎的凝视填满石化条 —— 石化值满即死亡",
            "搜寻线索打开北侧通道，避开凝视，寻找离开这里的路"
        };

        bool _playing;
        int _step;
        float _stepTimer;
        Canvas _canvas;
        CanvasGroup _group;
        Text _text;
        Text _indexText;
        Coroutine _routine;

        public static TutorialDirector Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("TutorialDirector");
            _inst = go.AddComponent<TutorialDirector>();
            return _inst;
        }

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

        void Update()
        {
            if (_playing) return;
            if (Input.GetKeyDown(KeyCode.H)) Play();
        }

        /// <summary>首次进入 W1 时调用：未看过则自动播放。</summary>
        public static void MaybeShowOnW1(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("W1")) return;
            if (SaveSystem.Data.tutorialDone) return;
            Ensure().Play();
        }

        public void Play()
        {
            if (_playing) return;
            _playing = true;
            _step = 0;
            _stepTimer = 0f;
            SetVisible(true);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(RunTips());
        }

        IEnumerator RunTips()
        {
            for (int i = 0; i < Tips.Length; i++)
            {
                _step = i;
                _stepTimer = 0f;
                if (_text != null) _text.text = Tips[i];
                if (_indexText != null) _indexText.text = (i + 1) + " / " + Tips.Length + "　（任意键跳过）";
                Fade(1f);
                while (_stepTimer < 4.2f)
                {
                    _stepTimer += Time.unscaledDeltaTime;
                    if (Input.anyKeyDown && _stepTimer > 0.35f) { Skip(); yield break; }
                    yield return null;
                }
                Fade(0f);
                yield return new WaitForSecondsRealtime(0.18f);
            }
            Finish();
        }

        public void Skip()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            Finish();
        }

        void Finish()
        {
            _playing = false;
            SetVisible(false);
            SaveSystem.Data.tutorialDone = true;
            SaveSystem.Save();
            var audio = AudioDirector.Instance;
            if (audio != null) audio.Play(SfxId.UiClick, 0.5f, 1.1f);
        }

        void Fade(float a)
        {
            if (_group != null) _group.alpha = a;
        }

        void SetVisible(bool v)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(v);
        }

        void BuildUi()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0.5f, 1f);
            prt.anchorMax = new Vector2(0.5f, 1f);
            prt.pivot = new Vector2(0.5f, 1f);
            prt.sizeDelta = new Vector2(1020f, 108f);
            prt.anchoredPosition = new Vector2(0f, -120f);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.07f, 0.86f);
            bg.raycastTarget = false;

            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(panel.transform, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.sizeDelta = new Vector2(0f, 3f);
            var bimg = bar.AddComponent<Image>();
            bimg.color = new Color(0.32f, 0.92f, 1f);
            bimg.raycastTarget = false;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(panel.transform, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = new Vector2(0f, 0f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.offsetMin = new Vector2(28f, 30f);
            trt.offsetMax = new Vector2(-28f, -14f);
            _text = txtGo.AddComponent<Text>();
            _text.font = CjkFont.Get();
            _text.fontSize = 26;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = new Color(0.95f, 0.96f, 1f);
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;

            var idxGo = new GameObject("Index", typeof(RectTransform));
            idxGo.transform.SetParent(panel.transform, false);
            var irt = (RectTransform)idxGo.transform;
            irt.anchorMin = new Vector2(1f, 0f);
            irt.anchorMax = new Vector2(1f, 0f);
            irt.pivot = new Vector2(1f, 0f);
            irt.sizeDelta = new Vector2(280f, 26f);
            irt.anchoredPosition = new Vector2(-16f, 8f);
            _indexText = idxGo.AddComponent<Text>();
            _indexText.font = CjkFont.Get();
            _indexText.fontSize = 17;
            _indexText.alignment = TextAnchor.MiddleRight;
            _indexText.color = new Color(0.55f, 0.8f, 1f, 0.85f);
            _indexText.raycastTarget = false;
        }
    }
}
