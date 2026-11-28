using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 全局异步场景加载器：程序化构建加载界面，走 SceneManager.LoadSceneAsync。
    /// 通过 RuntimeInitializeOnLoadMethod 自举，任何场景启动都可用，不需要在场景里手动挂。
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }
        public static bool IsLoading { get; private set; }

        [Tooltip("加载界面最短展示时长（秒，不受 timeScale 影响）")]
        public float minShowTime = 1.1f;

        Canvas _canvas;
        CanvasGroup _group;
        Image _barFill;
        Text _pctText;
        Text _tipText;
        Text _sceneText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("~SceneLoader");
            DontDestroyOnLoad(go);
            go.AddComponent<SceneLoader>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            IsLoading = false;
        }
        void LateUpdate()
        {
            if (IsLoading) MedusaHorror.Player.InputReader.blocked = true;
        }

        /// <summary>异步加载目标场景；sceneName 不在 Build Settings 中时回退主菜单。</summary>
        public static void Load(string sceneName, string tip = null)
        {
            if (IsLoading)
            {
                Debug.LogWarning("[SceneLoader] 已在加载中，忽略重复请求: " + sceneName);
                return;
            }
            if (Instance == null) Boot();

            string target = sceneName;
            if (string.IsNullOrEmpty(target) || !Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.LogWarning("[SceneLoader] 场景不可加载: " + target + "，回退主菜单");
                target = GameFlow.MenuScene;
                if (!Application.CanStreamedLevelBeLoaded(target))
                {
                    Debug.LogError("[SceneLoader] 主菜单也不可加载，放弃切换");
                    return;
                }
            }

            string source = SceneManager.GetActiveScene().name;
            if (!CampaignRoute.CanTravel(source, target, out var reason))
            {
                MedusaHorror.Gameplay.LootToast.Show(reason); return;
            }
            CampaignRoute.BeginTravel(source, target);
            // 记录进度落点：回主菜单时清空，避免"继续游戏"把玩家丢回菜单
            if (target != GameFlow.MenuScene) SaveSystem.Data.lastScene = target;
            SaveSystem.Save();

            Instance.StartCoroutine(Instance.LoadRoutine(target, tip));
        }

        IEnumerator LoadRoutine(string sceneName, string tip)
        {
            IsLoading = true;
            Time.timeScale = 1f;
            bool realityReturn = CampaignRoute.ReturnFromTrial;
            bool wasBlocked = MedusaHorror.Player.InputReader.blocked;
            MedusaHorror.Player.InputReader.blocked = true;
            Build(realityReturn ? "" : tip, sceneName);
            UI.RealityReturnEffect effect = null;
            if (realityReturn)
            {
                foreach (Transform child in _canvas.transform) child.gameObject.SetActive(false);
                var go = new GameObject("RealityReturn", typeof(RectTransform));
                effect = go.AddComponent<UI.RealityReturnEffect>(); effect.Build(_canvas.transform);
                var rt = go.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            }

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[SceneLoader] LoadSceneAsync 返回空: " + sceneName);
                Teardown();
                MedusaHorror.Player.InputReader.blocked = wasBlocked;
                IsLoading = false;
                yield break;
            }
            op.allowSceneActivation = false;

            float t = 0f;
            float minimum = realityReturn ? 4.2f : minShowTime;
            while (t < minimum)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                if (effect != null) effect.Tick(t);
                SetProgress(Mathf.Clamp01(op.progress / 0.9f) * 0.9f + 0.1f * Mathf.Clamp01(t / minShowTime));
                yield return null;
            }

            // 等真实加载到 0.9（Unity 在 allowSceneActivation=false 时最多到 0.9）
            while (op.progress < 0.9f)
            {
                SetProgress(Mathf.Lerp(0.55f, 0.9f, op.progress / 0.9f));
                yield return null;
            }

            SetProgress(1f);
            yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            if (effect != null)
            {
                float reveal = 0;
                while (reveal < 1.6f) { reveal += Mathf.Min(Time.unscaledDeltaTime, 0.1f); effect.Reveal(reveal / 1.6f); yield return null; }
            }
            Teardown();
            MedusaHorror.Player.InputReader.blocked = false;
            IsLoading = false;
        }

        void Build(string tip, string sceneName)
        {
            var canvasGo = new GameObject("LoadingCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 3000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var cg = canvasGo.AddComponent<CanvasGroup>();
            _group = cg;

            Font font = UI.CjkFont.Get();

            // 赛博关卡用青色霓虹，恐怖关卡保留暗红基调
            bool neon = sceneName == "W2_Wasteland" || sceneName == "W3_Subway"
                        || sceneName == "W4_NeonStreet" || sceneName == "W5_AlienGate";
            Color accent = neon ? new Color(0.32f, 0.92f, 1f, 1f) : new Color(0.72f, 0.14f, 0.13f, 1f);
            Color edgeLine = neon ? new Color(0.10f, 0.55f, 0.75f, 0.85f) : new Color(0.45f, 0.05f, 0.06f, 0.75f);
            Color titleColor = neon ? new Color(0.80f, 0.96f, 1f) : new Color(0.88f, 0.86f, 0.82f);

            var bg = Rect("Bg", canvasGo.transform);
            Stretch(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.018f, 0.016f, 0.020f, 1f);
            bgImg.raycastTarget = true;

            // 上下两道暗红压边，制造恐怖基调
            var topLine = Rect("TopLine", canvasGo.transform);
            topLine.anchorMin = new Vector2(0f, 1f);
            topLine.anchorMax = new Vector2(1f, 1f);
            topLine.pivot = new Vector2(0.5f, 1f);
            topLine.offsetMin = new Vector2(0f, -2f);
            topLine.offsetMax = Vector2.zero;
            topLine.gameObject.AddComponent<Image>().color = edgeLine;

            var botLine = Rect("BotLine", canvasGo.transform);
            botLine.anchorMin = new Vector2(0f, 0f);
            botLine.anchorMax = new Vector2(1f, 0f);
            botLine.pivot = new Vector2(0.5f, 0f);
            botLine.offsetMin = Vector2.zero;
            botLine.offsetMax = new Vector2(0f, 2f);
            botLine.gameObject.AddComponent<Image>().color = edgeLine;

            _sceneText = TextRect("SceneName", canvasGo.transform, font, 40, titleColor);
            var srt = _sceneText.rectTransform;
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, 40f);
            srt.sizeDelta = new Vector2(900f, 60f);
            _sceneText.alignment = TextAnchor.MiddleCenter;
            _sceneText.text = DisplayName(sceneName);

            // 进度条槽
            var slot = Rect("BarSlot", canvasGo.transform);
            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(0f, -20f);
            slot.sizeDelta = new Vector2(560f, 6f);
            slot.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.15f, 0.16f, 1f);

            var fill = Rect("BarFill", slot);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.pivot = new Vector2(0f, 0.5f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            _barFill = fill.gameObject.AddComponent<Image>();
            _barFill.color = accent;
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillAmount = 0f;

            if (neon) UI.UiSkin.Apply(canvasGo.GetComponent<RectTransform>());

            _pctText = TextRect("Pct", canvasGo.transform, font, 20, new Color(0.62f, 0.60f, 0.58f));
            var prt = _pctText.rectTransform;
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = new Vector2(0f, -50f);
            prt.sizeDelta = new Vector2(300f, 30f);
            _pctText.alignment = TextAnchor.MiddleCenter;
            _pctText.text = "0%";

            _tipText = TextRect("Tip", canvasGo.transform, font, 22, new Color(0.55f, 0.53f, 0.50f));
            var trt = _tipText.rectTransform;
            trt.anchorMin = new Vector2(0.5f, 0f);
            trt.anchorMax = new Vector2(0.5f, 0f);
            trt.anchoredPosition = new Vector2(0f, 120f);
            trt.sizeDelta = new Vector2(1100f, 40f);
            _tipText.alignment = TextAnchor.MiddleCenter;
            _tipText.text = !string.IsNullOrEmpty(tip) ? tip : sceneName == CampaignRoute.Trial ? "记忆仍然模糊，先留意身边能用的东西。" : sceneName == CampaignRoute.City ? "高墙内可以休整，离城前记得确认补给。" : "留意周围的道路，记住返回安全区的方向。";
        }

        public static string DisplayName(string scene)
        {
            switch (scene)
            {
                case "W0_Base": return "保安队训练场";
                case "W1_Hospital": return "无名医院";
                case "W2_Stronghold": return "荒原堡垒";
                case "W2_Wasteland": return "野外 · 旧村庄";
                case "W3_Subway": return "W3 · 地下铁";
                case "W4_NeonStreet": return "W4 · 霓虹街区";
                case "W5_AlienGate": return "W5 · 外星星门";
                case "MainMenu": return "主菜单";
                default: return string.IsNullOrEmpty(scene) ? "载入中" : scene;
            }
        }

        void SetProgress(float p)        {
            p = Mathf.Clamp01(p);
            if (_barFill != null) _barFill.fillAmount = p;
            if (_pctText != null) _pctText.text = Mathf.RoundToInt(p * 100f) + "%";
        }

        void Teardown()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            _group = null;
            _barFill = null;
            _pctText = null;
            _tipText = null;
            _sceneText = null;
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text TextRect(string name, Transform parent, Font font, int size, Color color)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
