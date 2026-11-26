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
    /// <summary>W5 polish (terrain layers + gate grounding) and the W0 station-hub rebuild.</summary>
    public static class SceneBuild2
    {
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/build2.txt";
        static readonly StringBuilder log = new StringBuilder();

        // ---------------- W5 polish ----------------
        [MenuItem("Medusa/A Fix W5 Terrain And Gate")]
        public static void FixW5()
        {
            log.Length = 0;
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/W5_AlienGate.unity", OpenSceneMode.Single);
                var terr = UnityEngine.Object.FindObjectsOfType<Terrain>(true).FirstOrDefault();
                if (terr != null)
                {
                    var layers = new List<TerrainLayer>();
                    layers.Add(MakeLayer("MainLand", "Assets/Space/Terrain/MainLand.png", "Assets/Space/Terrain/MainLand_NRM.tga", 20f));
                    layers.Add(MakeLayer("GrassDirt", "Assets/Space/Terrain/grassDirt.png", "Assets/Space/Terrain/grassDirt_NRM.png", 8f));
                    layers.Add(MakeLayer("Rock", "Assets/Space/Terrain/rock2.png", "Assets/Space/Terrain/rock_NRM.png", 14f));
                    terr.terrainData.terrainLayers = layers.Where(l => l != null).ToArray();
                    log.AppendLine("terrain layers=" + terr.terrainData.terrainLayers.Length);
                    // paint the first layer everywhere
                    int res = terr.terrainData.alphamapResolution;
                    var maps = new float[res, res, Mathf.Max(1, terr.terrainData.terrainLayers.Length)];
                    for (int y = 0; y < res; y++)
                        for (int x = 0; x < res; x++)
                        {
                            maps[y, x, 0] = 1f;
                            for (int k = 1; k < terr.terrainData.terrainLayers.Length; k++) maps[y, x, k] = 0f;
                        }
                    terr.terrainData.SetAlphamaps(0, 0, maps);
                    EditorUtility.SetDirty(terr.terrainData);
                }

                // ground the stargate
                var gate = GameObject.Find("Alien_Stargate");
                if (gate != null && terr != null)
                {
                    var rends = gate.GetComponentsInChildren<Renderer>(true);
                    if (rends.Length > 0)
                    {
                        Bounds b = rends[0].bounds;
                        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                        float ground = terr.SampleHeight(gate.transform.position) + terr.transform.position.y;
                        gate.transform.position += Vector3.up * (ground - b.min.y);
                        log.AppendLine("gate grounded, delta=" + (ground - b.min.y).ToString("F2"));
                    }
                }
                var portal = GameObject.Find("W5_ExitPortal");
                if (portal != null && gate != null) portal.transform.position = gate.transform.position + Vector3.up * 2.2f;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("W5 fixed OK");
            }
            catch (Exception e) { log.AppendLine("EXC " + e.GetType().Name + ": " + e.Message); }
            finally { Dump(); }
        }

        static TerrainLayer MakeLayer(string name, string albedo, string normal, float tile)
        {
            var a = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo);
            if (a == null) return null;
            string path = "Assets/Space/Terrain/" + name + "_Auto.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = a;
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            if (n != null) layer.normalMapTexture = n;
            layer.tileSize = new Vector2(tile, tile);
            EditorUtility.SetDirty(layer);
            AssetDatabase.SaveAssets();
            return layer;
        }

        // ---------------- W0 station hub ----------------
        [MenuItem("Medusa/B Build W0 Station Hub")]
        public static void BuildW0()
        {
            log.Length = 0;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                RenderSettings.skybox = null;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.24f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.05f, 0.07f, 0.10f);
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.012f;

                var metal = FindMaterial();
                var hall = new GameObject("BaseHall");
                const float W = 40f, D = 26f, H = 7f;

                // floor + ceiling
                Box(hall.transform, "Floor", new Vector3(0, -0.25f, 0), new Vector3(W, 0.5f, D), metal, new Color(0.20f, 0.22f, 0.26f));
                Box(hall.transform, "Ceiling", new Vector3(0, H + 0.25f, 0), new Vector3(W, 0.5f, D), metal, new Color(0.12f, 0.13f, 0.16f));

                // walls
                Box(hall.transform, "WallN", new Vector3(0, H / 2, D / 2), new Vector3(W, H, 0.5f), metal, new Color(0.17f, 0.19f, 0.23f));
                Box(hall.transform, "WallS", new Vector3(0, H / 2, -D / 2), new Vector3(W, H, 0.5f), metal, new Color(0.17f, 0.19f, 0.23f));
                Box(hall.transform, "WallE", new Vector3(W / 2, H / 2, 0), new Vector3(0.5f, H, D), metal, new Color(0.17f, 0.19f, 0.23f));
                Box(hall.transform, "WallW", new Vector3(-W / 2, H / 2, 0), new Vector3(0.5f, H, D), metal, new Color(0.17f, 0.19f, 0.23f));

                // wall panels for detail
                var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Space/Prefabs/corridorModules/wallPanel.prefab");
                if (panel != null)
                {
                    for (int i = -4; i <= 4; i++)
                    {
                        var p = (GameObject)PrefabUtility.InstantiatePrefab(panel, hall.transform);
                        p.transform.localPosition = new Vector3(i * 4.2f, 3.0f, D / 2 - 0.4f);
                        p.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        var q = (GameObject)PrefabUtility.InstantiatePrefab(panel, hall.transform);
                        q.transform.localPosition = new Vector3(i * 4.2f, 3.0f, -D / 2 + 0.4f);
                    }
                }

                // portal array on the north wall
                string[] targets = { "W1_Hospital", "W2_Wasteland", "W3_Subway", "W4_NeonStreet", "W5_AlienGate" };
                Color[] colors = { new Color(0.85f, 0.20f, 0.22f), new Color(0.95f, 0.62f, 0.30f), new Color(0.45f, 0.75f, 1f), new Color(1f, 0.35f, 0.72f), new Color(0.62f, 0.48f, 1f) };
                for (int i = 0; i < targets.Length; i++)
                {
                    float x = -16f + i * 8f;
                    Portal(hall.transform, targets[i], new Vector3(x, 0f, D / 2 - 1.4f), colors[i], metal);
                }

                // weapon bench + crates
                var bench = new GameObject("WeaponBench");
                bench.transform.SetParent(hall.transform, false);
                bench.transform.localPosition = new Vector3(-14f, 0f, -6f);
                Box(bench.transform, "TableTop", new Vector3(0, 0.9f, 0), new Vector3(3.2f, 0.16f, 1.3f), metal, new Color(0.26f, 0.27f, 0.30f));
                for (int i = -1; i <= 1; i += 2)
                    Box(bench.transform, "Leg" + i, new Vector3(i * 1.4f, 0.45f, 0), new Vector3(0.14f, 0.9f, 1.0f), metal, new Color(0.18f, 0.19f, 0.22f));
                var rifle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Space/Prefabs/riffle.prefab");
                if (rifle != null)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(rifle, bench.transform);
                    g.transform.localPosition = new Vector3(-0.6f, 1.02f, 0f);
                    g.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    g.transform.localScale = Vector3.one * 1.1f;
                }
                var revolver = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Space/Prefabs/revolver.prefab");
                if (revolver != null)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(revolver, bench.transform);
                    g.transform.localPosition = new Vector3(0.7f, 1.0f, 0.2f);
                }
                Light(bench.transform, new Vector3(0, 2.2f, -0.4f), new Color(0.6f, 0.85f, 1f), 2.2f, 6f);

                // save point console
                var saveGo = new GameObject("SavePoint");
                saveGo.transform.SetParent(hall.transform, false);
                saveGo.transform.localPosition = new Vector3(14f, 0f, -6f);
                Box(saveGo.transform, "Console", new Vector3(0, 0.55f, 0), new Vector3(1.6f, 1.1f, 0.9f), metal, new Color(0.22f, 0.24f, 0.28f));
                Box(saveGo.transform, "Screen", new Vector3(0, 1.22f, -0.2f), new Vector3(1.3f, 0.7f, 0.08f), null, new Color(0.35f, 0.95f, 0.85f), true);
                Light(saveGo.transform, new Vector3(0, 1.9f, 0), new Color(0.4f, 1f, 0.85f), 1.8f, 5f);

                // hall lights
                for (int i = -1; i <= 1; i++)
                {
                    Light(hall.transform, new Vector3(i * 12f, H - 0.8f, 0f), new Color(0.75f, 0.85f, 1f), 2.6f, 18f);
                    Box(hall.transform, "Lamp" + i, new Vector3(i * 12f, H - 0.15f, 0f), new Vector3(3.0f, 0.12f, 3.0f), null, new Color(0.85f, 0.92f, 1f), true);
                }

                // player + camera
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kazuko_W1_Player.prefab");
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.transform.position = new Vector3(0f, 0.2f, -8f);
                player.transform.rotation = Quaternion.identity;
                CameraRig(player);
                log.AppendLine("W0 hall built");

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene, "Assets/Scenes/W0_Base.unity");
                log.AppendLine("saved=" + saved);
                log.AppendLine("OK");
            }
            catch (Exception e) { log.AppendLine("EXC " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace); }
            finally { Dump(); }
        }

        static Material FindMaterial()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Space/Materials" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                if (m != null && m.shader != null && m.shader.name == "Standard") return m;
            }
            return null;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, Color color, bool emissive = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var col = go.GetComponent<Collider>();
            if (name.StartsWith("Lamp") || name == "Screen") UnityEngine.Object.DestroyImmediate(col);
            var m = new Material(Shader.Find("Standard"));
            m.color = color;
            m.SetFloat("_Glossiness", 0.45f);
            m.SetFloat("_Metallic", 0.55f);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.6f);
            }
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static void Light(Transform parent, Vector3 pos, Color c, float intensity, float range)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        static void Portal(Transform parent, string target, Vector3 pos, Color color, Material metal)
        {
            var root = new GameObject("Gate_" + target);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            Box(root.transform, "FrameL", new Vector3(-2.1f, 2.2f, 0), new Vector3(0.5f, 4.4f, 0.6f), metal, new Color(0.15f, 0.16f, 0.19f));
            Box(root.transform, "FrameR", new Vector3(2.1f, 2.2f, 0), new Vector3(0.5f, 4.4f, 0.6f), metal, new Color(0.15f, 0.16f, 0.19f));
            Box(root.transform, "Lintel", new Vector3(0f, 4.5f, 0), new Vector3(4.7f, 0.5f, 0.6f), metal, new Color(0.15f, 0.16f, 0.19f));
            Box(root.transform, "Glow", new Vector3(0f, 2.2f, 0.15f), new Vector3(3.6f, 4.2f, 0.06f), null, color, true);
            Light(root.transform, new Vector3(0f, 2.4f, 1.2f), color, 3.2f, 9f);

            var bp = root.AddComponent<MedusaHorror.Gameplay.BasePortal>();
            bp.targetScene = target;
        }

        static void CameraRig(GameObject player)
        {
            var camTarget = player.transform.Find("CameraTarget");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Cinemachine.CinemachineBrain>();
            var vcamGo = new GameObject("CM_W0_ThirdPerson");
            var vcam = vcamGo.AddComponent<Cinemachine.CinemachineVirtualCamera>();
            vcam.Priority = 20;
            if (camTarget != null) { vcam.Follow = camTarget; vcam.LookAt = camTarget; }
            var body = vcam.AddCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            body.m_CameraDistance = 4.0f;
            var tci = vcamGo.AddComponent<MedusaHorror.Player.ThirdPersonCameraInput>();
            tci.input = player.GetComponent<MedusaHorror.Player.InputReader>();
            tci.yawSource = player.transform;
        }

        static void Dump()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false));
        }
    }
}
