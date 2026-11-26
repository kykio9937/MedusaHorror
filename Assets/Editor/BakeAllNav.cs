using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace MedusaHorror.EditorTools
{
    /// <summary>Bakes a NavMeshSurface into every gameplay scene.</summary>
    public static class BakeAllNav
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/navall.txt";
        static readonly string[] Scenes =
        {
            "Assets/Scenes/W1_Hospital.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        [MenuItem("Medusa/Bake NavMesh All Scenes")]
        public static void Run()
        {
            var sb = new StringBuilder();
            foreach (var path in Scenes)
            {
                if (!File.Exists(path)) { sb.AppendLine(Path.GetFileName(path) + ": missing"); continue; }
                var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var old = GameObject.Find("NavSurface");
                if (old != null) Object.DestroyImmediate(old);

                var go = new GameObject("NavSurface");
                var surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.BuildNavMesh();

                var tri = NavMesh.CalculateTriangulation();
                sb.AppendLine(Path.GetFileName(path) + ": verts=" + tri.vertices.Length + " tris=" + tri.indices.Length / 3);

                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
            }
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[BakeAllNav]\n" + sb);
        }
    }
}
