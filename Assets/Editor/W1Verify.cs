using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace MedusaHorror.EditorTools
{
    /// <summary>Final acceptance report for W1.</summary>
    public static class W1Verify
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w1verify.txt";

        [MenuItem("Medusa/Verify W1")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W1_Hospital.unity", OpenSceneMode.Single);
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            var spawners = Object.FindObjectsOfType<MedusaHorror.Gameplay.MonsterSpawner>(true);
            var pickups = Object.FindObjectsOfType<MedusaHorror.Gameplay.W1ObjectivePickup>(true);
            var portal = Object.FindObjectOfType<MedusaHorror.Gameplay.ExitPortal>(true);
            var surface = Object.FindObjectOfType<Unity.AI.Navigation.NavMeshSurface>(true);
            var tri = NavMesh.CalculateTriangulation();

            int spawnTargets = spawners.Sum(s => s.targetCount);
            int renderers = Object.FindObjectsOfType<Renderer>(true).Length;
            int colliders = Object.FindObjectsOfType<Collider>(true).Length;

            // walkability over the level footprint
            int samples = 0, blocked = 0, narrow = 0;
            var rects = new (float x0, float z0, float x1, float z1)[]
            {
                (-6f, 0f, 6f, 12f), (-4f, 12f, 4f, 20f), (-4f, 20f, 4f, 80f),
                (-8f, 80f, 8f, 96f), (-8f, 96f, 8f, 108f), (-8f, 108f, 8f, 124f),
            };
            var allRects = new List<(float, float, float, float)>(rects);
            for (int i = 0; i < 5; i++)
            {
                float z0 = 22 + i * 12f;
                allRects.Add((-16f, z0, -4f, z0 + 8f));
                allRects.Add((4f, z0, 16f, z0 + 8f));
            }
            foreach (var rr in allRects)
            for (float x = rr.Item1 + 1f; x <= rr.Item3 - 1f; x += 1.5f)
            {
                for (float z = rr.Item2 + 1f; z <= rr.Item4 - 1f; z += 1.5f)
                {
                    var p = new Vector3(x, 0.05f, z);
                    if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, 1.2f)) continue;
                    samples++;
                    var a = p + Vector3.up * 0.4f;
                    var b = p + Vector3.up * 1.5f;
                    var cols = Physics.OverlapCapsule(a, b, 0.32f);
                    bool bad = false;
                    foreach (var c in cols)
                    {
                        if (c == null || c.isTrigger) continue;
                        if (c.GetComponent<MedusaHorror.Gameplay.MonsterSpawner>() != null) continue;
                        if (c.GetComponent<MedusaHorror.Gameplay.W1ObjectivePickup>() != null) continue;
                        bad = true; break;
                    }
                    if (bad) blocked++;
                }
            }

            sb.AppendLine("scene=" + sc.name);
            sb.AppendLine("player=" + (tpc != null));
            sb.AppendLine("spawners=" + spawners.Length + " spawnTargets=" + spawnTargets);
            sb.AppendLine("pickups=" + pickups.Length);
            sb.AppendLine("exitPortal=" + (portal != null) + " nextScene=" + (portal != null ? portal.nextSceneOverride : "-") + " requireActivation=" + (portal != null ? portal.requireActivation.ToString() : "-"));
            sb.AppendLine("navSurface=" + (surface != null) + " navVerts=" + tri.vertices.Length + " navTris=" + tri.indices.Length / 3);
            sb.AppendLine("renderers=" + renderers + " colliders=" + colliders);
            sb.AppendLine("walk: samples=" + samples + " blocked=" + blocked + " pass=" + (samples > 0 ? (100f * (samples - blocked) / samples).ToString("F1") : "0") + "%");

            var list = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToArray();
            sb.AppendLine("buildOrder=" + string.Join(" > ", list));

            File.WriteAllText(LogPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[W1Verify]\n" + sb);
        }
    }
}
