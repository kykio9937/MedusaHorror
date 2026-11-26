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
    /// <summary>Builds the alien finale from scratch: one Space-pack terrain, alien foliage, our camera rig and the stargate.</summary>
    public static class W5AlienBuilderV2
    {
        public const string ScenePath = "Assets/Scenes/W5_AlienGate.unity";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string TeleportFbx = "Assets/Art/Props/Teleport/Teleport.fbx";
        const string TerrainData = "Assets/Space/Terrain/New Terrain 1.asset";
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w5v2.txt";
        static readonly StringBuilder log = new StringBuilder();

        [MenuItem("Medusa/9 Build W5 From Scratch")]
        public static void Build()
        {
            log.Length = 0;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 1. terrain
                var td = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainData);
                var terrainGo = Terrain.CreateTerrainGameObject(td);
                terrainGo.name = "AlienTerrain";
                terrainGo.transform.position = Vector3.zero;
                var terr = terrainGo.GetComponent<Terrain>();
                terr.drawInstanced = true;
                log.AppendLine("terrain size=" + td.size);

                // 2. skybox + light
                RenderSettings.skybox = FindSkybox() ?? new Material(Shader.Find("Skybox/Procedural"));
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 1.15f;
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.35f, 0.30f, 0.45f);
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.0035f;

                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                light.color = new Color(0.85f, 0.88f, 1f);
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

                // 3. alien foliage
                ScatterFoliage(terr, 90);

                // 4. player
                Vector3 center = new Vector3(td.size.x * 0.5f, 0f, td.size.z * 0.5f);
                center.y = terr.SampleHeight(center) + 1.2f;
                var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab));
                player.transform.position = center;
                log.AppendLine("player at " + center.ToString("F1"));

                // 5. camera rig
                BuildCameraRig(player);

                // 6. stargate
                BuildGate(player, terr);

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine("saved=" + saved);

                var list = EditorBuildSettings.scenes.ToList();
                if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
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

        static Material FindSkybox()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Space/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.shader != null && m.shader.name.StartsWith("Skybox")) return m;
            }
            return null;
        }

        static void ScatterFoliage(Terrain terr, int count)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Space/Prefabs/foliage" });
            var prefabs = guids.Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
                               .Where(p => p != null).ToArray();
            if (prefabs.Length == 0) { log.AppendLine("no foliage"); return; }
            var root = new GameObject("AlienFoliage");
            var rnd = new System.Random(20261125);
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                var p = new Vector3((float)rnd.NextDouble() * terr.terrainData.size.x, 0f, (float)rnd.NextDouble() * terr.terrainData.size.z);
                if (Vector3.Distance(p, new Vector3(terr.terrainData.size.x * 0.5f, 0, terr.terrainData.size.z * 0.5f)) < 12f) continue;
                p.y = terr.SampleHeight(p);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[rnd.Next(prefabs.Length)]);
                inst.transform.SetParent(root.transform, true);
                inst.transform.position = p;
                inst.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
                float s = 0.8f + (float)rnd.NextDouble() * 0.8f;
                inst.transform.localScale = Vector3.one * s;
                placed++;
            }
            log.AppendLine("foliage placed=" + placed);
        }

        static void BuildCameraRig(GameObject player)
        {
            var camTarget = player.transform.Find("CameraTarget");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 2000f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Cinemachine.CinemachineBrain>();

            var vcamGo = new GameObject("CM_W5_ThirdPerson");
            var vcam = vcamGo.AddComponent<Cinemachine.CinemachineVirtualCamera>();
            vcam.Priority = 20;
            if (camTarget != null) { vcam.Follow = camTarget; vcam.LookAt = camTarget; }
            var body = vcam.AddCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            body.m_CameraDistance = 4.2f;
            body.m_ScreenY = 0.58f;
            vcamGo.AddComponent<MedusaHorror.Player.ThirdPersonCameraInput>().input = player.GetComponent<MedusaHorror.Player.InputReader>();
            var tci = vcamGo.GetComponent<MedusaHorror.Player.ThirdPersonCameraInput>();
            tci.yawSource = player.transform;
        }

        static void BuildGate(GameObject player, Terrain terr)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(TeleportFbx);
            if (fbx == null) { log.AppendLine("gate missing"); return; }
            var gate = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            Vector3 pos = player.transform.position + player.transform.forward * 22f;
            pos.y = terr.SampleHeight(pos);
            gate.transform.position = pos;
            gate.transform.rotation = Quaternion.LookRotation(-player.transform.forward, Vector3.up);
            var rends = gate.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                float sc = 6f / Mathf.Max(0.01f, b.size.y);
                gate.transform.localScale *= sc;
                gate.transform.position += Vector3.up * (0.5f * b.size.y * sc);
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
