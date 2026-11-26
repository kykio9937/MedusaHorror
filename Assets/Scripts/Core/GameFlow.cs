using System.Collections.Generic;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
using MedusaHorror.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace MedusaHorror.Core
{
    /// <summary>整局运行状态（P0-2 / P0-5 共用）。</summary>
    public enum RunState
    {
        Running,
        Failed,
        Won,
        Paused
    }

    /// <summary>
    /// 局内流程中枢：
    /// 1) 失败入口 Fail(...)：冻结玩家、启动死亡镜头表现、淡入失败面板，支持「重新开始 / 退出」；
    /// 2) 胜利入口 Win(...)：击杀美杜莎后延迟淡入结算面板，支持「再来一局 / 返回主菜单」；
    /// 3) ESC 暂停菜单：暂停时 timeScale 归零、屏蔽玩家操作，支持「继续 / 返回主菜单」；
    /// 4) 键盘兜底：R 重开、Q 退出、ESC 暂停/继续、M 返回主菜单，避免依赖鼠标点击（UGUI 按钮同样可用）。
    /// 说明：本工程的失败条件唯一且明确——「石化值满即死亡（石化致死）」，不存在血量体系。
    /// </summary>
    [DisallowMultipleComponent]
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        public const string MenuScene = "MainMenu";

        [Header("面板延迟（秒）：定格到面板淡入")]
        public float failDelay = 1.15f;
        public float winDelay = 2.35f;

        [Header("按键")]
        public Key restartKey = Key.R;
        public Key quitKey = Key.None;   // Q 已经用于切武器，退出给个空键避免误触
        public Key pauseKey = Key.Escape;
        public Key menuKey = Key.M;
        [Tooltip("胜利后「继续前进」按键：关闭结算面板，恢复自由行动去拾取钥匙")]
        public Key advanceKey = Key.C;

        /// <summary>编辑器诊断用的一次性 ESC 脉冲（读取后自动清零）。</summary>
        public static bool debugPausePulse;

        /// <summary>离场淡出进行中：屏蔽暂停/失败等其它面板流程。</summary>
        bool _leaving;

        public RunState State { get; private set; } = RunState.Running;
        public bool IsRunning { get { return State == RunState.Running; } }
        string panelState = "";
        public string Panel
        {
            get { return panelState; }
            private set
            {
                panelState = value ?? "";
                // 面板隐藏（Panel 置空）时同步清空标题，避免下次复用/诊断读到上一面板的旧文案
                if (panelState.Length == 0 && titleText != null) titleText.text = string.Empty;
            }
        }

        Canvas canvas;
        CanvasGroup group;
        Image vignette;
        Image card;
        Text titleText;
        Text subText;
        Text hintText;
        Button btn1;
        Button btn2;
        Text btn1Label;
        Text btn2Label;
        Button btn3;
        Text btn3Label;

        float panelTimer = -1f;
        float shakeTimer;
        bool built;

        // ---------------- 关卡计时 ----------------
        float _levelStart;
        bool _levelTiming;

        /// <summary>开始为本关计时（进入关卡时由关卡目标调用）。</summary>
        public void BeginLevelTimer()
        {
            _levelStart = Time.time;
            _levelTiming = true;
        }

        public void EndLevelTimer()
        {
            _levelTiming = false;
        }

        /// <summary>本关已用时（秒）。未开始计时返回 0。</summary>
        public float LevelElapsed
        {
            get { return _levelTiming ? Time.time - _levelStart : 0f; }
        }

        public static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--";
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return m.ToString("00") + ":" + s.ToString("00");
        }

        readonly List<MonoBehaviour> lensTargets = new List<MonoBehaviour>();
        readonly List<System.Reflection.FieldInfo> lensFields = new List<System.Reflection.FieldInfo>();
        readonly List<float> lensBase = new List<float>();
        readonly List<MonoBehaviour> distTargets = new List<MonoBehaviour>();
        readonly List<System.Reflection.FieldInfo> distFields = new List<System.Reflection.FieldInfo>();
        readonly List<float> distBase = new List<float>();

        // ---------------- 生命周期 ----------------

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUi();
            EnsureEventSystem();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>取（必要时创建）流程中枢。</summary>
        public static GameFlow Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("GameFlowRuntime");
            DontDestroyOnLoad(go);
            return go.AddComponent<GameFlow>();
        }

        /// <summary>静态失败入口：石化致死等系统直接调用。</summary>
        public static void FailRun(string title, string subtitle)
        {
            Ensure().Fail(title, subtitle);
        }

        /// <summary>静态胜利入口：Boss 死亡时调用。</summary>
        public static void WinRun(string title, string subtitle)
        {
            Ensure().Win(title, subtitle);
        }

        /// <summary>静态离场入口：顶层传送门开启后调用。nextScene 为空则回主菜单。</summary>
        public static void LeaveRun(string nextScene)
        {
            Ensure().LeaveLevel(nextScene);
        }

        // ---------------- 失败 ----------------

        public void Fail(string title, string subtitle)
        {
            if (State == RunState.Failed) return;
            State = RunState.Failed;
            Panel = "Failed";
            panelTimer = 0f;
            shakeTimer = 1.1f;

            titleText.text = string.IsNullOrEmpty(title) ? "你被石化了" : title;
            subText.text = BuildFailStats(subtitle);
            hintText.text = "按 R 重新开始　·　按 Q 退出游戏";
            ConfigureButtons("重新开始 (R)", Restart, "退出游戏 (Q)", QuitGame);
            ConfigureThird(null, null, false);

            FreezePlayer();
            CaptureLensTargets();
            ShowCursor();

            var audio = AudioDirector.Instance;
            if (audio != null)
            {
                audio.SetBattleMusic(false);
                audio.Play(SfxId.PetrifyBreak, 0.9f, 0.62f);
            }
            Debug.Log("[GameFlow] 失败：" + titleText.text);
        }

        /// <summary>失败结算文案：关卡 / 存活时间 / 击杀 / 等级 / 信用点。</summary>
        string BuildFailStats(string subtitle)
        {
            var d = SaveSystem.Data;
            int secs = Mathf.Max(0, Mathf.RoundToInt(d.playSeconds));
            string time = (secs / 60).ToString("00") + ":" + (secs % 60).ToString("00");
            string scene = PrettyScene(SceneManager.GetActiveScene().name);
            string head = string.IsNullOrEmpty(subtitle) ? "" : subtitle + (char)10;
            return head
                + "关卡 " + scene + "　·　存活 " + time
                + (char)10 + "击杀 " + d.killCount + "　·　等级 " + d.level + "　·　信用点 " + d.coins;
        }

        static string PrettyScene(string name)
        {
            if (string.IsNullOrEmpty(name)) return "-";
            if (name.StartsWith("W0")) return "基地";
            if (name.StartsWith("W2")) return "荒原";
            if (name.StartsWith("W3")) return "地下铁";
            if (name.StartsWith("W4")) return "霓虹街区";
            if (name.StartsWith("W5")) return "外星星门";
            if (name == "MainMenu") return "主菜单";
            return name;
        }

        // ---------------- 胜利 ----------------

        public void Win(string title, string subtitle)
        {
            if (State == RunState.Won || State == RunState.Failed) return;
            State = RunState.Won;
            Panel = "Won";
            panelTimer = 0f;
            shakeTimer = 0f;

            titleText.text = string.IsNullOrEmpty(title) ? "你击碎了美杜莎" : title;
            subText.text = subtitle ?? string.Empty;
            hintText.text = "按 C 继续前进　·　按 R 再来一局　·　按 M 返回主菜单";
            ConfigureButtons("继续前进 (C)", ContinueRun, "返回主菜单 (M)", ToMenu);
            ConfigureThird("重开本关 (R)", Restart, true);

            if (vignette != null) vignette.color = new Color(0.05f, 0.06f, 0.05f, 0f);
            FreezePlayer();
            ShowCursor();

            var audio = AudioDirector.Instance;
            if (audio != null)
            {
                audio.SetBattleMusic(false);
                audio.Play(SfxId.PetrifyBreak, 0.8f, 1f);
            }
            Debug.Log("[GameFlow] 胜利：" + titleText.text);
        }

        /// <summary>
        /// 胜利后继续探索：关闭结算面板并恢复自由行动（拾取钥匙、开启进度门）。
        /// 不影响「再来一局 / 返回主菜单」两条既有出口。
        /// </summary>
        public void ContinueRun()
        {
            if (State != RunState.Won) return;
            State = RunState.Running;
            Panel = "";
            panelTimer = -1f;
            Time.timeScale = 1f;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            if (vignette != null) vignette.color = new Color(0.05f, 0.06f, 0.05f, 0f);
            ConfigureThird(null, null, false);
            SetPlayerActive(true);
            // 恢复玩家后必须重新锁定光标，否则鼠标视角失效、走不到钥匙处
            var readers = Object.FindObjectsOfType<InputReader>();
            for (int i = 0; i < readers.Length; i++) readers[i].LockCursor(true);
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayUi();
            Debug.Log("[GameFlow] 继续前进：结算面板已关闭，恢复自由行动");
        }

        /// <summary>结算面板当前文案（诊断用）：标题 / 提示 / 三个按钮标签。</summary>
        public string PanelDiag()
        {
            string t = titleText != null ? titleText.text : "";
            string h = hintText != null ? hintText.text : "";
            string b1 = btn1Label != null ? btn1Label.text : "";
            string b2 = btn2Label != null ? btn2Label.text : "";
            string b3 = btn3Label != null ? btn3Label.text : "";
            bool s3 = btn3 != null && btn3.gameObject.activeSelf;
            return "state=" + State + " panel=" + (string.IsNullOrEmpty(Panel) ? "none" : Panel) +
                   " title=\"" + t + "\" hint=\"" + h + "\"" +
                   " b1=\"" + b1 + "\" b2=\"" + b2 + "\" b3=\"" + b3 + "\"(active=" + s3 + ")";
        }

        // ---------------- 暂停 ----------------

        public void Pause()
        {
            if (State != RunState.Running) return;
            State = RunState.Paused;
            Panel = "Paused";
            panelTimer = -1f;

            titleText.text = "已暂停";
            subText.text = "石化的凝视暂时凝固 · 按 ESC 继续前行";
            hintText.text = "按 ESC 继续　·　按 R 重新开始　·　按 Q 退出游戏";
            ConfigureButtons("继续游戏 (ESC)", Resume, "返回主菜单 (M)", ToMenu);
            ConfigureThird("设置", OpenSettings, true);

            if (vignette != null) vignette.color = new Color(0.03f, 0.03f, 0.04f, 0.55f);
            if (group != null) { group.alpha = 1f; group.blocksRaycasts = true; group.interactable = true; }
            Time.timeScale = 0f;
            SetPlayerActive(false);
            ShowCursor();
            Debug.Log("[GameFlow] 暂停");
        }

        public void Resume()
        {
            if (State != RunState.Paused) return;
            State = RunState.Running;
            Panel = "";
            panelTimer = -1f;
            Time.timeScale = 1f;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            SetPlayerActive(true);
            var readers = Object.FindObjectsOfType<InputReader>();
            for (int i = 0; i < readers.Length; i++) readers[i].LockCursor(true);
            Debug.Log("[GameFlow] 继续");
        }

        public void TogglePause()
        {
            if (State == RunState.Paused) Resume();
            else if (State == RunState.Running) Pause();
        }

        /// <summary>暂停面板打开设置页。</summary>
        public void OpenSettings()
        {
            SettingsPanel.Ensure().Open();
        }

        void SetPlayerActive(bool on)
        {
            var controllers = Object.FindObjectsOfType<ThirdPersonController>();
            for (int i = 0; i < controllers.Length; i++) controllers[i].enabled = on;

            var readers = Object.FindObjectsOfType<InputReader>();
            for (int i = 0; i < readers.Length; i++) readers[i].enabled = on;

            var weapons = Object.FindObjectsOfType<PlayerWeapon>();
            for (int i = 0; i < weapons.Length; i++) weapons[i].enabled = on;

            var interactors = Object.FindObjectsOfType<PlayerInteractor>();
            for (int i = 0; i < interactors.Length; i++) interactors[i].enabled = on;

            var camInputs = Object.FindObjectsOfType<ThirdPersonCameraInput>();
            for (int i = 0; i < camInputs.Length; i++) camInputs[i].enabled = on;
        }

        void FreezePlayer()
        {
            // 石化与凝视系统继续运行（保留定格表现），只冻结玩家操作链路
            SetPlayerActive(false);
        }

        static void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---------------- 重开 / 退出 / 主菜单 ----------------

        public void Restart()
        {
            Time.timeScale = 1f;
            State = RunState.Running;
            Panel = "";
            panelTimer = -1f;
            if (group != null) group.alpha = 0f;
            ShowCursor();

            var scene = SceneManager.GetActiveScene();
            Debug.Log("[GameFlow] 重新开始：" + scene.name);
            SceneLoader.Load(scene.name, "重新开始 · " + scene.name);
        }

        public void ToMenu()
        {
            Time.timeScale = 1f;
            State = RunState.Running;
            Panel = "";
            panelTimer = -1f;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            ShowCursor();
            Debug.Log("[GameFlow] 返回主菜单");
            if (Application.CanStreamedLevelBeLoaded(MenuScene)) SceneLoader.Load(MenuScene, "返回主菜单");
            else
            {
                Debug.LogWarning("[GameFlow] Build Settings 中没有 " + MenuScene + " 场景，改为重开当前关卡");
                var scene = SceneManager.GetActiveScene();
                SceneLoader.Load(scene.name, "重新开始 · " + scene.name);
            }
        }

        /// <summary>
        /// 离场：顶层传送门开启后由 ExitPortal 调用。不弹 Won 面板，淡出后切场景。
        /// nextScene 为空或不可加载时回主菜单。
        /// </summary>
        public void LeaveLevel(string nextScene)
        {
            if (_leaving) return;
            _leaving = true;
            State = RunState.Running;
            Panel = "";
            panelTimer = -1f;
            if (group != null) { group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false; }
            SetPlayerActive(false);
            ShowCursor();

            string target = string.IsNullOrEmpty(nextScene) ? MenuScene : nextScene;
            Debug.Log("[GameFlow] 离场：目标场景=" + target);
            StartCoroutine(LeaveRoutine(target));
        }

        System.Collections.IEnumerator LeaveRoutine(string target)
        {
            float t = 0f;
            float dur = Mathf.Max(0.15f, 1.05f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                if (vignette != null)
                {
                    Color c = vignette.color;
                    c.a = Mathf.Clamp01(t / dur);
                    vignette.color = c;
                }
                yield return null;
            }

            Time.timeScale = 1f;
            if (vignette != null)
            {
                Color c = vignette.color;
                c.a = 0f;
                vignette.color = c;
            }
            _leaving = false;
            if (Application.CanStreamedLevelBeLoaded(target))
            {
                SceneLoader.Load(target, target == MenuScene ? "返回主菜单" : "离场 · " + target);
            }
            else
            {
                Debug.LogWarning("[GameFlow] Build Settings 中没有 " + target + " 场景，改为返回主菜单");
                ToMenu();
            }

            // GameFlow 为 DontDestroyOnLoad：切场景后复位淡出状态，避免遮黑新场景
            _leaving = false;
            if (vignette != null)
            {
                Color c = vignette.color;
                c.a = 0f;
                vignette.color = c;
            }
        }

        public void QuitGame()
        {
            Debug.Log("[GameFlow] 退出游戏");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- 每帧 ----------------

        void Update()
        {
            if (_leaving) return;   // 离场淡出中：不再响应暂停/重开/主菜单
            var kb = GetKeyboard();
            bool pauseHit = false;
            if (kb != null && !InputReader.debugOverrideEnabled && kb[pauseKey].wasPressedThisFrame) pauseHit = true;
            if (debugPausePulse) { debugPausePulse = false; pauseHit = true; }
            if (State == RunState.Running && (InputReader.blocked || MedusaHorror.UI.InteractionPanelInput.ClosingThisFrame)) pauseHit = false;
            if (pauseHit)
            {
                if (State == RunState.Running) { Pause(); return; }
                if (State == RunState.Paused) { Resume(); return; }
            }

            if (State == RunState.Failed)
            {
                if (panelTimer >= 0f) panelTimer += Time.unscaledDeltaTime;
                if (HandleEndKeys()) return;
                FadeIn(failDelay, 0.86f);
                ApplyLens(Mathf.Clamp01(panelTimer / Mathf.Max(0.05f, failDelay)));
                Shake();
            }
            else if (State == RunState.Won)
            {
                if (panelTimer >= 0f) panelTimer += Time.unscaledDeltaTime;
                if (HandleEndKeys()) return;
                FadeIn(winDelay, 0.72f);
            }
            else if (State == RunState.Paused)
            {
                if (kb != null)
                {
                    if (kb[restartKey].wasPressedThisFrame) { Restart(); return; }
                    if (quitKey != Key.None && kb[quitKey].wasPressedThisFrame) { QuitGame(); return; }
                    if (kb[menuKey].wasPressedThisFrame) { ToMenu(); return; }
                }
            }
        }

        UnityEngine.InputSystem.Keyboard GetKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            return UnityEngine.InputSystem.Keyboard.current;
#else
            return null;
#endif
        }

        bool HandleEndKeys()
        {
            var kb = GetKeyboard();
            if (kb == null) return false;
            if (State == RunState.Won && kb[advanceKey].wasPressedThisFrame) { ContinueRun(); return true; }
            if (kb[restartKey].wasPressedThisFrame) { Restart(); return true; }
            if (State == RunState.Failed && quitKey != Key.None && kb[quitKey].wasPressedThisFrame) { QuitGame(); return true; }
            if (State == RunState.Won && kb[menuKey].wasPressedThisFrame) { ToMenu(); return true; }
            return false;
        }

        void FadeIn(float delay, float maxAlpha)
        {
            if (group != null)
            {
                float p = Mathf.Clamp01((panelTimer - delay) / 0.45f);
                group.alpha = p;
                group.blocksRaycasts = p > 0.4f;
                group.interactable = p > 0.4f;
            }
            if (vignette != null)
            {
                float k = Mathf.Clamp01(panelTimer / Mathf.Max(0.05f, delay));
                Color c = vignette.color;
                c.a = Mathf.Lerp(0f, maxAlpha, k);
                vignette.color = c;
            }
        }

        void Shake()
        {
            if (shakeTimer <= 0f) return;
            shakeTimer -= Time.unscaledDeltaTime;
            float amp = Mathf.Clamp01(shakeTimer) * 9f;
            if (card != null)
                card.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(Time.unscaledTime * 47f) * amp,
                                                                  Mathf.Cos(Time.unscaledTime * 39f) * amp);
        }

        // ---------------- Cinemachine 表现（反射，零编译依赖） ----------------

        void CaptureLensTargets()
        {
            lensTargets.Clear(); lensFields.Clear(); lensBase.Clear();
            distTargets.Clear(); distFields.Clear(); distBase.Clear();

            var all = Object.FindObjectsOfType<MonoBehaviour>();
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn != "CinemachineVirtualCamera" && tn != "Cinemachine3rdPersonFollow") continue;

                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic;

                if (tn == "CinemachineVirtualCamera")
                {
                    var lensField = mb.GetType().GetField("m_Lens", flags);
                    if (lensField != null)
                    {
                        object lens = lensField.GetValue(mb);
                        var fovField = lens.GetType().GetField("FieldOfView");
                        if (fovField != null)
                        {
                            lensTargets.Add(mb);
                            lensFields.Add(fovField);
                            lensBase.Add((float)fovField.GetValue(lens));
                        }
                    }
                }
                else
                {
                    var dField = mb.GetType().GetField("CameraDistance", flags);
                    if (dField != null && dField.FieldType == typeof(float))
                    {
                        distTargets.Add(mb);
                        distFields.Add(dField);
                        distBase.Add((float)dField.GetValue(mb));
                    }
                }
            }
            if (lensTargets.Count > 0)
                Debug.Log("[GameFlow] 死亡镜头：已捕获 " + lensTargets.Count + " 个 Cinemachine 参数");
        }

        void ApplyLens(float k)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                        System.Reflection.BindingFlags.NonPublic;

            // 1) 虚拟相机 FOV 收窄（70% 原值），营造"视线凝固"
            for (int i = 0; i < lensFields.Count; i++)
            {
                var mb = lensTargets[i];
                if (mb == null) continue;
                var lensField = mb.GetType().GetField("m_Lens", flags);
                if (lensField == null) continue;
                object lens = lensField.GetValue(mb);
                lensFields[i].SetValue(lens, Mathf.Lerp(lensBase[i], lensBase[i] * 0.70f, k));
                lensField.SetValue(mb, lens);
            }

            // 2) 第三人称跟随距离拉近到 55%
            for (int i = 0; i < distFields.Count; i++)
            {
                var mb = distTargets[i];
                if (mb == null) continue;
                distFields[i].SetValue(mb, Mathf.Lerp(distBase[i], distBase[i] * 0.55f, k));
            }
        }

        // ---------------- UI ----------------

        void ConfigureButtons(string label1, UnityEngine.Events.UnityAction action1,
                              string label2, UnityEngine.Events.UnityAction action2)
        {
            if (btn1 == null || btn2 == null) return;
            btn1.onClick.RemoveAllListeners();
            btn1.onClick.AddListener(() =>
            {
                var audio = AudioDirector.Instance;
                if (audio != null) audio.PlayUi();
                action1();
            });
            btn2.onClick.RemoveAllListeners();
            btn2.onClick.AddListener(() =>
            {
                var audio = AudioDirector.Instance;
                if (audio != null) audio.PlayUi();
                action2();
            });
            if (btn1Label != null) btn1Label.text = label1;
            if (btn2Label != null) btn2Label.text = label2;
        }

        /// <summary>第三枚按钮：仅胜利面板启用（重开本关），其余状态隐藏。</summary>
        void ConfigureThird(string label, UnityEngine.Events.UnityAction action, bool visible)
        {
            if (btn3 == null) return;
            btn3.gameObject.SetActive(visible);
            btn3.onClick.RemoveAllListeners();
            if (!visible || action == null) return;
            btn3.onClick.AddListener(() =>
            {
                var audio = AudioDirector.Instance;
                if (audio != null) audio.PlayUi();
                action();
            });
            if (btn3Label != null) btn3Label.text = label ?? string.Empty;
        }

        void BuildUi()
        {
            if (built) return;
            built = true;

            var canvasGo = new GameObject("GameFlowCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // 全屏暗化
            var vig = NewRect("FailVignette", canvasGo.transform);
            Stretch(vig);
            vignette = vig.gameObject.AddComponent<Image>();
            vignette.color = new Color(0.09f, 0.02f, 0.03f, 0f);
            vignette.raycastTarget = false;

            // 中央卡片
            var cardRt = NewRect("FlowCard", canvasGo.transform);
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(760f, 320f);
            cardRt.anchoredPosition = Vector2.zero;
            card = cardRt.gameObject.AddComponent<Image>();
            card.color = new Color(0.07f, 0.06f, 0.07f, 0.94f);

            Font font = CjkFont.Get();

            titleText = NewText("FlowTitle", cardRt, font, 56, new Vector2(0f, 92f), new Vector2(720f, 72f));
            // 初始面板是隐藏的，标题留空；真实文案一律由 Fail/Win/Pause 在显示时赋值
            titleText.text = string.Empty;
            titleText.color = new Color(0.93f, 0.86f, 0.78f, 1f);
            titleText.fontStyle = FontStyle.Bold;

            subText = NewText("FlowSub", cardRt, font, 24, new Vector2(0f, 22f), new Vector2(700f, 40f));
            subText.text = "石化值已满 · 美杜莎的凝视将你凝固成石";
            subText.color = new Color(0.86f, 0.83f, 0.79f, 0.92f);

            hintText = NewText("FlowHint", cardRt, font, 22, new Vector2(0f, -44f), new Vector2(700f, 36f));
            hintText.text = "按 R 重新开始　·　按 Q 退出游戏";
            hintText.color = new Color(0.80f, 0.76f, 0.70f, 0.85f);

            // 三枚按钮横向排布：胜利态左键为「继续前进」，中间返回主菜单，右侧重开本关。
            btn1 = NewButton("BtnPrimary", cardRt, font, "重新开始 (R)", new Vector2(-238f, -118f),
                new Color(0.36f, 0.20f, 0.18f, 0.96f), out btn1Label);
            btn2 = NewButton("BtnSecondary", cardRt, font, "退出游戏 (Q)", new Vector2(0f, -118f),
                new Color(0.18f, 0.18f, 0.20f, 0.96f), out btn2Label);
            btn3 = NewButton("BtnTertiary", cardRt, font, "重开本关 (R)", new Vector2(238f, -118f),
                new Color(0.20f, 0.22f, 0.26f, 0.96f), out btn3Label);
            ConfigureThird(null, null, false);
            ConfigureButtons("重新开始 (R)", Restart, "退出游戏 (Q)", QuitGame);

            canvasGo.SetActive(true);
        }

        Button NewButton(string name, Transform parent, Font font, string label, Vector2 pos, Color bg, out Text labelText)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(210f, 58f);
            rt.anchoredPosition = pos;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);
            btn.colors = colors;

            var labelRt = NewRect(name + "Label", rt);
            Stretch(labelRt);
            var txt = labelRt.gameObject.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 24;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.text = label;
            txt.color = new Color(0.97f, 0.95f, 0.92f, 1f);
            txt.raycastTarget = false;
            labelText = txt;

            // 按钮自身音效由 ConfigureButtons 统一挂钩
            return btn;
        }

        Text NewText(string name, Transform parent, Font font, int size, Vector2 pos, Vector2 sizeDelta)
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

        void EnsureEventSystem()
        {
            var es = Object.FindObjectOfType<EventSystem>();
            if (es != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}

