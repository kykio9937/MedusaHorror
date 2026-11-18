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
    public static class SpawnAdjust
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string PlayerName = "Kazuko_W1_Player";
        static readonly Vector3 TargetPos = new Vector3(-20f, 8.88f, 1.4f);
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Medusa Horror/Adjust Player Spawn")]
        public static void Run()
        {
            try
            {
                Move();
                Verify();
            }
            catch (Exception e)
            {
                Log.AppendLine("FATAL: " + e);
                Debug.LogError("[SpawnAdjust] FATAL: " + e);
            }
            WriteReport();
            Debug.Log("[SpawnAdjust] DONE");
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static GameObject FindPlayer(Scene scene)
        {
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == PlayerName) return r;
            GameObject fallback = null;
            foreach (var r in scene.GetRootGameObjects())
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.CompareTag("Player")) return t.gameObject;
                    if (fallback == null && t.GetComponent<CharacterController>() != null && t.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0) fallback = t.gameObject;
                }
            }
            return fallback;
        }

        static void Move()
        {
            Log.AppendLine("########## MOVE PASS ##########");
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single); }
            catch (Exception e) { Log.AppendLine("OPEN_FAILED: " + e.Message); return; }

            var player = FindPlayer(scene);
            if (player == null) { Log.AppendLine("PLAYER_NOT_FOUND"); return; }

            Vector3 oldPos = player.transform.position;
            Log.AppendLine("PLAYER_OBJECT=" + GetPath(player.transform));
            Log.AppendLine("PLAYER_PREFAB_STATUS=" + PrefabUtility.GetPrefabInstanceStatus(player));
            Log.AppendLine("OLD_POS=" + oldPos.ToString("F3") + " OLD_ROT=" + player.transform.eulerAngles.ToString("F1"));
            Undo.RecordObject(player.transform, "MoveSpawnPoint");
            player.transform.position = TargetPos;
            EditorUtility.SetDirty(player);
            Log.AppendLine("NEW_POS=" + player.transform.position.ToString("F3"));
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, TargetScene);
            Log.AppendLine("SCENE_SAVED=" + saved);
        }
        static void Verify()
        {
            Log.AppendLine("########## VERIFY PASS (reload) ##########");
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single); }
            catch (Exception e) { Log.AppendLine("VERIFY_OPEN_FAILED: " + e.Message); return; }
            var roots = scene.GetRootGameObjects();
            Log.AppendLine("SCENE_LOADED=" + scene.name + " valid=" + scene.IsValid() + " rootCount=" + roots.Length);

            var allCols = new List<Collider>();
            var meshCols = new List<MeshCollider>();
            var renderers = new List<Renderer>();
            foreach (var r in roots)
            {
                allCols.AddRange(r.GetComponentsInChildren<Collider>(true));
                meshCols.AddRange(r.GetComponentsInChildren<MeshCollider>(true));
                renderers.AddRange(r.GetComponentsInChildren<Renderer>(true));
            }
            Log.AppendLine("COLLIDER_TOTAL=" + allCols.Count);
            Log.AppendLine("MESHCOLLIDER_TOTAL=" + meshCols.Count);
            Log.AppendLine("MESHCOLLIDER_EMPTY=" + meshCols.Count(c => c.sharedMesh == null));
            Log.AppendLine("MESHCOLLIDER_NONCONVEX_ENABLED=" + meshCols.Count(c => c.enabled && c.sharedMesh != null && !c.convex));
            Log.AppendLine("RENDERER_TOTAL=" + renderers.Count);
            Log.AppendLine("RENDERER_WITH_MESH=" + renderers.Count(x => { var mf = x.GetComponent<MeshFilter>(); return mf != null && mf.sharedMesh != null; }));

            var player = FindPlayer(scene);
            if (player == null) { Log.AppendLine("PLAYER_NOT_FOUND_IN_VERIFY"); }
            else
            {
                Vector3 pos = player.transform.position;
                Log.AppendLine("PLAYER_POS=" + pos.ToString("F3") + " rot=" + player.transform.eulerAngles.ToString("F1"));
                var cc = player.GetComponent<CharacterController>();
                if (cc != null)
                {
                    Log.AppendLine("CHARACTER_CONTROLLER center=" + cc.center.ToString("F3") + " height=" + cc.height.ToString("F3") + " radius=" + cc.radius.ToString("F3") + " skin=" + cc.skinWidth + " enabled=" + cc.enabled);
                    float bottomY = pos.y + cc.center.y - cc.height * 0.5f;
                    Vector3 feetProbe = new Vector3(pos.x, bottomY + 0.02f, pos.z);
                    Log.AppendLine("FEET_Y=" + bottomY.ToString("F3"));
                    RaycastHit rh;
                    if (Physics.Raycast(feetProbe, Vector3.down, out rh, 3f, ~0, QueryTriggerInteraction.Ignore))
                        Log.AppendLine("FEET_GROUND_RAY hit=" + GetPath(rh.transform) + " point=" + rh.point.ToString("F3") + " dist=" + rh.distance.ToString("F3") + " gap=" + (bottomY - rh.point.y).ToString("F3"));
                    else Log.AppendLine("FEET_GROUND_RAY hit=NONE");

                    Vector3 p1 = pos + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius);
                    Vector3 p2 = pos + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius);
                    int overlapCount = Physics.OverlapCapsule(p1, p2, cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore).Length;
                    Log.AppendLine("CAPSULE_OVERLAP_COUNT=" + overlapCount);
                    foreach (var o in Physics.OverlapCapsule(p1, p2, cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore).Take(10))
                        Log.AppendLine("   OVERLAP " + GetPath(o.transform) + " type=" + o.GetType().Name);
                }
                else Log.AppendLine("CHARACTER_CONTROLLER_MISSING");

                string[] dirs = new string[] { "前", "后", "左", "右" };
                Vector3[] vecs = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
                for (int i = 0; i < vecs.Length; i++)
                {
                    RaycastHit wh;
                    if (Physics.Raycast(pos + Vector3.up * 0.9f, vecs[i], out wh, 5f, ~0, QueryTriggerInteraction.Ignore))
                        Log.AppendLine("WALL_" + dirs[i] + " " + vecs[i].ToString("F0") + " hit=" + GetPath(wh.transform) + " dist=" + wh.distance.ToString("F3"));
                    else Log.AppendLine("WALL_" + dirs[i] + " NONE_WITHIN_5M");
                }
            }

            var cams = new List<Camera>();
            foreach (var r in roots) cams.AddRange(r.GetComponentsInChildren<Camera>(true));
            foreach (var cam in cams)
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
                Log.AppendLine(string.Format("CAM {0} pos={1} euler={2} fov={3} RENDERERS_WITH_MESH={4} RENDERERS_INSIDE_FRUSTUM={5}",
                    cam.name, cam.transform.position.ToString("F2"), cam.transform.eulerAngles.ToString("F1"), cam.fieldOfView, withMesh, inFrustum));
            }
            Log.AppendLine("SCENE_DIRTY_AFTER_RELOAD=" + scene.isDirty);
        }

        static void WriteReport()
        {
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/spawn_adjust.txt"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false));
                Debug.Log("[SpawnAdjust] REPORT_WRITTEN: " + outPath);
            }
            catch (Exception e) { Debug.LogError("[SpawnAdjust] WRITE_FAILED: " + e.Message); }
            Debug.Log(Log.ToString());
        }
    }
}
