using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// Normalises the per-scene look that makes every level render blue:
    /// skybox material -> none, ambient -> flat neutral, camera clear -> solid dark colour.
    /// </summary>
    public static class SceneLookFix
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/lookfix.txt";
        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W1_Hospital.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        [MenuItem("Medusa/Fix Blue Look On All Scenes")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) continue;
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var before = RenderSettings.skybox != null ? RenderSettings.skybox.name : "<null>";
                var beforeAmb = RenderSettings.ambientMode.ToString();

                RenderSettings.skybox = null;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.40f, 0.41f, 0.44f);
                RenderSettings.ambientIntensity = 1f;
                RenderSettings.reflectionIntensity = 0.4f;
                RenderSettings.fog = false;

                int cams = 0;
                foreach (var cam in Object.FindObjectsOfType<Camera>(true))
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.03f, 0.035f, 0.045f);
                    cams++;
                }

                sb.AppendLine(Path.GetFileName(path) + ": skybox " + before + " -> null, ambient " + beforeAmb + " -> Flat, cameras=" + cams);
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
            }
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[SceneLookFix]\n" + sb);
        }
    }
}
