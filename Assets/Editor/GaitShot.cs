using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Renders the applied locomotion states (idle / walk / run) from a side view for gait review.</summary>
    public static class GaitShot
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_gait";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";

        [MenuItem("Medusa/Gait Shot")]
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.15f;
            var key = new GameObject("Key"); var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional; kl.intensity = 1.4f; key.transform.rotation = Quaternion.Euler(32, 120, 0);
            var fill = new GameObject("Fill"); var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional; fl.intensity = 0.7f; fill.transform.rotation = Quaternion.Euler(8, -70, 0);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = new Vector3(3, 1, 3);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            var anim = player.GetComponent<Animator>();
            if (anim == null) anim = player.GetComponentInChildren<Animator>(true);
            anim.SetLayerWeight(1, 0f);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(460, 660, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            Shots(cam, rt, anim, 0f, "idle");
            Shots(cam, rt, anim, 1.45f, "walk");
            Shots(cam, rt, anim, 3.2f, "run");
            Debug.Log("[GaitShot] done");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void Shots(Camera cam, RenderTexture rt, Animator anim, float speed, string tag)
        {
            anim.SetFloat("Speed", speed);
            for (int i = 0; i < 30; i++) anim.Update(1f / 30f);      // settle the blend
            for (int ph = 0; ph < 4; ph++)
            {
                for (int i = 0; i < 8; i++) anim.Update(1f / 30f);
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 t = new Vector3(anim.transform.position.x, hips.position.y - 0.05f, anim.transform.position.z);
                cam.transform.position = t + Vector3.right * 3.4f + Vector3.up * 0.1f;
                cam.transform.LookAt(t);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Out, tag + "_" + ph + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}

