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
    /// <summary>Builds the W1 hospital level from the horror-hospital modular kit.</summary>
    public static class W1HospitalBuilder
    {
        public const string ScenePath = "Assets/Scenes/W1_Hospital.unity";
        const string Kit = "Assets/HorrorEnvironment_Hospital/Prefabs";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const string LogPath = @"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/w1build.txt";

        static readonly StringBuilder log = new StringBuilder();
        static GameObject _floor, _wall, _wallHalf, _ceiling, _jamb, _pillar;
        static Transform _root;
        static readonly List<Rect> walk = new List<Rect>();

        struct Rect
        {
            public float x0, z0, x1, z1;
            public Rect(float a, float b, float c, float d) { x0 = a; z0 = b; x1 = c; z1 = d; }
            public float W { get { return x1 - x0; } }
            public float D { get { return z1 - z0; } }
            public Vector3 C { get { return new Vector3((x0 + x1) / 2f, 0f, (z0 + z1) / 2f); } }
        }

        [MenuItem("Medusa/Build W1 Hospital")]
        public static void Build()
        {
            log.Length = 0;
            try
            {
                LoadKit();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var rootGo = new GameObject("W1_Hospital_Kit");
                _root = rootGo.transform;

                // ---------- layout ----------
                var hall = new Rect(-6, 0, 6, 12);
                var link = new Rect(-4, 12, 4, 20);
                var corridor = new Rect(-4, 20, 4, 80);
                var stair = new Rect(-8, 80, 8, 96);
                var surgery = new Rect(-8, 96, 8, 108);
                var endHall = new Rect(-8, 108, 8, 124);

                var rooms = new List<Rect>();
                for (int i = 0; i < 5; i++)
                {
                    float z0 = 22 + i * 12f;
                    rooms.Add(new Rect(-16, z0, -4, z0 + 8));   // west
                    rooms.Add(new Rect(4, z0, 16, z0 + 8));     // east
                }

                foreach (var r in new[] { hall, link, corridor, stair, surgery, endHall }) { Floor(r); Ceiling(r); walk.Add(r); }
                foreach (var r in rooms)
                {
                    Floor(r); Ceiling(r); walk.Add(r);
                    Lamp(new Vector3(r.C.x, 4.6f, r.C.z), new Color(0.82f, 0.88f, 1f), 2.4f, 12f);
                }

                // ---------- walls ----------
                WallZ(hall.x0, hall.z0, hall.z1, -1);          // west of hall
                WallZ(hall.x1, hall.z0, hall.z1, 1);           // east of hall
                WallX(hall.z1, hall.x0, hall.x1, 0f, true);    // hall -> link doorway
                WallX(hall.z0, hall.x0, hall.x1, 0f, true);    // entrance

                WallZ(link.x0, link.z0, link.z1, -1);
                WallZ(link.x1, link.z0, link.z1, 1);

                // corridor walls with a 2 m doorway into every room
                WallZSegments(corridor.x0, 20, 80, -1, rooms.Where(r => r.x1 <= corridor.x0 + 0.1f).Select(r => (r.z0 + r.z1) / 2f).ToArray());
                WallZSegments(corridor.x1, 20, 80, 1, rooms.Where(r => r.x0 >= corridor.x1 - 0.1f).Select(r => (r.z0 + r.z1) / 2f).ToArray());
                WallX(corridor.z1, corridor.x0, corridor.x1, 0f, true);   // corridor -> stairwell

                // room perimeters
                foreach (var r in rooms)
                {
                    bool west = r.x1 <= 0;
                    WallZ(west ? r.x0 : r.x1, r.z0, r.z1, west ? -1 : 1);
                    WallX(r.z0, r.x0, r.x1, 0f, false);
                    WallX(r.z1, r.x0, r.x1, 0f, false);
                }

                WallZ(stair.x0, stair.z0, stair.z1, -1);
                WallZ(stair.x1, stair.z0, stair.z1, 1);
                WallX(stair.z1, stair.x0, stair.x1, 0f, true);            // stair -> surgery

                WallZ(surgery.x0, surgery.z0, surgery.z1, -1);
                WallZ(surgery.x1, surgery.z0, surgery.z1, 1);
                WallX(surgery.z1, surgery.x0, surgery.x1, 0f, true);      // surgery -> end hall

                WallZ(endHall.x0, endHall.z0, endHall.z1, -1);
                WallZ(endHall.x1, endHall.z0, endHall.z1, 1);
                WallX(endHall.z1, endHall.x0, endHall.x1, 0f, false);

                // pillars along the corridor for scale
                for (int i = 0; i < 6; i++)
                {
                    Pillar(new Vector3(-3.6f, 0f, 24 + i * 10f));
                    Pillar(new Vector3(3.6f, 0f, 24 + i * 10f));
                }

                // ---------- opaque shell (closes the level visually) ----------
                var shell = new GameObject("Shell").transform;
                shell.SetParent(_root, false);
                var shellMat = new Material(Shader.Find("Standard"));
                shellMat.color = new Color(0.09f, 0.10f, 0.12f);
                shellMat.SetFloat("_Glossiness", 0.1f);
                ShellBox(shell, shellMat, "ShellW", new Vector3(-18.3f, 3.3f, 61f), new Vector3(0.4f, 6.6f, 132f));
                ShellBox(shell, shellMat, "ShellE", new Vector3(18.3f, 3.3f, 61f), new Vector3(0.4f, 6.6f, 132f));
                ShellBox(shell, shellMat, "ShellN", new Vector3(0f, 3.3f, 125f), new Vector3(37f, 6.6f, 0.4f));
                ShellBox(shell, shellMat, "ShellS", new Vector3(0f, 3.3f, -1f), new Vector3(37f, 6.6f, 0.4f));
                ShellBox(shell, shellMat, "ShellTop", new Vector3(0f, 6.8f, 61f), new Vector3(37f, 0.4f, 132f));

                // ---------- safety colliders ----------
                var safety = new GameObject("SafetyColliders");
                safety.transform.SetParent(_root, false);
                BoxCol(safety.transform, new Vector3(0f, -0.3f, 62f), new Vector3(40f, 0.6f, 132f));   // ground
                BoxCol(safety.transform, new Vector3(0f, 5f, -0.2f), new Vector3(40f, 10f, 0.6f));     // back
                BoxCol(safety.transform, new Vector3(0f, 5f, 124.2f), new Vector3(40f, 10f, 0.6f));    // front

                // ---------- lighting ----------
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.26f, 0.28f, 0.34f);
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.02f, 0.02f, 0.03f);
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.012f;
                var sun = new GameObject("Directional Light");
                var dl = sun.AddComponent<Light>();
                dl.type = LightType.Directional; dl.intensity = 0.25f; dl.color = new Color(0.6f, 0.7f, 0.9f);
                sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
                Lamp(new Vector3(0, 5.6f, 6), new Color(0.85f, 0.9f, 1f), 2.2f, 14f);
                for (int i = 0; i < 12; i++) Lamp(new Vector3(0, 5.4f, 24 + i * 5f), new Color(0.85f, 0.9f, 1f), 3.4f, 18f);
                for (int i = 0; i < 10; i++) Lamp(new Vector3((i % 2 == 0 ? -9f : 9f), 5.2f, 26 + (i / 2) * 12f), new Color(0.8f, 0.85f, 1f), 2.6f, 14f);
                Lamp(new Vector3(0, 5.6f, 88), new Color(0.9f, 0.85f, 0.75f), 2.4f, 16f);
                Lamp(new Vector3(0, 5.6f, 102), new Color(0.9f, 0.95f, 1f), 2.6f, 16f);
                Lamp(new Vector3(0, 5.6f, 116), new Color(0.55f, 0.45f, 1.0f), 2.6f, 18f);

                // ---------- player ----------
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.transform.position = new Vector3(0f, 0.2f, 3f);
                CameraRig(player);
                log.AppendLine("player ok");

                // ---------- enemies ----------
                // spec: 6 in rooms + 3 in the corridor + 3 in the surgery = 12
                int idx = 0;
                foreach (var r in rooms.Take(6))
                    Spawner("Spawn_Room_" + (idx++), new Vector3(r.C.x, 0.2f, r.C.z), 1, 6f);
                Spawner("Spawn_Corridor_1", new Vector3(0f, 0.2f, 34f), 1, 8f);
                Spawner("Spawn_Corridor_2", new Vector3(0f, 0.2f, 56f), 1, 8f);
                Spawner("Spawn_Corridor_3", new Vector3(0f, 0.2f, 74f), 1, 8f);
                Spawner("Spawn_Surgery", new Vector3(0f, 0.2f, 102f), 3, 8f);
                log.AppendLine("spawners ok");

                // ---------- objectives ----------
                Objectives();
                log.AppendLine("objectives ok");

                // ---------- exit stargate ----------
                BuildStargate(new Vector3(0f, 0f, 121f));

                // ---------- bake navmesh ----------
                var navigation = _root.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                navigation.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
                navigation.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                navigation.BuildNavMesh();
                if (navigation.navMeshData != null)
                    AssetDatabase.CreateAsset(navigation.navMeshData,
                        AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/W1_Hospital_GeneratedNavMesh.asset"));
                log.AppendLine("navmesh baked");

                // ---------- walkability ----------
                WalkCheck();

                EditorSceneManager.MarkSceneDirty(scene);
                bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
                log.AppendLine("saved=" + saved);

                var list = EditorBuildSettings.scenes.ToList();
                if (!list.Any(s => s.path == ScenePath))
                {
                    var ordered = new List<EditorBuildSettingsScene>();
                    ordered.Add(new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true));
                    ordered.Add(new EditorBuildSettingsScene("Assets/Scenes/W0_Base.unity", true));
                    ordered.Add(new EditorBuildSettingsScene(ScenePath, true));
                    foreach (var s in list) if (s.path != "Assets/Scenes/MainMenu.unity" && s.path != "Assets/Scenes/W0_Base.unity") ordered.Add(s);
                    EditorBuildSettings.scenes = ordered.ToArray();
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

        // ---------- kit ----------
        static Vector3 _wallOff, _wallHalfOff, _ceilOff;

        static Vector3 PivotOffset(GameObject prefab)
        {
            if (prefab == null) return Vector3.zero;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            var rends = inst.GetComponentsInChildren<Renderer>(true);
            Vector3 off = Vector3.zero;
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                off = b.center;
            }
            UnityEngine.Object.DestroyImmediate(inst);
            return off;
        }

        static void LoadKit()
        {
            _floor = Prefab("floorPlane");
            _wall = Prefab("wallA");
            _wallHalf = Prefab("wallA_half");
            _ceiling = Prefab("roundCeiling");
            _jamb = Prefab("doors_jamb_A");
            _pillar = Prefab("pillar_A");
            _wallOff = PivotOffset(_wall);
            _wallHalfOff = PivotOffset(_wallHalf);
            _ceilOff = PivotOffset(_ceiling);
            log.AppendLine("offsets wall=" + _wallOff.ToString("F2") + " ceil=" + _ceilOff.ToString("F2"));
        }

        static GameObject Prefab(string name)
        {
            var guids = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/HorrorEnvironment_Hospital" });
            var path = guids.Select(g => AssetDatabase.GUIDToAssetPath(g)).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name);
            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }


        static void PlaceWall(GameObject prefab, Vector3 pos, float yaw, float thick)
        {
            Place(prefab, pos, yaw);
            Vector3 n = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * thick;
            Place(prefab, pos + n, yaw + 180f);
        }

        static void Place(GameObject prefab, Vector3 pos, float yaw)
        {
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---------- surfaces ----------
        static void Floor(Rect r)
        {
            for (float x = r.x0; x < r.x1 - 0.01f; x += 4f)
                for (float z = r.z0; z < r.z1 - 0.01f; z += 4f)
                    Place(_floor, new Vector3(x + 2f, 0f, z + 2f), 0f);
        }

        static void Ceiling(Rect r)
        {
            for (float x = r.x0; x < r.x1 - 0.01f; x += 4f)
                for (float z = r.z0; z < r.z1 - 0.01f; z += 4f)
                    Place(_ceiling, new Vector3(x + 2f, 6.0f, z + 2f), 0f);
        }

        // wall along Z at fixed x, from z0..z1
        static void WallZ(float x, float z0, float z1, int side)
        {
            for (float z = z0; z < z1 - 0.01f; z += 4f)
                PlaceWall(_wall, new Vector3(x, 0f, z + 2f), 0f, 0.06f);
        }

        static void WallZSegments(float x, float z0, float z1, int side, float[] doorCenters)
        {
            for (float z = z0; z < z1 - 0.01f; z += 4f)
            {
                float mid = z + 2f;
                bool door = doorCenters != null && doorCenters.Any(d => Mathf.Abs(d - mid) < 2.1f);
                if (!door) PlaceWall(_wall, new Vector3(x, 0f, mid), 0f, 0.06f);
                else
                {
                    PlaceWall(_wallHalf, new Vector3(x, 0f, mid - 1.5f), 0f, 0.06f);
                    PlaceWall(_wallHalf, new Vector3(x, 0f, mid + 1.5f), 0f, 0.06f);
                }
            }
        }

        // wall along X at fixed z, from x0..x1 (yaw 90); hasDoor opens a 3 m gap centred on doorCenter
        static void WallX(float z, float x0, float x1, float doorCenter, bool hasDoor)
        {
            for (float x = x0; x < x1 - 0.01f; x += 4f)
            {
                float mid = x + 2f;
                bool door = hasDoor && Mathf.Abs(doorCenter - mid) < 2.2f;
                if (door)
                {
                    PlaceWall(_wallHalf, new Vector3(mid - 1.5f, 0f, z), 90f, 0.06f);
                    PlaceWall(_wallHalf, new Vector3(mid + 1.5f, 0f, z), 90f, 0.06f);
                }
                else PlaceWall(_wall, new Vector3(mid, 0f, z), 90f, 0.06f);
            }
        }

        static void Pillar(Vector3 p) { Place(_pillar, p, 0f); }


        static void ShellBox(Transform parent, Material mat, string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void BoxCol(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Col");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
        }

        static void Lamp(Vector3 p, Color c, float intensity, float range)
        {
            var go = new GameObject("Lamp");
            go.transform.SetParent(_root, false);
            go.transform.position = p;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range;
            l.shadows = LightShadows.None;
        }

        static void Spawner(string name, Vector3 pos, int count, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.position = pos;
            var s = go.AddComponent<MedusaHorror.Gameplay.MonsterSpawner>();
            s.kind = MedusaHorror.Gameplay.Monster.Kind.Stalker;
            s.targetCount = count;
            s.spawnRadius = radius;
            s.activeRadius = 40f;
            s.respawnDelay = 20f;
        }


        static void Objectives()
        {
            var spots = new (string label, Vector3 pos)[]
            {
                ("值班室钥匙", new Vector3(-10f, 1.1f, 26f)),
                ("药房阀门",   new Vector3(-10f, 1.1f, 50f)),
                ("检查室门禁卡", new Vector3(10f, 1.1f, 38f)),
                ("储物间保险丝", new Vector3(10f, 1.1f, 74f)),
                ("手术室密码本", new Vector3(0f, 1.1f, 102f)),
            };
            var root = new GameObject("W1_Objectives");
            root.transform.SetParent(_root, false);
            foreach (var s in spots)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Objective_" + s.label;
                go.transform.SetParent(root.transform, false);
                go.transform.position = s.pos;
                go.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                var col = go.GetComponent<BoxCollider>();
                col.isTrigger = true;
                var m = new Material(Shader.Find("Standard"));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.3f) * 2.4f);
                m.color = new Color(1f, 0.9f, 0.4f);
                go.GetComponent<Renderer>().sharedMaterial = m;
                var l = new GameObject("L").AddComponent<Light>();
                l.transform.SetParent(go.transform, false);
                l.type = LightType.Point; l.range = 5f; l.intensity = 2.2f; l.color = new Color(1f, 0.85f, 0.4f);
                var pk = go.AddComponent<MedusaHorror.Gameplay.W1ObjectivePickup>();
                pk.label = s.label;
            }
            log.AppendLine("objectives placed=" + spots.Length);
        }

        static void BuildStargate(Vector3 basePos)
        {
            var gate = new GameObject("W1_ExitStargate");
            gate.transform.SetParent(_root, false);
            gate.transform.position = basePos;
            // two 6 m columns + lintel + glowing curtain
            Col(gate.transform, "ColL", new Vector3(-2.6f, 3f, 0), new Vector3(0.6f, 6f, 0.6f));
            Col(gate.transform, "ColR", new Vector3(2.6f, 3f, 0), new Vector3(0.6f, 6f, 0.6f));
            Col(gate.transform, "Top", new Vector3(0f, 6.1f, 0), new Vector3(6.2f, 0.6f, 0.6f));
            var glow = new GameObject("Glow");
            glow.transform.SetParent(gate.transform, false);
            glow.transform.localPosition = new Vector3(0f, 3f, 0.1f);
            glow.transform.localScale = new Vector3(4.6f, 5.6f, 0.05f);
            var m = new Material(Shader.Find("Standard"));
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(0.45f, 0.6f, 1f) * 2.2f);
            m.color = new Color(0.25f, 0.35f, 0.75f);
            glow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            glow.AddComponent<MeshRenderer>().sharedMaterial = m;
            var l = new GameObject("GateLight").AddComponent<Light>();
            l.transform.SetParent(gate.transform, false);
            l.transform.localPosition = new Vector3(0f, 3f, 2f);
            l.type = LightType.Point; l.range = 16f; l.intensity = 4f; l.color = new Color(0.4f, 0.55f, 1f);

            var portalGo = new GameObject("W1_ExitPortal");
            portalGo.transform.SetParent(gate.transform, false);
            portalGo.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            var p = portalGo.AddComponent<MedusaHorror.Gameplay.ExitPortal>();
            p.nextSceneOverride = "W2_Wasteland";
            p.requireKey = false;
            p.requireActivation = true;
        }

        static void Col(Transform parent, string name, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var m = new Material(Shader.Find("Standard"));
            m.color = new Color(0.12f, 0.13f, 0.16f);
            m.SetFloat("_Metallic", 0.7f);
            m.SetFloat("_Glossiness", 0.55f);
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        static void CameraRig(GameObject player)
        {
            var camTarget = player.transform.Find("CameraTarget");
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<Cinemachine.CinemachineBrain>();
            var vcamGo = new GameObject("CM_W1_ThirdPerson");
            var vcam = vcamGo.AddComponent<Cinemachine.CinemachineVirtualCamera>();
            vcam.Priority = 20;
            if (camTarget != null) { vcam.Follow = camTarget; vcam.LookAt = camTarget; }
            var body = vcam.AddCinemachineComponent<Cinemachine.CinemachineFramingTransposer>();
            body.m_CameraDistance = 3.6f;
            var tci = vcamGo.AddComponent<MedusaHorror.Player.ThirdPersonCameraInput>();
            tci.input = player.GetComponent<MedusaHorror.Player.InputReader>();
            tci.yawSource = player.transform;
        }

        // ---------- walkability ----------
        static void WalkCheck()
        {
            int samples = 0, blocked = 0;
            var hitList = new List<Collider>();
            foreach (var r in walk)
            {
                for (float x = r.x0 + 1f; x <= r.x1 - 1f; x += 1.5f)
                {
                    for (float z = r.z0 + 1f; z <= r.z1 - 1f; z += 1.5f)
                    {
                        samples++;
                        Vector3 p = new Vector3(x, 0.05f, z);
                        // ground below?
                        if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, 1.2f))
                        { blocked++; continue; }
                        // capsule overlap with walls/props?
                        Vector3 a = p + Vector3.up * 0.4f;
                        Vector3 b = p + Vector3.up * 1.5f;
                        var cols = Physics.OverlapCapsule(a, b, 0.32f);
                        bool bad = false;
                        foreach (var c in cols)
                        {
                            if (c == null || c.isTrigger) continue;
                            if (c.GetComponent<MedusaHorror.Gameplay.MonsterSpawner>() != null) continue;
                            bad = true; break;
                        }
                        if (bad) blocked++;
                    }
                }
            }
            log.AppendLine("walkability: samples=" + samples + " blocked=" + blocked + " passRate=" + (samples > 0 ? (100f * (samples - blocked) / samples).ToString("F1") : "0") + "%");
        }
    }
}
