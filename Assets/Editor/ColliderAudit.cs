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
    public static class ColliderAudit
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Medusa Horror/Audit Colliders")]
        public static void Run()
        {
            try { DoAudit(); }
            catch (Exception e) { Log.AppendLine("FATAL: " + e); Debug.LogError(e); }
            WriteReport();
            Debug.Log("[ColliderAudit] DONE");
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static void DoAudit()
        {
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single); }
            catch (Exception e) { Log.AppendLine("OPEN_FAILED: " + e.Message); return; }
            Physics.SyncTransforms();
            var roots = scene.GetRootGameObjects();
            Log.AppendLine("SCENE=" + scene.name + " loaded=" + scene.isLoaded + " rootCount=" + roots.Length);

            var cols = new List<Collider>();
            var meshCols = new List<MeshCollider>();
            var renderers = new List<Renderer>();
            foreach (var r in roots)
            {
                cols.AddRange(r.GetComponentsInChildren<Collider>(true));
                meshCols.AddRange(r.GetComponentsInChildren<MeshCollider>(true));
                renderers.AddRange(r.GetComponentsInChildren<Renderer>(true));
            }
            Log.AppendLine("COLLIDER_TOTAL=" + cols.Count);
            foreach (var g in cols.GroupBy(c => c.GetType().Name).OrderByDescending(g => g.Count()))
                Log.AppendLine("   COLLIDER_TYPE " + g.Key + " = " + g.Count());
            Log.AppendLine("MESHCOLLIDER_TOTAL=" + meshCols.Count);
            Log.AppendLine("MESHCOLLIDER_EMPTY_SHAREDMESH=" + meshCols.Count(c => c.sharedMesh == null));
            Log.AppendLine("MESHCOLLIDER_ENABLED=" + meshCols.Count(c => c.enabled));

            var zeroSize = meshCols.Where(c => c.sharedMesh != null && (c.bounds.size.x < 0.01f || c.bounds.size.y < 0.01f || c.bounds.size.z < 0.01f)).ToList();
            Log.AppendLine("MESHCOLLIDER_DEGENERATE_BOUNDS=" + zeroSize.Count);
            foreach (var c in zeroSize.Take(10)) Log.AppendLine("   DEGENERATE " + GetPath(c.transform) + " size=" + c.bounds.size.ToString("F3") + " mesh=" + c.sharedMesh.name);

            Log.AppendLine("---- MESH USAGE TOP20 ----");
            foreach (var g in meshCols.Where(c => c.sharedMesh != null).GroupBy(c => c.sharedMesh.name).OrderByDescending(g => g.Count()).Take(20))
                Log.AppendLine("   " + g.Key + " = " + g.Count());
            Log.AppendLine("MESH_NAME_COUNT=" + meshCols.Where(c => c.sharedMesh != null).Select(c => c.sharedMesh.name).Distinct().Count());

            Log.AppendLine("---- COLLIDER VS RENDERER BOUNDS RATIO (sample) ----");
            int sampled = 0, mismatch = 0;
            foreach (var c in meshCols)
            {
                var mr = c.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                sampled++;
                var cb = c.bounds.size; var rb = mr.bounds.size;
                float rx = rb.x <= 0.001f ? 1f : cb.x / rb.x;
                float ry = rb.y <= 0.001f ? 1f : cb.y / rb.y;
                float rz = rb.z <= 0.001f ? 1f : cb.z / rb.z;
                bool bad = rx < 0.4f || rx > 2.5f || ry < 0.4f || ry > 2.5f || rz < 0.4f || rz > 2.5f;
                if (bad)
                {
                    mismatch++;
                    if (mismatch <= 12)
                        Log.AppendLine("   MISMATCH " + GetPath(c.transform) + " mesh=" + c.sharedMesh.name +
                                        " colSize=" + cb.ToString("F2") + " rendSize=" + rb.ToString("F2"));
                }
            }
            Log.AppendLine("BOUNDS_SAMPLED=" + sampled + " BOUNDS_MISMATCH=" + mismatch);
            Log.AppendLine("---- PLAYER SPAWN CHECK ----");
            GameObject player = null;
            foreach (var r in roots) if (r.name == "Kazuko_W1_Player") { player = r; break; }
            if (player != null)
            {
                Vector3 pos = player.transform.position;
                Log.AppendLine("PLAYER_POS=" + pos.ToString("F3"));
                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    float bottomY = pos.y + cc.center.y - cc.height * 0.5f;
                    float topY = pos.y + cc.center.y + cc.height * 0.5f;
                    Log.AppendLine("PLAYER_CAPSULE bottomY=" + bottomY.ToString("F3") + " topY=" + topY.ToString("F3") + " radius=" + cc.radius.ToString("F3"));

                    RaycastHit fh;
                    if (Physics.Raycast(new Vector3(pos.x, pos.y + 0.02f, pos.z), Vector3.down, out fh, 3f, ~0, QueryTriggerInteraction.Ignore))
                        Log.AppendLine("GROUND_BELOW hit=" + GetPath(fh.transform) + " y=" + fh.point.y.ToString("F3") + " gap_feet_to_ground=" + (fh.point.y - bottomY).ToString("F3"));
                    else Log.AppendLine("GROUND_BELOW hit=NONE");

                    var ups = Physics.RaycastAll(new Vector3(pos.x, pos.y + 0.05f, pos.z), Vector3.up, 6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.point.y).ToArray();
                    if (ups.Length > 0)
                        Log.AppendLine("CEILING_ABOVE hit=" + GetPath(ups[0].transform) + " y=" + ups[0].point.y.ToString("F3") + " headroom_from_feet=" + (ups[0].point.y - (pos.y + cc.center.y - cc.height * 0.5f)).ToString("F3") + " headroom_from_eye=" + (ups[0].point.y - pos.y).ToString("F3"));
                    else Log.AppendLine("CEILING_ABOVE hit=NONE_WITHIN_6M");

                    var ov = Physics.OverlapCapsule(pos + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius), pos + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius), cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
                    Log.AppendLine("CAPSULE_OVERLAP_COUNT=" + ov.Length);
                    foreach (var o in ov.Take(8)) Log.AppendLine("   OVERLAP " + GetPath(o.transform) + " type=" + o.GetType().Name);

                    Vector3[] dv = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                    string[] dn = new string[] { "FRONT", "BACK", "LEFT", "RIGHT" };
                    for (int i = 0; i < dv.Length; i++)
                    {
                        RaycastHit wh;
                        if (Physics.Raycast(pos + Vector3.up * 0.9f, dv[i], out wh, 5f, ~0, QueryTriggerInteraction.Ignore))
                            Log.AppendLine("WALL_" + dn[i] + " dist=" + wh.distance.ToString("F3") + " hit=" + GetPath(wh.transform));
                        else Log.AppendLine("WALL_" + dn[i] + " NONE_WITHIN_5M");
                    }
                }
            }
            else Log.AppendLine("PLAYER_NOT_FOUND");

            Log.AppendLine("---- CAMERA FRUSTUM ----");
            foreach (var cam in roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                int withMesh = 0, inFrustum = 0;
                foreach (var mr in renderers)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    withMesh++;
                    if (GeometryUtility.TestPlanesAABB(planes, mr.bounds)) inFrustum++;
                }
                Log.AppendLine("CAM " + cam.name + " pos=" + cam.transform.position.ToString("F2") + " euler=" + cam.transform.eulerAngles.ToString("F1") +
                                " RENDERER_TOTAL=" + renderers.Count + " RENDERERS_WITH_MESH=" + withMesh + " RENDERERS_INSIDE_FRUSTUM=" + inFrustum);
            }
            Log.AppendLine("SCENE_DIRTY=" + scene.isDirty);
        }

        static void WriteReport()
        {
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/collider_audit.txt"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false));
                Debug.Log("[ColliderAudit] REPORT_WRITTEN: " + outPath);
            }
            catch (Exception e) { Debug.LogError("[ColliderAudit] WRITE_FAILED: " + e.Message); }
        }
    }
}
