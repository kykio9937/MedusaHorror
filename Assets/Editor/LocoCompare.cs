using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Side view gait comparison of candidate walk / run clips on the Kazuko model.</summary>
    public static class LocoCompare
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_loco";
        const string Model = "Assets/Suriyun/Kazuko/FBX/Kazuko.fbx";

        static readonly string[] WalkClips =
        {
            "Assets/Suriyun/Animations/Anim@Walk.fbx",
            "Assets/Space/Animation/Animations/Walking.fbx",
            "Assets/Kevin Iglesias/Basic Motions/Animations/Movement/BasicMotions@Walk01.fbx",
        };
        static readonly string[] RunClips =
        {
            "Assets/Suriyun/Animations/Anim@Run.fbx",
            "Assets/Space/Animation/Animations/RunForward.fbx",
            "Assets/Kevin Iglesias/Basic Motions/Animations/Movement/BasicMotions@Run01.fbx",
        };

        [MenuItem("Medusa/Loco Compare")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.2f;
            var key = new GameObject("Key"); var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional; kl.intensity = 1.45f; key.transform.rotation = Quaternion.Euler(30, 120, 0);
            var fill = new GameObject("Fill"); var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 0.65f; fill.transform.rotation = Quaternion.Euler(10, -60, 0);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(2, 1, 2);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(400, 600, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            RenderSet(cam, rt, go, anim, WalkClips, "walk");
            RenderSet(cam, rt, go, anim, RunClips, "run");

            File.WriteAllText(Path.Combine(Out, "done.txt"), "ok");
            Debug.Log("[LocoCompare] done");
        }

        static void RenderSet(Camera cam, RenderTexture rt, GameObject go, Animator anim, string[] clips, string tag)
        {
            AnimationMode.StartAnimationMode();
            try
            {
                for (int ci = 0; ci < clips.Length; ci++)
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath(clips[ci]).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                    if (clip == null) continue;
                    for (int ph = 0; ph < 4; ph++)
                    {
                        float t = clip.length * ph / 4f;
                        AnimationMode.BeginSampling();
                        AnimationMode.SampleAnimationClip(go, clip, t);
                        AnimationMode.EndSampling();

                        var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                        Vector3 target = new Vector3(0f, hips != null ? hips.position.y * 0.92f : 0.9f, 0f);
                        cam.transform.position = new Vector3(3.6f, target.y + 0.15f, -0.6f);
                        cam.transform.LookAt(target);
                        cam.Render();
                        RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        RenderTexture.active = null;
                        File.WriteAllBytes(Path.Combine(Out, tag + "_" + ci + "_" + ph + ".png"), tex.EncodeToPNG());
                        UnityEngine.Object.DestroyImmediate(tex);
                    }
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
        }
    }
}
