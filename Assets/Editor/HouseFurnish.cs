using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Furnishes the W4 player house with Space-pack furniture and shoots W0 for review.</summary>
    public static class HouseFurnish
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/furnish.txt";
        static readonly StringBuilder log = new StringBuilder();

        [MenuItem("Medusa/Furnish House And Shoot W0")]
        public static void Run()
        {
            log.Length = 0;
            try
            {
                Furnish();
                ShootW0();
                log.AppendLine("OK");
            }
            catch (Exception e) { log.AppendLine("EXC " + e.GetType().Name + ": " + e.Message); }
            finally { File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false)); }
        }

        static GameObject Prefab(string path)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static void Furnish()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W4_NeonStreet.unity", OpenSceneMode.Single);
            var house = GameObject.Find("PlayerHouse");
            if (house == null) { log.AppendLine("no house"); return; }
            var old = house.transform.Find("HouseFurniture");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var root = new GameObject("HouseFurniture").transform;
            root.SetParent(house.transform, false);
            var chair = Prefab("Assets/Space/Prefabs/furniture/chair.prefab");
            var stool = Prefab("Assets/Space/Prefabs/furniture/stool.prefab");
            var panel = Prefab("Assets/Space/Prefabs/furniture/panelA.prefab");
            var monitor = Prefab("Assets/Space/Prefabs/furniture/monitor.prefab");
            var crate = Prefab("Assets/Space/Prefabs/props/crateA.prefab");
            var barrel = Prefab("Assets/Space/Prefabs/props/barrel.prefab");

            int n = 0;
            void Put(GameObject p, Vector3 pos, float yaw, float scale)
            {
                if (p == null) return;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p, root);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                go.transform.localScale = Vector3.one * scale;
                n++;
            }

            Put(chair, new Vector3(-2.4f, 0f, 0.8f), 35f, 1f);
            Put(chair, new Vector3(2.4f, 0f, 0.8f), -35f, 1f);
            Put(stool, new Vector3(0f, 0f, 2.2f), 0f, 1f);
            Put(monitor, new Vector3(0f, 1.25f, -2.6f), 0f, 0.8f);
            Put(panel, new Vector3(-3.6f, 1.6f, -2.8f), 0f, 1f);
            Put(panel, new Vector3(3.6f, 1.6f, -2.8f), 0f, 1f);
            Put(crate, new Vector3(2.6f, 0f, 2.6f), 20f, 0.8f);
            Put(barrel, new Vector3(-2.8f, 0f, 2.6f), 0f, 0.9f);
            log.AppendLine("furniture placed=" + n);

            var lightGo = new GameObject("HouseLight");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point; l.range = 9f; l.intensity = 2.2f; l.color = new Color(1f, 0.82f, 0.6f);

            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            log.AppendLine("house furnished");
        }

        static void ShootW0()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity", OpenSceneMode.Single);
            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 65f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.transform.position = new Vector3(0f, 2.7f, -10f);
            cam.transform.LookAt(new Vector3(0f, 2.4f, 12f));
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(@"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w0_hall_v2.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(camGo);
            EditorSceneManager.SaveScene(sc);
            log.AppendLine("W0 shot saved");
        }
    }
}
