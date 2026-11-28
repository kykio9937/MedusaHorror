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
using MedusaHorror.Gameplay;

namespace MedusaHorror.EditorTools
{
    [InitializeOnLoad]
    public static class MinimapValidation
    {
        const string Work = "E:/UnityProjects/PlayerAnimationWork/MinimapCheck";
        const string Key = "Medusa.MinimapCheck";
        static readonly string[] Scenes = { "W0_Base", "W1_Hospital", "W2_Wasteland", "W3_Subway", "W4_NeonStreet", "W5_AlienGate" };
        static readonly Vector3[] HospitalPoints = { new Vector3(-10, 0.2f, 26), new Vector3(0, 0.2f, 26), new Vector3(10, 0.2f, 38), new Vector3(-5, 8.2f, 145), new Vector3(6, 14.2f, 150) };
        static readonly string[] HospitalAreas = { "值班室", "一楼住院走廊", "检查室", "二楼环形侧廊", "天台星门平台" };
        static double _since;
        static int _index, _stage, _point, _checks, _errors, _warnings;
        static GameObject _target;
        static bool _fog;
        static UnityEngine.Rendering.AmbientMode _ambient;
        static Color _light;
        static readonly StringBuilder Report = new StringBuilder();
        static MinimapValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StateChanged;
            Application.logMessageReceived += (text, stack, type) =>
            {
                if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
                if (type == LogType.Error || type == LogType.Exception) { _errors++; Report.AppendLine("ERROR " + text); }
                if (type == LogType.Warning) { _warnings++; Report.AppendLine("WARNING " + text); }
            };
        }
        public static void Start()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("请先保存并退出运行");
            Directory.CreateDirectory(Work + "/SaveBackup");
            var files = Directory.GetFiles(Application.persistentDataPath).Where(IsSave).ToArray();
            File.WriteAllLines(Work + "/original-files.txt", files.Select(Path.GetFileName));
            foreach (var file in files) File.Copy(file, Work + "/SaveBackup/" + Path.GetFileName(file), true);
            SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static bool IsSave(string path) => path.EndsWith(".json") || path.EndsWith(".txt");
        static void StateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                _index = _stage = _checks = _errors = _warnings = 0; Report.Clear();
                var view = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (view != null) EditorWindow.GetWindow(view).Show();
                Load();
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                try
                {
                    var names = File.ReadAllLines(Work + "/original-files.txt");
                    foreach (var file in Directory.GetFiles(Application.persistentDataPath).Where(IsSave))
                        if (!names.Contains(Path.GetFileName(file))) File.Delete(file);
                    foreach (var name in names)
                    {
                        string target = Path.Combine(Application.persistentDataPath, name);
                        File.Copy(Work + "/SaveBackup/" + name, target, true);
                        if (!File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(Work + "/SaveBackup/" + name))) throw new InvalidOperationException("存档恢复失败");
                    }
                    File.AppendAllText(Work + "/result.txt", "\n存档逐文件恢复并核对一致。\n");
                    var original = SessionState.GetString(Key + ".scene", "");
                    if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
                }
                finally { SessionState.SetBool(Key, false); }
            }
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(Scenes[_index] + " " + message);
            _checks++; Report.AppendLine("PASS " + Scenes[_index] + " " + message);
        }
        static void Load() { SceneManager.LoadScene(Scenes[_index]); _stage = 0; _since = EditorApplication.timeSinceStartup; }
        static void Stage(int stage) { _stage = stage; _since = EditorApplication.timeSinceStartup; }
        static void Warp(Vector3 position)
        {
            var player = UnityEngine.Object.FindObjectOfType<ThirdPersonController>();
            var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
            player.transform.position = position; controller.enabled = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                double elapsed = EditorApplication.timeSinceStartup - _since;
                var map = LevelMinimap.Instance;
                if (_stage == 0)
                {
                    if (map == null || map.SceneName != Scenes[_index] || map.PositionText.Contains("等待") || InputReader.blocked || elapsed < 1) return;
                    Check(map.Panel.anchorMin == new Vector2(0,1) && map.Panel.pivot == new Vector2(0,1), "地图和位置固定在左上角");
                    Check(map.CurrentArea != "定位中" && map.PositionText.Contains("X ") && map.PositionText.Contains("Z "), "实时区域与坐标显示");
                    Check(map.Panel.Find("LocationInfo/SceneAndRoom").GetComponent<Text>().text.Contains(new MapLocationResolver(Scenes[_index]).SceneName), "场景标签已切换");
                    Check(map.Panel.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), "地图不抢鼠标交互");
                    Check(map.MapCamera.targetTexture != null && !map.MapCamera.enabled, "地图使用缓存渲染");
                    Check(map.Panel.Find("LocationInfo").GetComponent<Image>().sprite != null, "位置面板使用本场景素材");
                    Check(MapLocationResolver.Heading(0) == "北" && MapLocationResolver.Heading(90) == "东" && MapLocationResolver.Heading(-90) == "西", "朝向换算正确");
                    float radius = map.VisibleRadius;
                    map.HandleScroll(1); Check(map.VisibleRadius < radius, "滚轮向上放大地图");
                    map.HandleScroll(-1); Check(Mathf.Abs(map.VisibleRadius - radius) < 0.01f, "滚轮向下缩小地图");
                    map.Zoom(100); Check(map.VisibleRadius <= radius * 2 + 0.01f, "缩小范围有限制");
                    map.Zoom(0.001f); Check(map.VisibleRadius >= radius * 0.5f - 0.01f, "放大范围有限制");
                    map.Zoom(2);
                    map.SetCollapsed(true); map.HandleScroll(1);
                    Check(Mathf.Abs(map.VisibleRadius - radius) < 0.01f && map.Panel.Find("LocationInfo").gameObject.activeInHierarchy, "折叠保留位置且不响应滚轮");
                    map.SetCollapsed(false); PlayerPanel.Ensure().Open(); map.HandleScroll(1);
                    Check(Mathf.Abs(map.VisibleRadius - radius) < 0.01f, "背包滚动不影响地图");
                    PlayerPanel.Ensure().Close();
                    _fog = RenderSettings.fog; _ambient = RenderSettings.ambientMode; _light = RenderSettings.ambientLight;
                    Stage(1);
                }
                else if (_stage == 1 && elapsed > 2)
                {
                    Check(RenderSettings.fog == _fog && RenderSettings.ambientMode == _ambient && RenderSettings.ambientLight == _light, "地图渲染后还原场景光照与雾");
                    if (Scenes[_index] == "W3_Subway") Check(map.IconReport().Contains("Train "), "列车入口显示导航图标");
                    if (Scenes[_index] == "W4_NeonStreet") Check(map.IconReport().Contains("Shop "), "商店显示地图图标");
                    var player = UnityEngine.Object.FindObjectOfType<ThirdPersonController>();
                    _target = new GameObject("MinimapValidationPortal");
                    _target.transform.position = player.transform.position + new Vector3(1000, 0, 1000);
                    _target.AddComponent<BasePortal>(); Stage(2);
                }
                else if (_stage == 2 && elapsed > 2)
                {
                    Check(map.IconReport().Contains("MinimapValidationPortal"), "运行中新增目标被发现");
                    foreach (var image in map.Panel.Find("View/Icons").GetComponentsInChildren<Image>())
                        if (image.enabled) Check(Mathf.Abs(image.rectTransform.anchoredPosition.x) <= 116.1f && Mathf.Abs(image.rectTransform.anchoredPosition.y) <= 116.1f, "图标保持在地图边界内");
                    _target.SetActive(false); Stage(3);
                }
                else if (_stage == 3 && elapsed > 2)
                {
                    Check(!map.IconReport().Contains("MinimapValidationPortal"), "失效目标图标及时移除");
                    UnityEngine.Object.Destroy(_target); _target = null;
                    ScreenCapture.CaptureScreenshot(Work + "/" + Scenes[_index] + ".png");
                    if (_index == 1) { _point = 0; Warp(HospitalPoints[0]); Stage(4); }
                    else if (_index == 4) { Warp(new Vector3(-12.5f, 0.2f, 3)); Stage(5); }
                    else Next();
                }
                else if (_stage == 4 && elapsed > 0.8)
                {
                    Check(map.CurrentArea == HospitalAreas[_point], "进入/离开区域更新为 " + HospitalAreas[_point]);
                    if (_point == 3) Check(Mathf.Abs(map.MapCamera.transform.position.y - 12.6f) < 0.01f, "二楼地图自动切层");
                    if (_point == 4) Check(Mathf.Abs(map.MapCamera.transform.position.y - 25) < 0.01f, "天台地图自动切层");
                    ScreenCapture.CaptureScreenshot(Work + "/Hospital-area-" + _point + ".png");
                    if (++_point < HospitalPoints.Length) { Warp(HospitalPoints[_point]); Stage(4); }
                    else Next();
                }
                else if (_stage == 5 && elapsed > 0.8)
                {
                    Check(map.CurrentArea == "霓虹公寓 7B", "进入公寓更新房间名");
                    Check(Mathf.Abs(map.MapCamera.transform.position.y - 3) < 0.01f, "进入公寓切换室内地图高度"); Next();
                }
            }
            catch (Exception e) { _errors++; Report.AppendLine("FAIL " + e); Finish(); }
        }
        static void Next() { if (++_index == Scenes.Length) Finish(); else Load(); }
        static void Finish()
        {
            File.WriteAllText(Work + "/result.txt", Report + "\n通过=" + _checks + " 错误=" + _errors + " 警告=" + _warnings);
            EditorApplication.isPlaying = false;
        }
    }
}
