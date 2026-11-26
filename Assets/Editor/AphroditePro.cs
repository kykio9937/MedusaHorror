using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// Aphrodite 身体比例诊断与渲染：
    /// 1) Measure(): 量手-大腿间隙、脖子长度、肩宽、头高、披风渲染状态；
    /// 2) Bright(): 披风换成洋红材质渲染，确认披风几何是否真的在渲染；
    /// 3) Shot(): 常规三视图。
    /// </summary>
    public static class AphroditePro
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string PrefabPath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";
        const string Fbx = "Assets/AscendantOne/Aphrodite/Aphrodite.fbx";
        const string IdleClip = "Assets/Kevin Iglesias/Basic Motions/Animations/Idles/BasicMotions@Idle01.fbx";
        const string ProjectRoot = @"E:/UnityProjects/MedusaHorror";
        const string VariantPath = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string SourcePath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        public static void Measure()
        {
            var log = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;

            var anim = go.GetComponent<Animator>();
            if (anim.avatar == null)
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
                    if (a is Avatar av && av.isHuman) { anim.avatar = av; break; }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;

            var clip = LoadClip(IdleClip);
            log.AppendLine("[CLIP] idle=" + (clip != null ? clip.length.ToString("F2") : "missing"));

            // 披风/各 SMR 渲染状态
            log.AppendLine("\n=== SMR 状态 ===");
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = s.sharedMesh;
                var mat = s.sharedMaterial;
                log.AppendLine(string.Format("[SMR] {0} enabled={1} goActive={2} updOff={3} mesh={4} verts={5} mat={6} shader={7}",
                    s.transform.name, s.enabled, s.gameObject.activeSelf, s.updateWhenOffscreen,
                    m != null ? m.name : "null", m != null ? m.vertexCount : 0,
                    mat != null ? mat.name : "null", mat != null && mat.shader != null ? mat.shader.name : "null"));
            }

            // 静态测量（绑定姿态）
            log.AppendLine("\n=== 绑定姿态骨骼 ===");
            string[] bones = { "Bip001-Pelvis", "Bip001-Spine", "Bip001-Spine2", "Bip001-Neck", "Bip001-Neck1", "Bip001-Head",
                                "Bip001-L-Clavicle", "Bip001-R-Clavicle", "Bip001-L-UpperArm", "Bip001-R-UpperArm",
                                "Bip001-L-Hand", "Bip001-R-Hand", "Bip001-L-Thigh", "Bip001-R-Thigh", "Bip001-L-Foot", "Bip001-R-Foot" };
            foreach (var bn in bones)
            {
                var t = FindDeep(go.transform, bn);
                if (t == null) { log.AppendLine(bn + " = NULL"); continue; }
                log.AppendLine(string.Format("{0} world={1}", bn, t.position.ToString("F4")));
            }

            // 采样 idle 后逐 armIn 测量手-腿间隙
            log.AppendLine("\n=== 手-大腿间隙（idle 第 0 帧）===");
            float[] arms = { 0f, 5f, 10f, 15f, 20f };
            foreach (var ai in arms)
            {
                SampleIdle(go, anim, clip, 0f);
                AphroditePoseFixer.ApplyPose(anim, go.transform, ai, 10f, 0f);
                var handL = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                var handR = anim.GetBoneTransform(HumanBodyBones.RightHand);
                var thL = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                var thR = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                float surfL = ThighSurfaceX(go, handL.position, true);
                float surfR = ThighSurfaceX(go, handR.position, false);
                log.AppendLine(string.Format("armIn={0,4} L hand.x={1:F3} thighBone.x={2:F3} legSurface.x={3:F3} clearance={4:F3} | R hand.x={5:F3} legSurface.x={6:F3} clearance={7:F3}",
                    ai, handL.position.x, thL.position.x, surfL, handL.position.x - surfL,
                    handR.position.x, surfR, surfR - handR.position.x));
            }

            // 恢复默认并量整体比例
            SampleIdle(go, anim, clip, 0f);
            AphroditePoseFixer.ApplyPose(anim, go.transform, 20f, 10f, 0f);
            log.AppendLine("\n=== 比例（idle，armIn=20）===");
            float headTop = 0f, footY = 999f;
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                headTop = Mathf.Max(headTop, s.bounds.max.y);
                footY = Mathf.Min(footY, s.bounds.min.y);
            }
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            var neck = FindDeep(go.transform, "Bip001-Neck");
            var neck1 = FindDeep(go.transform, "Bip001-Neck1");
            var clavL = FindDeep(go.transform, "Bip001-L-Clavicle");
            var clavR = FindDeep(go.transform, "Bip001-R-Clavicle");
            var pelvis = FindDeep(go.transform, "Bip001-Pelvis");
            log.AppendLine(string.Format("总高={0:F3} 头顶={1:F3} 脚底={2:F3}", headTop - footY, headTop, footY));
            log.AppendLine(string.Format("Neck.y={0:F3} Neck1.y={1:F3} Head.y={2:F3} 颈长={3:F3}",
                neck.position.y, neck1.position.y, head.position.y, head.position.y - neck.position.y));
            log.AppendLine(string.Format("肩宽(clavicle 间距)={0:F3} 骨盆宽={1:F3}",
                Mathf.Abs(clavL.position.x - clavR.position.x), 0.182f));
            log.AppendLine(string.Format("头高(Head.y 到头顶)={0:F3} 头身比={1:F2}",
                headTop - head.position.y, (headTop - footY) / Mathf.Max(0.001f, headTop - head.position.y)));
            log.AppendLine(string.Format("腿长(pelvis.y-脚底)={0:F3} 占身高={1:P1}", pelvis.position.y - footY, (pelvis.position.y - footY) / (headTop - footY)));

            // 披风在不同拉伸系数下的包围盒
            log.AppendLine("\n=== 披风包围盒 vs 骨骼拉伸系数 ===");
            float cur = 2.15f;
            foreach (var f in new float[] { 1.0f, 1.2f, 1.5f, 1.8f, 2.15f })
            {
                float k = f / cur;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    var nm = t.name;
                    if (nm.Contains("Cloak_Bone_") && (nm.EndsWith("_02") || nm.EndsWith("_03")))
                        t.localPosition = new Vector3(t.localPosition.x * k, t.localPosition.y, t.localPosition.z);
                }
                cur = f;

                SampleIdle(go, anim, clip, 0f);
                AphroditePoseFixer.ApplyPose(anim, go.transform, 10f, 10f, 0f);
                var sb = new StringBuilder();
                foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!s.transform.name.Contains(".001")) continue;
                    var baked = new Mesh();
                    s.BakeMesh(baked);
                    var bb = baked.bounds;
                    sb.Append(string.Format(" [{0} 宽={1:F2} 高={2:F2} y={3:F2}..{4:F2} x={5:F2}..{6:F2}]",
                        s.transform.name, bb.size.x, bb.size.y, bb.min.y, bb.max.y, bb.min.x, bb.max.x));
                    Object.DestroyImmediate(baked);
                }
                log.AppendLine(string.Format("factor={0:F2}{1}", f, sb.ToString()));
            }

            Object.DestroyImmediate(go);
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_pro.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditePro]\n" + log.ToString());
        }

        /// <summary>把披风材质换成洋红自发光，渲染三视图，确认披风是否在渲染。</summary>
        public static void Bright()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLight();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(520, 780, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var b = GetBounds(go);
            go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            var anim = go.GetComponent<Animator>();
            if (anim.avatar == null)
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
                    if (a is Avatar av && av.isHuman) { anim.avatar = av; break; }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;

            var magenta = new Material(Shader.Find("Standard"));
            magenta.SetColor("_Color", new Color(1f, 0f, 1f, 1f));
            magenta.SetColor("_EmissionColor", new Color(0.6f, 0f, 0.6f, 1f));
            magenta.EnableKeyword("_EMISSION");
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.transform.name.Contains(".001")) s.sharedMaterial = magenta;
                s.updateWhenOffscreen = true;
            }

            var clip = LoadClip(IdleClip);
            int[] yaws = { 180, 270, 0 };
            string[] tags = { "front", "side", "back" };
            var all = go.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < yaws.Length; i++)
            {
                SampleIdle(go, anim, clip, 0f);
                AphroditePoseFixer.ApplyPose(anim, go.transform, 10f, 10f, 0f);
                anim.enabled = false;

                var pivot = new Vector3(b.center.x, 0.85f, b.center.z);
                float dist = 2.1f;
                float yaw = yaws[i] * Mathf.Deg2Rad;
                cam.transform.position = pivot + new Vector3(Mathf.Sin(yaw) * dist, 0.25f, Mathf.Cos(yaw) * dist);
                cam.transform.LookAt(pivot + new Vector3(0f, 0.08f, 0f));

                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(OutDir, "bright_" + tags[i] + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                anim.enabled = true;
            }
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_bright.txt"), "[BRIGHT] done", new UTF8Encoding(false));
            Debug.Log("[AphroditePro] bright done");
        }

        /// <summary>正常材质三视图（idle，当前 prefab 参数），输出 pro_idle_*.png。</summary>
        public static void Shot3()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLight();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.fieldOfView = 30f;
            var rt = new RenderTexture(520, 780, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 8;
            cam.targetTexture = rt;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath));
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var b = GetBounds(go);
            go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);

            var anim = go.GetComponent<Animator>();
            if (anim.avatar == null)
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(Fbx))
                    if (a is Avatar av && av.isHuman) { anim.avatar = av; break; }
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) s.updateWhenOffscreen = true;

            var clip = LoadClip(IdleClip);
            float arm = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "armIn", 10f);
            float leg = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "legIn", 10f);
            int[] yaws = { 180, 270, 0 };
            string[] tags = { "front", "side", "back" };
            for (int i = 0; i < yaws.Length; i++)
            {
                SampleIdle(go, anim, clip, 0f);
                AphroditePoseFixer.ApplyPose(anim, go.transform, arm, leg, 0f);
                ApplyBodyStatic(anim, ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "neckScale", 1f),
                    ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "legThick", 1f),
                    ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "calfThick", 1f));
                anim.enabled = false;

                var pivot = new Vector3(b.center.x, 0.92f, b.center.z);
                float dist = (b.size.y * 0.62f) / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float yaw = yaws[i] * Mathf.Deg2Rad;
                cam.transform.position = pivot + new Vector3(Mathf.Sin(yaw) * dist, 0.10f, Mathf.Cos(yaw) * dist);
                cam.transform.LookAt(pivot);

                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(OutDir, "pro_idle_" + tags[i] + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                anim.enabled = true;
            }
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_shot3.txt"), "[SHOT3] armIn=" + arm + " legIn=" + leg, new UTF8Encoding(false));
            Debug.Log("[AphroditePro] Shot3 done");
        }

        static void SampleIdle(GameObject go, Animator anim, AnimationClip clip, float t)
        {
            anim.enabled = true;
            if (clip == null) return;
            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, t);
            AnimationMode.EndSampling();
        }

        static readonly System.Collections.Generic.Dictionary<Transform, Vector3> _basePos = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        static readonly System.Collections.Generic.Dictionary<Transform, Vector3> _baseScale = new System.Collections.Generic.Dictionary<Transform, Vector3>();

        /// <summary>渲染用：脖子拉长 + 腿加粗（按首次见到的值作为基准，避免累积）。</summary>
        static void ApplyBodyStatic(Animator anim, float nk, float thick, float cthick)
        {
            if (anim == null) return;
            var neck1 = FindDeep(anim.transform, "Bip001-Neck1");
            var head = anim.GetBoneTransform(HumanBodyBones.Head);
            if (neck1 != null && head != null && Mathf.Abs(nk - 1f) > 0.001f)
            {
                if (!_basePos.ContainsKey(neck1)) _basePos[neck1] = neck1.localPosition;
                if (!_basePos.ContainsKey(head)) _basePos[head] = head.localPosition;
                neck1.localPosition = _basePos[neck1] * nk;
                head.localPosition = _basePos[head] * nk;
            }
            if (Mathf.Abs(thick - 1f) > 0.001f)
            {
                foreach (var b in new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg })
                {
                    var t = anim.GetBoneTransform(b);
                    if (t == null) continue;
                    if (!_baseScale.ContainsKey(t)) _baseScale[t] = t.localScale;
                    var bs = _baseScale[t];
                    t.localScale = new Vector3(bs.x, bs.y * thick, bs.z * thick);
                }
            }
            if (Mathf.Abs(cthick - 1f) > 0.001f)
            {
                foreach (var b in new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg })
                {
                    var t = anim.GetBoneTransform(b);
                    if (t == null) continue;
                    if (!_baseScale.ContainsKey(t)) _baseScale[t] = t.localScale;
                    var bs = _baseScale[t];
                    t.localScale = new Vector3(bs.x, bs.y * cthick, bs.z * cthick);
                }
            }
        }

        /// <summary>取 body 网格在 hand 高度附近、同一侧的腿部最外侧 x。</summary>
        static float ThighSurfaceX(GameObject go, Vector3 handPos, bool left)
        {
            float best = left ? 9999f : -9999f;
            foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.transform.name != "Mesh_0_0_0" && s.transform.name != "Mesh_1_0_1") continue;
                var baked = new Mesh();
                s.BakeMesh(baked);
                var mtx = s.transform.localToWorldMatrix;
                var vs = baked.vertices;
                for (int i = 0; i < vs.Length; i++)
                {
                    var w = mtx.MultiplyPoint3x4(vs[i]);
                    if (Mathf.Abs(w.y - handPos.y) > 0.06f) continue;
                    if (Mathf.Abs(w.z - handPos.z) > 0.10f) continue;
                    if (left) { if (w.x < 0f && w.x < best) best = w.x; }
                    else { if (w.x > 0f && w.x > best) best = w.x; }
                }
                Object.DestroyImmediate(baked);
            }
            return best;
        }

        static void SetupLight()
        {
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
        }

        static void AddLight(string name, Vector3 euler, float intensity, Color c)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = c;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        static Bounds GetBounds(GameObject go)
        {
            var bs = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (first) { bs = r.bounds; first = false; }
                else bs.Encapsulate(r.bounds);
            }
            return bs;
        }

        static AnimationClip LoadClip(string path)
        {
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && !c.name.StartsWith("__preview")) return c;
            return null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        // ---------------------------------------------------------------- 写入

        /// <summary>按 capefix.json 的 factor 设置披风骨骼拉伸（幂等，用 capeapplied.txt 记录当前值）。</summary>
        public static void SetCape()
        {
            float target = ReadJsonFloat(Path.Combine(ProjectRoot, "capefix.json"), "factor", 1f);
            string appliedFile = Path.Combine(ProjectRoot, "capeapplied.txt");
            float applied = 1f;
            if (File.Exists(appliedFile))
                float.TryParse(File.ReadAllText(appliedFile).Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out applied);
            if (applied < 0.01f) applied = 1f;
            float delta = target / applied;

            var log = new StringBuilder();
            if (Mathf.Abs(delta - 1f) < 0.001f)
            {
                log.AppendLine("[CAPE] already " + target);
            }
            else
            {
                var root = PrefabUtility.LoadPrefabContents(SourcePath);
                int n = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    var nm = t.name;
                    if (!nm.Contains("Cloak_Bone_") || !(nm.EndsWith("_02") || nm.EndsWith("_03"))) continue;
                    var p = t.localPosition;
                    t.localPosition = new Vector3(p.x * delta, p.y, p.z);
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(root, SourcePath);
                PrefabUtility.UnloadPrefabContents(root);
                File.WriteAllText(appliedFile, target.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), new UTF8Encoding(false));
                log.AppendLine("[CAPE] bones=" + n + " delta=" + delta.ToString("F3") + " " + applied.ToString("F3") + " -> " + target.ToString("F3"));
            }

            var vroot = PrefabUtility.LoadPrefabContents(VariantPath);
            foreach (var fx in vroot.GetComponentsInChildren<AphroditePoseFixer>(true)) fx.capeFactor = target;
            PrefabUtility.SaveAsPrefabAsset(vroot, VariantPath);
            PrefabUtility.UnloadPrefabContents(vroot);
            log.AppendLine("[CAPE] variant capeFactor=" + target);

            File.WriteAllText(Path.Combine(OutDir, "aphrodite_cape.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditePro] SetCape\n" + log);
        }

        /// <summary>按 posefix.json 把 armIn/legIn/toeIn 写进 variant prefab 与 5 个场景。</summary>
        public static void SetPose()
        {
            float arm = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "armIn", 10f);
            float leg = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "legIn", 10f);
            float toe = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "toeIn", 0f);
            float nk = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "neckScale", 1f);
            float thick = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "legThick", 1f);
            float cthick = ReadJsonFloat(Path.Combine(ProjectRoot, "posefix.json"), "calfThick", 1f);
            var log = new StringBuilder();
            log.AppendLine("[POSE] armIn=" + arm + " legIn=" + leg + " toeIn=" + toe + " neckScale=" + nk + " legThick=" + thick + " calfThick=" + cthick);

            int vn = 0;
            var vroot = PrefabUtility.LoadPrefabContents(VariantPath);
            foreach (var fx in vroot.GetComponentsInChildren<AphroditePoseFixer>(true))
            { fx.armIn = arm; fx.legIn = leg; fx.toeIn = toe; fx.neckScale = nk; fx.legThick = thick; fx.calfThick = cthick; vn++; }
            PrefabUtility.SaveAsPrefabAsset(vroot, VariantPath);
            PrefabUtility.UnloadPrefabContents(vroot);
            log.AppendLine("[POSE] variant fixers=" + vn);

            foreach (var sp in Scenes)
            {
                if (!File.Exists(sp)) { log.AppendLine("[POSE] missing " + sp); continue; }
                var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                int n = 0;
                foreach (var rt in sc.GetRootGameObjects())
                    foreach (var fx in rt.GetComponentsInChildren<AphroditePoseFixer>(true))
                    { fx.armIn = arm; fx.legIn = leg; fx.toeIn = toe; fx.neckScale = nk; fx.legThick = thick; fx.calfThick = cthick; n++; }
                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
                log.AppendLine("[POSE] " + Path.GetFileName(sp) + " fixers=" + n);
            }

            File.WriteAllText(Path.Combine(OutDir, "aphrodite_setpose.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditePro] SetPose\n" + log);
        }

        public static void PoseInfo()
        {
            var log = new StringBuilder();
            foreach (var sp in Scenes)
            {
                if (!File.Exists(sp)) continue;
                var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                foreach (var rt in sc.GetRootGameObjects())
                    foreach (var fx in rt.GetComponentsInChildren<AphroditePoseFixer>(true))
                        log.AppendLine("[FX] " + Path.GetFileName(sp) + " armIn=" + fx.armIn + " legIn=" + fx.legIn + " cape=" + fx.capeFactor);
            }
            var vroot = PrefabUtility.LoadPrefabContents(VariantPath);
            foreach (var fx in vroot.GetComponentsInChildren<AphroditePoseFixer>(true))
                log.AppendLine("[FX] variant armIn=" + fx.armIn + " legIn=" + fx.legIn + " cape=" + fx.capeFactor);
            PrefabUtility.UnloadPrefabContents(vroot);
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_fxinfo.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditePro] PoseInfo\n" + log);
        }

        static float ReadJsonFloat(string path, string key, float def)
        {
            if (!File.Exists(path)) return def;
            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim().TrimEnd(',').Trim();
                    if (!line.StartsWith("\"" + key + "\"")) continue;
                    int c = line.IndexOf(':');
                    if (c < 0) continue;
                    var v = line.Substring(c + 1).Trim().Trim('"');
                    float f;
                    if (float.TryParse(v, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out f)) return f;
                }
            }
            catch { }
            return def;
        }
    }
}
