using System.Collections.Generic;
using System.IO;
using MedusaHorror.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// P0-5 流程壳：程序化生成 MainMenu 场景并写入 Build Settings。
    /// 主菜单场景只包含「一台相机 + MainMenuScreen 组件」，UI 全部运行时构建。
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string MenuAssetPath = "Assets/Scenes/MainMenu.unity";
        public const string FirstLevelPath = "Assets/Scenes/W2_Wasteland.unity";

        [MenuItem("MedusaHorror/P0/Build Main Menu Scene")]
        public static void BuildMenuFromMenu()
        {
            Debug.Log("[MainMenuBuilder] " + Build());
        }

        [MenuItem("MedusaHorror/P0/Apply Build Settings Order")]
        public static void ApplyBuildOrderFromMenu()
        {
            Debug.Log("[MainMenuBuilder] " + ApplyBuildOrder());
        }

        /// <summary>创建（或重建）主菜单场景，并返回人类可读摘要。</summary>
        public static string Build()
        {
            if (EditorApplication.isPlaying)
                return "FAIL: 请先退出 Play 模式再生成主菜单场景";

            string activePath = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(activePath))
            {
                try { EditorSceneManager.SaveOpenScenes(); }
                catch (System.Exception e) { Debug.LogWarning("[MainMenuBuilder] 保存当前场景失败：" + e.Message); }
            }

            var dir = Path.GetDirectoryName(MenuAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 1.2f, -10f);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.028f, 0.032f, 1f);
            cam.fieldOfView = 55f;
            camGo.AddComponent<AudioListener>();

            var menuGo = new GameObject("MainMenuScreen");
            var screen = menuGo.AddComponent<MainMenuScreen>();
            screen.gameScene = "W2_Wasteland";

            bool saved = EditorSceneManager.SaveScene(scene, MenuAssetPath);
            string order = ApplyBuildOrder();

            if (!string.IsNullOrEmpty(activePath) && File.Exists(activePath))
            {
                try { EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single); }
                catch (System.Exception e) { Debug.LogWarning("[MainMenuBuilder] 回到原场景失败：" + e.Message); }
            }

            AssetDatabase.Refresh();
            return (saved ? "OK" : "SAVE_FAIL") + ": MainMenu -> " + MenuAssetPath + " | " + order;
        }

        /// <summary>Build Settings 顺序：保留现有场景，剔除已移除的医院场景，主菜单置首。</summary>
        public static string ApplyBuildOrder()
        {
            var list = new List<EditorBuildSettingsScene>();
            if (File.Exists(MenuAssetPath)) list.Add(new EditorBuildSettingsScene(MenuAssetPath, true));
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == MenuAssetPath) continue;
                if (s.path.EndsWith("W1_Hospital.unity")) continue;
                if (s.path.EndsWith("W1_Sandbox.unity")) continue;
                list.Add(s);
            }
            EditorBuildSettings.scenes = list.ToArray();

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) sb.Append(" / ");
                sb.Append(i).Append("=").Append(Path.GetFileNameWithoutExtension(list[i].path));
            }
            return "BUILDSET: " + sb;
        }

        public static string Describe()
        {
            var arr = EditorBuildSettings.scenes;
            var sb = new System.Text.StringBuilder();
            sb.Append("count=").Append(arr.Length);
            for (int i = 0; i < arr.Length; i++)
                sb.Append(" [").Append(i).Append("]").Append(Path.GetFileNameWithoutExtension(arr[i].path))
                  .Append(arr[i].enabled ? "(on)" : "(off)");
            sb.Append(" | menuExists=").Append(File.Exists(MenuAssetPath));
            return sb.ToString();
        }
    }
}
