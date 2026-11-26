using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Adds side fill lights to the W0 hall (non-destructive).</summary>
    public static class W0Lights
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w0lights.txt";

        [MenuItem("Medusa/Add W0 Side Lights")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity", OpenSceneMode.Single);
            var hall = GameObject.Find("BaseHall");
            if (hall == null) { sb.AppendLine("no BaseHall"); }
            else
            {
                var old = hall.transform.Find("SideLights");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var root = new GameObject("SideLights").transform;
                root.SetParent(hall.transform, false);
                int n = 0;
                float[] xs = { -14f, 14f };
                float[] zs = { -8f, 4f };
                foreach (var x in xs)
                    foreach (var z in zs)
                    {
                        var go = new GameObject("SideLight");
                        go.transform.SetParent(root, false);
                        go.transform.localPosition = new Vector3(x, 4.6f, z);
                        var l = go.AddComponent<Light>();
                        l.type = LightType.Point;
                        l.color = new Color(0.55f, 0.7f, 1f);
                        l.intensity = 2.4f;
                        l.range = 16f;
                        n++;
                    }
                sb.AppendLine("side lights added=" + n);
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
            }
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
