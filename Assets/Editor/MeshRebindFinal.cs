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
    public static class MeshRebindFinal
    {
        const string MeshDir = "Assets/HorrorEnvironment_Hospital/Meshes";
        const string PrefabDir = "Assets/HorrorEnvironment_Hospital/Prefabs";
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";

        static Dictionary<string, Mesh> normal = new Dictionary<string, Mesh>();
        static Dictionary<string, Mesh> colliders = new Dictionary<string, Mesh>();
        static Mesh builtinPlane = null;
        static StringBuilder log = new StringBuilder();

        static readonly Dictionary<string, string> Alias = new Dictionary<string, string>
        {
            { "elevator_jamb_CLOSED", "elevator_jamb" },
            { "elevator_jamb_OPENED", "elevator_jamb" },
            { "elevator_jamb_closed", "elevator_jamb" },
            { "elevator_jamb_opened", "elevator_jamb" },
            { "toilet_C", "toilet_A" },
            { "toilet_B", "toilet_A" },
            { "strangeMedicine", "medicine_mid_A" }
        };

        [MenuItem("Medusa Horror/Run Final Mesh Rebind")]
        public static void Run()
        {
            try
            {
                BuildMeshIndex();
                RebindPrefabs();
                VerifyScene();
                WriteReport();
                Debug.Log("[MeshRebindFinal] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[MeshRebindFinal] FAILED: " + e);
                log.AppendLine("FATAL: " + e);
                WriteReport();
                if (Application.isBatchMode) EditorApplication.Exit(2);
            }
        }

        static void BuildMeshIndex()
        {
            var fbx = AssetDatabase.FindAssets("t:Model", new[] { MeshDir })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p).ToArray();
            log.AppendLine("FBX_COUNT=" + fbx.Length);
            foreach (var path in fbx)
            {
                var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                log.AppendLine("FBX " + Path.GetFileName(path) + " meshes=" + meshes.Length);
                foreach (var m in meshes)
                {
                    if (m == null || string.IsNullOrEmpty(m.name)) continue;
                    bool isCol = m.name.EndsWith("_COLLIDER", StringComparison.OrdinalIgnoreCase);
                    var dict = isCol ? colliders : normal;
                    if (!dict.ContainsKey(m.name)) dict[m.name] = m;
                    string key = Norm(m.name);
                    if (!dict.ContainsKey(key)) dict[key] = m;
                }
            }
            log.AppendLine("MESH_INDEX normal=" + normal.Count + " collider=" + colliders.Count);
            builtinPlane = Resources.GetBuiltinResource<Mesh>("Plane.fbx");
            log.AppendLine("BUILTIN_PLANE=" + (builtinPlane != null ? builtinPlane.name + " verts=" + builtinPlane.vertexCount + " bounds=" + builtinPlane.bounds.size.ToString("F1") : "<null>"));
        }
        static string Norm(string s)
        {
            if (s == null) return "";
            s = s.ToLowerInvariant();
            foreach (var suf in new[] { "_collider", "_closed", "_opened", "_open", "_close", "_on", "_off" })
                if (s.EndsWith(suf)) s = s.Substring(0, s.Length - suf.Length);
            var sb = new StringBuilder();
            foreach (var c in s) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        static Mesh Resolve(string name, bool wantCollider)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var primary = wantCollider ? colliders : normal;
            var fallback = wantCollider ? normal : colliders;

            string aliasTarget;
            if (Alias.TryGetValue(name, out aliasTarget))
            {
                Mesh am;
                if (primary.TryGetValue(aliasTarget, out am)) return am;
                if (fallback.TryGetValue(aliasTarget, out am)) return am;
            }

            if (name.IndexOf("glassPlane", StringComparison.OrdinalIgnoreCase) >= 0 && builtinPlane != null)
                return builtinPlane;

            Mesh m;
            if (primary.TryGetValue(name, out m)) return m;
            if (fallback.TryGetValue(name, out m)) return m;

            string n = Norm(name);
            if (primary.TryGetValue(n, out m)) return m;
            if (fallback.TryGetValue(n, out m)) return m;

            if (wantCollider)
            {
                var withSuffix = n + "collider";
                if (colliders.TryGetValue(withSuffix, out m)) return m;
            }
            return null;
        }

        static void RebindPrefabs()
        {
            var files = Directory.GetFiles(PrefabDir, "*.prefab", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p).ToArray();
            log.AppendLine("PREFAB_FILES=" + files.Length);

            int totalEmptyBefore = 0, totalFixed = 0, totalEmptyAfter = 0, prefabsTouched = 0;
            var unresolved = new List<string>();

            foreach (var file in files)
            {
                GameObject root = null;
                try { root = PrefabUtility.LoadPrefabContents(file); }
                catch (Exception e) { log.AppendLine("LOAD_FAIL " + file + " :: " + e.Message); continue; }

                int before = 0, fixedNow = 0;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null) continue;
                    before++;
                    var mesh = Resolve(mf.gameObject.name, false);
                    if (mesh != null) { mf.sharedMesh = mesh; EditorUtility.SetDirty(mf); fixedNow++; }
                    else unresolved.Add(file + " :: " + mf.gameObject.name);
                }

                int after = 0;
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh == null) after++;

                totalEmptyBefore += before;
                totalFixed += fixedNow;
                totalEmptyAfter += after;

                if (fixedNow > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, file);
                    prefabsTouched++;
                    log.AppendLine("PREFAB " + file + " before=" + before + " fixed=" + fixedNow + " after=" + after);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.AppendLine("SUMMARY_PREFAB emptyBefore=" + totalEmptyBefore + " fixed=" + totalFixed + " emptyAfter=" + totalEmptyAfter + " touched=" + prefabsTouched);
            log.AppendLine("UNRESOLVED_COUNT=" + unresolved.Count);
            foreach (var u in unresolved.Take(60)) log.AppendLine("UNRESOLVED " + u);
        }
        static void VerifyScene()
        {
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            log.AppendLine("SCENE_LOADED=" + scene.name + " valid=" + scene.IsValid() + " rootCount=" + scene.rootCount);

            var allFilters = UnityEngine.Object.FindObjectsOfType<MeshFilter>(true);
            var allRenderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            int emptyFilter = 0, rendererWithMesh = 0, emptyCollider = 0;
            foreach (var f in allFilters) if (f.sharedMesh == null) emptyFilter++;
            foreach (var r in allRenderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) rendererWithMesh++;
                else if (r is SkinnedMeshRenderer && ((SkinnedMeshRenderer)r).sharedMesh != null) rendererWithMesh++;
            }
            foreach (var c in UnityEngine.Object.FindObjectsOfType<MeshCollider>(true)) if (c.sharedMesh == null) emptyCollider++;

            var cam = Camera.main;
            int inFrustum = 0;
            if (cam != null)
            {
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                foreach (var r in allRenderers)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    var mf = r.GetComponent<MeshFilter>();
                    bool hasMesh = (mf != null && mf.sharedMesh != null) || (r is SkinnedMeshRenderer && ((SkinnedMeshRenderer)r).sharedMesh != null);
                    if (!hasMesh) continue;
                    if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) inFrustum++;
                }
                log.AppendLine("CAM " + cam.name + " pos=" + cam.transform.position.ToString("F2") + " euler=" + cam.transform.eulerAngles.ToString("F2") + " fov=" + cam.fieldOfView);
            }
            log.AppendLine("MESHFILTER_TOTAL=" + allFilters.Length);
            log.AppendLine("MESHFILTER_EMPTY=" + emptyFilter);
            log.AppendLine("RENDERER_TOTAL=" + allRenderers.Length);
            log.AppendLine("RENDERER_WITH_MESH=" + rendererWithMesh);
            log.AppendLine("RENDERER_IN_FRUSTUM=" + inFrustum);
            log.AppendLine("MESHCOLLIDER_EMPTY=" + emptyCollider);

            AnalyzeSpawn(new Vector3(-20f, 8.85f, 2.4f));
        }

        static void AnalyzeSpawn(Vector3 p)
        {
            log.AppendLine("SPAWN_TEST pos=" + p.ToString("F2"));
            float downDist = -1f, upDist = -1f;
            string downHit = "<none>", upHit = "<none>";
            RaycastHit hit;
            Physics.SyncTransforms();
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore)) { downDist = hit.distance; downHit = hit.collider.name; }
            if (Physics.Raycast(p + Vector3.up * 0.05f, Vector3.up, out hit, 30f, ~0, QueryTriggerInteraction.Ignore)) { upDist = hit.distance; upHit = hit.collider.name; }
            log.AppendLine("SPAWN_RAYCAST down=" + downDist.ToString("F2") + " hit=" + downHit + " | up=" + upDist.ToString("F2") + " hit=" + upHit);

            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            var names = new[] { "+Z", "-Z", "-X", "+X" };
            for (int i = 0; i < dirs.Length; i++)
            {
                float d = -1f; string n = "<none>";
                if (Physics.Raycast(p, dirs[i], out hit, 40f, ~0, QueryTriggerInteraction.Ignore)) { d = hit.distance; n = hit.collider.name; }
                log.AppendLine("SPAWN_HIT " + names[i] + " dist=" + d.ToString("F2") + " obj=" + n);
            }

            int contain = 0;
            foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (r.bounds.Contains(p)) { contain++; if (contain <= 10) log.AppendLine("SPAWN_INSIDE_BOUNDS " + r.name + " size=" + r.bounds.size.ToString("F1")); }
            }
            log.AppendLine("SPAWN_BOUNDS_CONTAINS=" + contain);
        }

        static void WriteReport()
        {
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/mesh_rebind_final.txt"));
            File.WriteAllText(outPath, log.ToString(), new UTF8Encoding(false));
            Debug.Log("[MeshRebindFinal] REPORT_WRITTEN: " + outPath);
        }
    }
}