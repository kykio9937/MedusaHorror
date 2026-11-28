using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MedusaHorror.UI;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>实际运行验证：换关、素材、位置、计时与 UI 阻挡；结束恢复原存档和场景。</summary>
    [InitializeOnLoad]
    public static class SceneNoticeValidation
    {
        const string Work = "E:/UnityProjects/PlayerAnimationWork/SceneNoticeCheck";
        const string Key = "Medusa.SceneNoticeValidation";
        static readonly string[] Scenes = { "W0_Base", "W1_Hospital", "W2_Wasteland", "W3_Subway", "W4_NeonStreet", "W5_AlienGate" };
        static double _since;
        static int _index, _stage, _passed, _errors, _warnings;
        static readonly StringBuilder Report = new StringBuilder();
        static SceneNoticeValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayState;
            Application.logMessageReceived += (text, stack, type) =>
            {
                if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
                if (type == LogType.Error || type == LogType.Exception) { _errors++; Report.AppendLine("ERROR " + text); }
                if (type == LogType.Warning) { _warnings++; Report.AppendLine("WARNING " + text); }
            };
        }

        public static void Start()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("先保存并退出运行");
            Directory.CreateDirectory(Work);
            Directory.CreateDirectory(Work + "/SaveBackup");
            var save = Application.persistentDataPath;
            foreach (var file in Directory.GetFiles(save).Where(IsSave)) File.Copy(file, Work + "/SaveBackup/" + Path.GetFileName(file), true);
            File.WriteAllLines(Work + "/save-original-files.txt", Directory.GetFiles(save).Where(IsSave).Select(Path.GetFileName));
            SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key, true);
            EditorApplication.isPlaying = true;
        }

        static bool IsSave(string file) => file.EndsWith(".json") || file.EndsWith(".txt");
        static void OnPlayState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _index = _stage = _passed = _errors = _warnings = 0; Report.Clear();
                var gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameView != null) EditorWindow.GetWindow(gameView).Show();
                Load();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    var save = Application.persistentDataPath;
                    var originals = File.ReadAllLines(Work + "/save-original-files.txt");
                    foreach (var file in Directory.GetFiles(save).Where(IsSave))
                        if (!originals.Contains(Path.GetFileName(file))) File.Delete(file);
                    foreach (var name in originals) File.Copy(Work + "/SaveBackup/" + name, Path.Combine(save, name), true);
                    foreach (var name in originals)
                        if (!File.ReadAllBytes(Work + "/SaveBackup/" + name).SequenceEqual(File.ReadAllBytes(Path.Combine(save, name))))
                            throw new InvalidOperationException("存档恢复核对失败 " + name);
                    File.AppendAllText(Work + "/result.txt", "\n原存档逐文件恢复并核对一致。\n");
                    string original = SessionState.GetString(Key + ".scene", "");
                    if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
                }
                finally { SessionState.SetBool(Key, false); }
            }
        }

        static void Load()
        {
            SceneManager.LoadScene(Scenes[_index]);
            _since = EditorApplication.timeSinceStartup; _stage = 0;
        }
        static Transform Find(string name) => UnityEngine.Object.FindObjectsOfType<Canvas>(true)
            .Select(c => c.transform.Find(name)).FirstOrDefault(t => t != null);
        static void Check(bool pass, string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            _passed++; Report.AppendLine("PASS " + Scenes[_index] + " " + message);
        }
        static void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Work + "/" + name + ".png");
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - _since;
                if (_stage == 0 && elapsed > 1.3)
                {
                    var title = Find("SceneTitle"); var room = Find("RoomNotice");
                    if (title == null || title.GetComponent<CanvasGroup>().alpha < 0.9f)
                    {
                        if (elapsed < 30) return;
                    }
                    Check(title != null && title.GetComponent<CanvasGroup>().alpha > 0.9f, "入场标题正常显示");
                    Check(title.GetComponent<RectTransform>().anchorMin == new Vector2(0.5f, 1), "标题位于顶部中央");
                    Check(room.GetComponent<RectTransform>().anchorMin == new Vector2(0.69f, 0.51f), "区域提示位于中间偏右");
                    Check(title.Find("SceneMaterial").GetComponent<Image>().sprite != null, "对应场景素材已加载");
                    var theme = SceneNoticeTheme.For(Scenes[_index]);
                    Check(title.Find("SceneMaterial").GetComponent<Image>().sprite.name == theme.key, "素材随场景正确切换");
                    Check(title.GetComponentsInChildren<Text>().All(t => t.font != null && t.font.HasCharacter('荒') && t.font.HasCharacter('医')), "中文字符正常");
                    Check(title.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), "提示不阻挡鼠标交互");
                    var objective = Find("ObjectiveCard");
                    if (objective != null && objective.GetComponentInParent<CanvasGroup>().alpha > 0)
                    {
                        Check(!ScreenRect(title).Overlaps(ScreenRect(objective)), "场景标题与任务进度卡不重叠");
                        Check(!ScreenRect(room).Overlaps(ScreenRect(objective)), "区域提示与任务进度卡不重叠");
                    }
                    Check(!UnityEngine.Object.FindObjectsOfType<TextMesh>().Any(t => t.name == "RouteSign"), "无旧悬浮引导文字");
                    Capture(Scenes[_index] + "-entry"); _since = EditorApplication.timeSinceStartup; _stage = 1;
                }
                if (_stage == 1 && elapsed > 7)
                {
                    Check(Find("SceneTitle").GetComponent<CanvasGroup>().alpha == 0, "标题数秒后自动消失");
                    if (_index == 1)
                    {
                        StoryBanner.Clear();
                        StoryBanner.Show("值班室 · 搜索训练", "寻找值班室钥匙。靠近并用准星对准物品后，按 E 拾取。", 3f);
                        _since = EditorApplication.timeSinceStartup; _stage = 2;
                    }
                    else Next();
                }
                else if (_stage == 2 && elapsed > 0.8)
                {
                    Check(Find("RoomNotice").GetComponent<CanvasGroup>().alpha > 0.9f, "房间任务提示正常显示");
                    Capture("W1-room-notice");
                    _since = EditorApplication.timeSinceStartup; _stage = 6;
                }
                else if (_stage == 6 && elapsed > 0.3)
                {
                    PlayerPanel.Ensure().Open(); _since = EditorApplication.timeSinceStartup; _stage = 3;
                }
                else if (_stage == 3 && elapsed > 1)
                {
                    Check(Find("RoomNotice").GetComponent<CanvasGroup>().alpha == 0, "背包打开时提示隐藏");
                    PlayerPanel.Ensure().Close(); _since = EditorApplication.timeSinceStartup; _stage = 4;
                }
                else if (_stage == 4 && elapsed > 0.5)
                {
                    Check(Find("RoomNotice").GetComponent<CanvasGroup>().alpha > 0.5f, "关闭背包后继续显示剩余时间");
                    _stage = 5;
                }
                else if (_stage == 5 && elapsed > 4)
                {
                    Check(Find("RoomNotice").GetComponent<CanvasGroup>().alpha == 0, "房间提示自动消失");
                    Next();
                }
            }
            catch (Exception e) { Report.AppendLine("FAIL " + e); _errors++; Finish(); }
        }
        static void Next() { if (++_index == Scenes.Length) Finish(); else Load(); }
        static Rect ScreenRect(Transform target)
        {
            var corners = new Vector3[4]; ((RectTransform)target).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        static void Finish()
        {
            File.WriteAllText(Work + "/result.txt", Report + "\n断言通过=" + _passed + " 错误=" + _errors + " 警告=" + _warnings);
            EditorApplication.isPlaying = false;
        }
    }
}
