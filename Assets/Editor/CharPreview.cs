using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>把候选角色预制体逐个渲染成 PNG，用于挑选玩家模型。</summary>
    public static class CharPreview
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";

        static readonly string[] Assets =
        {
            "Assets/Space/Prefabs/spaceMan.prefab",
            "Assets/Space/Prefabs/spaceManB.prefab",
            "Assets/Space/Prefabs/android.prefab",
            "Assets/Space/Prefabs/marauder.prefab",
            "Assets/Space/Prefabs/Mech.prefab",
            "Assets/Space/Prefabs/Mech 1.prefab",
            "Assets/Space/Prefabs/rider.prefab",
            "Assets/Space/Prefabs/riderB.prefab",
        };

        // 与 Assets 一一对应：模型在包内的原始朝向不一致，逐个指定朝向镜头的旋转角
        static readonly float[] Yaws = { 180f, 0f, 180f, 180f, 180f, 0f, 180f, 180f };

        public static void RenderAll()
        {
            Directory.CreateDirectory(OutDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("PreviewSun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightGo.transform.rotation = Quaternion.Euler(32f, 148f, 0f);

            var fillGo = new GameObject("PreviewFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fillGo.transform.rotation = Quaternion.Euler(18f, -40f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.45f, 0.5f);
            RenderSettings.fog = false;

            var camGo = new GameObject("PreviewCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 200f;

            var rt = new RenderTexture(760, 960, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            var report = new List<string>();

            for (int i = 0; i < Assets.Length; i++)
            {
                string path = Assets[i];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { report.Add("MISSING " + path); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.position = Vector3.zero;
                // Space 包模型朝 +Z，相机在 -Z 侧，转 180° 让正面朝向镜头
                go.transform.rotation = Quaternion.Euler(0f, Yaws[i], 0f);
                go.transform.localScale = Vector3.one;

                // 去掉自带相机/灯光，避免干扰
                foreach (var c in go.GetComponentsInChildren<Camera>(true)) c.enabled = false;
                foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;

                Bounds b = GetBounds(go);
                if (b.size == Vector3.zero) { UnityEngine.Object.DestroyImmediate(go); report.Add("NO_RENDERER " + path); continue; }

                // 把模型整体挪到原点、脚底贴地
                go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
                b = GetBounds(go);

                float h = Mathf.Max(b.size.y, 0.3f);
                float w = Mathf.Max(b.size.x, b.size.z, 0.3f);
                float dist = Mathf.Max(h * 1.85f, w * 1.9f);

                cam.transform.position = new Vector3(dist * 0.34f, h * 0.62f, -dist);
                cam.transform.LookAt(new Vector3(0f, h * 0.48f, 0f));

                // 地面
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "PreviewFloor";
                floor.transform.position = new Vector3(0f, 0f, 0f);
                floor.transform.localScale = new Vector3(dist, 1f, dist);
                var fr = floor.GetComponent<Renderer>();
                var fm = new Material(Shader.Find("Standard"));
                fm.color = new Color(0.22f, 0.23f, 0.25f);
                fr.sharedMaterial = fm;

                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                string name = string.Format("pick_{0:00}_{1}.png", i + 1, Path.GetFileNameWithoutExtension(path).Replace(" ", "_"));
                string outPath = Path.Combine(OutDir, name);
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                report.Add(name + "  h=" + h.ToString("F2") + "m  w=" + w.ToString("F2") + "m  asset=" + path);

                UnityEngine.Object.DestroyImmediate(floor);
                UnityEngine.Object.DestroyImmediate(go);
            }

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(fillGo);

            string rep = string.Join("\n", report.ToArray());
            File.WriteAllText(Path.Combine(OutDir, "pick_report.txt"), rep);
            Debug.Log("[CharPreview]\n" + rep);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static Bounds GetBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds b = new Bounds();
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!r.enabled) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!has) return new Bounds(Vector3.zero, Vector3.zero);
            return b;
        }
    }
}
