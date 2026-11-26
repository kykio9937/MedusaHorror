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
    /// <summary>Builds the alien-surface finale (W5) from the Space pack terrain + the Teleport stargate.</summary>
    public static class W5AlienBuilder
    {
        public const string ScenePath = "Assets/Scenes/W5_AlienGate.unity";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string TeleportFbx = "Assets/Art/Props/Teleport/Teleport.fbx";
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w5build.txt";

        static readonly StringBuilder log = new StringBuilder();

        [MenuItem("Medusa/8 Build W5 Alien Surface")]
        public static void Build()
        {
            log.Length = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                log.AppendLine("opened " + scene.name + " roots=" + scene.rootCount);

                CleanupOld();
                PurgeForeignBehaviours();
                log.AppendLine("cleanup + purge done");

                var player = SpawnPlayer();
                log.AppendLine("player=" + (player != null ? player.transform.position.ToString("F2") : "NULL"));

                BuildCameraRig(player);
                log.AppendLine("camera rig done");

                BuildGate(player);
                log.AppendLine("gate done");

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene);
                log.AppendLine("saved=" + saved);

                var list = EditorBuildSettings.scenes.ToList();
                if (!list.Any(s => s.path == ScenePath))
                {
                    list.Add(new EditorBuildSettingsScene(ScenePath, true));
                    EditorBuildSettings.scenes = list.ToArray();
                    log.AppendLine("added to build settings");
                }
                log.AppendLine("OK");
            }
            catch (Exception e)
            {
                log.AppendLine("EXC " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
                File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false));
            }
        }


        /// <summary>The Space demo scene carries vehicle/effect demo scripts that spam the console
        /// (missing Input axes, unassigned refs). Keep only our own gameplay scripts + Cinemachine.</summary>
        static void PurgeForeignBehaviours()
        {
            int removed = 0;
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var t = mb.GetType();
                var ns = t.Namespace ?? "";
                if (ns.StartsWith("MedusaHorror") || ns.StartsWith("Cinemachine")) continue;
                UnityEngine.Object.DestroyImmediate(mb);
                removed++;
            }
            log.AppendLine("purged foreign behaviours=" + removed);
        }

        static void CleanupOld()
        {
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>(true).ToArray())
            {
                if (go == null) continue;
                var n = go.name;
                if (n == "Kazuko_W1_Player" || n == "Alien_Stargate" || n == "W5_ExitPortal" ||
                    n == "Main Camera" || n == "CM_W5_ThirdPerson") UnityEngine.Object.DestroyImmediate(go);
            }
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Camera>(true)) UnityEngine.Object.DestroyImmediate(c.gameObject);
            log.AppendLine("cleanup: removed old rig instances");
        }

        static GameObject SpawnPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            if (prefab == null) { log.AppendLine("player prefab missing"); return null; }
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.position = SpawnPoint();
            player.transform.rotation = Quaternion.identity;
            return player;
        }

        static Terrain MainTerrain()
        {
            return UnityEngine.Object.FindObjectsOfType<Terrain>(true)
                .OrderByDescending(t => t.terrainData.size.x * t.terrainData.size.z).FirstOrDefault();
        }

        static Vector3 SpawnPoint()
        {
            var terr = MainTerrain();
            if (terr == null) return new Vector3(0f, 6f, 0f);
            Vector3 p = terr.transform.position + new Vector3(terr.terrainData.size.x * 0.5f, 0f, terr.terrainData.size.z * 0.5f);
            p.y = terr.SampleHeight(p) + terr.transform.position.y + 1.5f;
            return p;
        }

        static void BuildCameraRig(GameObject player)
        {
            var camTarget = player != null ? player.transform.Find("CameraTarget") : null;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 1200f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Cinemachine.CinemachineBrain>();

            var vcamGo = new GameObject("CM_W5_ThirdPerson");
            var vcam = vcamGo.AddComponent<Cinemachine.CinemachineVirtualCamera>();
            vcam.Priority = 20;
            if (camTarget != null) { vcam.Follow = camTarget; vcam.LookAt = camTarget; }
            var body = vcam.AddCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            body.m_CameraDistance = 4.2f;
            body.m_ScreenX = 0.5f;
            body.m_ScreenY = 0.55f;
            body.m_XDamping = 0.1f;
            body.m_YDamping = 0.1f;
            body.m_ZDamping = 0.1f;

            var tci = vcamGo.AddComponent<MedusaHorror.Player.ThirdPersonCameraInput>();
            if (player != null)
            {
                tci.input = player.GetComponent<MedusaHorror.Player.InputReader>();
                tci.yawSource = player.transform;
            }
        }

        static void BuildGate(GameObject player)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(TeleportFbx);
            if (fbx == null) { log.AppendLine("teleport fbx missing"); return; }
            var gate = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            Vector3 pos = player != null ? player.transform.position + player.transform.forward * 18f : new Vector3(0, 0, 18);
            var terr = MainTerrain();
            if (terr != null) pos.y = terr.SampleHeight(pos) + terr.transform.position.y;
            gate.transform.position = pos;
            gate.transform.rotation = Quaternion.LookRotation(player != null ? -player.transform.forward : Vector3.back, Vector3.up);

            var rends = gate.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float h = Mathf.Max(0.01f, b.size.y);
                float sc = 6f / h;
                gate.transform.localScale *= sc;
                gate.transform.position += Vector3.up * (0.5f * b.size.y * sc); // sit on the ground
            }
            gate.name = "Alien_Stargate";

            var portalGo = new GameObject("W5_ExitPortal");
            portalGo.transform.position = gate.transform.position + Vector3.up * 2.2f;
            var portal = portalGo.AddComponent<MedusaHorror.Gameplay.ExitPortal>();
            portal.nextSceneOverride = "MainMenu";
            portal.requireKey = false;
            portal.requireActivation = true;
            log.AppendLine("gate at " + gate.transform.position.ToString("F1"));
        }
    }
}
