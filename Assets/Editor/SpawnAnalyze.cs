using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class SpawnAnalyze
    {
        static readonly Vector3 Spawn = new Vector3(-20f, 8.85f, 2.4f);
        static StringBuilder log = new StringBuilder();

        [MenuItem("Medusa Horror/Analyze Spawn")]
        public static void Run()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity", OpenSceneMode.Single);
                var rends = UnityEngine.Object.FindObjectsOfType<Renderer>(true)
                    .Where(r => r != null && r.enabled)
                    .Select(r => new { r = r, mf = r.GetComponent<MeshFilter>() })
                    .Where(x => x.mf != null && x.mf.sharedMesh != null)
                    .ToArray();

                Bounds all = rends[0].r.bounds;
                foreach (var x in rends) all.Encapsulate(x.r.bounds);
                log.AppendLine("SCENE_AABB center=" + all.center.ToString("F2") + " size=" + all.size.ToString("F2") + " min=" + all.min.ToString("F2") + " max=" + all.max.ToString("F2"));
                log.AppendLine("SPAWN=" + Spawn.ToString("F2") + " insideAABB=" + all.Contains(Spawn));
                log.AppendLine("SPAWN_DIST_TO_EDGE x+=" + (all.max.x - Spawn.x).ToString("F2") + " x-=" + (Spawn.x - all.min.x).ToString("F2") + " z+=" + (all.max.z - Spawn.z).ToString("F2") + " z-=" + (Spawn.z - all.min.z).ToString("F2"));

                // 楼层聚类：按 renderer bounds 底面 y 统计（地板块）
                var floorLike = rends.Where(x => IsName(x.r.name, "floor", "ground", "plane", "grid", "tile")).ToArray();
                log.AppendLine("FLOORLIKE_COUNT=" + floorLike.Length);
                foreach (var g in floorLike.GroupBy(x => Math.Round(x.r.bounds.max.y, 1)).OrderBy(g => g.Key))
                {
                    float area = 0f;
                    foreach (var x in g) area += x.r.bounds.size.x * x.r.bounds.size.z;
                    log.AppendLine("FLOOR_LEVEL yTop=" + g.Key.ToString("F2") + " count=" + g.Count() + " areaSum=" + area.ToString("F0"));
                }

                // 出生点附近 6m 内对象
                log.AppendLine("--- NEAR_SPAWN (radius 6) ---");
                foreach (var x in rends)
                {
                    float d = Vector3.Distance(x.r.bounds.ClosestPoint(Spawn), Spawn);
                    if (d > 6f) continue;
                    log.AppendLine("NEAR " + x.r.name + " | d=" + d.ToString("F2") + " | bmin=" + x.r.bounds.min.ToString("F1") + " bmax=" + x.r.bounds.max.ToString("F1"));
                }

                // 该点上方最近天花板、下方最近地板（几何）
                float ceilY = float.MaxValue; string ceilName = "<none>";
                float floorY = float.MinValue; string floorName = "<none>";
                foreach (var x in rends)
                {
                    var b = x.r.bounds;
                    if (Spawn.x < b.min.x || Spawn.x > b.max.x || Spawn.z < b.min.z || Spawn.z > b.max.z) continue;
                    if (b.min.y > Spawn.y + 0.3f && b.min.y < ceilY) { ceilY = b.min.y; ceilName = x.r.name; }
                    if (b.max.y <= Spawn.y + 0.3f && b.max.y > floorY) { floorY = b.max.y; floorName = x.r.name; }
                }
                log.AppendLine("GEOM_FLOOR_BELOW name=" + floorName + " yTop=" + floorY.ToString("F2") + " clearance=" + (Spawn.y - floorY).ToString("F2"));
                log.AppendLine("GEOM_CEILING_ABOVE name=" + ceilName + " yBottom=" + ceilY.ToString("F2") + " headroom=" + (ceilY - Spawn.y).ToString("F2"));

                // 墙体距离（xz 平面最近 wall 类对象）
                float minWall = float.MaxValue; string wallName = "<none>";
                foreach (var x in rends.Where(x => IsName(x.r.name, "wall", "segment", "corridor", "door")))
                {
                    var b = x.r.bounds;
                    Vector2 c = new Vector2(Mathf.Clamp(Spawn.x, b.min.x, b.max.x), Mathf.Clamp(Spawn.z, b.min.z, b.max.z));
                    float d = Vector2.Distance(new Vector2(Spawn.x, Spawn.z), c);
                    if (d < minWall) { minWall = d; wallName = x.r.name; }
                }
                log.AppendLine("NEAREST_WALL name=" + wallName + " distXZ=" + minWall.ToString("F2"));

                WriteReport();
                Debug.Log("[SpawnAnalyze] DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("FATAL: " + e);
                WriteReport();
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        static bool IsName(string n, params string[] keys)
        {
            if (string.IsNullOrEmpty(n)) return false;
            var l = n.ToLowerInvariant();
            foreach (var k in keys) if (l.Contains(k)) return true;
            return false;
        }

        static void WriteReport()
        {
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/spawn_analyze.txt"));
            File.WriteAllText(outPath, log.ToString(), new UTF8Encoding(false));
            Debug.Log("[SpawnAnalyze] REPORT_WRITTEN: " + outPath);
        }
    }
}