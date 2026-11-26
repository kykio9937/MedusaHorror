using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// Aphrodite 外观调整：
    /// 1) 在源 prefab 上拉长披风骨骼（幂等，按当前长度判断）；
    /// 2) 给玩家 variant / 场景挂 AphroditePoseFixer；
    /// 3) Shot() 渲染正面/侧面/背面验证姿态。
    /// </summary>
    public static class AphroditeTune
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string VariantPath = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string SourcePath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";
        const string Kevin = "Assets/Kevin Iglesias/Basic Motions/Animations";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        const float CapeFactor = 2.15f;

        public static void Apply()
        {
            var log = new StringBuilder();
            log.AppendLine(StretchSourceCape());
            log.AppendLine(AttachFixers());
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_tune.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditeTune]\n" + log.ToString());
        }

        /// <summary>
        /// 在源 prefab 上拉长披风骨骼。按 variant 上 fixer.capeFactor 记录的"已应用系数"计算增量，
        /// 因此可以反复调用把系数从 1.55 调到 2.15 而不会指数放大。
        /// </summary>
        static string StretchSourceCape()
        {
            float applied = ReadAppliedFactor();
            float delta = applied > 0.01f ? CapeFactor / applied : CapeFactor;
            if (Mathf.Abs(delta - 1f) < 0.001f) return "[CAPE] already at factor " + CapeFactor;

            var root = PrefabUtility.LoadPrefabContents(SourcePath);
            if (root == null) return "[CAPE] 源 prefab 不存在";
            int n = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var nm = t.name;
                if (!nm.Contains("Cloak_Bone_")) continue;
                if (!(nm.EndsWith("_02") || nm.EndsWith("_03"))) continue;
                var p = t.localPosition;
                t.localPosition = new Vector3(p.x * delta, p.y, p.z);
                n++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, SourcePath);
            PrefabUtility.UnloadPrefabContents(root);
            return "[CAPE] bones=" + n + " delta=" + delta.ToString("F3") + " applied=" + applied.ToString("F3") + " -> " + CapeFactor;
        }

        /// <summary>把 .001 披风网格恢复为 fbx 原始网格（修复引用断裂导致的网格不显示）。</summary>
        public static void RestoreCape()
        {
            var root = PrefabUtility.LoadPrefabContents(SourcePath);
            if (root == null) { Debug.Log("[RestoreCape] prefab 不存在"); return; }
            var fbxAll = AssetDatabase.LoadAllAssetsAtPath("Assets/AscendantOne/Aphrodite/Aphrodite.fbx");
            var log = new StringBuilder();
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.name.EndsWith(".001")) continue;
                foreach (var a in fbxAll)
                    if (a is Mesh mm && mm.name == smr.name)
                    {
                        smr.sharedMesh = mm;
                        log.AppendLine("[RESTORE] " + smr.name + " -> fbx 原始 (" + mm.vertexCount + " verts)");
                        break;
                    }
            }
            PrefabUtility.SaveAsPrefabAsset(root, SourcePath);
            PrefabUtility.UnloadPrefabContents(root);
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_restore.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[RestoreCape]\n" + log.ToString());
        }

        /// <summary>
        /// 披风主体（Mesh_*.001）是刚性网格、不跟 Cloak 骨骼变形，只能直接拉长顶点：
        /// 腰部以下的顶点按系数向下延伸，形成更长的下摆。
        /// </summary>
        static string StretchCapeMeshes()
        {
            var root = PrefabUtility.LoadPrefabContents(SourcePath);
            if (root == null) return "[MESH] 源 prefab 不存在";
            const string dir = "Assets/AscendantOne/Aphrodite/Meshes";
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/AscendantOne/Aphrodite", "Meshes");

            const float yTop = 1.30f;
            const float k = 1.45f;
            int done = 0;
            var log = new StringBuilder();
            var fbxAll = AssetDatabase.LoadAllAssetsAtPath("Assets/AscendantOne/Aphrodite/Aphrodite.fbx");
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.name.EndsWith(".001")) continue;

                // 始终以 fbx 里的原始网格为准，避免在已拉伸的网格上二次拉伸
                Mesh src = null;
                foreach (var a in fbxAll)
                    if (a is Mesh mm && mm.name == smr.name) { src = mm; break; }
                if (src == null)
                {
                    src = smr.sharedMesh;
                    log.AppendLine("[MESH] " + smr.name + " fbx 原始网格未找到，回退到当前网格");
                }
                if (src == null) continue;
                log.AppendLine("[MESH] " + smr.name + " src=" + src.name + " verts=" + src.vertexCount + " bounds=" + src.bounds.size.ToString("F3"));

                var m = Object.Instantiate(src);
                m.name = smr.name + "_Long";
                var vs = m.vertices;
                for (int i = 0; i < vs.Length; i++)
                    if (vs[i].y < yTop) vs[i].y = yTop - (yTop - vs[i].y) * k;
                m.vertices = vs;
                m.RecalculateBounds();

                string path = dir + "/" + m.name + ".asset";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                smr.sharedMesh = m;
                done++;
                log.AppendLine("[MESH] -> " + path);
            }
            PrefabUtility.SaveAsPrefabAsset(root, SourcePath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            return "[MESH] stretched=" + done + "\n" + log.ToString().TrimEnd();
        }

        static float ReadAppliedFactor()        {
            var root = PrefabUtility.LoadPrefabContents(VariantPath);
            float f = 0f;
            if (root != null)
            {
                var anim = root.GetComponentInChildren<Animator>(true);
                var fx = anim != null ? anim.GetComponent<MedusaHorror.Player.AphroditePoseFixer>() : null;
                if (fx != null) f = fx.capeFactor;
                PrefabUtility.UnloadPrefabContents(root);
            }
            return f;
        }

        static string AttachFixers()
        {
            var log = new StringBuilder();
            var root = PrefabUtility.LoadPrefabContents(VariantPath);
            if (root != null)
            {
                var anim = root.GetComponentInChildren<Animator>(true);
                if (anim != null)
                {
                    var fx = anim.GetComponent<MedusaHorror.Player.AphroditePoseFixer>();
                    if (fx == null) { fx = anim.gameObject.AddComponent<MedusaHorror.Player.AphroditePoseFixer>(); log.AppendLine("[FIXER] added on variant"); }
                    else log.AppendLine("[FIXER] exists on variant");
                    fx.capeFactor = CapeFactor;
                    fx.armIn = 20f;
                    fx.legIn = 10f;
                }
                PrefabUtility.SaveAsPrefabAsset(root, VariantPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var sp in Scenes)
            {
                if (!File.Exists(sp)) continue;
                var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                int added = 0, exists = 0;
                foreach (var r in sc.GetRootGameObjects())
                {
                    var tpc = r.GetComponentInChildren<MedusaHorror.Player.ThirdPersonController>(true);
                    if (tpc == null) continue;
                    var anim = tpc.GetComponentInChildren<Animator>(true);
                    if (anim == null) continue;
                    var fx = anim.GetComponent<MedusaHorror.Player.AphroditePoseFixer>();
                    if (fx == null) { fx = anim.gameObject.AddComponent<MedusaHorror.Player.AphroditePoseFixer>(); added++; }
                    else exists++;
                    fx.capeFactor = CapeFactor;
                    fx.armIn = 20f;
                    fx.legIn = 10f;
                }
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
                log.AppendLine("[SCENE] " + sc.name + " added=" + added + " exists=" + exists);
            }
            return log.ToString();
        }

        // ---------------- 渲染验证 ----------------

        public static void Shot()
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
            var rt = new RenderTexture(560, 840, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath));
            var b0 = GetBounds(go);
            go.transform.position -= new Vector3(b0.center.x, b0.min.y, b0.center.z);

            var anim = go.GetComponentInChildren<Animator>();
            if (anim.avatar == null)
            {
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/AscendantOne/Aphrodite/Aphrodite.fbx"))
                    if (a is Avatar av && av.isHuman) { anim.avatar = av; break; }
            }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;

            var all = go.GetComponentsInChildren<Transform>(true);

            var idleClip = LoadClip(Kevin + "/Idles/BasicMotions@Idle01.fbx");
            var walkClip = LoadClip(Kevin + "/Movement/BasicMotions@Walk01.fbx");

            float armIn = 24f, legIn = 10f, toeIn = 0f;
            var fx = anim.GetComponent<MedusaHorror.Player.AphroditePoseFixer>();
            if (fx != null) { armIn = fx.armIn; legIn = fx.legIn; toeIn = fx.toeIn; }
            log.AppendLine("[POSE] armIn=" + armIn + " legIn=" + legIn);

            int[] yaws = { 180, 270, 0 };
            string[] yname = { "front", "side", "back" };

            RenderOne(go, anim, all, idleClip, 0f, yaws, yname, "idle", cam, rt, log, armIn, legIn, toeIn);
            if (walkClip != null)
                RenderOne(go, anim, all, walkClip, walkClip.length * 0.25f, yaws, yname, "walk", cam, rt, log, armIn, legIn, toeIn);

            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = false;
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(probeGo);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            foreach (var l in Object.FindObjectsOfType<Light>()) Object.DestroyImmediate(l.gameObject);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            File.WriteAllText(Path.Combine(OutDir, "aphrodite_shot.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditeTune.Shot]\n" + log.ToString());
        }

        static void RenderOne(GameObject go, Animator anim, Transform[] all, AnimationClip clip, float t,
            int[] yaws, string[] yname, string label, Camera cam, RenderTexture rt, StringBuilder log,
            float armIn, float legIn, float toeIn)
        {
            if (clip == null) { log.AppendLine("[SHOT] missing " + label); return; }

            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, t);
            AnimationMode.EndSampling();

            var snapP = new Vector3[all.Length];
            var snapR = new Quaternion[all.Length];
            for (int k = 0; k < all.Length; k++) { snapP[k] = all[k].localPosition; snapR[k] = all[k].localRotation; }
            AnimationMode.StopAnimationMode();
            anim.enabled = false;
            for (int k = 0; k < all.Length; k++) { all[k].localPosition = snapP[k]; all[k].localRotation = snapR[k]; }

            MedusaHorror.Player.AphroditePoseFixer.ApplyPose(anim, go.transform, armIn, legIn, toeIn);

            float h = GetBounds(go).size.y;
            var lh = anim.GetBoneTransform(HumanBodyBones.LeftHand);
            var rh = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var lt = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            var rThigh = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            if (lh != null && lt != null && hips != null)
                log.AppendLine("[ARM] L hand dx=" + (lh.position.x - hips.position.x).ToString("F3")
                    + " thigh dx=" + (lt.position.x - hips.position.x).ToString("F3")
                    + " | R hand dx=" + (rh.position.x - hips.position.x).ToString("F3")
                    + " thigh dx=" + (rThigh.position.x - hips.position.x).ToString("F3"));
            log.AppendLine("[BOUNDS] " + label + " total h=" + h.ToString("F3"));
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bb = s.bounds;
                log.AppendLine("   " + s.name + " y=[" + bb.min.y.ToString("F3") + "," + bb.max.y.ToString("F3")
                    + "] x=[" + bb.min.x.ToString("F3") + "," + bb.max.x.ToString("F3") + "]");
            }
            Vector3 target = new Vector3(0f, h * 0.52f, 0f);
            for (int yi = 0; yi < yaws.Length; yi++)
            {
                var rot = Quaternion.Euler(0f, yaws[yi], 0f);
                cam.transform.position = target + rot * new Vector3(0f, 0f, -h * 1.7f);
                cam.transform.LookAt(target);
                Render(cam, rt, "tune_" + label + "_" + yname[yi]);
            }
            log.AppendLine("[SHOT] " + label + " rendered");
            anim.enabled = true;
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
            Object.DestroyImmediate(tex);
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
