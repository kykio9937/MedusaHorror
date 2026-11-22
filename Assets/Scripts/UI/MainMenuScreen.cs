using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace MedusaHorror.UI
{
    /// <summary>
    /// 主菜单：左侧竖排菜单栏 + 独立存档页，暗色霓虹 / 扫描线 / glitch 的赛博朋克风格。
    /// 全部 UI 运行时程序化生成，中文走 CjkFont，不依赖预制体与美术资源。
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuScreen : MonoBehaviour
    {
        public string gameScene = "W0_Base";

        /// <summary>编辑器诊断用的一次性「开始」脉冲（读取后自动清零）。</summary>
        public static bool debugStartPulse;

        /// <summary>编辑器诊断：一次性打开指定子页面。</summary>
#if UNITY_EDITOR
        public static string debugOpenView;
#endif

        enum View { Main, Load, NewGame, Settings }

        const float MenuX = 112f;
        const float MenuTop = 424f;
        const float MenuStep = 68f;

        static readonly Color NeonYellow = new Color(0.988f, 0.933f, 0.039f, 1f);
        static readonly Color NeonCyan = new Color(0.10f, 0.88f, 0.94f, 1f);
        static readonly Color NeonMagenta = new Color(1f, 0.17f, 0.62f, 1f);
        static readonly Color TextIdle = new Color(0.74f, 0.75f, 0.78f, 0.62f);
        static readonly Color TextOff = new Color(0.42f, 0.43f, 0.46f, 0.5f);

        static readonly string[] MainLabels = { "继续游戏", "新游戏", "载入存档", "设置", "退出游戏" };

        Canvas _canvas;
        Font _font;
        RectTransform _mainRoot;
        RectTransform _panelRoot;

        readonly Text[] _itemTexts = new Text[5];
        readonly Image[] _itemBars = new Image[5];
        readonly Image[] _itemMarks = new Image[5];
        int _mainIndex;

        RectTransform _bandA, _bandB, _bandC;
        RectTransform[] _glitchBars;
        float _glitchTimer;

        View _view = View.Main;
        int _slotIndex;
        int _pendingDelete = -1;
        float _pendingDeleteUntil;
        int _settingsIndex;
        readonly Text[] _settingValues = new Text[3];
        readonly Image[] _settingFills = new Image[3];

        // ------------------------------------------------------------------ 生命周期

        void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Start()
        {
            Build();
            ApplyVolumes();
            RefreshMain();
            ShowView(View.Main);

            var audio = MedusaHorror.Core.AudioDirector.Ensure();
            if (audio != null) audio.StartMusic();
        }

        // ------------------------------------------------------------------ 构建

        void Build()
        {
            _font = CjkFont.Get();
            EnsureEventSystem();

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

            BuildBackground(canvasGo.transform);

            _mainRoot = FullRect("MainMenuRoot", canvasGo.transform);
            BuildTitle(_mainRoot);
            BuildMenuItems(_mainRoot);
            BuildFooter(canvasGo.transform);

            _panelRoot = null;
        }

        void BuildBackground(Transform parent)
        {
            var bg = FullRect("Bg", parent);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.043f, 0.043f, 0.055f, 1f);
            bgImg.raycastTarget = false;

            _bandA = Band(parent, "BandCyan", NeonCyan, 0.055f, -14f, 300f);
            _bandB = Band(parent, "BandMagenta", NeonMagenta, 0.045f, -14f, 560f);
            _bandC = Band(parent, "BandYellow", NeonYellow, 0.030f, -14f, 830f);

            var bottom = FullRect("BottomFade", parent);
            var bImg = bottom.gameObject.AddComponent<Image>();
            bImg.sprite = GradientSprite();
            bImg.type = Image.Type.Simple;
            bImg.color = new Color(0f, 0f, 0f, 0.85f);
            bImg.raycastTarget = false;
            bottom.anchorMin = new Vector2(0f, 0f);
            bottom.anchorMax = new Vector2(1f, 0.45f);
            bottom.offsetMin = Vector2.zero;
            bottom.offsetMax = Vector2.zero;

            var top = FullRect("TopFade", parent);
            var tImg = top.gameObject.AddComponent<Image>();
            tImg.sprite = GradientSprite();
            tImg.color = new Color(0f, 0f, 0f, 0.7f);
            tImg.raycastTarget = false;
            top.anchorMin = new Vector2(0f, 0.72f);
            top.anchorMax = new Vector2(1f, 1f);
            top.offsetMin = Vector2.zero;
            top.offsetMax = Vector2.zero;
            top.localScale = new Vector3(1f, -1f, 1f);

            var scan = FullRect("Scanlines", parent);
            var sImg = scan.gameObject.AddComponent<Image>();
            sImg.sprite = ScanlineSprite();
            sImg.type = Image.Type.Simple;
            sImg.color = new Color(1f, 1f, 1f, 0.55f);
            sImg.raycastTarget = false;

            _glitchBars = new RectTransform[4];
            for (int i = 0; i < _glitchBars.Length; i++)
            {
                var bar = FullRect("Glitch" + i, parent);
                var img = bar.gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
                _glitchBars[i] = bar;
            }
        }

        RectTransform Band(Transform parent, string name, Color color, float alpha, float angle, float y)
        {
            var rt = FullRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, alpha);
            img.raycastTarget = false;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(2600f, 150f);
            rt.anchoredPosition = new Vector2(0f, y - 540f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            return rt;
        }

        void BuildTitle(Transform parent)
        {
            var accent = R("Accent", parent, MenuX - 2f, 118f, 6f, 190f);
            var aImg = accent.gameObject.AddComponent<Image>();
            aImg.color = NeonYellow;
            aImg.raycastTarget = false;

            var title = R("Title", parent, MenuX + 26f, 106f, 900f, 120f);
            var tTxt = title.gameObject.AddComponent<Text>();
            tTxt.font = _font;
            tTxt.fontSize = 86;
            tTxt.fontStyle = FontStyle.Bold;
            tTxt.text = "MEDUSA";
            tTxt.color = NeonYellow;
            tTxt.alignment = TextAnchor.UpperLeft;
            tTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            tTxt.raycastTarget = false;
            AddShadow(title.gameObject, new Color(0f, 0.9f, 1f, 0.35f), new Vector2(4f, -4f));

            var sub = R("SubTitle", parent, MenuX + 30f, 222f, 1000f, 44f);
            var sTxt = sub.gameObject.AddComponent<Text>();
            sTxt.font = _font;
            sTxt.fontSize = 26;
            sTxt.text = "美杜莎：石化之屋 · MEDUSA HORROR";
            sTxt.color = new Color(0.62f, 0.80f, 0.84f, 0.9f);
            sTxt.alignment = TextAnchor.MiddleLeft;
            sTxt.raycastTarget = false;

            var line = R("TitleLine", parent, MenuX + 30f, 274f, 520f, 2f);
            var lImg = line.gameObject.AddComponent<Image>();
            lImg.color = new Color(0.10f, 0.88f, 0.94f, 0.55f);
            lImg.raycastTarget = false;
        }

        void BuildMenuItems(Transform parent)
        {
            for (int i = 0; i < MainLabels.Length; i++)
            {
                int index = i;
                float y = MenuTop + i * MenuStep;

                var hit = R("Item" + i, parent, MenuX, y, 560f, 58f);
                var hitImg = hit.gameObject.AddComponent<Image>();
                hitImg.color = new Color(1f, 1f, 1f, 0f);
                var btn = hit.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() =>
                {
                    _mainIndex = index;
                    RefreshMain();
                    ConfirmMain();
                });
                var hover = hit.gameObject.AddComponent<HoverProxy>();
                hover.onEnter = () => { _mainIndex = index; RefreshMain(); };

                var bar = R("Bar" + i, parent, MenuX, y + 9f, 5f, 40f);
                var barImg = bar.gameObject.AddComponent<Image>();
                barImg.color = new Color(0f, 0f, 0f, 0f);
                barImg.raycastTarget = false;
                _itemBars[i] = barImg;

                var txt = R("Label" + i, parent, MenuX + 26f, y, 560f, 58f);
                var t = txt.gameObject.AddComponent<Text>();
                t.font = _font;
                t.fontSize = 30;
                t.text = MainLabels[i];
                t.alignment = TextAnchor.MiddleLeft;
                t.color = TextIdle;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = false;
                _itemTexts[i] = t;

                var mark = R("Mark" + i, parent, MenuX + 6f, y + 24f, 10f, 10f);
                var mImg = mark.gameObject.AddComponent<Image>();
                mImg.color = new Color(0f, 0f, 0f, 0f);
                mImg.raycastTarget = false;
                _itemMarks[i] = mImg;
            }
        }

        void BuildFooter(Transform parent)
        {
            var hint = R("FooterHint", parent, MenuX, 1006f, 1200f, 34f);
            BottomAnchor(hint, MenuX, 26f, 1200f, 34f);
            var hTxt = hint.gameObject.AddComponent<Text>();
            hTxt.font = _font;
            hTxt.fontSize = 20;
            hTxt.text = "↑ ↓ 选择  Enter 确认  Esc 返回 / 退出";
            hTxt.color = new Color(0.55f, 0.57f, 0.60f, 0.7f);
            hTxt.alignment = TextAnchor.MiddleLeft;
            hTxt.raycastTarget = false;

            var ver = R("Version", parent, 0f, 0f, 300f, 30f);
            var vrt = ver;
            vrt.anchorMin = new Vector2(1f, 0f);
            vrt.anchorMax = new Vector2(1f, 0f);
            vrt.pivot = new Vector2(1f, 0f);
            vrt.anchoredPosition = new Vector2(-52f, 34f);
            var vTxt = ver.gameObject.AddComponent<Text>();
            vTxt.font = _font;
            vTxt.fontSize = 18;
            vTxt.text = "v0.4.0 · BUILD 2026.11";
            vTxt.color = new Color(0.50f, 0.52f, 0.55f, 0.65f);
            vTxt.alignment = TextAnchor.LowerRight;
            vTxt.raycastTarget = false;
        }

        // ------------------------------------------------------------------ 每帧

        void Update()
        {
            AnimateBackground();

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(debugOpenView))
            {
                string v = debugOpenView;
                debugOpenView = null;
                switch (v)
                {
                    case "load": ShowView(View.Load); break;
                    case "newgame": ShowView(View.NewGame); break;
                    case "settings": ShowView(View.Settings); break;
                }
                return;
            }
