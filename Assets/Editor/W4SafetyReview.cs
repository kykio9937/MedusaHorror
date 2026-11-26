using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using MedusaHorror.Player;
using MedusaHorror.Gameplay;

namespace MedusaHorror.EditorTools
{
    public static class W4SafetyReview
    {
        public static void FixApartmentSettings()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            foreach (var build in Object.FindObjectsOfType<HouseBuildMode>())
            {
                var floor = build.transform.Find("HouseFloor");
                if (floor == null || floor.GetComponent<Collider>() == null) continue;
                var b = floor.GetComponent<Collider>().bounds;
                build.houseCenter = new Vector3(b.center.x, b.max.y, b.center.z);
                build.houseRadius = Mathf.Min(b.extents.x, b.extents.z);
                EditorUtility.SetDirty(build);
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        public static void Fix()
        {
            Audit();
            int added = 0;
            foreach (var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (renderer == null || filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                string name = filter.name.ToLowerInvariant();
                if (!(name.StartsWith("street_road") || name.StartsWith("street_pavement") || name.StartsWith("street_panels") || name.StartsWith("floor_"))) continue;
                if (name.Contains("lod") || name.Contains("_l1") || name.Contains("_l2")) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(filter.transform);
                added++;
            }
            var player = Object.FindObjectOfType<ThirdPersonController>();
            if (player.GetComponent<W4FallRecovery>() == null) player.gameObject.AddComponent<W4FallRecovery>();
            int entranceRepairs = 0;
            foreach(var ceiling in Object.FindObjectsOfType<Transform>().Where(t=>t.name.StartsWith("Bar_Ceiling") && t.position.x < -28 && t.position.x > -37 && t.position.z > -62 && t.position.z < -53).ToArray())
            {
                var renderer=ceiling.GetComponent<Renderer>();
                if(renderer != null && renderer.bounds.min.y < 3.2f)
                {
                    ceiling.position += Vector3.up*(3.4f-renderer.bounds.min.y);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ceiling);
                    entranceRepairs++;
                }
            }
            foreach(var panel in Object.FindObjectsOfType<Transform>().Where(t=>t.name=="panelA" && t.position.x > -31 && t.position.x < -26 && t.position.z < -57 && t.position.z > -62).ToArray())
            {
                panel.localScale *= 0.4f;
                panel.position = new Vector3(-33.5f,2.4f,-60.5f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
                entranceRepairs++;
            }
            var surface = Object.FindObjectOfType<NavMeshSurface>();
            surface.BuildNavMesh();
            string path = "Assets/Scenes/W4_SafeNavigation.asset";
            var saved = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (saved == null) AssetDatabase.CreateAsset(surface.navMeshData,path);
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData,saved);
                surface.RemoveData(); surface.navMeshData = saved; surface.AddData();
                EditorUtility.SetDirty(saved);
            }
            Physics.SyncTransforms();
            int samples = 0, unsupported = 0, cutouts = 0;
            foreach(var filter in Object.FindObjectsOfType<MeshFilter>())
            {
                string name = filter.name.ToLowerInvariant();
                if (!(name.StartsWith("street_road") || name.StartsWith("street_pavement") || name.StartsWith("street_panels")) || filter.GetComponent<MeshCollider>() == null) continue;
                var bounds = filter.GetComponent<Renderer>().bounds;
                if(bounds.max.y > 3) continue;
                for(int x = 1; x <= 3; x++) for(int z = 1; z <= 3; z++)
                {
                    var origin = new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x/4f),bounds.max.y+0.3f,Mathf.Lerp(bounds.min.z,bounds.max.z,z/4f));
                    samples++;
                    if (!Physics.Raycast(origin,Vector3.down,2f,~0,QueryTriggerInteraction.Ignore))
                    {
                        var mesh = filter.GetComponent<MeshCollider>();
                        if (mesh.Raycast(new Ray(origin,Vector3.down),out var hit,2f)) unsupported++;
                        else cutouts++;
                    }
                }
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/w4-safety-fix.txt", "addedColliders="+added+" samples="+samples+" unsupported="+unsupported+" meshCutouts="+cutouts+" entranceRepairs="+entranceRepairs);
        }
        public static void Audit()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play mode first.");
            EditorSceneManager.OpenScene("Assets/Scenes/W4_NeonStreet.unity");
            Physics.SyncTransforms();
            var report = new StringBuilder();
            var all = Object.FindObjectsOfType<Transform>(true);
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(o=>o.GetComponent<ThirdPersonController>()!=null || o.name.Contains("House") || o.name.Contains("Ground"))) report.AppendLine("ROOT " + root.name + " " + root.transform.position);
            foreach (var filter in Object.FindObjectsOfType<MeshFilter>().Where(f=> f.GetComponent<Renderer>() != null && f.GetComponent<Renderer>().bounds.max.y < 3 && (f.GetComponent<Renderer>().bounds.size.x > 3 || f.GetComponent<Renderer>().bounds.size.z > 3)))
            {
                var bounds = filter.GetComponent<Renderer>().bounds;
                report.AppendLine("SURFACE " + filter.name + " mesh=" + filter.sharedMesh.name + " collider=" + (filter.GetComponent<Collider>()!=null) + " bounds=" + bounds);
            }
            foreach (var col in Object.FindObjectsOfType<Collider>().Where(c => c.name.ToLower().Contains("ground") || c.name.ToLower().Contains("floor") || c.name.Contains("SideWalk") || c.bounds.size.x > 30 || c.bounds.size.z > 30).Take(100))
                report.AppendLine("COL " + col.name + " " + col.GetType().Name + " trigger=" + col.isTrigger + " bounds=" + col.bounds);
            foreach (var renderer in Object.FindObjectsOfType<Renderer>().Where(r=>r.name.ToLower().Contains("road") || r.name.ToLower().Contains("street") || r.name.ToLower().Contains("floor")).Take(70))
                report.AppendLine("VIS " + renderer.name + " " + renderer.bounds);
            foreach(var point in all.Where(t => t.GetComponent<IInteractable>() != null)) report.AppendLine("INTERACT " + point.name + " " + point.position);
            var triangle = NavMesh.CalculateTriangulation();
            int unsupported = 0;
            foreach(var vertex in triangle.vertices)
                if(!Physics.Raycast(vertex + Vector3.up*0.2f,Vector3.down,1f,~0,QueryTriggerInteraction.Ignore))
                {
                    if(unsupported < 30) report.AppendLine("UNSUPPORTED_NAV " + vertex);
                    unsupported++;
                }
            report.AppendLine("navVertices="+triangle.vertices.Length+" unsupported="+unsupported);
            foreach(var target in Object.FindObjectsOfType<MonoBehaviour>().Where(m=>m is HousePurchase || m is ShopPoint))
            {
                NavMesh.SamplePosition(new Vector3(-20,1.3f,-62),out var start,3,NavMesh.AllAreas);
                NavMesh.SamplePosition(target.transform.position,out var end,8,NavMesh.AllAreas);
                var path = new NavMeshPath();
                NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path);
                report.AppendLine("SERVICE "+target.name+" pos="+target.transform.position+" nav="+end.position+" status="+path.status);
            }
            for(float x=-21;x>=-30;x-=0.5f)
            {
                var hits=Physics.RaycastAll(new Vector3(x,4,-58),Vector3.down,8,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance).Take(4);
                report.AppendLine("CROSS "+x+" "+string.Join(" ; ",hits.Select(h=>h.collider.name+" y="+h.point.y.ToString("F2"))));
            }
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/w4-safety-audit.txt",report.ToString());
        }

        public static void Entrance()
        {
            var report = new StringBuilder();
            foreach(var collider in Object.FindObjectsOfType<Collider>())
            {
                var b=collider.bounds;
                if(b.Intersects(new Bounds(new Vector3(-29,2,-58),new Vector3(6,3,7))))
                    report.AppendLine(collider.name+" "+b+" trigger="+collider.isTrigger);
            }
            File.WriteAllText("E:/UnityProjects/PlayerAnimationWork/w4-entrance.txt",report.ToString());
        }
    }
}
