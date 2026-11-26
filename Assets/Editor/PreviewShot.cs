using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MedusaHorror.EditorTools
{
    /// <summary>渲染 Aphrodite 全身三视角，用于核对材质是否与官方预览一致。</summary>
    public static class PreviewShot
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string PrefabPath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";

        public static void Shoot()
        {
            var log = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 环境：明亮的室内感，金属才有可反射的东西
            var skyShader = Shader.Find("Skybox/Procedural");
            var sky = new Material(skyShader);
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetColor("_SkyTint", new Color(0.72f, 0.78f, 0.9f));
            sky.SetColor("_GroundColor", new Color(0.45f, 0.45f, 0.47f));
            sky.SetFloat("_Exposure", 1.6f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.35f;
            RenderSettings.fog = false;

            AddLight("Key", new Vector3(35f, 155f, 0f), 1.7f, new Color(1f, 0.97f, 0.92f));
            AddLight("Fill", new Vector3(10f, -50f, 0f), 0.9f, new Color(0.78f, 0.85f, 1f));
            AddLight("Rim", new Vector3(-18f, 20f, 0f), 1.2f, new Color(0.9f, 0.95f, 1f));

            // 金属装甲必须有环境反射，否则镀铬会渲染成黑块
            var probeGo = new GameObject("ReflectProbe");
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(24f, 24f, 24f);
            probe.resolution = 256;
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.RenderProbe();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);
            cam.fieldOfView = 26f;
            cam.nearClipPlane = 0.05f;
            var rt = new RenderTexture(700, 1100, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            var b = GetBounds(go);
            go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = GetBounds(go);
            float h = b.size.y;
            log.AppendLine("[BOUNDS] size=" + b.size.ToString("F3") + " min=" + b.min.ToString("F2"));

            // 地面
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.position = new Vector3(0f, 0.001f, 0f);
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
            var gm = new Material(Shader.Find("Standard"));
            gm.color = new Color(0.28f, 0.29f, 0.31f);
            gm.SetFloat("_Glossiness", 0.15f);
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            float cy = h * 0.55f;
            float dist = h * 1.55f;
            string[] names = { "yaw000", "yaw045", "yaw090", "yaw135", "yaw180", "yaw225", "yaw270", "yaw315" };
            int[] yaws = { 0, 45, 90, 135, 180, 225, 270, 315 };
            for (int i = 0; i < names.Length; i++)
            {
                Vector3 target = new Vector3(0f, cy, 0f);
                cam.fieldOfView = 26f;
                var rot = Quaternion.Euler(0f, yaws[i], 0f);
                cam.transform.position = target + rot * new Vector3(0f, 0f, -dist);
                cam.transform.LookAt(target);
                Render(cam, rt, "ap_" + names[i]);
                log.AppendLine("[SHOT] " + names[i]);
            }

            // 材质核对
            var rs = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rs)
            {
                var m = r.sharedMaterial;
                if (m == null) { log.AppendLine("[REND] " + r.name + " material=NULL"); continue; }
                log.AppendLine("[REND] " + r.name + " mat=" + m.name
                    + " tex=" + (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null ? m.GetTexture("_MainTex").name : "null")
                    + " metalMap=" + (m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null ? "yes" : "no")
                    + " metal=" + (m.HasProperty("_Metallic") ? m.GetFloat("_Metallic").ToString("F2") : "-"));
            }

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(probeGo);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            foreach (var l in GameObject.FindObjectsOfType<Light>()) UnityEngine.Object.DestroyImmediate(l.gameObject);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            File.WriteAllText(Path.Combine(OutDir, "preview.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[PreviewShot]\n" + log.ToString());
        }

        static void AddLight(string name, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.55f;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        static void Render(Camera cam, RenderTexture rt, string name)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutDir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static Bounds GetBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool has = false;
            Bounds b = new Bounds();
            foreach (var r in rs)
            {
                if (!r.enabled) continue;
                if (r.GetComponent<MeshRenderer>() != null && r.GetComponent<SkinnedMeshRenderer>() == null) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return has ? b : new Bounds(Vector3.zero, Vector3.zero);
        }
    }
}
