using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>诊断：把动画 clip 直接采样到 Aphrodite 上并渲染多帧，判断 retarget 是否正常。</summary>
    public static class AnimProbe
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string PrefabPath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";
        const string Fbx = "Assets/AscendantOne/Aphrodite/Aphrodite.fbx";
        const string KevinDir = "Assets/Kevin Iglesias/Basic Motions/Animations";

        public static void Probe()
        {
            var log = new StringBuilder();

            var sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
            var fillGo = new GameObject("Fill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.6f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fillGo.transform.rotation = Quaternion.Euler(12f, -55f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.0f;
            RenderSettings.fog = false;

            // 金属材质必须有环境反射，否则银色装甲会渲染成死灰
            var skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                var sky = new Material(skyShader);
                sky.SetFloat("_SunDisk", 2f);
                sky.SetFloat("_SunSize", 0.04f);
                sky.SetFloat("_AtmosphereThickness", 0.9f);
                sky.SetColor("_SkyTint", new Color(0.55f, 0.62f, 0.75f));
                sky.SetColor("_GroundColor", new Color(0.32f, 0.32f, 0.34f));
                sky.SetFloat("_Exposure", 1.25f);
                RenderSettings.skybox = sky;
            }
            DynamicGI.UpdateEnvironment();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
            cam.fieldOfView = 32f;
            cam.nearClipPlane = 0.02f;
            var rt = new RenderTexture(600, 900, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // 对齐脚底
            var b = GetBounds(go);
            go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            // 确保 Animator 有 avatar
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(Fbx);
                for (int i = 0; i < all.Length; i++)
                {
                    var a = all[i] as Avatar;
                    if (a != null && a.isHuman) { anim.avatar = a; break; }
                }
            }
            log.AppendLine("[AVATAR] " + (anim.avatar != null ? anim.avatar.name + " valid=" + anim.avatar.isValid : "null"));

            string[] clips = { "Movement/BasicMotions@Walk01.fbx", "Movement/BasicMotions@Run01.fbx", "Idles/BasicMotions@Idle01.fbx" };
            string[] labels = { "walk", "run", "idle" };

            for (int ci = 0; ci < clips.Length; ci++)
            {
                var clip = LoadClip(KevinDir + "/" + clips[ci], labels[ci]);
                if (clip == null) { log.AppendLine("[CLIP] missing " + labels[ci]); continue; }
                log.AppendLine("[CLIP] " + labels[ci] + " name=" + clip.name + " len=" + clip.length.ToString("F2") + " humanMotion=" + clip.humanMotion + " isHuman=" + clip.isHumanMotion);

                float[] times = { 0f, 0.25f, 0.5f, 0.75f };
                for (int ti = 0; ti < times.Length; ti++)
                {
                    float t = clip.length * times[ti];
                    AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(go, clip, t);
                    AnimationMode.EndSampling();

                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                    var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                    log.AppendLine("  " + labels[ci] + "@" + times[ti].ToString("F2")
                        + " hips=" + (hips != null ? hips.position.ToString("F2") : "-")
                        + " lf=" + (lf != null ? lf.position.ToString("F2") : "-")
                        + " rf=" + (rf != null ? rf.position.ToString("F2") : "-")
                        + " lh=" + (lh != null ? lh.position.ToString("F2") : "-"));

                    Render(cam, rt, "probe_" + labels[ci] + "_" + ti);
                    AnimationMode.StopAnimationMode();
                }
            }

            // 静态正面近景，用于核对贴图
            cam.transform.position = new Vector3(0.1f, 1.05f, -1.55f);
            cam.transform.LookAt(new Vector3(0f, 1.0f, 0f));
            cam.fieldOfView = 30f;
            Render(cam, rt, "probe_tex_front");
            cam.transform.position = new Vector3(-1.1f, 1.05f, -1.05f);
            cam.transform.LookAt(new Vector3(0f, 1.0f, 0f));
            Render(cam, rt, "probe_tex_side");

            UnityEngine.Object.DestroyImmediate(go);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(fillGo);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            File.WriteAllText(Path.Combine(OutDir, "probe.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AnimProbe]\n" + log.ToString());
        }

        static AnimationClip LoadClip(string path, string keyword)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in all)
            {
                var c = a as AnimationClip;
                if (c == null || c.name.StartsWith("__preview__")) continue;
                if (c.name.ToLowerInvariant().Contains("forwards")) return c;
            }
            foreach (var a in all)
            {
                var c = a as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview__")) return c;
            }
            return null;
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
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            return has ? b : new Bounds(Vector3.zero, Vector3.zero);
        }
    }
}
