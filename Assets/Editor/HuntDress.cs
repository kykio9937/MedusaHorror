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
    /// <summary>Dresses W0 base and the W4 player house with The Hunt cyberpunk props (neons, pipes, cables).</summary>
    public static class HuntDress
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/hunt.txt";
        static readonly StringBuilder log = new StringBuilder();

        static List<GameObject> Prefabs(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new List<GameObject>();
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(p => p != null)
                .OrderBy(p => p.name).ToList();
        }

        [MenuItem("Medusa/Dress W0 And W4 With Hunt")]
        public static void Dress()
        {
            log.Length = 0;
            try
            {
                var neons = Prefabs("Assets/beffio/The Hunt/Content/Prefabs/Neons");
                if (neons.Count == 0) neons = Prefabs("Assets/beffio/The Hunt/Content/Prefabs").Where(p => p.name.ToLower().Contains("neon")).ToList();
                var pipes = Prefabs("Assets/beffio/The Hunt/Content/Prefabs/Pipes");
                var cables = Prefabs("Assets/beffio/The Hunt/Content/Prefabs/Props");
                var bottles = Prefabs("Assets/beffio/The Hunt/Content/Prefabs/Bar");
                if (bottles.Count == 0) bottles = Prefabs("Assets/beffio/The Hunt/Content/Prefabs/Props");
                var panels = Prefabs("Assets/beffio/The Hunt/Content/Prefabs/Walls_modular_elements");
                log.AppendLine("prefabs neon=" + neons.Count + " pipe=" + pipes.Count + " cable=" + cables.Count + " bottle=" + bottles.Count + " panel=" + panels.Count);

                DressW0(neons, pipes, cables, panels);
                DressW4(neons, pipes, cables, bottles);
                ShotW0();
                log.AppendLine("OK");
            }
            catch (Exception e) { log.AppendLine("EXC " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false));
            }
        }

        static GameObject Put(GameObject prefab, Transform parent, Vector3 localPos, Vector3 euler, float scale)
        {
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }


        static void ShotW0()
        {
            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 65f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.transform.position = new Vector3(0f, 2.6f, -9f);
            cam.transform.LookAt(new Vector3(0f, 2.4f, 12f));
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(@"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w0_dressed.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(camGo);
            log.AppendLine("W0 shot saved");
        }

        static void DressW0(List<GameObject> neons, List<GameObject> pipes, List<GameObject> cables, List<GameObject> panels)
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W0_Base.unity", OpenSceneMode.Single);
            var hall = GameObject.Find("BaseHall");
            if (hall == null) { log.AppendLine("W0: no BaseHall"); return; }
            var root = new GameObject("HuntDressing").transform;
            root.SetParent(hall.transform, false);
            var rnd = new System.Random(7);

            // neon signs between the five gates on the north wall
            for (int i = 0; i < 5 && neons.Count > 0; i++)
            {
                float x = -16f + i * 8f;
                var n = Put(neons[rnd.Next(neons.Count)], root, new Vector3(x + 4f, 3.2f, 12.4f), new Vector3(0, 180, 0), 1.0f);
                if (n != null) n.name = "Neon_" + i;
            }
            // ceiling pipes and cables running the length of the hall
            for (int i = 0; i < 6 && pipes.Count > 0; i++)
            {
                var p = Put(pipes[rnd.Next(pipes.Count)], root, new Vector3(-18f + i * 7.2f, 6.4f, 0f), new Vector3(0, 90, 0), 1f);
                if (p != null) p.name = "Pipe_" + i;
            }
            for (int i = 0; i < 8 && cables.Count > 0; i++)
            {
                var c = Put(cables[rnd.Next(cables.Count)], root, new Vector3(-17f + i * 5f, 5.6f + (float)rnd.NextDouble() * 0.6f, -11f), new Vector3(0, 0, 90f), 1f);
                if (c != null) c.name = "Cable_" + i;
            }
            // wall panels on the west and east walls
            for (int i = 0; i < 6 && panels.Count > 0; i++)
            {
                Put(panels[rnd.Next(panels.Count)], root, new Vector3(-19.6f, 2.2f, -9f + i * 3.6f), new Vector3(0, 90, 0), 1f);
                Put(panels[rnd.Next(panels.Count)], root, new Vector3(19.6f, 2.2f, -9f + i * 3.6f), new Vector3(0, -90, 0), 1f);
            }
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            log.AppendLine("W0 dressed")
;        }

        static void DressW4(List<GameObject> neons, List<GameObject> pipes, List<GameObject> cables, List<GameObject> bottles)
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/W4_NeonStreet.unity", OpenSceneMode.Single);
            var house = GameObject.Find("PlayerHouse");
            if (house == null) { log.AppendLine("W4: no PlayerHouse"); return; }
            var rends = house.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) { log.AppendLine("W4: house has no renderer"); return; }
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            log.AppendLine("W4 house bounds size=" + b.size.ToString("F1") + " center=" + b.center.ToString("F1"));

            var root = new GameObject("HuntHouseDressing").transform;
            root.SetParent(house.transform, false);
            var rnd = new System.Random(11);
            // neon above the entrance, inside the house footprint
            if (neons.Count > 0)
                Put(neons[rnd.Next(neons.Count)], root, new Vector3(0, b.size.y * 0.6f, -b.size.z * 0.45f), Vector3.zero, 1f);
            for (int i = 0; i < 4 && bottles.Count > 0; i++)
                Put(bottles[rnd.Next(bottles.Count)], root, new Vector3(-1.5f + i * 0.9f, 0.9f, 1.2f), new Vector3(0, rnd.Next(360), 0), 1f);
            for (int i = 0; i < 3 && cables.Count > 0; i++)
                Put(cables[rnd.Next(cables.Count)], root, new Vector3(-2f, 2.6f + i * 0.2f, 0f), new Vector3(0, 0, 90), 1f);
            if (pipes.Count > 0) Put(pipes[rnd.Next(pipes.Count)], root, new Vector3(0, 2.8f, 2f), new Vector3(0, 90, 0), 1f);
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            log.AppendLine("W4 house dressed");
        }
    }
}
