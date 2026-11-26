using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>导入运星一号 Aphrodite：贴图/骨骼/材质/prefab/预览图。</summary>
    public static class AphroditeSetup
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string Root = "Assets/AscendantOne/Aphrodite";
        const string FbxPath = Root + "/Aphrodite.fbx";
        const string CirclePath = Root + "/Circle.fbx";
        const string TexDir = Root + "/Textures";
        const string MatDir = Root + "/Materials";
        const string PrefabPath = Root + "/Aphrodite_Player.prefab";

        public static void Run()
        {
            var log = new StringBuilder();

            Directory.CreateDirectory(MatDir);

            // 1) 贴图导入设置：1024、法线图正确类型
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                ti.maxTextureSize = 1024;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                bool isNormal = p.ToLowerInvariant().Contains("_nrm");
                ti.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !isNormal;
                ti.alphaIsTransparency = !isNormal && p.ToLowerInvariant().Contains("opacity");
                ti.SaveAndReimport();
            }
            log.AppendLine("[TEX] done");

            // 2) FBX 导入设置：Humanoid + 不导入材质
            var mi = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
            if (mi == null) { log.AppendLine("FBX MISSING " + FbxPath); Debug.Log(log.ToString()); return; }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = false;
            mi.importBlendShapes = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            mi.materialSearch = ModelImporterMaterialSearch.Everywhere;
            mi.SaveAndReimport();
            log.AppendLine("[FBX] imported");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (model == null) { log.AppendLine("LOAD FAIL"); Debug.Log(log.ToString()); return; }

            // 3) 结构报告
            var animator = model.GetComponent<Animator>();
            var avatar = animator != null ? animator.avatar : null;
            log.AppendLine("[AVATAR] " + (avatar == null ? "NULL" : ("valid=" + avatar.isValid + " human=" + avatar.isHuman)));
            var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var meshes = model.GetComponentsInChildren<MeshRenderer>(true);
            log.AppendLine("[MESH] skinned=" + skinned.Length + " static=" + meshes.Length);
            foreach (var s in skinned) log.AppendLine("  skin: " + s.name + " bones=" + (s.bones != null ? s.bones.Length : 0) + " rootBone=" + (s.rootBone != null ? s.rootBone.name : "null"));
            foreach (var m in meshes) log.AppendLine("  mesh: " + m.name);
            var smr = skinned.Length > 0 ? skinned[0] : null;
            log.AppendLine("[BONES] " + (smr != null && smr.bones != null ? smr.bones.Length : 0));

            // 4) 材质
            var body = MakeMat("Aphrodite_Body", "Aphrodite_Basebody_Alb", "Aphrodite_Basebody_Nrm", 0.25f, 0.42f, false);
            var face = MakeMat("Aphrodite_Face", "Aphrodite_Face_Alb", "Aphrodite_Face_Nrm", 0.05f, 0.55f, false);
            var hair = MakeMat("Aphrodite_Hair", "Aphrodite_Hair_Alb", null, 0.1f, 0.6f, false, true);
            var cape = MakeMat("Aphrodite_Cape", "Aphrodite_Cape_Alb", "Aphrodite_Cape_Nrm", 0.1f, 0.35f, false);
            var eye = MakeMat("Aphrodite_Eye", "Aphrodite_Eye_Alb", "Aphrodite_Eye_Nrm", 0.0f, 0.85f, false);
            var circle = MakeMat("Aphrodite_Circle", "Aphrodite_Circle_Alb", null, 0.3f, 0.7f, true);
            log.AppendLine("[MAT] created");

            // 5) 打成 prefab
            var inst = (GameObject)UnityEngine.Object.Instantiate(model);
            inst.name = "Aphrodite_Model";
            int painted = 0;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                string slot = r.sharedMaterial != null ? r.sharedMaterial.name : "null";
                log.AppendLine("  renderer: " + r.name + " slot=" + slot
                    + " c=" + r.bounds.center.ToString("F2") + " s=" + r.bounds.size.ToString("F2"));
                Material m = body;
                switch (r.name)
                {
                    case "Mesh_0_0_0.001": m = cape; break;
                    case "Mesh_0_0_0.002": m = hair; break;
                    case "Mesh_1_0_1.001": m = cape; break;
                    case "Mesh_2_0_2": m = face; break;
                    case "Mesh_3_0_3":
                    case "Mesh_4_0_4": m = eye; break;
                    case "Mesh_5_0_5": m = hair; break;
                    case "Mesh_6_0_6": m = face; break;
                    default: m = body; break;
                }
                var arr = new Material[r.sharedMaterials.Length <= 0 ? 1 : r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
                painted++;
            }
            log.AppendLine("[PAINT] renderers=" + painted);

            PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            UnityEngine.Object.DestroyImmediate(inst);
            AssetDatabase.SaveAssets();
            log.AppendLine("[PREFAB] " + PrefabPath);

            // 6) 渲染预览
            string preview = RenderPreview(PrefabPath, "ap_", out string dims);
            log.AppendLine("[PREVIEW] " + preview + " " + dims);

            File.WriteAllText(Path.Combine(OutDir, "ap_report.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditeSetup]\n" + log.ToString());
        }

        public static void DumpAvatar()
        {
            var sb = new StringBuilder();
            var all = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            Avatar av = null;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i] as Avatar;
                if (c != null && c.isHuman) { av = c; break; }
            }
            if (av == null) { sb.AppendLine("NO AVATAR"); File.WriteAllText(Path.Combine(OutDir, "avatar_dump.txt"), sb.ToString()); return; }
            var hd = av.humanDescription;
            sb.AppendLine("isValid=" + av.isValid + " isHuman=" + av.isHuman);
            sb.AppendLine("human bones=" + (hd.human != null ? hd.human.Length : 0));
            var watch = new HashSet<string> { "Hips", "Spine", "Chest", "Neck", "Head", "LeftUpperLeg", "RightUpperLeg", "LeftLowerLeg", "RightLowerLeg", "LeftFoot", "RightFoot", "LeftUpperArm", "RightUpperArm", "LeftHand", "RightHand" };
            for (int i = 0; i < hd.human.Length; i++)
            {
                var h = hd.human[i];
                if (watch.Contains(h.humanName))
                    sb.AppendLine("  " + h.humanName + " -> " + h.boneName);
            }
            File.WriteAllText(Path.Combine(OutDir, "avatar_dump.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[DumpAvatar]\n" + sb.ToString());
        }

        public static void DebugParts()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            lightGo.transform.rotation = Quaternion.Euler(28f, 150f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.6f);
            RenderSettings.fog = false;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.15f, 0.16f, 0.18f, 1f);
            cam.fieldOfView = 20f;
            cam.nearClipPlane = 0.02f;
            var rt = new RenderTexture(600, 600, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var all = go.GetComponentsInChildren<Renderer>(true);

            string[] targets = { "Mesh_2_0_2", "Mesh_3_0_3", "Mesh_4_0_4", "Mesh_5_0_5", "Mesh_6_0_6", "Mesh_0_0_0.002" };

            foreach (var t in targets)
            {
                foreach (var r in all) r.enabled = (r.name == t);
                cam.transform.position = new Vector3(0.14f, 1.68f, -0.42f);
                cam.transform.LookAt(new Vector3(0f, 1.66f, 0.02f));
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(OutDir, "part_" + t + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }

            foreach (var r in all) r.enabled = true;
            UnityEngine.Object.DestroyImmediate(go);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Debug.Log("[DebugParts] done");
        }

        static Material MakeMat(string name, string alb, string nrm, float metallic, float smooth, bool transparent, bool alphaClip = false)
        {
            string path = MatDir + "/" + name + ".mat";
            var shader = Shader.Find("Standard");
            var m = new Material(shader);
            var tAlb = string.IsNullOrEmpty(alb) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + alb + ".tga");
            if (tAlb == null && !string.IsNullOrEmpty(alb)) tAlb = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + alb + ".jpg");
            var tNrm = string.IsNullOrEmpty(nrm) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + nrm + ".tga");
            if (tAlb != null) m.SetTexture("_MainTex", tAlb);
            if (tNrm != null) { m.SetTexture("_BumpMap", tNrm); m.EnableKeyword("_NORMALMAP"); }
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smooth);
            if (alphaClip)
            {
                m.SetFloat("_Mode", 1f);
                m.SetFloat("_Cutoff", 0.35f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                m.SetInt("_ZWrite", 1);
                m.EnableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 2450;
            }
            if (transparent)
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 3000;
            }
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        static string RenderPreview(string prefabPath, string prefix, out string dims)
        {
            dims = "";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Sun");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.97f, 0.93f);
            lightGo.transform.rotation = Quaternion.Euler(30f, 145f, 0f);

            var fillGo = new GameObject("Fill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.6f;
            fill.color = new Color(0.72f, 0.8f, 1f);
            fillGo.transform.rotation = Quaternion.Euler(15f, -50f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);
            RenderSettings.fog = false;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.18f, 0.2f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 200f;
            var rt = new RenderTexture(720, 1000, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var b = GetBounds(go);
            go.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = GetBounds(go);
            dims = "h=" + b.size.y.ToString("F2") + " w=" + b.size.x.ToString("F2") + " d=" + b.size.z.ToString("F2");

            float h = Mathf.Max(b.size.y, 0.5f);
            float dist = h * 1.75f;
            cam.transform.position = new Vector3(dist * 0.22f, h * 0.55f, -dist);
            cam.transform.LookAt(new Vector3(0f, h * 0.5f, 0f));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(dist, 1f, dist);
            var fm = new Material(Shader.Find("Standard"));
            fm.color = new Color(0.2f, 0.21f, 0.23f);
            floor.GetComponent<Renderer>().sharedMaterial = fm;

            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string outPath = Path.Combine(OutDir, prefix + "preview.png");
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            // 头部特写，用于核对脸/头发/眼睛贴图
            cam.transform.position = new Vector3(h * 0.16f, h * 0.92f, -h * 0.45f);
            cam.transform.LookAt(new Vector3(0f, h * 0.91f, 0f));
            cam.fieldOfView = 22f;
            cam.Render();
            RenderTexture.active = rt;
            var tex2 = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex2.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex2.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(OutDir, prefix + "head.png"), tex2.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex2);

            UnityEngine.Object.DestroyImmediate(floor);
            UnityEngine.Object.DestroyImmediate(go);
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(lightGo);
            UnityEngine.Object.DestroyImmediate(fillGo);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return outPath;
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
