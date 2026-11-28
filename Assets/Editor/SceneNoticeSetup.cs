using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MedusaHorror.Gameplay;

namespace MedusaHorror.EditorTools
{
    public static class SceneNoticeSetup
    {
        const string Work = "E:/UnityProjects/PlayerAnimationWork";
        public static void Audit()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("请先退出运行并保存当前场景");
            string original = EditorSceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            try
            {
                foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var scene = EditorSceneManager.OpenScene(entry.path);
                    foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)))
                    {
                        var names = t.name;
                        for (var p = t.transform.parent; p != null; p = p.parent) names = p.name + "/" + names;
                        report.AppendLine(scene.name + " | " + names + " | " + t.text.Replace('\n', '|'));
                    }
                }
            }
            finally
            {
                File.WriteAllText(Work + "/scene-notice-audit.txt", report.ToString());
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            }
        }

        public static void Apply()
        {
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new System.InvalidOperationException("请先退出运行并保存当前场景");
            string original = EditorSceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            try
            {
                foreach (string path in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UI/SceneNotices" }).Select(AssetDatabase.GUIDToAssetPath))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
                foreach (var entry in EditorBuildSettings.scenes.Where(s => s.enabled))
                {
                    var scene = EditorSceneManager.OpenScene(entry.path);
                    bool changed = false;
                    foreach (var notice in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ProximityNotice>(true)))
                        if (notice.verticalOffset != 2.5f) { notice.verticalOffset = 2.5f; changed = true; }
                    foreach (var lettering in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MedusaHorror.UI.WorldSignText>(true)))
                        if (lettering.GetComponent<TextMesh>() == null) { Object.DestroyImmediate(lettering); changed = true; }
                    var texts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TextMesh>(true)).ToArray();
                    foreach (var text in texts)
                    {
                        if (HasParent(text.transform, "W1_ExperienceRoute") && text.text.Contains("线索"))
                        {
                            text.text = text.text.Split('/')[0].Trim();
                            changed = true;
                        }
                        bool route = text.name == "RouteSign" && HasParent(text.transform, "LaterLevelGuidance");
                        bool hospital = HasParent(text.transform, "W1_SimulationExpansion") &&
                            new[] { "Floor2Direction", "BossDirection", "RoofDirection", "SimulationExitStory" }.Contains(text.transform.parent != null ? text.transform.parent.name : "");
                        hospital |= HasParent(text.transform, "W1_ExperienceRoute") &&
                            new[] { "ReceptionRoute", "MiddleRoute", "SupplyRoute", "SurgeryRoute", "ReturnRoute" }.Contains(text.transform.parent != null ? text.transform.parent.name : "");
                        if (!route && !hospital) continue;
                        var content = text.text.Split('\n');
                        bool intro = content[0] == "荒废村庄" || content[0] == "废弃地铁" || content[0] == "霓虹街区" || content[0] == "异星之门";
                        var parent = text.transform.parent;
                        var target = hospital ? parent.gameObject : text.gameObject;
                        if (!intro && target.GetComponent<ProximityNotice>() == null)
                        {
                            var notice = target.AddComponent<ProximityNotice>();
                            notice.title = content[0]; notice.description = string.Join("\n", content.Skip(1));
                            notice.radius = hospital ? 5f : 7f;
                            notice.verticalOffset = 2.5f;
                        }
                        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                        report.AppendLine(scene.name + " 已迁移：" + text.text.Replace('\n', '|'));
                        // TextMesh 由 WorldSignText 依赖，禁用其对象而非删除组件，避免 Unity 重新挂回旧文字。
                        if (hospital) text.gameObject.SetActive(false);
                        else
                        {
                            var lettering = text.GetComponent<MedusaHorror.UI.WorldSignText>();
                            if (lettering != null) Object.DestroyImmediate(lettering);
                            Object.DestroyImmediate(text);
                        }
                        // 保留位置及区域触发组件，不保留漂浮文字和面板。
                        changed = true;
                    }
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                File.WriteAllText(Work + "/scene-notice-migration.txt", report.ToString());
                if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            }
        }

        static bool HasParent(Transform t, string name)
        {
            for (; t != null; t = t.parent) if (t.name == name) return true;
            return false;
        }
    }
}
