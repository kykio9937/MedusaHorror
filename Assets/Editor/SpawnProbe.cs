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
    public static class SpawnProbe
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Medusa Horror/Probe Spawn Volume")]
        public static void Run()
        {
            try { DoProbe(); }
            catch (Exception e) { Log.AppendLine("FATAL: " + e); Debug.LogError(e); }
            WriteReport();
            Debug.Log("[SpawnProbe] DONE");
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static void DoProbe()
        {
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single); }
            catch (Exception e) { Log.AppendLine("OPEN_FAILED: " + e.Message); return; }
            Physics.SyncTransforms();
            Log.AppendLine("SCENE=" + scene.name + " loaded=" + scene.isLoaded);

            float[] xs = new float[] { -21f, -20f, -19f };
            float[] zs = new float[] { 0.5f, 1.4f, 2.4f };
            foreach (var x in xs)
            {
                foreach (var z in zs)
                {
                    var hits = Physics.RaycastAll(new Vector3(x, 12.5f, z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore)
                                     .OrderByDescending(h => h.point.y).ToArray();
                    Log.AppendLine("---- PROBE x=" + x.ToString("F1") + " z=" + z.ToString("F1") + " ----");
                    if (hits.Length == 0) Log.AppendLine("   NO_HIT");
                    foreach (var h in hits.Take(8))
                        Log.AppendLine("   DROP y=" + h.point.y.ToString("F3") + " obj=" + GetPath(h.transform) + " col=" + h.collider.GetType().Name + " dist=" + h.distance.ToString("F2"));
                }
            }

            Log.AppendLine("---- CAPSULE OVERLAP DETAIL at (-20, 8.88, 1.4) ----");
            Vector3 pos = new Vector3(-20f, 8.88f, 1.4f);
            var cc = new Vector3(0f, 0.741f, 0f);
            float half = 1.702f * 0.5f - 0.306f;
            var p1 = pos + cc + Vector3.up * half;
            var p2 = pos + cc - Vector3.up * half;
            foreach (var o in Physics.OverlapCapsule(p1, p2, 0.29f, ~0, QueryTriggerInteraction.Ignore).OrderBy(o => o.name).Take(30))
            {
                var b = o.bounds;
                Log.AppendLine("   OVERLAP " + GetPath(o.transform) + " type=" + o.GetType().Name +
                               " boundsMinY=" + b.min.y.ToString("F2") + " maxY=" + b.max.y.ToString("F2") +
                               " centerXZ=(" + b.center.x.ToString("F1") + "," + b.center.z.ToString("F1") + ") sizeXZ=(" + b.size.x.ToString("F1") + "," + b.size.z.ToString("F1") + ")");
            }

            Log.AppendLine("---- WALKABLE HEIGHT SCAN (floor->ceiling) ----");
            foreach (var x in xs)
            {
                foreach (var z in zs)
                {
                    float floorY = float.NaN, ceilY = float.NaN;
                    string floorObj = "", ceilObj = "";
                    var drops = Physics.RaycastAll(new Vector3(x, 9.0f, z), Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore);
                    foreach (var h in drops.OrderByDescending(h => h.point.y))
                    {
                        if (h.point.y < 8.6f) { floorY = h.point.y; floorObj = GetPath(h.transform); break; }
                    }
                    var ups = Physics.RaycastAll(new Vector3(x, 8.95f, z), Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore);
                    if (ups.Length > 0)
                    {
                        var best = ups.OrderBy(h => h.point.y).First();
                        ceilY = best.point.y; ceilObj = GetPath(best.transform);
                    }
                    Log.AppendLine("   x=" + x.ToString("F1") + " z=" + z.ToString("F1") +
                                    " floor=" + (float.IsNaN(floorY) ? "NA" : floorY.ToString("F3")) + " (" + floorObj + ")" +
                                    " ceiling=" + (float.IsNaN(ceilY) ? "NA" : ceilY.ToString("F3")) + " (" + ceilObj + ")" +
                                    " clear=" + ((float.IsNaN(floorY) || float.IsNaN(ceilY)) ? "NA" : (ceilY - floorY).ToString("F2")));
                }
            }
        }

        static void WriteReport()
        {
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/spawn_probe.txt"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false));
                Debug.Log("[SpawnProbe] REPORT_WRITTEN: " + outPath);
            }
            catch (Exception e) { Debug.LogError("[SpawnProbe] WRITE_FAILED: " + e.Message); }
        }
    }
}
