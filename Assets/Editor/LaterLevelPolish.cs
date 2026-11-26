using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    public static class LaterLevelPolish
    {
        const string RootName = "LaterLevelGuidance";
        public static void Apply()
        {
            string original = EditorSceneManager.GetActiveScene().path;
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save pending scene edits and exit Play mode first.");
            var report = new StringBuilder();
            try
            {
                foreach (string name in new[] { "W2_Wasteland", "W3_Subway", "W4_NeonStreet", "W5_AlienGate" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    if (scene.GetRootGameObjects().Any(o => o.name == RootName)) { report.AppendLine(name + " already applied"); continue; }
                    var root = new GameObject(RootName).transform;
                    var player = UnityEngine.Object.FindObjectOfType<ThirdPersonController>();
                    var exit = UnityEngine.Object.FindObjectOfType<ExitPortal>();
                    var all = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
                    foreach (var t in all)
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)
                        {
                            report.AppendLine(name + " removed missing component from " + t.name);
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                        }
                    if (name == "W5_AlienGate")
                    {
                        var terrain = Terrain.activeTerrain;
                        var pos = exit.transform.position;
                        pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y + 0.12f;
                        exit.transform.position = pos;
                        report.AppendLine(name + " gate grounded at " + pos);
                    }
                    if (name == "W4_NeonStreet")
                    {
                        Vector3 corrected = FindReachableExit(player.transform.position, exit.transform.position);
                        report.AppendLine(name + " exit " + exit.transform.position + " -> " + corrected);
                        exit.transform.position = corrected;
                        exit.nextSceneOverride = "W5_AlienGate";
                    }
                    Vector3 destination;
                    if (exit != null) destination = exit.transform.position;
                    else
                    {
                        var board = UnityEngine.Object.FindObjectOfType<TrainBoardPoint>();
                        board.fare = 200;
                        destination = board.transform.position;
                        Sign(root, "列车入口\n车票 200 信用点 · 按 E 前往霓虹街区", destination + Vector3.up * 2.3f, player.transform.position);
                        report.AppendLine(name + " train fare is 200 credits");
                    }
                    var path = Path(player.transform.position, destination);
                    if (path == null) throw new InvalidOperationException(name + " exit has no complete navigation path");
                    string intro = name == "W2_Wasteland" ? "荒废村庄\n沿路前往村口星门 · 结晶可缓解石化" :
                        name == "W3_Subway" ? "废弃地铁\n按 E 探索站台 · 前往列车入口上车" :
                        name == "W4_NeonStreet" ? "霓虹街区\n黑刃工坊 / 义体门诊 · 补强后前往星门" :
                        "异星之门\n前往星门 · 按 E 点亮后走入门内";
                    Vector3 signPosition = player.transform.position + player.transform.forward * 3f + player.transform.right * 2.2f;
                    signPosition.y = player.transform.position.y + 1.7f;
                    Sign(root, intro, signPosition, player.transform.position);
                    if (exit != null)
                        Sign(root, name == "W5_AlienGate" ? "星门控制台\n靠近按 E 点亮" : "离场星门\n走入门内前往下一站", destination + Vector3.up * 2.7f, player.transform.position);
                    foreach (var shop in UnityEngine.Object.FindObjectsOfType<ShopPoint>())
                        Sign(root, shop.shopName + "\n按 E 进入 · 数字键购买 · Esc 返回", shop.transform.position + Vector3.up * 2.2f, player.transform.position);
                    if (name == "W2_Wasteland")
                        foreach (float fraction in new[] { 0.25f, 0.5f, 0.75f }) AddSupply(root, AlongPath(path, fraction));
                    Physics.SyncTransforms();
                    report.AppendLine(name + " exit path PASS corners=" + path.corners.Length);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Unable to save " + name);
                }
                AssetDatabase.SaveAssets();
            }
            finally
            {
                File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/later-level-polish.txt", report.ToString());
                EditorSceneManager.OpenScene(original);
            }
        }

        static NavMeshPath Path(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out var a, 5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 3f, NavMesh.AllAreas)) return null;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete ? path : null;
        }

        static Vector3 FindReachableExit(Vector3 spawn, Vector3 desired)
        {
            for (int radius = 1; radius <= 24; radius++)
                for (int angle = 0; angle < 24; angle++)
                {
                    float radians = angle * Mathf.PI / 12;
                    var candidate = desired + new Vector3(Mathf.Cos(radians), 0, Mathf.Sin(radians)) * radius;
                    if (!NavMesh.SamplePosition(candidate, out var hit, 1.2f, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - spawn.y) > 0.8f) continue;
                    if (Path(spawn, hit.position) == null) continue;
                    Vector3 bottom = hit.position + Vector3.up * 0.45f;
                    if (Physics.CheckCapsule(bottom, bottom + Vector3.up * 1.1f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    return hit.position + Vector3.up * 0.08f;
                }
            throw new InvalidOperationException("No reachable exit placement found near the street portal.");
        }

        static Vector3 AlongPath(NavMeshPath path, float fraction)
        {
            float distance = 0;
            for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i-1],path.corners[i]);
            float remaining = distance * fraction;
            for (int i = 1; i < path.corners.Length; i++)
            {
                float segment = Vector3.Distance(path.corners[i-1],path.corners[i]);
                if (remaining <= segment) return Vector3.Lerp(path.corners[i-1],path.corners[i],remaining / Mathf.Max(segment,0.001f));
                remaining -= segment;
            }
            return path.corners.Last();
        }

        static void Sign(Transform root, string label, Vector3 position, Vector3 viewer)
        {
            var obj = new GameObject("RouteSign");
            obj.transform.SetParent(root,false);
            obj.transform.position = position;
            Vector3 direction = position - viewer;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f) obj.transform.rotation = Quaternion.LookRotation(direction);
            var text = obj.AddComponent<TextMesh>();
            text.font = MedusaHorror.UI.CjkFont.Get();
            text.fontSize = 64;
            text.characterSize = 0.045f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.text = label;
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/W1Hospital/SignLettering.mat");
            obj.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void AddSupply(Transform root, Vector3 point)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            obj.name = "净化结晶";
            obj.transform.SetParent(root,false);
            obj.transform.position = point + Vector3.up * 0.45f;
            obj.transform.localScale = new Vector3(0.35f,0.7f,0.35f);
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/W1Hospital/Diffuser.mat");
            var pickup = obj.AddComponent<PetrifyPickup>();
            pickup.restore = 25;
            pickup.pickupRadius = 1.5f;
            Sign(root, "净化结晶\n石化时靠近使用", point + Vector3.up * 1.5f, point - Vector3.forward * 2);
        }
    }
}
