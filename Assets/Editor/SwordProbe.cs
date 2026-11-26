using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class SwordProbe
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim3";
        const string Model = "Assets/Suriyun/Kazuko/FBX/Kazuko.fbx";
        const string Sword = "Assets/Art/Weapons/Claymore/Kazuko_claymore.prefab";
        const string Clip = "Assets/Suriyun/Animations/Anim@Atk1.fbx";

        static readonly Vector3[] Eulers =
        {
            new Vector3(0,0,0), new Vector3(0,90,0), new Vector3(0,-90,0),
            new Vector3(90,0,0), new Vector3(-90,0,0), new Vector3(0,0,90),
            new Vector3(0,0,-90), new Vector3(0,180,0),
        };
        static readonly string[] Tags = { "A", "B", "C", "D", "E", "F", "G", "H" };

        [MenuItem("Medusa/Sword Probe")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            var sb = new StringBuilder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.2f;
            var key = new GameObject("Key"); var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional; kl.intensity = 1.5f; key.transform.rotation = Quaternion.Euler(35, -30, 0);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(2, 1, 2);

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            var animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            var clip = AssetDatabase.LoadAllAssetsAtPath(Clip).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(520, 700, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, clip.length * 0.35f);
            AnimationMode.EndSampling();

            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            for (int i = 0; i < Eulers.Length; i++)
            {
                var sword = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Sword));
                sword.transform.SetParent(hand, false);
                sword.transform.localPosition = Vector3.zero;
                sword.transform.localRotation = Quaternion.Euler(Eulers[i]);
                sword.transform.localScale = Vector3.one;

                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 target = new Vector3(go.transform.position.x, 1.0f, go.transform.position.z);
                cam.transform.position = target + Quaternion.Euler(0, 155, 0) * new Vector3(0, 0.45f, -3.6f);
                cam.transform.LookAt(target);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Out, Tags[i] + "_" + Eulers[i].x + "_" + Eulers[i].y + "_" + Eulers[i].z + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                var tip = sword.transform.position + sword.transform.TransformDirection(new Vector3(0, 0, 1)) * 1.13f;
                sb.AppendLine(Tags[i] + " euler=" + Eulers[i] + " tipWorld=" + tip.ToString("F2") + " handWorld=" + hand.position.ToString("F2"));
                UnityEngine.Object.DestroyImmediate(sword);
            }
            AnimationMode.StopAnimationMode();
            File.WriteAllText(Path.Combine(Out, "report.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[SwordProbe]\n" + sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
