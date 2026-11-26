using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class AnimShot
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Medusa/AnimShot")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            sb.Length = 0;
            Shot("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", null, "aphrodite_rest");
            Shot("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", "Assets/Suriyun/Animations/Anim@Walk.fbx", "aphrodite_walk");
            Shot("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", "Assets/Suriyun/Animations/Anim@Atk1.fbx", "aphrodite_atk1");
            Shot("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", null, "kazuko_rest");
            Shot("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Suriyun/Animations/Anim@Walk.fbx", "kazuko_walk");
            Shot("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Suriyun/Animations/Anim@Atk1.fbx", "kazuko_atk1");
            File.WriteAllText(Path.Combine(Out, "animshot.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[AnimShot] done " + sb.Length);
        }

        static void Shot(string modelPath, string clipPath, string tag)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.1f;

            var key = new GameObject("Key");
            var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional; kl.intensity = 1.5f; kl.shadows = LightShadows.Soft;
            key.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
            var fill = new GameObject("Fill");
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 0.7f; fl.color = new Color(0.75f, 0.82f, 1f);
            fill.transform.rotation = Quaternion.Euler(10f, 140f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            var gm = new Material(Shader.Find("Standard")); gm.color = new Color(0.30f, 0.31f, 0.33f);
            ground.GetComponent<Renderer>().sharedMaterial = gm;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;

            Bounds b = BoundsOf(go);
            go.transform.position = Vector3.zero;
            sb.AppendLine("[" + tag + "] height=" + b.size.y.ToString("F3") + " boundsMinY=" + b.min.y.ToString("F3"));

            float h = Mathf.Max(1.2f, b.size.y);
            float cy = h * 0.52f;
            float dist = h * 2.0f;
            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(560, 720, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            Vector3 target = new Vector3(0f, cy, 0f);

            if (clipPath == null)
            {
                // rest pose
                Place(cam, target, dist, 35f);
                Render(cam, rt, tag + "_rest");
                ReportPose(animator, tag + " rest");
            }
            else
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (clip == null) { sb.AppendLine("  clip missing " + clipPath); return; }
                sb.AppendLine("  clip=" + clip.name + " len=" + clip.length.ToString("F3"));
                AnimationMode.StartAnimationMode();
                try
                {
                    int phases = 4;
                    for (int i = 0; i < phases; i++)
                    {
                        float t = clip.length * i / (float)phases;
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, clip, t);
                        AnimationMode.EndSampling();
                        Place(cam, target, dist, 35f);
                        Render(cam, rt, tag + "_" + i);
                        ReportPose(animator, tag + " t=" + t.ToString("F2"));
                    }
                }
                catch (Exception e) { sb.AppendLine("  EXC " + e.Message); }
                finally { AnimationMode.StopAnimationMode(); }
            }

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(key);
            UnityEngine.Object.DestroyImmediate(fill);
        }

        static void ReportPose(Animator a, string label)
        {
            if (a == null || !a.isHuman) { sb.AppendLine("  " + label + " not human"); return; }
            var lf = a.GetBoneTransform(HumanBodyBones.LeftFoot);
            var rf = a.GetBoneTransform(HumanBodyBones.RightFoot);
            var hips = a.GetBoneTransform(HumanBodyBones.Hips);
            var head = a.GetBoneTransform(HumanBodyBones.Head);
            var lu = a.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var ru = a.GetBoneTransform(HumanBodyBones.RightUpperArm);
            if (lf == null || rf == null || hips == null) { sb.AppendLine("  " + label + " missing bones"); return; }
            float spread = Vector3.Distance(new Vector3(lf.position.x, 0f, lf.position.z), new Vector3(rf.position.x, 0f, rf.position.z));
            float shoulder = (lu != null && ru != null) ? Vector3.Distance(lu.position, ru.position) : 0f;
            sb.AppendLine(string.Format("  {0}: hipY={1:F3} headY={2:F3} footY={3:F3}/{4:F3} spread={5:F3} shoulder={6:F3}",
                label, hips.position.y, head ? head.position.y : 0f, lf.position.y, rf.position.y, spread, shoulder));
        }

        static void Place(Camera cam, Vector3 target, float dist, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.position = target + rot * new Vector3(0f, 0.06f * dist, -dist);
            cam.transform.LookAt(target);
        }

        static void Render(Camera cam, RenderTexture rt, string name)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool has = false; Bounds b = new Bounds();
            foreach (var r in rs)
            {
                if (!r.enabled) continue;
                if (r is MeshRenderer && !(r is SkinnedMeshRenderer)) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            return has ? b : new Bounds(Vector3.zero, Vector3.one);
        }
    }
}
