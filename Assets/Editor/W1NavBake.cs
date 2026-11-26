using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace MedusaHorror.EditorTools
{
    public static class W1NavBake
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w1nav.txt";

        [MenuItem("Medusa/Bake W1 NavMesh")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity", OpenSceneMode.Single);
            var old = GameObject.Find("NavSurface");
            if (old != null) Object.DestroyImmediate(old);

            var go = new GameObject("NavSurface");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            var tri = NavMesh.CalculateTriangulation();
            sb.AppendLine("navVerts=" + tri.vertices.Length + " navTris=" + tri.indices.Length / 3);
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
