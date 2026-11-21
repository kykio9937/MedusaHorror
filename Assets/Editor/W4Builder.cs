using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W4「赛博街区」一键搭建：8m 网格街道 + 两侧高层 + 远景天际线 + 霓虹招牌 + 夜雾 + 街尾离场门。
    /// 素材来自 Assets/Dark City 2。
    /// </summary>
    public static class W4Builder
    {
        public const string ScenePath = "Assets/Scenes/W4_NeonStreet.unity";
        public const string SceneName = "W4_NeonStreet";

        const string BUILD = "Assets/Dark City 2/Assets/Prefabs/Prefabs buildings/";
        const string STREET = "Assets/Dark City 2/Assets/Prefabs/Streets/";
        const string PROPS = "Assets/Dark City 2/Assets/Prefabs/Street Props/";
        const string LIGHTS = "Assets/Dark City 2/Assets/Prefabs/Street Lights/";
        const string BG = "Assets/Dark City 2/Assets/Prefabs/Background/";
        const string VEH = "Assets/Dark City 2/Assets/Prefabs/Vehicles/";
        const string NEON = "Assets/Dark City 2/Assets/Source/Materials/Neons/";
        const string MAT = "Assets/Dark City 2/Assets/Source/Materials/";

        const float ROAD_HALF = 8f;
        const float WALK_X = 9.3f;
        const float Z0 = -52f;
        const float Z1 = 52f;

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "[W4] 播放中拒绝，请先 STOPPLAY";
            EditorSceneManager.SaveOpenScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("W4_Root").transform;
            var sb = new StringBuilder("[W4] 赛博街区\n");

            int roads = BuildRoad(root, sb);
            int walks = BuildSideWalks(root, sb);
            int builds = BuildBuildings(root, sb);
            BuildSkyline(root, sb);
            BuildNeon(root, sb);
            int props = BuildProps(root, sb);
            BuildAtmosphere(root, sb);
            var player = LevelKit.BuildPlayer(root, new Vector3(0f, 0.25f, Z0 + 8f), 0f, sb, "W4");
            LevelKit.BuildCamera(player, "CM_W4_ThirdPerson", sb, "W4");
            LevelKit.BuildExitPortal(root, "W4_ExitPortal", new Vector3(0f, 0f, Z1 - 7f), 180f,
                new Color(0.95f, 0.30f, 0.85f, 0.45f), new Color(1f, 0.35f, 0.95f),
                LevelKit.LoadMat(MAT + "Metallic/Metal_01.mat"), "", sb, "W4");
            LevelKit.BuildIntro(root, "霓虹在雨里发亮。街尾那道光，是下一段路。", 1.6f);

            sb.AppendLine("[W4] 路面=" + roads + " 人行道=" + walks + " 建筑=" + builds + " 杂物=" + props);
            sb.Append(LevelKit.CleanScene("W4"));
            LevelKit.SaveAndRegister(scene, ScenePath, "W3_Subway.unity", sb, "W4");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 路面
        static int BuildRoad(Transform root, StringBuilder sb)
        {
            var mat = LevelKit.LoadFirstMaterial(MAT + "Rough", "asphalt");
            if (mat == null) mat = LevelKit.LoadFirstMaterial(MAT + "Rough", null);
            mat = LevelKit.MakeTinted("W4_Road", mat, new Color(0.20f, 0.20f, 0.22f));

            // 碰撞地面：整片街区隐形托底板（包内路面无碰撞体），不渲染，让原包路面贴图完整露出
            LevelKit.InvisibleSlab("GroundCollider", root, new Vector3(0f, -0.35f, 0f),
                new Vector3(150f, 0.3f, 150f));

            int n = 0;
            string[] chunks = { "Street_Road_01_1", "Street_Road_01_2", "Street_Road_01_3", "Street_Road_01_4", "Street_Road_01_5", "Street_Road_01_6" };
            for (float z = Z0; z < Z1; z += 8f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    string p = STREET + chunks[Mathf.Abs(Mathf.RoundToInt(z / 8f) + (side > 0 ? 3 : 0)) % chunks.Length] + ".prefab";
                    var go = LevelKit.Place(p, new Vector3(side * 4f, 0f, z + 4f), 0f, root);
                    if (go != null) n++;
                }
            }
            sb.AppendLine("[W4] 路面块=" + n);

            // 街道两端封板：只留碰撞、不渲染，避免出现巨大的纯色挡墙
            for (int s = -1; s <= 1; s += 2)
            {
                LevelKit.InvisibleSlab("StreetCap" + (s < 0 ? "S" : "N"), root,
                    new Vector3(0f, 7f, s * 62f), new Vector3(46f, 14f, 1.5f));
            }

            // 两侧隐形挡墙：把人行道和建筑隔开，避免走进建筑缝隙被 collider 挤到屋顶
            for (int side = -1; side <= 1; side += 2)
            {
                var bound = new GameObject("SideBound" + (side < 0 ? "L" : "R"));
                bound.transform.SetParent(root, false);
                bound.transform.position = new Vector3(side * 10.9f, 3f, 0f);
                var bc = bound.AddComponent<BoxCollider>();
                bc.size = new Vector3(1.6f, 10f, 132f);
            }
            return n;
        }

        static int BuildSideWalks(Transform root, StringBuilder sb)
        {
            var mat = LevelKit.LoadFirstMaterial(MAT + "Rough", "concrete");
            mat = LevelKit.MakeTinted("W4_Walk", mat, new Color(0.30f, 0.30f, 0.32f));
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                LevelKit.Slab("SideWalk" + (side < 0 ? "L" : "R"), root,
                    new Vector3(side * (ROAD_HALF + 1.2f), 0.12f, (Z0 + Z1) * 0.5f),
                    new Vector3(2.4f, 0.24f, Z1 - Z0), mat);
                n++;
                // 路缘
                LevelKit.Slab("Curb" + (side < 0 ? "L" : "R"), root,
                    new Vector3(side * (ROAD_HALF + 0.1f), 0.13f, (Z0 + Z1) * 0.5f),
                    new Vector3(0.3f, 0.26f, Z1 - Z0), mat, false);
            }
            sb.AppendLine("[W4] 人行道=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 建筑
        static int BuildBuildings(Transform root, StringBuilder sb)
        {
            string[] facades =
            {
                "Building_01.prefab", "Building_02.prefab", "Building_03.prefab", "Building_04.prefab",
                "Building_05.prefab", "Building_05a.prefab", "Building_06.prefab", "Building_08.prefab",
                "Building_10.prefab", "Building_12.prefab", "Building_14.prefab", "Building_16.prefab"
            };
            int n = 0;
            int i = 0;
            for (float z = Z0 + 10f; z < Z1 + 6f; z += 18f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    string path = BUILD + facades[(i * 2 + (side > 0 ? 1 : 0)) % facades.Length];
                    var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (pf == null) continue;
                    var b = LevelKit.PrefabBounds(pf);
                    float depth = Mathf.Max(b.size.x, b.size.z);
                    float x = side * (ROAD_HALF + 2.6f + depth * 0.5f);
                    // 正面朝街
                    float yaw = side < 0 ? 90f : -90f;
                    var go = LevelKit.Place(path, new Vector3(x, 0f, z), yaw, root);
                    if (go != null) n++;
                }
                i++;
            }
            sb.AppendLine("[W4] 建筑=" + n);

            // 街尾封口：横跨街道的大楼，挡住尽头视线（替代纯色挡墙）
            for (int s = -1; s <= 1; s += 2)
            {
                var cap = LevelKit.Place(BUILD + (s < 0 ? "Building_10.prefab" : "Building_12.prefab"),
                    new Vector3(0f, 0f, s * 66f), s < 0 ? 0f : 180f, root);
                if (cap != null) n++;
            }
            return n;
        }

        static void BuildSkyline(Transform root, StringBuilder sb)
        {
            string[] towers =
            {
                "Skyscraper_01.prefab", "Skyscraper_02.prefab", "Skyscraper_03.prefab", "Skyscraper_04.prefab",
                "Skyscraper_05.prefab"
            };
            string[] distant =
            {
                "Skyscraper_distance_1.prefab", "Skyscraper_distance_2.prefab", "Skyscraper_distance_3.prefab",
                "Skyscraper_distance_4.prefab", "Skyscraper_distance_5.prefab", "Skyscraper_distance_6.prefab",
                "Skyscraper_distance_7.prefab", "Skyscraper_distance_8.prefab"
            };
            var rnd = new System.Random(4404);
            int n = 0;
            for (int i = 0; i < 6; i++)
            {
                float z = Z0 + 8f + i * 18f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (52f + (float)rnd.NextDouble() * 14f);
                    var go = LevelKit.Place(BG + towers[rnd.Next(0, towers.Length)],
                        new Vector3(x, 0f, z), side < 0 ? 90f : -90f, root);
                    if (go != null) n++;
                }
            }
            for (int i = 0; i < 10; i++)
            {
                float z = Z0 - 10f + i * 13f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (100f + (float)rnd.NextDouble() * 40f);
                    LevelKit.Place(BG + distant[rnd.Next(0, distant.Length)],
                        new Vector3(x, 0f, z), side < 0 ? 90f : -90f, root, true);
                    n++;
                }
            }
            sb.AppendLine("[W4] 天际线=" + n);
        }

        // ------------------------------------------------------------------ 霓虹
        static void BuildNeon(Transform root, StringBuilder sb)
        {
            string[] neonMats =
            {
                "Neon_01_Blue.mat", "Neon_03_Turquoise.mat", "Neon_08_Red.mat",
                "Neon_09_Pink.mat", "Neon_10_Violet.mat", "Neon_02_Sky.mat"
            };
            var rnd = new System.Random(7707);
            int n = 0;
            for (float z = Z0 + 14f; z < Z1 - 8f; z += 11f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rnd.NextDouble() > 0.62) continue;
                    var mat = LevelKit.LoadMat(NEON + neonMats[rnd.Next(0, neonMats.Length)]);
                    if (mat == null) continue;

                    float h = 3.2f + (float)rnd.NextDouble() * 6f;
                    var sign = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sign.name = "NeonSign";
                    sign.transform.SetParent(root, false);
                    // 贴在建筑正立面（建筑面固定在 ±(ROAD_HALF+2.6)），不再悬在街道上空
                    sign.transform.position = new Vector3(side * (ROAD_HALF + 2.35f), h, z);
                    sign.transform.localScale = new Vector3(0.25f, 1.1f + (float)rnd.NextDouble() * 1.6f, 2.6f + (float)rnd.NextDouble() * 2.4f);
                    var r = sign.GetComponent<MeshRenderer>();
                    r.sharedMaterial = mat;
                    Object.DestroyImmediate(sign.GetComponent<Collider>());

                    LevelKit.AddPointLight(root, "NeonLight_" + Mathf.RoundToInt(z) + (side > 0 ? "R" : "L"),
                        new Vector3(side * (ROAD_HALF + 1.6f), h - 0.4f, z), r.sharedMaterial.color, 1.6f, 9f);
                    n++;
                }
            }
            sb.AppendLine("[W4] 霓虹招牌=" + n);
        }

        // ------------------------------------------------------------------ 杂物
        static int BuildProps(Transform root, StringBuilder sb)
        {
            string[] props =
            {
                PROPS + "Barrel1.prefab", PROPS + "Barrel2.prefab", PROPS + "Garbage_Bag1.prefab",
                PROPS + "Garbage_Container_1A.prefab", PROPS + "Container.prefab",
                PROPS + "Street_Barrier2.prefab", PROPS + "Bench_01.prefab",
                PROPS + "Antenna_3.prefab", PROPS + "Antenna_5.prefab"
            };
            var rnd = new System.Random(9911);
            int n = 0;
            for (float z = Z0 + 8f; z < Z1 - 8f; z += 6.5f)
            {
                if (rnd.NextDouble() > 0.62) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    if (rnd.NextDouble() > 0.5) continue;
                    string p = props[rnd.Next(0, props.Length)];
                    LevelKit.Place(p, new Vector3(side * (WALK_X + 0.4f + (float)rnd.NextDouble() * 0.6f), 0.25f, z),
                        (float)(rnd.NextDouble() * 360.0), root);
                    n++;
                }
            }

            // 街上停几辆车
            LevelKit.Place(VEH + "Truck_01.prefab", new Vector3(-5.6f, 0f, -22f), 4f, root);
            LevelKit.Place(VEH + "Truck_03.prefab", new Vector3(5.4f, 0f, 16f), 186f, root);
            LevelKit.Place(VEH + "Truck_05.prefab", new Vector3(-5.2f, 0f, 38f), -8f, root);
            n += 3;

            // 路灯
            for (float z = Z0 + 12f; z < Z1; z += 24f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    LevelKit.Place(LIGHTS + "Street_Lamp1.prefab",
                        new Vector3(side * (WALK_X + 0.6f), 0.25f, z), side < 0 ? 90f : -90f, root);
                    LevelKit.AddPointLight(root, "StreetLamp_" + Mathf.RoundToInt(z) + (side > 0 ? "R" : "L"),
                        new Vector3(side * (WALK_X - 0.2f), 6.2f, z), new Color(1f, 0.78f, 0.45f), 3.1f, 16f);
                    n++;
                }
            }
            sb.AppendLine("[W4] 杂物=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 氛围
        static void BuildAtmosphere(Transform root, StringBuilder sb)
        {
            // 夜空盒：低曝光紫色
            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0f);
            sky.SetFloat("_SunSizeConvergence", 1f);
            sky.SetFloat("_AtmosphereThickness", 0.35f);
            sky.SetColor("_SkyTint", new Color(0.30f, 0.12f, 0.42f));
            sky.SetColor("_GroundColor", new Color(0.03f, 0.02f, 0.05f));
            sky.SetFloat("_Exposure", 0.55f);
            RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
            RenderSettings.fogColor = new Color(0.16f, 0.10f, 0.24f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.21f, 0.16f, 0.30f);
            RenderSettings.ambientEquatorColor = new Color(0.14f, 0.11f, 0.21f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.03f, 0.06f);

            // 冷色月光
            var lightGo = new GameObject("Moon");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.rotation = Quaternion.Euler(52f, 148f, 0f);
            var moon = lightGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.55f, 0.60f, 0.95f);
            moon.intensity = 0.90f;
            moon.shadows = LightShadows.Soft;
            sb.AppendLine("[W4] 氛围：紫夜+霓虹+月光");
        }
    }
}
