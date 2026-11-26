using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MedusaHorror.EditorTools
{
    /// <summary>渲染 Walk/Run 的多个相位，用于判断姿态是否像真人走路。</summary>
    public static class WalkShot
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string PrefabPath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";
        const string Fbx = "Assets/AscendantOne/Aphrodite/Aphrodite.fbx";
        const string Kevin = "Assets/Kevin Iglesias/Basic Motions/Animations";

        public static void Shoot()
        {
            var log = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_AtmosphereThickness", 0.55f);
            sky.SetColor("_SkyTint", new Color(0.72f, 0.78f, 0.9f));
            sky.SetColor("_GroundColor", new Color(0.45f, 0.45f, 0.47f));
            sky.SetFloat("_Exposure", 1.6f);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.35f;

            AddLight("Key", new Vector3(35f, 155f, 0f), 1.7f, new Color(1f, 0.97f, 0.92f));
            AddLight("Fill", new Vector3(10f, -50f, 0f), 0.9f, new Color(0.78f, 0.85f, 1f));
            AddLight("Rim", new Vector3(-18f, 20f, 0f), 1.2f, new Color(0.9f, 0.95f, 1f));

            var probeGo = new GameObject("RP");
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(24f, 24f, 24f);
            probe.resolution = 256;
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.RenderProbe();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(520, 780, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // 面向 -Z，即面向相机
            var b = GetBounds(go);
            go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            var anim = go.GetComponent<Animator>();
            if (anim.avatar == null)
            {
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
                    if (a is Avatar av && av.isHuman) { anim.avatar = av; break; }
            }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;

            var all = go.GetComponentsInChildren<Transform>(true);
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in smrs) s.updateWhenOffscreen = true;

            string[] clipPaths = { Kevin + "/Movement/BasicMotions@Walk01.fbx", Kevin + "/Movement/BasicMotions@Run01.fbx" };
            string[] labels = { "walk", "run" };
            int[] yaws = { 165, 270 }; // 前侧 3/4 视角 + 正侧面，最能看出迈步

            for (int ci = 0; ci < clipPaths.Length; ci++)
            {
                var clip = LoadClip(clipPaths[ci]);
                if (clip == null) { log.AppendLine("[CLIP] missing " + labels[ci]); continue; }
                log.AppendLine("[CLIP] " + labels[ci] + " len=" + clip.length.ToString("F2"));

                for (int yi = 0; yi < yaws.Length; yi++)
                {
                    for (int pi = 0; pi < 4; pi++)
                    {
                        float t = clip.length * (pi * 0.25f);

                        AnimationMode.StartAnimationMode();
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, clip, t);
                        AnimationMode.EndSampling();

                        var snapP = new Vector3[all.Length];
                        var snapR = new Quaternion[all.Length];
                        for (int k = 0; k < all.Length; k++)
                        {
                            snapP[k] = all[k].localPosition;
                            snapR[k] = all[k].localRotation;
                        }
                        AnimationMode.StopAnimationMode();
                        anim.enabled = false; // 否则 Animator 会在渲染前把骨骼写回原状
                        for (int k = 0; k < all.Length; k++)
                        {
                            all[k].localPosition = snapP[k];
                            all[k].localRotation = snapR[k];
                        }

                        var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                        var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                        log.AppendLine("  " + labels[ci] + " p" + pi + " hipsY=" + (hips ? hips.position.y.ToString("F3") : "-")
                            + " lf=" + (lf ? lf.position.ToString("F2") : "-"));

                        float h = GetBounds(go).size.y;
                        Vector3 target = new Vector3(0f, h * 0.52f, 0f);
                        var rot = Quaternion.Euler(0f, yaws[yi], 0f);
                        cam.transform.position = target + rot * new Vector3(0f, 0f, -h * 1.7f);
                        cam.transform.LookAt(target);

                        Render(cam, rt, "anim_" + labels[ci] + "_" + yaws[yi] + "_" + pi);
                        anim.enabled = true;
                    }
                }
            }

            foreach (var s in smrs) s.updateWhenOffscreen = false;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(probeGo);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            foreach (var l in GameObject.FindObjectsOfType<Light>()) UnityEngine.Object.DestroyImmediate(l.gameObject);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            File.WriteAllText(Path.Combine(OutDir, "walkshot.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[WalkShot]\n" + log.ToString());
        }

        static AnimationClip LoadClip(string path)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && !c.name.StartsWith("__preview__") && c.name.ToLowerInvariant().Contains("forwards")) return c;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
            return null;
        }

        static void AddLight(string name, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.5f;
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
            bool has = false; Bounds b = new Bounds();
            foreach (var r in rs)
            {
                if (!r.enabled || !(r is SkinnedMeshRenderer)) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? b : new Bounds(Vector3.zero, Vector3.zero);
        }
    }
}
