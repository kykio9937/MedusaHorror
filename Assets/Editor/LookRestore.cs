using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Restores the original per-scene skybox/ambient and hides the NavMesh gizmo overlay.</summary>
    public static class LookRestore
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/lookrestore.txt";

        static readonly (string path, string skybox, UnityEngine.Rendering.AmbientMode mode)[] Scenes =
        {
            ("Assets/Scenes/W0_Base.unity", null, UnityEngine.Rendering.AmbientMode.Flat),
            ("Assets/Scenes/W1_Hospital.unity", "Default-Skybox", UnityEngine.Rendering.AmbientMode.Flat),
            ("Assets/Scenes/W2_Wasteland.unity", "ApocalipticSky", UnityEngine.Rendering.AmbientMode.Flat),
            ("Assets/Scenes/W3_Subway.unity", "Skybox2", UnityEngine.Rendering.AmbientMode.Trilight),
            ("Assets/Scenes/W4_NeonStreet.unity", "_Skybox", UnityEngine.Rendering.AmbientMode.Trilight),
            ("Assets/Scenes/W5_AlienGate.unity", "skyBox", UnityEngine.Rendering.AmbientMode.Skybox),
        };

        [MenuItem("Medusa/Restore Look And Hide NavMesh Gizmo")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var s in Scenes)
            {
                if (!File.Exists(s.path)) continue;
                var sc = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);

                Material sky = null;
                if (!string.IsNullOrEmpty(s.skybox))
                {
                    foreach (var g in AssetDatabase.FindAssets(s.skybox + " t:Material"))
                    {
                        var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                        if (m != null && m.name == s.skybox) { sky = m; break; }
                    }
                }
                if (sky == null && s.skybox == "Default-Skybox")
                    sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = s.mode;
                RenderSettings.reflectionIntensity = 1f;
                RenderSettings.fog = false;

                int hidden = Object.FindObjectsOfType<Unity.AI.Navigation.NavMeshSurface>(true).Length;

                foreach (var cam in Object.FindObjectsOfType<Camera>(true))
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                }

                sb.AppendLine(Path.GetFileName(s.path) + ": skybox=" + (sky != null ? sky.name : "<null>")
                    + " ambient=" + s.mode + " navSurfaces=" + hidden);
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
            }
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[LookRestore]\n" + sb);
        }
    }
}
