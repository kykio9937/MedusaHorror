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
    public static class MeshColliderRebind
    {
        const string MeshDir = "Assets/HorrorEnvironment_Hospital/Meshes";
        const string PrefabDir = "Assets/HorrorEnvironment_Hospital/Prefabs";
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";

        static readonly Dictionary<string, Mesh> Col = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, Mesh> Norm2 = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
        static Mesh BuiltinPlane;
        static readonly StringBuilder Log = new StringBuilder();
        static readonly List<string> Unresolved = new List<string>();

        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "elevator_jamb_CLOSED", "elevator_jamb" },
            { "elevator_jamb_OPENED", "elevator_jamb" },
            { "toilet_C", "toilet_A" },
            { "toilet_B", "toilet_A" },
            { "strangeMedicine", "medicine_mid_A" },
        };

        [MenuItem("Medusa Horror/Rebind Colliders")]
        public static void Run()
        {
            try
            {
                BuildMeshIndex();
                RebindPrefabs();
                RebindScene();
                ProbeScene();
            }
            catch (Exception e)
            {
                Log.AppendLine("FATAL: " + e);
                Debug.LogError("[ColliderRebind] FATAL: " + e);
            }
            WriteReport();
            Debug.Log("[ColliderRebind] DONE");
        }

        static string ToAssetPath(string full)
        {
            string p = full.Replace('\\', '/');
            int i = p.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return p.Substring(i + 1);
            return p;
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static void BuildMeshIndex()
        {
            string abs = Application.dataPath.Replace('\\', '/') + "/HorrorEnvironment_Hospital/Meshes";
            var files = Directory.GetFiles(abs, "*.fbx").Select(p => ToAssetPath(p)).OrderBy(p => p).ToArray();
            Log.AppendLine("FBX_FILES=" + files.Length);
            foreach (var assetPath in files)
            {
                var meshes = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Mesh>();
                foreach (var m in meshes)
                {
                    if (m == null) continue;
                    bool isColl = m.name.ToLowerInvariant().EndsWith("_collider");
                    var dict = isColl ? Col : Norm2;
                    if (!dict.ContainsKey(m.name)) dict[m.name] = m;
                }
            }
            try { BuiltinPlane = Resources.GetBuiltinResource<Mesh>("Plane.fbx"); } catch { BuiltinPlane = null; }
            Log.AppendLine("MESH_INDEX normal=" + Norm2.Count + " collider=" + Col.Count + " plane=" + (BuiltinPlane != null));
        }

        static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s.ToLowerInvariant();
            string[] sufs = new string[] { "_collider", "_closed", "_opened", "_open", "_close", "_on", "_off" };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var suf in sufs)
                {
                    if (t.Length > suf.Length && t.EndsWith(suf)) { t = t.Substring(0, t.Length - suf.Length); changed = true; }
                }
            }
            var sb = new StringBuilder();
            foreach (var ch in t) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        static Mesh Resolve(string name, bool wantCollider)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var primary = wantCollider ? Col : Norm2;
            var fallback = wantCollider ? Norm2 : Col;
            if (Alias.ContainsKey(name))
            {
                string a = Alias[name];
                if (primary.ContainsKey(a)) return primary[a];
                if (fallback.ContainsKey(a)) return fallback[a];
            }
            if (name.IndexOf("glassPlane", StringComparison.OrdinalIgnoreCase) >= 0 && BuiltinPlane != null) return BuiltinPlane;
            if (primary.ContainsKey(name)) return primary[name];
            if (wantCollider)
            {
                if (Col.ContainsKey(name + "_COLLIDER")) return Col[name + "_COLLIDER"];
                if (Col.ContainsKey(name + "_collider")) return Col[name + "_collider"];
                string nn = Norm(name);
                if (nn.Length > 0)
                {
                    foreach (var kv in Col) if (Norm(kv.Key) == nn) return kv.Value;
                }
            }
            if (fallback.ContainsKey(name)) return fallback[name];
            if (wantCollider)
            {
                string nn2 = Norm(name);
                if (nn2.Length > 0)
                {
                    foreach (var kv in Norm2) if (Norm(kv.Key) == nn2) return kv.Value;
                }
            }
            return null;
        }
        static void RebindPrefabs()
        {
            var files = Directory.GetFiles(PrefabDir, "*.prefab", SearchOption.AllDirectories).Select(f => ToAssetPath(f)).OrderBy(p => p).ToArray();
            Log.AppendLine("########## PREFAB PASS ##########");
            Log.AppendLine("PREFAB_FILES=" + files.Length);
            int emptyBefore = 0, fixedCount = 0, emptyAfter = 0, touched = 0, loadFail = 0;
            foreach (var assetPath in files)
            {
                GameObject root = null;
                try { root = PrefabUtility.LoadPrefabContents(assetPath); }
                catch (Exception e) { Log.AppendLine("LOAD_FAILED " + assetPath + " :: " + e.Message); loadFail++; continue; }
                if (root == null) { Log.AppendLine("LOAD_NULL " + assetPath); loadFail++; continue; }
                var cols = root.GetComponentsInChildren<MeshCollider>(true);
                int b = 0;
                foreach (var c in cols) if (c.sharedMesh == null) b++;
                emptyBefore += b;
                bool dirty = false;
                int fixedHere = 0;
                foreach (var c in cols)
                {
                    if (c.sharedMesh != null) continue;
                    Mesh m = Resolve(c.gameObject.name, true);
                    if (m == null)
                    {
                        var mf = c.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) m = mf.sharedMesh;
                    }
                    if (m != null) { c.sharedMesh = m; EditorUtility.SetDirty(c); dirty = true; fixedHere++; }
                    else Unresolved.Add(assetPath + " :: " + GetPath(c.transform) + " :: go=" + c.gameObject.name);
                }
                int a = 0;
                foreach (var c in root.GetComponentsInChildren<MeshCollider>(true)) if (c.sharedMesh == null) a++;
                emptyAfter += a;
                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    touched++;
                    Log.AppendLine("FIXED " + assetPath + " colliders=" + cols.Length + " before=" + b + " fixed=" + fixedHere + " after=" + a);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log.AppendLine("SUMMARY_PREFAB files=" + files.Length + " loadFail=" + loadFail + " emptyBefore=" + emptyBefore + " fixed=" + fixedCount + " fixedTotal=" + (emptyBefore - emptyAfter) + " emptyAfter=" + emptyAfter + " touched=" + touched);
        }

        static void RebindScene()
        {
            Log.AppendLine("########## SCENE PASS ##########");
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single); }
            catch (Exception e) { Log.AppendLine("OPEN_SCENE_FAILED: " + e.Message); return; }
            int sceneColTotal = 0, sceneEmpty = 0, sceneFixed = 0, notPrefabEmpty = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var cols = root.GetComponentsInChildren<MeshCollider>(true);
                foreach (var c in cols)
                {
                    sceneColTotal++;
                    if (c.sharedMesh != null) continue;
                    sceneEmpty++;
                    if (PrefabUtility.GetPrefabInstanceStatus(c.gameObject) != PrefabInstanceStatus.NotAPrefab) continue;
                    notPrefabEmpty++;
                    Mesh m = Resolve(c.gameObject.name, true);
                    if (m == null)
                    {
                        var mf = c.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null) m = mf.sharedMesh;
                    }
                    if (m != null)
                    {
                        Undo.RecordObject(c, "RebindCollider");
                        c.sharedMesh = m;
                        EditorUtility.SetDirty(c);
                        sceneFixed++;
                    }
                    else Unresolved.Add("SCENE :: " + GetPath(c.transform) + " :: go=" + c.gameObject.name);
                }
            }
            Log.AppendLine("SCENE_COLLIDERS total=" + sceneColTotal + " empty=" + sceneEmpty + " notPrefabEmpty=" + notPrefabEmpty + " fixed=" + sceneFixed);
            if (sceneFixed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                bool ok = EditorSceneManager.SaveScene(scene, TargetScene);
                Log.AppendLine("SCENE_SAVED=" + ok);
            }
            else Log.AppendLine("SCENE_SAVED=skipped(no direct-scene collider fixed)");
        }
        static void ProbeScene()
        {
            Log.AppendLine("########## VERIFY PASS ##########");
            Scene scene;
            try { scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single); }
            catch (Exception e) { Log.AppendLine("VERIFY_OPEN_FAILED: " + e.Message); return; }
            var roots = scene.GetRootGameObjects();
            Log.AppendLine("SCENE_LOADED=" + scene.name + " valid=" + scene.IsValid() + " rootCount=" + roots.Length);

            var allCols = new List<Collider>();
            var allMeshCols = new List<MeshCollider>();
            var allRenderers = new List<Renderer>();
            var allTransforms = new List<Transform>();
            foreach (var r in roots)
            {
                allCols.AddRange(r.GetComponentsInChildren<Collider>(true));
                allMeshCols.AddRange(r.GetComponentsInChildren<MeshCollider>(true));
                allRenderers.AddRange(r.GetComponentsInChildren<Renderer>(true));
                allTransforms.AddRange(r.GetComponentsInChildren<Transform>(true));
            }
            Log.AppendLine("COLLIDER_TOTAL=" + allCols.Count);
            Log.AppendLine("MESHCOLLIDER_TOTAL=" + allMeshCols.Count);
            Log.AppendLine("MESHCOLLIDER_EMPTY=" + allMeshCols.Count(c => c.sharedMesh == null));
            Log.AppendLine("RENDERER_TOTAL=" + allRenderers.Count + " RENDERER_WITH_MESH=" + allRenderers.Count(x => { var mf = x.GetComponent<MeshFilter>(); return mf != null && mf.sharedMesh != null; }));

            foreach (var r in roots)
                Log.AppendLine("ROOT " + r.name + " active=" + r.activeSelf + " pos=" + r.transform.position.ToString("F2") + " children=" + r.transform.childCount);

            var keys = new string[] { "player", "spawn", "character", "controller", "avatar", "rig" };
            foreach (var t in allTransforms)
            {
                string ln = t.name.ToLowerInvariant();
                bool hit = keys.Any(k => ln.Contains(k)) || t.CompareTag("Player");
                if (hit) Log.AppendLine("PLAYER_CANDIDATE path=" + GetPath(t) + " pos=" + t.position.ToString("F3") + " tag=" + t.tag + " comps=" + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name).ToArray()));
            }

            var cams = new List<Camera>();
            foreach (var r in roots) cams.AddRange(r.GetComponentsInChildren<Camera>(true));
            Log.AppendLine("CAMERA_COUNT=" + cams.Count);
            foreach (var cam in cams)
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                int inFrustum = 0, withMesh = 0;
                foreach (var mr in allRenderers)
                {
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    withMesh++;
                    if (GeometryUtility.TestPlanesAABB(planes, mr.bounds)) inFrustum++;
                }
                Log.AppendLine(string.Format("CAM {0} path={1} pos={2} euler={3} fov={4} RENDERERS_WITH_MESH={5} RENDERERS_INSIDE_FRUSTUM={6}",
                    cam.name, GetPath(cam.transform), cam.transform.position.ToString("F3"), cam.transform.eulerAngles.ToString("F1"), cam.fieldOfView, withMesh, inFrustum));
            }

            Vector3 probe = new Vector3(-20f, 8.85f, 2.4f);
            RaycastHit hitInfo;
            if (Physics.Raycast(probe + Vector3.up * 0.2f, Vector3.down, out hitInfo, 5f))
                Log.AppendLine("SPAWN_GROUND_RAY hit=" + GetPath(hitInfo.transform) + " point=" + hitInfo.point.ToString("F3") + " dist=" + hitInfo.distance.ToString("F3"));
            else Log.AppendLine("SPAWN_GROUND_RAY hit=NONE");

            Log.AppendLine("UNRESOLVED_COUNT=" + Unresolved.Count);
            foreach (var u in Unresolved.Take(60)) Log.AppendLine("  UNRESOLVED " + u);
        }

        static void WriteReport()
        {
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/collider_rebind.txt"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                var head = new StringBuilder();
                head.AppendLine("===== MESH COLLIDER REBIND REPORT =====");
                head.AppendLine("TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                head.AppendLine("UNITY: " + Application.unityVersion);
                head.Append(head.ToString());
                File.WriteAllText(outPath, Log.ToString(), new UTF8Encoding(false));
                Debug.Log("[ColliderRebind] REPORT_WRITTEN: " + outPath);
            }
            catch (Exception e) { Debug.LogError("[ColliderRebind] WRITE_FAILED: " + e.Message); }
            Debug.Log(Log.ToString());
        }
    }
}
