using System.Collections.Generic;
using System.Text;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W2「城市废墟街道」一键搭建：
    /// 长街（道路 + 人行道）→ 两侧废墟建筑 → 街道杂物 → 黄昏氛围 → 玩家/相机 → 街尾离场门。
    /// 素材全部来自 Assets/ApocalypticWasteland（Wasteland 包）。
    /// </summary>
    public static class W2Builder
    {
        public const string ScenePath = "Assets/Scenes/W2_Wasteland.unity";
        public const string SceneName = "W2_Wasteland";

        const string URBAN = "Assets/ApocalypticWasteland/UrbanAssets/Prefabs/";
        const string BUILD = "Assets/ApocalypticWasteland/UrbanAssets/Prefabs/Buildings/";
        const string PLAYER_PREFAB = "Assets/Prefabs/Kazuko_W1_Player.prefab";

        const float STREET_HALF = 4.1f;   // 路面半宽（RoadChunLong1 宽 8.2）
        const float WALK_X = 5.5f;        // 人行道中心
        const float Z0 = -80f;
        const float Z1 = 80f;

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "[W2] 播放中拒绝，请先 STOPPLAY";
            EditorSceneManager.SaveOpenScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("W2_Root").transform;
            var sb = new StringBuilder("[W2] 城市废墟街道\n");

            int roads = BuildRoad(root, sb);
            int walks = BuildSideWalks(root, sb);
            int builds = BuildBuildings(root, sb);
            int props = BuildProps(root, sb);
            BuildAtmosphere(root, sb);
            var player = BuildPlayer(root, sb);
            BuildCamera(player, sb);
            BuildExit(root, sb);
            BuildIntro(root);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                sb.AppendLine("[W2] 场景保存失败");
                return sb.ToString();
            }

            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == ScenePath);
            int insertAt = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].path.EndsWith("W1_Hospital.unity")) insertAt = i + 1;
            list.Insert(insertAt, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();

            sb.AppendLine("[W2] 保存完成 road=" + roads + " walk=" + walks + " build=" + builds + " prop=" + props);
            sb.AppendLine("[W2] BuildSettings=" + DescribeBuildSettings());
            return sb.ToString();
        }

        static string DescribeBuildSettings()
        {
            var sb = new StringBuilder();
            var list = EditorBuildSettings.scenes;
            for (int i = 0; i < list.Length; i++)
                sb.Append(i).Append(':').Append(System.IO.Path.GetFileNameWithoutExtension(list[i].path)).Append(' ');
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------ 道路
        static int BuildRoad(Transform root, StringBuilder sb)
        {
            int n = 0;
            float z = Z0;
            int idx = 0;
            while (z < Z1)
            {
                var go = Place(URBAN + "RoadChunLong1.prefab", new Vector3(0f, 0f, z + 22f), 0f, root);
                if (go == null) { z += 40f; continue; }
                n++;
                z += 43.9f;
                idx++;
            }
            // 街口与街尾的十字路口块
            Place(URBAN + "RoadChunkT.prefab", new Vector3(0f, 0f, Z0 - 6f), 0f, root);
            Place(URBAN + "RoadChunkT.prefab", new Vector3(0f, 0f, Z1 + 6f), 180f, root);
            sb.AppendLine("[W2] 道路段=" + n);
            return n;
        }

        static int BuildSideWalks(Transform root, StringBuilder sb)
        {
            // 长条人行道用程序化薄板（性能友好），上面再点缀包内 SideWalk 块
            var mat = LoadMat("Assets/ApocalypticWasteland/UrbanAssets/Materials/BetonBase_ALBEDO.mat");
            if (mat == null) mat = LoadMat("Assets/ApocalypticWasteland/UrbanAssets/Materials/Roads_Albedo.mat");
            mat = MakeTinted("W2_SideWalk", mat, new Color(0.42f, 0.40f, 0.37f));
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "SideWalk" + (side < 0 ? "L" : "R");
                slab.transform.SetParent(root, false);
                slab.transform.position = new Vector3(side * WALK_X, 0.10f, (Z0 + Z1) * 0.5f);
                slab.transform.localScale = new Vector3(2.4f, 0.20f, Z1 - Z0);
                ApplyMat(slab, mat);
                n++;
            }
            // 包内人行道砖点缀（左右各若干段）
            float z = Z0 + 4f;
            int k = 0;
            while (z < Z1 - 4f)
            {
                float x = (k % 2 == 0) ? -WALK_X : WALK_X;
                Place(URBAN + (k % 3 == 0 ? "SideWalk2.prefab" : "SideWalk1.prefab"), new Vector3(x, 0.2f, z), 0f, root, false);
                z += 9f;
                k++;
                n++;
            }
            sb.AppendLine("[W2] 人行道段=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 建筑
        static int BuildBuildings(Transform root, StringBuilder sb)
        {
            string[] facades =
            {
                BUILD + "SuBurbProperty.prefab",
                BUILD + "SubUrbanProperty2.prefab",
                BUILD + "SubUrbanProperty3.prefab",
                BUILD + "SuBurbProperty5.prefab",
                BUILD + "SuBurbProperty6.prefab",
                BUILD + "SubUrbRuinedProperty1.prefab",
                BUILD + "MetalSlum1.prefab",
                BUILD + "MetalSlum2.prefab",
                BUILD + "MetalSlum3.prefab",
                BUILD + "GasStation.prefab",
                BUILD + "Diner.prefab"
            };
            int n = 0;
            float z = Z0 + 12f;
            int i = 0;
            while (z < Z1 - 10f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    string path = facades[(i * 2 + (side > 0 ? 1 : 0)) % facades.Length];
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (pf == null) continue;
                    var b = PrefabBounds(pf);
                    float depth = Mathf.Max(6f, b.size.x);
                    float x = side * (WALK_X + 2.4f + depth * 0.5f);
                    // 建筑正面朝街：左侧朝 +X（yaw 90），右侧朝 -X（yaw -90）
                    float yaw = side < 0 ? 90f : -90f;
                    var go = Place(path, new Vector3(x, 0f, z), yaw, root);
                    if (go != null) n++;
                }
                z += 26f;
                i++;
            }
            sb.AppendLine("[W2] 建筑=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 杂物
        static int BuildProps(Transform root, StringBuilder sb)
        {
            string[] props =
            {
                URBAN + "Car1.prefab", URBAN + "Car2.prefab", URBAN + "Car3.prefab",
                URBAN + "Barrel1.prefab", URBAN + "Barrel2.prefab", URBAN + "Barrel3.prefab",
                URBAN + "TrashCan1.prefab", URBAN + "TrashBasket1.prefab",
                URBAN + "MailBox1.prefab", URBAN + "FireHydrant.prefab",
                URBAN + "MetalFance.prefab", URBAN + "NewsPaperBox1.prefab",
                URBAN + "WoodPlank1.prefab", URBAN + "Tire1.prefab"
            };
            var rnd = new System.Random(20261121);
            int n = 0;
            for (float z = Z0 + 8f; z < Z1 - 6f; z += 7.5f)
            {
                if (rnd.NextDouble() > 0.72) continue;
                int pick = rnd.Next(0, props.Length);
                float x = (float)(rnd.NextDouble() * 7.4 - 3.7);
                float yaw = (float)(rnd.NextDouble() * 360.0);
                var go = Place(props[pick], new Vector3(x, 0.2f, z + (float)(rnd.NextDouble() * 4.0)), yaw, root);
                if (go != null) n++;
            }
            // 街心几辆翻覆的车
            Place(URBAN + "Car1.prefab", new Vector3(-2.2f, 0.2f, -20f), 28f, root);
            Place(URBAN + "Car3.prefab", new Vector3(2.6f, 0.2f, 18f), -142f, root);
            Place(URBAN + "Car2.prefab", new Vector3(-1.4f, 0.2f, 46f), 76f, root);
            n += 3;
            sb.AppendLine("[W2] 杂物=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 氛围
        static void BuildAtmosphere(Transform root, StringBuilder sb)
        {
            // 地面大底板（防止走到道路外掉出世界）
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "GroundPlate";
            ground.transform.SetParent(root, false);
            ground.transform.position = new Vector3(0f, -0.35f, (Z0 + Z1) * 0.5f);
            ground.transform.localScale = new Vector3(220f, 0.6f, Z1 - Z0 + 120f);
            var gmat = LoadMat("Assets/ApocalypticWasteland/UrbanAssets/Materials/BetonBase_ALBEDO.mat");
            if (gmat == null) gmat = LoadMat("Assets/ApocalypticWasteland/UrbanAssets/Materials/Roads_Albedo.mat");
            gmat = MakeTinted("W2_Ground", gmat, new Color(0.34f, 0.31f, 0.28f));
            ApplyMat(ground, gmat);

            // 天空盒
            var sky = LoadFirstMaterial("Assets/ApocalypticWasteland/Skyboxes", null);
            if (sky != null) RenderSettings.skybox = sky;

            // 黄昏主光
            var lightGo = new GameObject("Sun_Dusk");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.rotation = Quaternion.Euler(16f, -38f, 0f);
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.62f, 0.36f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;

            // 雾：沙尘感
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.011f;
            RenderSettings.fogColor = new Color(0.42f, 0.33f, 0.26f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.26f, 0.24f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.19f, 0.17f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.09f, 0.08f);

            // 街灯残火
            for (float z = Z0 + 20f; z < Z1; z += 30f)
            {
                var fire = new GameObject("StreetFire_" + Mathf.RoundToInt(z));
                fire.transform.SetParent(root, false);
                fire.transform.position = new Vector3(3.6f, 1.4f, z);
                var fl = fire.AddComponent<Light>();
                fl.type = LightType.Point;
                fl.color = new Color(1f, 0.55f, 0.22f);
                fl.intensity = 2.4f;
                fl.range = 9f;
            }
            sb.AppendLine("[W2] 氛围：雾+黄昏光+街火 skybox=" + (sky != null ? sky.name : "none"));
        }

        // ------------------------------------------------------------------ 玩家
        static GameObject BuildPlayer(Transform root, StringBuilder sb)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB);
            if (pf == null) { sb.AppendLine("[W2] 玩家预制体缺失"); return null; }

            var player = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            player.name = "Kazuko_W1_Player";
            player.transform.SetParent(root, false);
            player.transform.position = new Vector3(0f, 0.25f, Z0 + 10f);
            player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            if (player.GetComponent<CharacterController>() == null)
            {
                var cc = player.AddComponent<CharacterController>();
                cc.height = 1.7f;
                cc.radius = 0.34f;
                cc.center = new Vector3(0f, 0.85f, 0f);
                cc.slopeLimit = 50f;
                cc.stepOffset = 0.35f;
            }
            if (player.GetComponent<InputReader>() == null) player.AddComponent<InputReader>();
            if (player.GetComponent<ThirdPersonController>() == null) player.AddComponent<ThirdPersonController>();
            if (player.GetComponent<PlayerInteractor>() == null) player.AddComponent<PlayerInteractor>();
            if (player.GetComponent<PlayerPetrify>() == null) player.AddComponent<PlayerPetrify>();

            sb.AppendLine("[W2] 玩家就位 pos=" + player.transform.position.ToString("F1"));
            return player;
        }

        static void BuildCamera(GameObject player, StringBuilder sb)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.1f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<CinemachineBrain>();
            camGo.transform.position = new Vector3(0f, 2f, Z0 + 4f);
            camGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

            if (player == null) { sb.AppendLine("[W2] 无玩家，跳过虚拟相机"); return; }

            Transform target = player.transform.Find("CameraTarget");
            if (target == null) target = player.transform;

            var vcamGo = new GameObject("CM_W2_ThirdPerson");
            var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
            vcam.Priority = 10;
            vcam.Follow = target;
            vcam.LookAt = target;

            var body = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            body.m_CameraDistance = 3.6f;
            body.m_ScreenX = 0.5f;
            body.m_ScreenY = 0.58f;
            vcam.m_Lens.FieldOfView = 55f;
            vcam.m_Lens.NearClipPlane = 0.1f;

            var input = vcamGo.AddComponent<ThirdPersonCameraInput>();
            input.input = player.GetComponent<InputReader>();
            input.yawSource = player.transform;
            input.initialPitch = 12f;
            input.minPitch = -25f;
            input.maxPitch = 55f;

            var col = vcamGo.AddComponent<CinemachineCollider>();
            col.m_CollideAgainst = LayerMask.GetMask("Default");
            col.m_MinimumDistanceFromTarget = 0.6f;
            col.m_Strategy = CinemachineCollider.ResolutionStrategy.PullCameraForward;
            col.m_CameraRadius = 0.2f;

            vcamGo.transform.position = new Vector3(0f, 2f, Z0 + 4f);
            vcamGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            sb.AppendLine("[W2] 相机 + 第三人称虚拟相机就位");
        }

        // ------------------------------------------------------------------ 出口
        static void BuildExit(Transform root, StringBuilder sb)
        {
            var exitRoot = new GameObject("W2_ExitPortal");
            exitRoot.transform.SetParent(root, false);
            exitRoot.transform.position = new Vector3(0f, 0f, Z1 - 8f);
            exitRoot.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var frameMat = LoadMat("Assets/ApocalypticWasteland/UrbanAssets/Materials/Metal_Albedo.mat");
            if (frameMat == null) frameMat = LoadMat("Assets/ApocalypticWasteland/UrbanAssets/Materials/MetalPlates_Albedo.mat");

            // 门框：两柱 + 横梁
            var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
            left.name = "PortalPillar_L";
            left.transform.SetParent(exitRoot.transform, false);
            left.transform.localPosition = new Vector3(-1.55f, 1.6f, 0f);
            left.transform.localScale = new Vector3(0.35f, 3.2f, 0.35f);
            ApplyMat(left, frameMat);

            var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
            right.name = "PortalPillar_R";
            right.transform.SetParent(exitRoot.transform, false);
            right.transform.localPosition = new Vector3(1.55f, 1.6f, 0f);
            right.transform.localScale = new Vector3(0.35f, 3.2f, 0.35f);
            ApplyMat(right, frameMat);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "PortalBeam";
            beam.transform.SetParent(exitRoot.transform, false);
            beam.transform.localPosition = new Vector3(0f, 3.35f, 0f);
            beam.transform.localScale = new Vector3(3.45f, 0.35f, 0.4f);
            ApplyMat(beam, frameMat);

            // 光幕
            var fx = new GameObject("ExitPortal_FX");
            fx.transform.SetParent(exitRoot.transform, false);
            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            veil.name = "Veil";
            veil.transform.SetParent(fx.transform, false);
            veil.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            veil.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            veil.transform.localScale = new Vector3(2.7f, 3.1f, 1f);
            var veilRend = veil.GetComponent<MeshRenderer>();
            var veilMat = new Material(Shader.Find("Standard"));
            veilMat.color = new Color(0.25f, 0.85f, 0.95f, 0.45f);
            veilMat.SetFloat("_Mode", 3f);
            veilMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            veilMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            veilMat.SetInt("_ZWrite", 0);
            veilMat.DisableKeyword("_ALPHATEST_ON");
            veilMat.EnableKeyword("_ALPHABLEND_ON");
            veilMat.renderQueue = 3000;
            veilRend.sharedMaterial = veilMat;
            Object.DestroyImmediate(veil.GetComponent<Collider>());

            var glow = new GameObject("PortalGlow");
            glow.transform.SetParent(fx.transform, false);
            glow.transform.localPosition = new Vector3(0f, 1.6f, -0.4f);
            var gl = glow.AddComponent<Light>();
            gl.type = LightType.Point;
            gl.color = new Color(0.35f, 0.9f, 1f);
            gl.intensity = 3.2f;
            gl.range = 10f;

            var portal = exitRoot.AddComponent<ExitPortal>();
            portal.door = null;
            portal.fxRoot = fx;
            portal.requireKey = false;
            portal.halfWidth = 1.45f;
            portal.halfDepth = 1.2f;
            portal.lowerY = -0.3f;
            portal.upperY = 3.2f;

            sb.AppendLine("[W2] 出口门洞 pos=" + exitRoot.transform.position.ToString("F1"));
        }

        // ------------------------------------------------------------------ 工具
        static void BuildIntro(Transform root)
        {
            var go = new GameObject("LevelIntro");
            go.transform.SetParent(root, false);
            var li = go.AddComponent<MedusaHorror.Core.LevelIntro>();
            li.toast = "风里有铁锈味。沿着废墟街道往北，尽头有出口。";
            li.delay = 1.6f;
        }
        static GameObject Place(string prefabPath, Vector3 pos, float yaw, Transform parent, bool snapGround = true)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = pos;
            if (snapGround)
            {
                Bounds b = BoundsOf(go);
                go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
            }
            return go;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static Bounds PrefabBounds(GameObject pf)
        {
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            var b = BoundsOf(tmp);
            Object.DestroyImmediate(tmp);
            return b;
        }

        static void ApplyMat(GameObject go, Material mat)
        {
            if (go == null || mat == null) return;
            var r = go.GetComponent<MeshRenderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        /// <summary>基于包内材质派生一份可调色的实例材质并落盘，避免改动原素材。</summary>
        static Material MakeTinted(string name, Material src, Color tint)
        {
            if (src == null) return null;
            string path = "Assets/Materials/" + name + ".mat";
            var exist = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (exist != null) return exist;
            var m = new Material(src);
            m.name = name;
            m.color = tint;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material LoadMat(string path)        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) Debug.LogWarning("[W2] 材质缺失: " + path);
            return m;
        }

        static Material LoadFirstMaterial(string dir, string keyword)
        {
            if (!AssetDatabase.IsValidFolder(dir)) return null;
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (keyword != null && p.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) return m;
            }
            return null;
        }
    }
}
