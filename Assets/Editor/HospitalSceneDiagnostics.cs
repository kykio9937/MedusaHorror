using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// Read-only diagnostics for W1_Hospital geometry loss.
    /// Run: Unity.exe -batchmode -quit -executeMethod MedusaHorror.EditorTools.HospitalSceneDiagnostics.DiagnoseScene
    /// </summary>
    public static class HospitalSceneDiagnostics
    {
        const string TargetScene = "Assets/Scenes/W1_Hospital.unity";
        const string SourceScene = "Assets/HorrorEnvironment_Hospital/Hespital_EXAMPLE.unity";
        const float FrontDistance = 60f;

        [MenuItem("Medusa Horror/Diagnose Hospital Scene")]
        public static void DiagnoseScene()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== HOSPITAL SCENE DIAGNOSTICS REPORT =====");
            sb.AppendLine("TIME: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("UNITY: " + Application.unityVersion);
            sb.AppendLine();

            sb.AppendLine("########## [1] SOURCE SCENE ##########");
            DumpScene(SourceScene, sb);
            sb.AppendLine();

            sb.AppendLine("########## [2] TARGET SCENE ##########");
            DumpScene(TargetScene, sb);
            sb.AppendLine();

            sb.AppendLine("########## [3] VERDICT ##########");
            sb.AppendLine("Compare SOURCE vs TARGET numbers above:");
            sb.AppendLine(" - If SOURCE also has near-zero effective renderers, the source scene itself cannot render correctly.");
            sb.AppendLine(" - If SOURCE is fine but TARGET MESH_RENDERER_TOTAL drops, instances were lost on save.");
            sb.AppendLine(" - If TARGET PREFAB_GUID_FAILED > 0, prefab assets fail to resolve.");
            sb.AppendLine(" - If renderers exist but CAM RENDERERS_INSIDE_FRUSTUM is 0, camera placement/orientation is the issue.");
            sb.AppendLine(" - If RENDERERS_CULLED_BY_MASK > 0, camera cullingMask excludes those layers.");
            sb.AppendLine("===== END REPORT =====");

            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/scene_diag_report.txt"));
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
                Debug.Log("[SceneDiag] REPORT_WRITTEN: " + outPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[SceneDiag] WRITE_FAILED: " + e.Message);
            }

            Debug.Log(sb.ToString());
            Debug.Log("[SceneDiag] DONE");
        }

        static void DumpScene(string path, StringBuilder sb)
        {
            sb.AppendLine("---- SCENE: " + path + " ----");
            if (!File.Exists(path))
            {
                sb.AppendLine("SCENE_FILE_MISSING: true");
                return;
            }
            var fi = new FileInfo(path);
            sb.AppendLine("FILE_SIZE_BYTES: " + fi.Length + " | LAST_WRITE: " + fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));

            Scene scene;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
            catch (Exception e)
            {
                sb.AppendLine("OPEN_SCENE_FAILED: " + e.Message);
                return;
            }
            sb.AppendLine("SCENE_IS_LOADED: " + scene.isLoaded + " | SCENE_PATH: " + scene.path);

            var roots = scene.GetRootGameObjects();
            sb.AppendLine("ROOT_COUNT: " + roots.Length);
            foreach (var r in roots)
            {
                sb.AppendLine(string.Format("   ROOT: {0} | activeSelf={1} | activeInHierarchy={2} | children={3} | pos={4} | scale={5}",
                    r.name, r.activeSelf, r.activeInHierarchy, r.transform.childCount,
                    r.transform.position.ToString("F2"), r.transform.lossyScale.ToString("F2")));
            }
            sb.AppendLine("INACTIVE_ROOT_COUNT(m_IsActive=false): " + roots.Count(r => !r.activeSelf));

            var meshRenderers = new List<MeshRenderer>();
            var skinned = new List<SkinnedMeshRenderer>();
            var meshFilters = new List<MeshFilter>();
            var monos = new List<MonoBehaviour>();
            foreach (var r in roots)
            {
                meshRenderers.AddRange(r.GetComponentsInChildren<MeshRenderer>(true));
                skinned.AddRange(r.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                meshFilters.AddRange(r.GetComponentsInChildren<MeshFilter>(true));
                monos.AddRange(r.GetComponentsInChildren<MonoBehaviour>(true));
            }

            sb.AppendLine("MESH_RENDERER_TOTAL(incl prefab instances): " + meshRenderers.Count);
            sb.AppendLine("SKINNED_MESH_RENDERER_TOTAL: " + skinned.Count);
            sb.AppendLine("MESH_RENDERER_COMPONENT_ENABLED: " + meshRenderers.Count(m => m.enabled));
            sb.AppendLine("MESH_RENDERER_EFFECTIVELY_VISIBLE(enabled && activeInHierarchy): " +
                          meshRenderers.Count(m => m.enabled && m.gameObject.activeInHierarchy));
            sb.AppendLine("MESH_FILTER_TOTAL: " + meshFilters.Count);
            sb.AppendLine("MESH_FILTER_NULL_MESH: " + meshFilters.Count(f => f.sharedMesh == null));
            sb.AppendLine("RENDERER_NULL_MATERIAL: " + meshRenderers.Count(m => m.sharedMaterial == null));
            sb.AppendLine("RENDERER_MATERIAL_NULL_SHADER: " +
                          meshRenderers.Count(m => m.sharedMaterial != null && m.sharedMaterial.shader == null));
            sb.AppendLine("MONOBEHAVIOUR_TOTAL: " + monos.Count + " | NULL_SCRIPT(missing): " + monos.Count(m => m == null));

            var layerGroups = meshRenderers.GroupBy(m => m.gameObject.layer)
                                           .Select(g => "L" + g.Key + "=" + g.Count());
            sb.AppendLine("MESH_RENDERER_BY_LAYER: " + string.Join(", ", layerGroups.ToArray()));

            var cams = new List<Camera>();
            foreach (var r in roots) cams.AddRange(r.GetComponentsInChildren<Camera>(true));
            sb.AppendLine("CAMERA_COUNT(incl inactive): " + cams.Count);
            foreach (var c in cams)
            {
                var t = c.transform;
                sb.AppendLine(string.Format("   CAM: {0} | path={1} | pos={2} | euler={3} | forward={4} | fov={5} | near={6} | far={7} | depth={8} | compEnabled={9} | activeInHierarchy={10} | cullingMask=0x{11:X}",
                    c.name, GetPath(t), t.position.ToString("F3"), t.eulerAngles.ToString("F1"),
                    t.forward.ToString("F3"), c.fieldOfView, c.nearClipPlane, c.farClipPlane, c.depth,
                    c.enabled, c.gameObject.activeInHierarchy, c.cullingMask));

                int inFront = 0, inFrustum = 0, culledByMask = 0;
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(c);
                foreach (var mr in meshRenderers)
                {
                    var mfCheck = mr.GetComponent<MeshFilter>();
                    if (mfCheck == null || mfCheck.sharedMesh == null) continue;
                    Bounds b = mr.bounds;
                    Vector3 to = b.center - t.position;
                    float dist = to.magnitude;
                    bool front = dist <= FrontDistance && Vector3.Dot(t.forward, to.normalized) > 0f;
                    if (front) inFront++;
                    if (GeometryUtility.TestPlanesAABB(planes, b)) inFrustum++;
                    if ((c.cullingMask & (1 << mr.gameObject.layer)) == 0) culledByMask++;
                }
                sb.AppendLine("      RENDERERS_WITHIN_" + FrontDistance + "M_IN_FRONT: " + inFront);
                sb.AppendLine("      RENDERERS_INSIDE_FRUSTUM: " + inFrustum);
                sb.AppendLine("      RENDERERS_CULLED_BY_MASK: " + culledByMask);
            }

            var lights = new List<Light>();
            foreach (var r in roots) lights.AddRange(r.GetComponentsInChildren<Light>(true));
            sb.AppendLine("LIGHT_COUNT: " + lights.Count + " | ENABLED: " + lights.Count(l => l.enabled && l.gameObject.activeInHierarchy));

            DumpPrefabs(path, sb);
        }

        static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Insert(0, t.name); t = t.parent; }
            return string.Join("/", parts.ToArray());
        }

        static void DumpPrefabs(string scenePath, StringBuilder sb)
        {
            string text;
            try { text = File.ReadAllText(scenePath); }
            catch (Exception e) { sb.AppendLine("PREFAB_SCAN_READ_FAILED: " + e.Message); return; }

            var rx = new Regex(@"m_SourcePrefab: \{fileID: \d+, guid: ([0-9a-fA-F]{32}), type: 3\}");
            var counter = new Dictionary<string, int>();
            int total = 0;
            foreach (Match m in rx.Matches(text))
            {
                total++;
                string g = m.Groups[1].Value;
                if (!counter.ContainsKey(g)) counter[g] = 0;
                counter[g]++;
            }

            sb.AppendLine("PREFAB_INSTANCE_REFS_TOTAL: " + total + " | DISTINCT_GUIDS: " + counter.Count);
            int ok = 0, fail = 0;
            foreach (var kv in counter)
            {
                string p = AssetDatabase.GUIDToAssetPath(kv.Key);
                GameObject asset = string.IsNullOrEmpty(p) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(p);
                bool resolved = asset != null;
                if (resolved) ok++; else fail++;
                sb.AppendLine(string.Format("   PREFAB_GUID: {0} | refCount={1} | assetPath={2} | RESOLVED={3}",
                    kv.Key, kv.Value, string.IsNullOrEmpty(p) ? "<none>" : p, resolved));
                if (!resolved)
                    sb.AppendLine("      *** RESOLVE_FAILED *** guid=" + kv.Key + " (instance renders as empty placeholder)");
            }
            sb.AppendLine("PREFAB_GUID_SUMMARY: RESOLVED=" + ok + " | FAILED=" + fail);
        }
    }
}