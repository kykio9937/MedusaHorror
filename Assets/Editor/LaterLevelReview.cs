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
    public static class LaterLevelReview
    {
        static readonly string[] Scenes = { "W2_Wasteland", "W3_Subway", "W4_NeonStreet", "W5_AlienGate" };
        public static void Audit()
        {
            string original = EditorSceneManager.GetActiveScene().path;
            var report = new StringBuilder();
            try
            {
                foreach (string name in Scenes)
                {
                    var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                    var objects = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
                    report.AppendLine(name + " missing=" + objects.Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
                    var player = Object.FindObjectOfType<ThirdPersonController>();
                    report.AppendLine(" player=" + (player != null ? player.transform.position.ToString() : "NONE"));
                    foreach (var t in objects.Where(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) > 0)) report.AppendLine(" missingObject=" + t.name);
                    foreach (var board in Object.FindObjectsOfType<TrainBoardPoint>(true)) report.AppendLine(" train=" + board.transform.position + " fare=" + board.fare + " reachable=" + Reachable(player.transform.position, board.transform.position, 3));
                    foreach (var exit in Object.FindObjectsOfType<ExitPortal>(true))
                    {
                        report.AppendLine(" exitPath=" + Reachable(player.transform.position, exit.transform.position, 4));
                        NavMesh.SamplePosition(player.transform.position, out var a, 20, NavMesh.AllAreas);
                        NavMesh.SamplePosition(exit.transform.position, out var b, 20, NavMesh.AllAreas);
                        var path = new NavMeshPath();
                        NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path);
                        report.AppendLine(" navStart="+a.position+" navExit="+b.position+" status="+path.status+" triangles="+NavMesh.CalculateTriangulation().indices.Length);
                        foreach(var hit in Physics.RaycastAll(exit.transform.position+Vector3.up*15,Vector3.down,40,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).Take(5)) report.AppendLine(" ground="+hit.collider.name+" pos="+hit.point);
                    }
                    foreach (var goal in Object.FindObjectsOfType<LevelGoalPoint>(true))
                    {
                        bool reachable = player != null && Reachable(player.transform.position, goal.transform.position, goal.radius);
                        report.AppendLine(" goal=" + goal.label + " kind=" + goal.kind + " required=" + goal.required + " pos=" + goal.transform.position + " navReachable=" + reachable);
                    }
                    foreach (var objective in Object.FindObjectsOfType<LevelObjective>(true)) report.AppendLine(" objective=" + objective.objectiveTitle);
                    foreach (var portal in Object.FindObjectsOfType<ExitPortal>(true)) report.AppendLine(" exit=" + portal.name + " key=" + portal.requireKey + " activation=" + portal.requireActivation + " cost=" + portal.cost + " next=" + portal.nextSceneOverride + " pos=" + portal.transform.position + " inner=" + portal.InnerEdge + " scale=" + portal.transform.lossyScale);
                    foreach (var boss in Object.FindObjectsOfType<MedusaBoss>(true)) report.AppendLine(" boss=" + boss.name + " pos=" + boss.transform.position);
                    foreach (var shop in Object.FindObjectsOfType<ShopPoint>(true)) report.AppendLine(" shop=" + shop.shopName + " type=" + shop.kind + " pos=" + shop.transform.position);
                    foreach (var encounter in Object.FindObjectsOfType<SubwayEncounter>(true)) report.AppendLine(" encounter=" + encounter.name + " pos=" + encounter.transform.position);
                    report.AppendLine(" spawners=" + Object.FindObjectsOfType<MonsterSpawner>(true).Length + " supplies=" + Object.FindObjectsOfType<PetrifyPickup>(true).Length);
                }
            }
            finally { EditorSceneManager.OpenScene(original); File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/later-level-audit.txt", report.ToString()); }
        }
        static bool Reachable(Vector3 from, Vector3 to, float radius)
        {
            if (!NavMesh.SamplePosition(from, out var start, 5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var end, Mathf.Max(radius, 2f), NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
    }
}
