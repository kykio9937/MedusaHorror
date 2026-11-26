using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>列出场景里每个材质的 shader / 贴图 / 颜色，定位"棋盘格"到底来自哪。</summary>
    public static class SceneDiag
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";

        public static void W5()
        {            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W5_AlienGate.unity", OpenSceneMode.Single);
            var sb = new StringBuilder();
            var seen = new Dictionary<Material, int>();
            var users = new Dictionary<Material, List<string>>();

            foreach (var root in sc.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        if (!seen.ContainsKey(m)) { seen[m] = 0; users[m] = new List<string>(); }
                        seen[m]++;
                        if (users[m].Count < 3) users[m].Add(r.name);
                    }
                }
            }

            sb.AppendLine("[MATERIALS] unique=" + seen.Count);
            foreach (var kv in seen)
            {
                var m = kv.Key;
                string tex = "-";
                if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) tex = m.GetTexture("_MainTex").name;
                else if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) tex = m.GetTexture("_BaseMap").name;
                Color c = m.HasProperty("_Color") ? m.GetColor("_Color") : (m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.magenta);
                sb.AppendLine("  " + m.name + " | shader=" + (m.shader != null ? m.shader.name : "NULL")
                    + " | tex=" + tex + " | color=" + c.ToString("F2")
                    + " | used=" + kv.Value + " | e.g. " + string.Join(",", users[m]));
            }

            // 场景光照/环境
            sb.AppendLine("[ENV] skybox=" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")
                + " ambientMode=" + RenderSettings.ambientMode
                + " ambientLight=" + RenderSettings.ambientLight.ToString("F2")
                + " fog=" + RenderSettings.fog
                + " sun=" + (RenderSettings.sun != null ? RenderSettings.sun.name : "null"));

            // 相机
            var cam = Camera.main;
            sb.AppendLine("[CAM] " + (cam != null ? cam.name + " pos=" + cam.transform.position.ToString("F1") + " fov=" + cam.fieldOfView : "none"));

            File.WriteAllText(Path.Combine(OutDir, "w5diag.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[SceneDiag]\n" + sb.ToString());
        }

        /// <summary>W5 全景俯视截图，确认整体渲染状态。</summary>
        public static void Pano()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W5_AlienGate.unity", OpenSceneMode.Single);

            Bounds b = new Bounds();
            bool has = false;
            foreach (var root in sc.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.GetComponent<SkinnedMeshRenderer>() != null) continue;
                    if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
                }
            }
            var sb = new StringBuilder();
            sb.AppendLine("[BOUNDS] " + b.center.ToString("F1") + " size=" + b.size.ToString("F1"));

            var camGo = new GameObject("PanoCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 4000f;
            var rt = new RenderTexture(1100, 700, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // 站在玩家出生点上方远处俯视
            Vector3 eye = b.center + new Vector3(-b.extents.x * 0.9f, b.extents.y * 0.9f, -b.extents.z * 1.1f);
            cam.transform.position = eye;
            cam.transform.LookAt(b.center);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutDir, "w5_pano.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);

            // 玩家视角正前方
            var cam2Go = new GameObject("PanoCam2");
            var cam2 = cam2Go.AddComponent<Camera>();
            cam2.fieldOfView = 60f;
            cam2.farClipPlane = 4000f;
            var rt2 = new RenderTexture(1100, 700, 24, RenderTextureFormat.ARGB32);
            cam2.targetTexture = rt2;
            var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tpc != null)
            {
                Vector3 p = tpc.transform.position;
                cam2.transform.position = p + Vector3.up * 1.7f;
                cam2.transform.rotation = tpc.transform.rotation;
                sb.AppendLine("[PLAYER] " + p.ToString("F1") + " rot=" + tpc.transform.eulerAngles.ToString("F0"));
            }
            cam2.Render();
            RenderTexture.active = rt2;
            var tex2 = new Texture2D(rt2.width, rt2.height, TextureFormat.RGB24, false);
            tex2.ReadPixels(new Rect(0, 0, rt2.width, rt2.height), 0, 0);
            tex2.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutDir, "w5_player.png"), tex2.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex2);
            cam2.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt2);
            UnityEngine.Object.DestroyImmediate(cam2Go);

            File.WriteAllText(Path.Combine(OutDir, "w5pano.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Pano]\n" + sb.ToString());
        }
    }
}
