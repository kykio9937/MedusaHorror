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
    public static class SpawnReverify
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string PlayerName = "Kazuko_W1_Player";
        static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Medusa Horror/Reverify Spawn")]
        public static void Run()
        {
            try { DoWork(); }
            catch (Exception e) { Log.AppendLine("FATAL: " + e); Debug.LogError(e); }
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/spawn_reverify.txt"));
            try { File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false)); Debug.Log("[SpawnReverify] REPORT_WRITTEN: " + outPath); }
            catch (Exception e) { Debug.LogError("[SpawnReverify] WRITE_FAILED: " + e.Message); }
            Debug.Log("[SpawnReverify] DONE");
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
            Log.AppendLine("SCENE=" + scene.name + " rootCount=" + scene.GetRootGameObjects().Length);
            GameObject player = null;
            foreach (var r in scene.GetRootGameObjects()) if (r.name == PlayerName) { player = r; break; }
            if (player == null) { Log.AppendLine("PLAYER_NOT_FOUND"); return; }
            var cc = player.GetComponent<CharacterController>();
            Vector3 pos = player.transform.position;
            float foot = pos.y + cc.center.y - cc.height * 0.5f;
            float top = pos.y + cc.center.y + cc.height * 0.5f;
            Log.AppendLine("PLAYER_POS=" + pos.ToString("F3"));
            Log.AppendLine("CAPSULE bottomY=" + foot.ToString("F3") + " topY=" + top.ToString("F3"));

            RaycastHit gh;
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 0.02f, pos.z), Vector3.down, out gh, 3f, ~0, QueryTriggerInteraction.Ignore))
                Log.AppendLine("GROUND obj=" + GetPath(gh.transform) + " y=" + gh.point.y.ToString("F3") + " feetGap=" + (foot - gh.point.y).ToString("F3"));

            var ups = Physics.RaycastAll(new Vector3(pos.x, pos.y + 0.05f, pos.z), Vector3.up, 10f, ~0, QueryTriggerInteraction.Ignore)
                              .Where(h => h.transform != player.transform && !h.transform.IsChildOf(player.transform))
                              .OrderBy(h => h.point.y).ToArray();
            if (ups.Length > 0)
                Log.AppendLine("CEILING obj=" + GetPath(ups[0].transform) + " y=" + ups[0].point.y.ToString("F3") +
                                " headroomFromFeet=" + (ups[0].point.y - foot).ToString("F3") +
                                " clearanceAboveHead=" + (ups[0].point.y - top).ToString("F3"));
            else Log.AppendLine("CEILING NONE_WITHIN_10M");

            var ov = Physics.OverlapCapsule(pos + cc.center + Vector3.up * (cc.height * 0.5f - cc.radius), pos + cc.center - Vector3.up * (cc.height * 0.5f - cc.radius), cc.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore)
                              .Where(o => o.transform != player.transform && !o.transform.IsChildOf(player.transform)).ToArray();
            Log.AppendLine("CAPSULE_OVERLAP=" + ov.Length);
            foreach (var o in ov) Log.AppendLine("   OVERLAP " + GetPath(o.transform));

            Vector3[] dv = new Vector3[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            string[] dn = new string[] { "FRONT", "BACK", "LEFT", "RIGHT" };
            for (int i = 0; i < dv.Length; i++)
            {
                RaycastHit wh;
                bool hit = false;
                var rhs = Physics.RaycastAll(pos + Vector3.up * 0.9f, dv[i], 10f, ~0, QueryTriggerInteraction.Ignore)
                                  .Where(h => h.transform != player.transform && !h.transform.IsChildOf(player.transform))
                                  .OrderBy(h => h.distance).ToArray();
                if (rhs.Length > 0) Log.AppendLine("OBSTACLE_" + dn[i] + " dist=" + rhs[0].distance.ToString("F3") + " obj=" + GetPath(rhs[0].transform));
                else Log.AppendLine("OBSTACLE_" + dn[i] + " NONE_WITHIN_10M");
            }

            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == "Main Camera" || go.name.StartsWith("CM_"))
                    Log.AppendLine("CAMERA_RIG " + go.name + " pos=" + go.transform.position.ToString("F2"));
            }
            Log.AppendLine("SCENE_DIRTY=" + scene.isDirty);
        }
    }
}
