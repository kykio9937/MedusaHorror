using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MedusaHorror.UI
{
    /// <summary>
    /// 主菜单（P0-5 流程壳）：程序化构建，不依赖预制体与美术资源。
    /// 场景里只需挂本组件 + 一台相机，所有 UI 在运行时生成，全部中文文案走 CjkFont。
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuScreen : MonoBehaviour
    {
        public string gameScene = "W0_Base";

        /// <summary>编辑器诊断用的一次性「开始游戏」脉冲（读取后自动清零）。</summary>
        public static bool debugStartPulse;

        Canvas _canvas;
        Text _title;
        Text _sub;
        Text _hint;

        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Build();
        }

        void Build()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = CjkFont.Get();

            // 背景：上部近黑、下部暗血色
            var bg = NewRect("Bg", canvasGo.transform);
            Stretch(bg);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.035f, 0.030f, 0.033f, 1f);
            bgImg.raycastTarget = false;

            var glow = NewRect("Glow", canvasGo.transform);
            glow.anchorMin = new Vector2(0f, 0f);
            glow.anchorMax = new Vector2(1f, 0.55f);
            glow.offsetMin = Vector2.zero;
            glow.offsetMax = Vector2.zero;
            var glowImg = glow.gameObject.AddComponent<Image>();
            glowImg.color = new Color(0.26f, 0.06f, 0.07f, 0.42f);
            glowImg.raycastTarget = false;

            _title = NewText("Title", canvasGo.transform, font, 84, "美杜莎：石化之屋",
                new Vector2(0f, 205f), new Vector2(1400f, 120f));
            _title.color = new Color(0.94f, 0.90f, 0.84f, 1f);
            _title.fontStyle = FontStyle.Bold;

            _sub = NewText("Sub", canvasGo.transform, font, 26, "第一章 · 医院长廊（W1）",
                new Vector2(0f, 118f), new Vector2(1200f, 44f));
            _sub.color = new Color(0.72f, 0.66f, 0.62f, 0.95f);

            var start = NewButton("BtnStart", canvasGo.transform, font, "开始游戏",
                new Vector2(0f, -30f), new Color(0.36f, 0.16f, 0.15f, 0.98f), StartGame);

            var quit = NewButton("BtnQuit", canvasGo.transform, font, "退出游戏",
                new Vector2(0f, -122f), new Color(0.16f, 0.16f, 0.18f, 0.98f), QuitGame);

            _hint = NewText("Hint", canvasGo.transform, font, 22,
                "回车 / 空格 开始　·　ESC 退出",
                new Vector2(0f, -232f), new Vector2(900f, 40f));
            _hint.color = new Color(0.70f, 0.66f, 0.62f, 0.8f);

            var foot = NewText("Foot", canvasGo.transform, font, 20,
                "操作：WASD 移动　Shift 奔跑　鼠标左键 挥砍 / 射击　1 剑 2 枪　Q 切换　R 换弹　E 交互　ESC 暂停　M 小地图",
                new Vector2(0f, -300f), new Vector2(1500f, 36f));
            foot.color = new Color(0.58f, 0.55f, 0.52f, 0.75f);

            canvasGo.SetActive(true);

            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null) audio.StartMusic();
        }

        void Update()
        {
            if (debugStartPulse)
            {
                debugStartPulse = false;
                StartGame();
                return;
            }
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) StartGame();
            else if (kb.escapeKey.wasPressedThisFrame) QuitGame();
#endif
        }

        public void StartGame()
        {
            Time.timeScale = 1f;
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null) audio.PlayUi();
            Debug.Log("[MainMenu] 开始游戏 -> " + gameScene);
            if (Application.CanStreamedLevelBeLoaded(gameScene))
                MedusaHorror.Core.SceneLoader.Load(gameScene, "W1 · 废弃医院　消毒水与铁锈的气味");
            else Debug.LogError("[MainMenu] Build Settings 中找不到场景 " + gameScene);
        }

        public void QuitGame()
        {
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null) audio.PlayUi();
            Debug.Log("[MainMenu] 退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        Button NewButton(string name, Transform parent, Font font, string label, Vector2 pos, Color bg,
                         UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 74f);
            rt.anchoredPosition = pos;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.16f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 0.95f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            return btn;
        }

        Text NewText(string name, Transform parent, Font font, int size, string content,
                     Vector2 pos, Vector2 sizeDelta)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.text = content;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
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

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
