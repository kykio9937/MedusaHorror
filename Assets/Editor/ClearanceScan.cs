using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    public static class ClearanceScan
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Medusa Horror/Scan Clearance")]
        public static void Run()
        {
            try { DoScan(); }
            catch (Exception e) { Log.AppendLine("FATAL: " + e); Debug.LogError(e); }
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/clearance_scan.txt"));
            try { File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false)); Debug.Log("[ClearanceScan] REPORT_WRITTEN: " + outPath); }
            catch (Exception e) { Debug.LogError("[ClearanceScan] WRITE_FAILED: " + e.Message); }
            Debug.Log("[ClearanceScan] DONE");
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static void DoScan()
        {
            Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            Physics.SyncTransforms();
            Log.AppendLine("SCENE=" + scene.name);
            var spawn = new Vector3(-20f, 8.88f, 1.4f);
            var results = new List<string>();
            var candidates = new List<Vector3>();

            for (float x = -40f; x <= 34f; x += 2f)
            {
                for (float z = -52f; z <= 52f; z += 2f)
                {
                    RaycastHit dh;
                    if (!Physics.Raycast(new Vector3(x, 8.95f, z), Vector3.down, out dh, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (dh.point.y < 8.55f || dh.point.y > 9.0f) continue;
                    var ups = Physics.RaycastAll(new Vector3(x, dh.point.y + 0.1f, z), Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore);
                    float ceilY = float.MaxValue;
                    string ceilObj = "";
                    foreach (var u in ups) if (u.point.y > dh.point.y + 0.3f && u.point.y < ceilY) { ceilY = u.point.y; ceilObj = GetPath(u.transform); }
                    if (ceilY == float.MaxValue) continue;
                    float clear = ceilY - dh.point.y;
                    if (clear < 2.2f) continue;
                    var center = new Vector3(x, dh.point.y + 1.1f, z);
                    if (Physics.CheckSphere(center, 0.75f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    candidates.Add(new Vector3(x, dh.point.y + 0.11f, z));
                    results.Add("   OK x=" + x.ToString("F1") + " z=" + z.ToString("F1") + " floorY=" + dh.point.y.ToString("F2") + " ceilY=" + ceilY.ToString("F2") + " clear=" + clear.ToString("F2") + " ceilObj=" + ceilObj + " distToSpawn=" + Vector3.Distance(new Vector3(x, 0, z), new Vector3(spawn.x, 0, spawn.z)).ToString("F1"));
                }
            }
            Log.AppendLine("CLEARANCE_OK_POINTS=" + results.Count + " (step=2m, clear>=2.2m, sphere r=0.75 free)");
            foreach (var r in results.OrderBy(s => double.Parse(s.Substring(s.IndexOf("distToSpawn=") + 12))).Take(15)) Log.AppendLine(r);
        }
    }
}
