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
    public static class MoveSpawnV2
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string PlayerName = "Kazuko_W1_Player";
        static readonly string[] RootSceneDirs = new string[] { "Assets" };
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Medusa Horror/Move Spawn V2")]
        public static void Run()
        {
            try { DoWork(); }
            catch (Exception e) { Log.AppendLine("FATAL: " + e); Debug.LogError(e); }
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/spawn_move_v2.txt"));
            try { File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false)); Debug.Log("[MoveSpawnV2] REPORT_WRITTEN: " + outPath); }
            catch (Exception e) { Debug.LogError("[MoveSpawnV2] WRITE_FAILED: " + e.Message); }
            Debug.Log("[MoveSpawnV2] DONE");
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static void DoWork()
        {
            Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            Physics.SyncTransforms();
            Log.AppendLine("SCENE=" + scene.name + " loaded=" + scene.isLoaded);

            GameObject player = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == PlayerName) { player = r; break; }
            if (player == null) { Log.AppendLine("PLAYER_NOT_FOUND"); return; }
            var cc = player.GetComponent<CharacterController>();
            if (cc == null) { Log.AppendLine("CHARACTER_CONTROLLER_MISSING"); return; }
            float footOffset = cc.center.y - cc.height * 0.5f;
            Log.AppendLine("OLD_POS=" + player.transform.position.ToString("F3") + " OLD_ROT=" + player.transform.eulerAngles.ToString("F1"));
            Log.AppendLine("CC center=" + cc.center.ToString("F3") + " height=" + cc.height.ToString("F3") + " radius=" + cc.radius.ToString("F3") + " skin=" + cc.skinWidth + " footOffset=" + footOffset.ToString("F3"));

            var seedXZ = new Vector2(-2.0f, 12.0f);
            var good = new List<Vector3>();
            var best = Vector3.zero;
            float bestDist = float.MaxValue;
            string bestInfo = "";
            int tested = 0;
            for (float dx = -4f; dx <= 4.001f; dx += 0.5f)
            {
                for (float dz = -4f; dz <= 4.001f; dz += 0.5f)
                {
                    float x = seedXZ.x + dx, z = seedXZ.y + dz;
                    tested++;
                    RaycastHit dh;
                    if (!Physics.Raycast(new Vector3(x, 9.0f, z), Vector3.down, out dh, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (dh.point.y < 8.55f || dh.point.y > 9.0f) continue;
                    float floorY = dh.point.y;
                    var ups = Physics.RaycastAll(new Vector3(x, floorY + 0.1f, z), Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore);
                    float ceilY = float.MaxValue; string ceilObj = "";
                    foreach (var u in ups) if (u.point.y > floorY + 0.3f && u.point.y < ceilY) { ceilY = u.point.y; ceilObj = GetPath(u.transform); }
                    if (ceilY == float.MaxValue) continue;
                    float clear = ceilY - floorY;
                    if (clear < 2.4f) continue;
                    Vector3 pos = new Vector3(x, floorY - footOffset, z);
                    if (!CapsuleFree(pos, cc, player.transform)) continue;
                    float d = Vector2.Distance(new Vector2(x, z), seedXZ);
                    good.Add(pos);
                    if (d < bestDist) { bestDist = d; best = pos; bestInfo = "floorY=" + floorY.ToString("F3") + " ceilY=" + ceilY.ToString("F3") + " clear=" + clear.ToString("F2") + " ceilObj=" + ceilObj + " floorObj=" + GetPath(dh.transform); }
                }
            }
            Log.AppendLine("LOCAL_SEARCH tested=" + tested + " PASS=" + good.Count + " bestDistXZ=" + bestDist.ToString("F2"));
            foreach (var g in good.OrderBy(v => Vector2.Distance(new Vector2(v.x, v.z), seedXZ)).Take(10))
                Log.AppendLine("   PASS_POS=" + g.ToString("F3"));
            if (best == Vector3.zero) { Log.AppendLine("NO_VALID_POSITION_NEAR_SEED"); return; }
            Log.AppendLine("BEST_POS=" + best.ToString("F3") + " " + bestInfo);
            player.transform.position = best;
            EditorUtility.SetDirty(player);
            if (PrefabUtility.IsPartOfPrefabInstance(player)) PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            bool saved = EditorSceneManager.SaveScene(scene);
            Log.AppendLine("MOVE_NEW_POS=" + player.transform.position.ToString("F3") + " SCENE_SAVED=" + saved);

            Log.AppendLine("########## VERIFY PASS (reload) ##########");
            Scene sc2 = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            Physics.SyncTransforms();
            Log.AppendLine("SCENE_LOADED=" + sc2.name + " valid=" + sc2.IsValid() + " rootCount=" + sc2.GetRootGameObjects().Length);
            GameObject pl2 = null;
            foreach (var r in sc2.GetRootGameObjects()) if (r.name == PlayerName) { pl2 = r; break; }
            if (pl2 == null) { Log.AppendLine("PLAYER_NOT_FOUND_AFTER_RELOAD"); return; }
            var cc2 = pl2.GetComponent<CharacterController>();
            float foot2 = cc2.center.y - cc2.height * 0.5f;

            var allCols = new List<Collider>();
            var meshCols = new List<MeshCollider>();
            var renderers = new List<Renderer>();
            foreach (var r in sc2.GetRootGameObjects())
            {
                allCols.AddRange(r.GetComponentsInChildren<Collider>(true));
                meshCols.AddRange(r.GetComponentsInChildren<MeshCollider>(true));
                renderers.AddRange(r.GetComponentsInChildren<Renderer>(true));
            }
            Log.AppendLine("COLLIDER_TOTAL=" + allCols.Count);
            Log.AppendLine("MESHCOLLIDER_TOTAL=" + meshCols.Count + " EMPTY=" + meshCols.Count(c => c.sharedMesh == null));

            Vector3 pos2 = pl2.transform.position;
            float bottomY = pos2.y + cc2.center.y - cc2.height * 0.5f;
            float topY = pos2.y + cc2.center.y + cc2.height * 0.5f;
            Log.AppendLine("PLAYER_POS=" + pos2.ToString("F3") + " rot=" + pl2.transform.eulerAngles.ToString("F1"));
            Log.AppendLine("CAPSULE bottomY=" + bottomY.ToString("F3") + " topY=" + topY.ToString("F3"));

            RaycastHit gh;
            if (Physics.Raycast(new Vector3(pos2.x, pos2.y + 0.02f, pos2.z), Vector3.down, out gh, 3f, ~0, QueryTriggerInteraction.Ignore))
                Log.AppendLine("GROUND_BELOW obj=" + GetPath(gh.transform) + " y=" + gh.point.y.ToString("F3") + " feetGap=" + (bottomY - gh.point.y).ToString("F3"));
            else Log.AppendLine("GROUND_BELOW NONE");

            var ups2 = Physics.RaycastAll(new Vector3(pos2.x, pos2.y + 0.05f, pos2.z), Vector3.up, 8f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.point.y).ToArray();
            if (ups2.Length > 0)
                Log.AppendLine("CEILING_ABOVE obj=" + GetPath(ups2[0].transform) + " y=" + ups2[0].point.y.ToString("F3") + " headroom=" + (ups2[0].point.y - bottomY).ToString("F3") + " clearanceAboveTop=" + (ups2[0].point.y - topY).ToString("F3"));
            else Log.AppendLine("CEILING_ABOVE NONE_WITHIN_8M");

            var ov = Physics.OverlapCapsule(pos2 + cc2.center + Vector3.up * (cc2.height * 0.5f - cc2.radius), pos2 + cc2.center - Vector3.up * (cc2.height * 0.5f - cc2.radius), cc2.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore)
                              .Where(o => o.transform != pl2.transform && !o.transform.IsChildOf(pl2.transform)).ToArray();
            Log.AppendLine("CAPSULE_OVERLAP_EXCLUDING_SELF=" + ov.Length);
            foreach (var o in ov) Log.AppendLine("   OVERLAP " + GetPath(o.transform) + " type=" + o.GetType().Name);

            if (CapsuleFree(pos2, cc2, pl2.transform)) Log.AppendLine("CAPSULE_FREE=True");
            else Log.AppendLine("CAPSULE_FREE=False");

            Vector3[] dv = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            string[] dn = new string[] { "FRONT", "BACK", "LEFT", "RIGHT" };
            for (int i = 0; i < dv.Length; i++)
            {
                RaycastHit wh;
                if (Physics.Raycast(pos2 + Vector3.up * 0.9f, dv[i], out wh, 8f, ~0, QueryTriggerInteraction.Ignore))
                    Log.AppendLine("WALL_" + dn[i] + " dist=" + wh.distance.ToString("F3") + " obj=" + GetPath(wh.transform));
                else Log.AppendLine("WALL_" + dn[i] + " NONE_WITHIN_8M");
            }

            var cams = new List<Camera>();
            foreach (var r in sc2.GetRootGameObjects()) cams.AddRange(r.GetComponentsInChildren<Camera>(true));
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
                Log.AppendLine("CAM " + cam.name + " pos=" + cam.transform.position.ToString("F2") + " euler=" + cam.transform.eulerAngles.ToString("F1") +
                                " RENDERER_TOTAL=" + renderers.Count + " RENDERERS_WITH_MESH=" + withMesh + " RENDERERS_INSIDE_FRUSTUM=" + inFrustum);
            }
            Log.AppendLine("SCENE_DIRTY_AFTER_RELOAD=" + sc2.isDirty);
        }

        static bool CapsuleFree(Vector3 pos, CharacterController cc, Transform self)
        {
            Vector3 p1 = pos + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius);
            Vector3 p2 = pos + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius);
            var arr = Physics.OverlapCapsule(p1, p2, cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var o in arr)
            {
                if (o.transform == self || o.transform.IsChildOf(self)) continue;
                return false;
            }
            return true;
        }
    }
}
