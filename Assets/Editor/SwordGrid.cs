using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Grid-test sword socket rotations on the real Animator (attack pose).</summary>
    public static class SwordGrid
    {
        const string Out = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim3";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string Sword = "Assets/Art/Weapons/Claymore/Kazuko_claymore.prefab";

        static readonly Vector3[] Eulers =
        {
            new Vector3(0,0,0), new Vector3(0,90,0), new Vector3(0,-90,0),
            new Vector3(90,0,0), new Vector3(-90,0,0), new Vector3(0,180,0),
            new Vector3(-45,0,0), new Vector3(-135,0,0), new Vector3(-90,90,0), new Vector3(-90,-90,0),
        };
        static readonly string[] Tags = { "A","B","C","D","E","F","G","H","I","J" };

        [MenuItem("Medusa/Sword Grid")]
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

            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
            var anim = player.GetComponent<Animator>();
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 34f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(520, 700, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            for (int i = 0; i < Eulers.Length; i++)
            {
                var sword = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Sword));
                sword.transform.SetParent(hand, false);
                sword.transform.localPosition = Vector3.zero;
                sword.transform.localRotation = Quaternion.Euler(Eulers[i]);
                sword.transform.localScale = Vector3.one;

                anim.SetFloat("Speed", 0f);
                anim.SetInteger("ComboStep", 1);
                anim.SetTrigger("Attack");
                Anim.Update(1f / 30f, 9);   // ~0.3 s into the swing

                Vector3 target = new Vector3(player.transform.position.x, 1.05f, player.transform.position.z + 0.35f);
                cam.transform.position = target + Quaternion.Euler(0, 150, 0) * new Vector3(0, 0.30f, -3.3f);
                cam.transform.LookAt(target);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(Out, "G" + Tags[i] + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                var tip = sword.transform.position + sword.transform.TransformDirection(Vector3.forward) * 1.13f;
                var dir = (tip - hand.position).normalized;
                sb.AppendLine(Tags[i] + " euler=" + Eulers[i] + " dir=" + dir.ToString("F2") + " tip=" + tip.ToString("F2"));
                UnityEngine.Object.DestroyImmediate(sword);

                Anim.Update(1f / 30f, 40); // let the state settle back
            }

            File.WriteAllText(Path.Combine(Out, "grid.txt"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[SwordGrid]\n" + sb.ToString());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static class Anim
        {
            public static void Update(float dt, int frames)
            {
                var anim = UnityEngine.Object.FindObjectOfType<Animator>();
                if (anim == null) return;
                for (int i = 0; i < frames; i++) anim.Update(dt);
            }
        }
    }
}
