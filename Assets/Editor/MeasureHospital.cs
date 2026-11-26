using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class MeasureHospital
    {
        [MenuItem("Medusa/Measure Hospital Prefabs")]
        public static void Run()
        {
            var sb = new StringBuilder();
            string[] names = { "floorPlane", "floorPlane_jamb", "wallA", "wallA_half", "wallA_window_A", "roundCeiling", "doors_jamb_A", "doors_jamb_wide", "pillar_A", "stairsHall" };
            foreach (var n in names)
            {
                var guids = AssetDatabase.FindAssets(n + " t:Prefab", new[] { "Assets/HorrorEnvironment_Hospital" });
                var path = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == n);
                if (path == null) { sb.AppendLine(n + ": MISSING"); continue; }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                var rends = inst.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0) { sb.AppendLine(n + ": no renderer"); Object.DestroyImmediate(inst); continue; }
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                var cols = inst.GetComponentsInChildren<Collider>(true).Length;
                sb.AppendLine(string.Format("{0}: size={1} centerY={2:F2} colliders={3}", n, b.size.ToString("F2"), b.center.y, cols));
                Object.DestroyImmediate(inst);
            }
            File.WriteAllText(@"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/hospital_measure.txt", sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Measure]\n" + sb);
        }
    }
}