#endif

            if (debugStartPulse)
            {
                debugStartPulse = false;
                if (_view == View.Main) ConfirmMain();
                return;
            }

            if (_pendingDelete >= 0 && Time.unscaledTime > _pendingDeleteUntil)
            {
                _pendingDelete = -1;
                RebuildPanel();
            }

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;

            bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            bool left = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            bool right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            bool enter = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
            bool esc = kb.escapeKey.wasPressedThisFrame;
            bool del = kb.deleteKey.wasPressedThisFrame;

            switch (_view)
            {
                case View.Main:
                    if (up) MoveMain(-1);
                    if (down) MoveMain(1);
                    if (enter) ConfirmMain();
                    if (esc) QuitGame();
                    break;

                case View.Load:
                case View.NewGame:
                    if (up) MoveSlot(-1);
                    if (down) MoveSlot(1);
                    if (enter) ConfirmSlot();
                    if (del) RequestDelete();
                    if (esc) BackToMain();
                    break;

                case View.Settings:
                    if (up) MoveSetting(-1);
                    if (down) MoveSetting(1);
                    if (left) AdjustSetting(-0.1f);
                    if (right) AdjustSetting(0.1f);
                    if (esc) BackToMain();
                    break;
            }
#endif
        }

        void AnimateBackground()
        {
            float t = Time.unscaledTime;
            if (_bandA != null) _bandA.anchoredPosition = new Vector2(Mathf.Sin(t * 0.11f) * 180f, 240f);
            if (_bandB != null) _bandB.anchoredPosition = new Vector2(Mathf.Sin(t * 0.08f + 2f) * 240f, -20f);
            if (_bandC != null) _bandC.anchoredPosition = new Vector2(Mathf.Sin(t * 0.13f + 4f) * 150f, -300f);

            _glitchTimer -= Time.unscaledDeltaTime;
            if (_glitchTimer <= 0f)
            {
                _glitchTimer = UnityEngine.Random.Range(0.06f, 0.22f);
                if (_glitchBars != null)
                {
                    for (int i = 0; i < _glitchBars.Length; i++)
                    {
                        var bar = _glitchBars[i];
                        if (bar == null) continue;
                        var img = bar.GetComponent<Image>();
                        bool show = UnityEngine.Random.value < 0.35f;
                        if (!show)
                        {
                            img.color = new Color(0f, 0f, 0f, 0f);
                            continue;
                        }
                        float h = UnityEngine.Random.Range(2f, 10f);
                        float y = UnityEngine.Random.Range(0f, 1f);
                        bar.anchorMin = new Vector2(0f, y);
                        bar.anchorMax = new Vector2(1f, y);
                        bar.offsetMin = new Vector2(UnityEngine.Random.Range(-60f, 60f), 0f);
                        bar.offsetMax = new Vector2(UnityEngine.Random.Range(-60f, 60f), h);
                        Color c = UnityEngine.Random.value < 0.5f ? NeonCyan : NeonMagenta;
                        img.color = new Color(c.r, c.g, c.b, UnityEngine.Random.Range(0.06f, 0.16f));
                    }
                }
            }
        }

        // ------------------------------------------------------------------ 主菜单

        void MoveMain(int delta)
        {
            int start = _mainIndex;
            for (int i = 0; i < MainLabels.Length; i++)
            {
                _mainIndex = (_mainIndex + delta + MainLabels.Length) % MainLabels.Length;
                if (IsMainEnabled(_mainIndex) || _mainIndex == start) break;
            }
            if (_mainIndex != start) PlayUi();
            RefreshMain();
        }

        bool IsMainEnabled(int index)
        {
            if (index == 0) return MedusaHorror.Core.SaveSystem.HasAnySave;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>编辑器回归用：外部改动存档后刷新菜单显示。</summary>
        public void DebugRefresh()
        {
            RefreshMain();
        }
#endif

        void RefreshMain()
        {
            if (!IsMainEnabled(_mainIndex))
            {
                for (int i = 0; i < MainLabels.Length; i++)
                {
                    if (IsMainEnabled(i)) { _mainIndex = i; break; }
                }
            }

            bool hasSave = MedusaHorror.Core.SaveSystem.HasAnySave;
            int latest = hasSave ? MedusaHorror.Core.SaveSystem.LatestSlot : 0;

            for (int i = 0; i < MainLabels.Length; i++)
            {
                bool enabled = IsMainEnabled(i);
                bool selected = i == _mainIndex;
                string label = MainLabels[i];

                if (i == 0)
                {
                    if (!hasSave) label = "继续游戏 — 暂无存档";
                    else
                    {
                        var info = MedusaHorror.Core.SaveSystem.GetSlotInfo(latest);
                        label = "继续游戏 — " + info.sceneName;
                    }
                }

                if (_itemTexts[i] != null)
                {
                    _itemTexts[i].text = label;
                    _itemTexts[i].color = !enabled ? TextOff : (selected ? NeonYellow : TextIdle);
                }
                if (_itemBars[i] != null)
                    _itemBars[i].color = (selected && enabled) ? NeonYellow : new Color(0f, 0f, 0f, 0f);
                if (_itemMarks[i] != null)
                    _itemMarks[i].color = (selected && enabled)
                        ? new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.9f)
                        : new Color(0f, 0f, 0f, 0f);
            }
        }

        void ConfirmMain()
        {
            if (!IsMainEnabled(_mainIndex))
            {
                PlayUi();
                return;
            }
            PlayUi();
            switch (_mainIndex)
            {
                case 0: ContinueGame(); break;
                case 1: ShowView(View.NewGame); break;
                case 2: ShowView(View.Load); break;
                case 3: ShowView(View.Settings); break;
                case 4: QuitGame(); break;
            }
        }

        void ContinueGame()
        {
            string scene = MedusaHorror.Core.SaveSystem.ContinueLatest();
            if (string.IsNullOrEmpty(scene))
            {
                Debug.Log("[MainMenu] 没有可继续的存档");
                return;
            }
            LoadScene(scene);
        }

        void QuitGame()
        {
            PlayUi();
            Debug.Log("[MainMenu] 退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ 视图切换

        void ShowView(View view)
        {
            _view = view;
            _pendingDelete = -1;

            if (_mainRoot != null) _mainRoot.gameObject.SetActive(view == View.Main);
            if (view == View.Main)
            {
                if (_panelRoot != null)
                {
                    _panelRoot.gameObject.SetActive(false);
                    Destroy(_panelRoot.gameObject);
                    _panelRoot = null;
                }
                RefreshMain();
                return;
            }
            RebuildPanel();
        }

        void BackToMain()
        {
            PlayUi();
            ShowView(View.Main);
        }

        void RebuildPanel()
        {
            if (_panelRoot != null)
            {
                _panelRoot.gameObject.SetActive(false);
                Destroy(_panelRoot.gameObject);
            }
            _panelRoot = FullRect("Panel", _canvas.transform);
            _slotIndex = Mathf.Clamp(_slotIndex, 0, MedusaHorror.Core.SaveSystem.SlotCount - 1);

            var veil = FullRect("Veil", _panelRoot);
            var vImg = veil.gameObject.AddComponent<Image>();
            vImg.color = new Color(0.02f, 0.02f, 0.03f, 0.86f);
            vImg.raycastTarget = true;

            string title = _view == View.Load ? "载入存档"
                         : _view == View.NewGame ? "新游戏"
                         : "设置";
            string sub = _view == View.Load ? "选择一个存档继续你的旅程"
                       : _view == View.NewGame ? "选择存档槽开始新的一轮"
                       : "音频";

            var titleRt = R("PanelTitle", _panelRoot, MenuX, 92f, 900f, 70f);
            var tTxt = titleRt.gameObject.AddComponent<Text>();
            tTxt.font = _font;
            tTxt.fontSize = 52;
            tTxt.fontStyle = FontStyle.Bold;
            tTxt.text = title;
            tTxt.color = NeonYellow;
            tTxt.alignment = TextAnchor.MiddleLeft;
            tTxt.raycastTarget = false;

            var accent = R("PanelAccent", _panelRoot, MenuX - 2f, 96f, 6f, 60f);
            var aImg = accent.gameObject.AddComponent<Image>();
            aImg.color = NeonCyan;
            aImg.raycastTarget = false;

            var subRt = R("PanelSub", _panelRoot, MenuX, 166f, 900f, 34f);
            var sTxt = subRt.gameObject.AddComponent<Text>();
            sTxt.font = _font;
            sTxt.fontSize = 22;
            sTxt.text = sub;
            sTxt.color = new Color(0.60f, 0.64f, 0.68f, 0.85f);
            sTxt.alignment = TextAnchor.MiddleLeft;
            sTxt.raycastTarget = false;

            if (_view == View.Settings) BuildSettingsPage();
            else BuildSlotPage();

            var back = R("BtnBack", _panelRoot, MenuX, 1006f, 200f, 40f);
            BottomAnchor(back, MenuX, 22f, 200f, 40f);
            var backImg = back.gameObject.AddComponent<Image>();
            backImg.color = new Color(1f, 1f, 1f, 0.05f);
            var backBtn = back.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            backBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            backBtn.onClick.AddListener(BackToMain);
            var backTxt = R("BackLabel", back, 0f, 0f, 200f, 40f);
            BottomAnchor(backTxt, 0f, 4f, 200f, 34f);
            var bTxt = backTxt.gameObject.AddComponent<Text>();
            bTxt.font = _font;
            bTxt.fontSize = 20;
            bTxt.text = "← Esc 返回";
            bTxt.color = new Color(0.72f, 0.74f, 0.78f, 0.9f);
            bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.raycastTarget = false;
        }

        // ------------------------------------------------------------------ 存档页

        void BuildSlotPage()
        {
            for (int i = 0; i < MedusaHorror.Core.SaveSystem.SlotCount; i++)
                BuildSlotCard(i);

            BuildSlotDetail();

            string tip = _view == View.Load
                ? "Enter 载入  Delete 删除  Esc 返回"
                : "Enter 新建 / 覆盖  Esc 返回";
            var tipRt = R("SlotTip", _panelRoot, 740f, 1006f, 1000f, 34f);
            BottomAnchor(tipRt, 740f, 26f, 1000f, 34f);
            var tipTxt = tipRt.gameObject.AddComponent<Text>();
            tipTxt.font = _font;
            tipTxt.fontSize = 20;
            tipTxt.text = tip;
            tipTxt.color = new Color(0.78f, 0.81f, 0.85f, 0.95f);
            tipTxt.alignment = TextAnchor.MiddleLeft;
            tipTxt.raycastTarget = false;
        }

        void BuildSlotCard(int i)
        {
            var info = MedusaHorror.Core.SaveSystem.GetSlotInfo(i + 1);
            bool selected = i == _slotIndex;
            float y = 236f + i * 142f;

            var card = R("Card" + i, _panelRoot, MenuX, y, 600f, 122f);
            var cardImg = card.gameObject.AddComponent<Image>();
            cardImg.color = selected
                ? new Color(0.99f, 0.93f, 0.04f, 0.10f)
                : new Color(1f, 1f, 1f, 0.035f);

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardImg;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            int index = i;
            btn.onClick.AddListener(() =>
            {
                if (_slotIndex != index) { _slotIndex = index; PlayUi(); RebuildPanel(); }
                else ConfirmSlot();
            });

            var hover = card.gameObject.AddComponent<HoverProxy>();
            hover.onEnter = () =>
            {
                if (_slotIndex != index) { _slotIndex = index; RebuildPanel(); }
            };

            var edge = R("Edge", card, 0f, 0f, 5f, 122f);
            var eImg = edge.gameObject.AddComponent<Image>();
            eImg.color = selected ? NeonYellow : new Color(0.4f, 0.42f, 0.46f, 0.25f);
            eImg.raycastTarget = false;

            var num = R("Num", card, 26f, 22f, 70f, 80f);
            var nTxt = num.gameObject.AddComponent<Text>();
            nTxt.font = _font;
            nTxt.fontSize = 46;
            nTxt.fontStyle = FontStyle.Bold;
            nTxt.text = "0" + (i + 1);
            nTxt.color = selected ? NeonYellow : new Color(0.5f, 0.52f, 0.56f, 0.7f);
            nTxt.alignment = TextAnchor.MiddleLeft;
            nTxt.raycastTarget = false;

            bool pending = _pendingDelete == i + 1;
            string pendingText = _view == View.NewGame ? "再按一次 Enter 确认覆盖" : "再按一次 Delete 确认删除";
            string head = pending ? pendingText
                        : info.exists ? info.sceneName
                        : "空存档位";
            var headRt = R("Head", card, 106f, 20f, 470f, 44f);
            var hTxt = headRt.gameObject.AddComponent<Text>();
            hTxt.font = _font;
            hTxt.fontSize = 26;
            hTxt.text = head;
            hTxt.color = pending ? new Color(1f, 0.28f, 0.30f, 1f)
                       : info.exists ? new Color(0.94f, 0.94f, 0.96f, 1f)
                       : new Color(0.52f, 0.54f, 0.58f, 0.8f);
            hTxt.alignment = TextAnchor.MiddleLeft;
            hTxt.raycastTarget = false;

            string meta = info.exists
                ? string.Format("{0} · {1} 信用点 · 任务 {2} · {3}",
                    FormatTime(info.savedAt), info.coins, info.questsDone, FormatDuration(info.playSeconds))
                : (_view == View.NewGame ? "在此槽开始新的旅程" : "此槽暂无存档");
            var metaRt = R("Meta", card, 106f, 66f, 470f, 32f);
            var mTxt = metaRt.gameObject.AddComponent<Text>();
            mTxt.font = _font;
            mTxt.fontSize = 18;
            mTxt.text = meta;
            mTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            mTxt.color = new Color(0.60f, 0.63f, 0.67f, 0.8f);
            mTxt.alignment = TextAnchor.MiddleLeft;
            mTxt.raycastTarget = false;
        }

        void BuildSlotDetail()
        {
            var info = MedusaHorror.Core.SaveSystem.GetSlotInfo(_slotIndex + 1);

            var panel = R("Detail", _panelRoot, 760f, 236f, 1040f, 560f);
            var pImg = panel.gameObject.AddComponent<Image>();
            pImg.color = new Color(0.07f, 0.07f, 0.09f, 0.72f);

            var topEdge = R("TopEdge", panel, 0f, 0f, 1040f, 3f);
            var teImg = topEdge.gameObject.AddComponent<Image>();
            teImg.color = new Color(0.10f, 0.88f, 0.94f, 0.7f);
            teImg.raycastTarget = false;

            var label = R("Label", panel, 40f, 34f, 500f, 28f);
            var lTxt = label.gameObject.AddComponent<Text>();
            lTxt.font = _font;
            lTxt.fontSize = 18;
            lTxt.text = "存档槽 0" + (_slotIndex + 1) + " · SLOT DETAIL";
            lTxt.color = new Color(0.10f, 0.88f, 0.94f, 0.85f);
            lTxt.alignment = TextAnchor.MiddleLeft;
            lTxt.raycastTarget = false;

            string big = info.exists ? info.sceneName : "空存档位";
            var bigRt = R("Big", panel, 40f, 74f, 960f, 92f);
            var bTxt = bigRt.gameObject.AddComponent<Text>();
            bTxt.font = _font;
            bTxt.fontSize = 62;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.text = big;
            bTxt.color = info.exists ? NeonYellow : new Color(0.45f, 0.47f, 0.5f, 0.7f);
            bTxt.alignment = TextAnchor.MiddleLeft;
            bTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            bTxt.raycastTarget = false;

            var divider = R("Divider", panel, 40f, 182f, 960f, 2f);
            var dImg = divider.gameObject.AddComponent<Image>();
            dImg.color = new Color(1f, 1f, 1f, 0.12f);
            dImg.raycastTarget = false;

            if (!info.exists)
            {
                var empty = R("Empty", panel, 40f, 214f, 960f, 60f);
                var eTxt = empty.gameObject.AddComponent<Text>();
                eTxt.font = _font;
                eTxt.fontSize = 22;
                eTxt.text = _view == View.NewGame ? "回车即可在此槽建立新档" : "这个位置还没有冒险记录";
                eTxt.color = new Color(0.55f, 0.58f, 0.62f, 0.85f);
                eTxt.alignment = TextAnchor.MiddleLeft;
                eTxt.raycastTarget = false;
                return;
            }

            StatRow(panel, 216f, "当前进度", info.sceneName);
            StatRow(panel, 268f, "信用点", info.coins.ToString());
            StatRow(panel, 320f, "已完成任务", info.questsDone.ToString());
            StatRow(panel, 372f, "名下房产", info.housesOwned.ToString());
            StatRow(panel, 424f, "游玩时长", FormatDuration(info.playSeconds));
            StatRow(panel, 476f, "最后保存", FormatTime(info.savedAt));
        }

        void StatRow(RectTransform panel, float y, string key, string value)
        {
            var k = R("K" + key, panel, 40f, y, 220f, 34f);
            var kTxt = k.gameObject.AddComponent<Text>();
            kTxt.font = _font;
            kTxt.fontSize = 20;
            kTxt.text = key;
            kTxt.color = new Color(0.58f, 0.61f, 0.65f, 0.85f);
            kTxt.alignment = TextAnchor.MiddleLeft;
            kTxt.raycastTarget = false;

            var v = R("V" + key, panel, 280f, y, 700f, 34f);
            var vTxt = v.gameObject.AddComponent<Text>();
            vTxt.font = _font;
            vTxt.fontSize = 24;
            vTxt.text = value;
            vTxt.color = new Color(0.95f, 0.95f, 0.97f, 1f);
            vTxt.alignment = TextAnchor.MiddleLeft;
            vTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            vTxt.raycastTarget = false;
        }

        void MoveSlot(int delta)
        {
            _slotIndex = (_slotIndex + delta + MedusaHorror.Core.SaveSystem.SlotCount) % MedusaHorror.Core.SaveSystem.SlotCount;
            _pendingDelete = -1;
            PlayUi();
            RebuildPanel();
        }

        void ConfirmSlot()
        {
            int slot = _slotIndex + 1;
            if (_view == View.NewGame)
            {
                bool exists = MedusaHorror.Core.SaveSystem.HasSlot(slot);
                if (exists && _pendingDelete != slot)
                {
                    _pendingDelete = slot;
                    _pendingDeleteUntil = Time.unscaledTime + 2.5f;
                    RebuildPanel();
                    return;
                }
                _pendingDelete = -1;
                PlayUi();
                MedusaHorror.Core.SaveSystem.StartNewGame(slot);
                LoadScene(gameScene);
                return;
            }

            var info = MedusaHorror.Core.SaveSystem.GetSlotInfo(slot);
            if (!info.exists)
            {
                PlayUi();
                return;
            }
            PlayUi();
            MedusaHorror.Core.SaveSystem.SetCurrentSlot(slot);
            LoadScene(string.IsNullOrEmpty(info.scene) ? gameScene : info.scene);
        }

        void RequestDelete()
        {
            int slot = _slotIndex + 1;
            if (!MedusaHorror.Core.SaveSystem.HasSlot(slot)) return;

            if (_pendingDelete == slot)
            {
                MedusaHorror.Core.SaveSystem.DeleteSlot(slot);
                _pendingDelete = -1;
                PlayUi();
                RebuildPanel();
                return;
            }
            _pendingDelete = slot;
            _pendingDeleteUntil = Time.unscaledTime + 2.5f;
            RebuildPanel();
        }

        // ------------------------------------------------------------------ 设置页

        void BuildSettingsPage()
        {
            string[] names = { "主音量", "音乐", "音效" };
            var data = MedusaHorror.Core.SaveSystem.Data;
            float[] values = { data.masterVolume, data.musicVolume, data.sfxVolume };

            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                float y = 246f + i * 92f;
                bool selected = i == _settingsIndex;

                var row = R("Set" + i, _panelRoot, MenuX, y, 1000f, 72f);
                var rImg = row.gameObject.AddComponent<Image>();
                rImg.color = selected ? new Color(0.99f, 0.93f, 0.04f, 0.09f) : new Color(1f, 1f, 1f, 0.03f);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = rImg;
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                btn.onClick.AddListener(() => { _settingsIndex = index; RebuildPanel(); });
                var hover = row.gameObject.AddComponent<HoverProxy>();
                hover.onEnter = () => { if (_settingsIndex != index) { _settingsIndex = index; RebuildPanel(); } };

                var edge = R("Edge", row, 0f, 0f, 5f, 72f);
                var eImg = edge.gameObject.AddComponent<Image>();
                eImg.color = selected ? NeonYellow : new Color(0f, 0f, 0f, 0f);
                eImg.raycastTarget = false;

                var nameRt = R("Name", row, 30f, 0f, 300f, 72f);
                var nTxt = nameRt.gameObject.AddComponent<Text>();
                nTxt.font = _font;
                nTxt.fontSize = 26;
                nTxt.text = names[i];
                nTxt.color = selected ? NeonYellow : TextIdle;
                nTxt.alignment = TextAnchor.MiddleLeft;
                nTxt.raycastTarget = false;

                var track = R("Track", row, 360f, 31f, 480f, 10f);
                var tImg = track.gameObject.AddComponent<Image>();
                tImg.color = new Color(1f, 1f, 1f, 0.10f);
                tImg.raycastTarget = false;

                var fill = R("Fill", track, 0f, 0f, 480f, 10f);
                var fImg = fill.gameObject.AddComponent<Image>();
                fImg.color = NeonCyan;
                fImg.raycastTarget = false;
                fImg.type = Image.Type.Filled;
                fImg.fillMethod = Image.FillMethod.Horizontal;
                fImg.fillAmount = values[i];
                _settingFills[i] = fImg;

                var valRt = R("Val", row, 870f, 0f, 120f, 72f);
                var vTxt = valRt.gameObject.AddComponent<Text>();
                vTxt.font = _font;
                vTxt.fontSize = 24;
                vTxt.text = Mathf.RoundToInt(values[i] * 100f) + "%";
                vTxt.color = new Color(0.95f, 0.95f, 0.97f, 1f);
                vTxt.alignment = TextAnchor.MiddleRight;
                vTxt.raycastTarget = false;
                _settingValues[i] = vTxt;
            }

            var tipRt = R("SetTip", _panelRoot, MenuX, 1006f, 1200f, 34f);
            BottomAnchor(tipRt, MenuX, 26f, 1200f, 34f);
            var tipTxt = tipRt.gameObject.AddComponent<Text>();
            tipTxt.font = _font;
            tipTxt.fontSize = 20;
            tipTxt.text = "↑ ↓ 选择  ← → 调整  Esc 返回";
            tipTxt.color = new Color(0.78f, 0.81f, 0.85f, 0.95f);
            tipTxt.alignment = TextAnchor.MiddleLeft;
            tipTxt.raycastTarget = false;
        }

        void MoveSetting(int delta)
        {
            _settingsIndex = (_settingsIndex + delta + 3) % 3;
            PlayUi();
            RebuildPanel();
        }

        void AdjustSetting(float delta)
        {
            var d = MedusaHorror.Core.SaveSystem.Data;
            switch (_settingsIndex)
            {
                case 0: d.masterVolume = Mathf.Clamp01(d.masterVolume + delta); break;
                case 1: d.musicVolume = Mathf.Clamp01(d.musicVolume + delta); break;
                case 2: d.sfxVolume = Mathf.Clamp01(d.sfxVolume + delta); break;
            }
            float v = _settingsIndex == 0 ? d.masterVolume : _settingsIndex == 1 ? d.musicVolume : d.sfxVolume;
            if (_settingFills[_settingsIndex] != null) _settingFills[_settingsIndex].fillAmount = v;
            if (_settingValues[_settingsIndex] != null) _settingValues[_settingsIndex].text = Mathf.RoundToInt(v * 100f) + "%";
            ApplyVolumes();
            MedusaHorror.Core.SaveSystem.SavePreferences();
        }

        void ApplyVolumes()
        {
            var a = MedusaHorror.Core.AudioDirector.Instance;
            if (a == null) return;
            var d = MedusaHorror.Core.SaveSystem.Data;
            a.SetBusVolume(MedusaHorror.Core.AudioBus.Master, d.masterVolume);
            a.SetBusVolume(MedusaHorror.Core.AudioBus.Music, d.musicVolume);
            a.SetBusVolume(MedusaHorror.Core.AudioBus.Sfx, d.sfxVolume);
        }

        // ------------------------------------------------------------------ 工具

        void LoadScene(string scene)
        {
            if (string.IsNullOrEmpty(scene)) scene = gameScene;
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(scene))
                MedusaHorror.Core.SceneLoader.Load(scene, MedusaHorror.Core.SceneLoader.DisplayName(scene));
            else
                Debug.LogError("[MainMenu] Build Settings 中找不到场景 " + scene);
        }

        void PlayUi()
        {
            var a = MedusaHorror.Core.AudioDirector.Instance;
            if (a != null) a.PlayUi();
        }

        static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int h = total / 3600;
            int m = (total % 3600) / 60;
            if (h > 0) return h + " 小时 " + m + " 分";
            return m + " 分钟";
        }

        static string FormatTime(DateTime t)
        {
            if (t.Year < 2000) return "—";
            return t.ToString("yyyy-MM-dd HH:mm");
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        static void AddShadow(GameObject go, Color color, Vector2 distance)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = distance;
        }

        RectTransform FullRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>左上角锚点布局：x/y 从屏幕左上角量起，向下为正。</summary>
        RectTransform R(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>把元素底部锚定在窗口底边上方 bottom 像素处，避免窄/宽窗口下被裁。</summary>
        static void BottomAnchor(RectTransform rt, float x, float bottom, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(x, bottom);
            rt.sizeDelta = new Vector2(w, h);
        }

        static Sprite _scanline;
        static Sprite _gradient;

        static Sprite ScanlineSprite()
        {
            if (_scanline != null) return _scanline;
            const int h = 1024;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                float a = (y % 16) < 5 ? 0.30f : 0f;
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _scanline = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            _scanline.name = "ScanlineSprite";
            return _scanline;
        }

        static Sprite GradientSprite()
        {
            if (_gradient != null) return _gradient;
            const int h = 256;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                float a = 1f - (float)y / (h - 1);
                tex.SetPixel(0, y, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            _gradient = Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f), 100f);
            _gradient.name = "GradientSprite";
            return _gradient;
        }
    }

    /// <summary>鼠标悬停回调，用于让菜单跟随鼠标高亮。</summary>
    public class HoverProxy : MonoBehaviour, IPointerEnterHandler
    {
        public Action onEnter;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (onEnter != null) onEnter();
        }
    }
}
