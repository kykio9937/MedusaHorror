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
        const string BILL = "Assets/Dark City 2/Assets/Prefabs/Billboards/";
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
            BuildCrossStreets(root, sb);
            int walks = BuildSideWalks(root, sb);
            int builds = BuildBuildings(root, sb);
            BuildSkyline(root, sb);
            BuildNeon(root, sb);
            BuildBillboards(root, sb);
            BuildPlayerHouse(root, sb);
            BuildShops(root, sb);
            int props = BuildProps(root, sb);
            BuildCrowd(root, sb);
            BuildWeather(root, sb);
            BuildAtmosphere(root, sb);
            var player = LevelKit.BuildPlayer(root, new Vector3(0f, 0.25f, Z0 + 8f), 0f, sb, "W4");
            LevelKit.BuildCamera(player, "CM_W4_ThirdPerson", sb, "W4");
            LevelKit.BuildExitPortal(root, "W4_ExitPortal", new Vector3(0f, 0f, Z1 - 7f), 180f,
                new Color(0.95f, 0.30f, 0.85f, 0.45f), new Color(1f, 0.35f, 0.95f),
                LevelKit.LoadMat(MAT + "Metallic/Metal_01.mat"), "", sb, "W4", true);
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
            // 顶面必须与路面贴图齐平（y=0），否则角色会站进路面里、下半身看不见
            LevelKit.InvisibleSlab("GroundCollider", root, new Vector3(0f, -0.15f, 0f),
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

            // 两侧隐形挡墙：分三段，在 z≈±26 处留出横街开口，形成可绕行的支路
            float[][] segs = { new[] { -66f, -31f }, new[] { -21f, 21f }, new[] { 31f, 66f } };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int si = 0; si < segs.Length; si++)
                {
                    float a = segs[si][0], b = segs[si][1];
                    var bound = new GameObject("SideBound" + (side < 0 ? "L" : "R") + si);
                    bound.transform.SetParent(root, false);
                    bound.transform.position = new Vector3(side * 10.9f, 3f, (a + b) * 0.5f);
                    var bc = bound.AddComponent<BoxCollider>();
                    bc.size = new Vector3(1.6f, 10f, b - a);
                }
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

        // ------------------------------------------------------------------ 横街
        static int BuildCrossStreets(Transform root, StringBuilder sb)
        {
            var mat = LevelKit.LoadFirstMaterial(MAT + "Rough", "asphalt");
            if (mat == null) mat = LevelKit.LoadFirstMaterial(MAT + "Rough", null);
            mat = LevelKit.MakeTinted("W4_Cross", mat, new Color(0.20f, 0.20f, 0.22f));

            int n = 0;
            float[] zs = { -26f, 26f };
            for (int i = 0; i < zs.Length; i++)
            {
                float z = zs[i];
                LevelKit.Slab("CrossRoad" + i, root, new Vector3(0f, 0.02f, z),
                    new Vector3(56f, 0.04f, 10f), mat);
                // 横街两侧路缘
                LevelKit.Slab("CrossCurbA" + i, root, new Vector3(0f, 0.12f, z - 5.4f),
                    new Vector3(56f, 0.24f, 1.0f), mat, false);
                LevelKit.Slab("CrossCurbB" + i, root, new Vector3(0f, 0.12f, z + 5.4f),
                    new Vector3(56f, 0.24f, 1.0f), mat, false);
                // 尽头封板，避免走到建筑缝隙里
                for (int s = -1; s <= 1; s += 2)
                    LevelKit.InvisibleSlab("CrossCap" + i + (s < 0 ? "L" : "R"), root,
                        new Vector3(s * 28f, 5f, z), new Vector3(1.5f, 10f, 10f));
                n++;
            }
            sb.AppendLine("[W4] 横街=" + n);
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
                // 横街开口处不放建筑，留出可通行空间
                if (Mathf.Abs(z + 24f) < 0.1f || Mathf.Abs(z - 30f) < 0.1f) { i++; continue; }
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

        /// <summary>真实霓虹广告牌：逐个挂在临街建筑的正立面上，避免悬空。</summary>
        static void BuildBillboards(Transform root, StringBuilder sb)
        {
            string[] boards =
            {
                "Billboard_01.prefab", "Billboard_02.prefab", "Billboard_03.prefab",
                "Billboard_04.prefab", "Billboard_05.prefab", "Billboard_06.prefab",
                "Billboard_07a.prefab", "Banner_Moving_01.prefab",
                "Banner_Moving_02.prefab", "Banner_Moving_03.prefab"
            };
            var rnd = new System.Random(9911);
            int n = 0;
            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.name.StartsWith("Building_")) continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                for (int k = 1; k < rs.Length; k++) b.Encapsulate(rs[k].bounds);

                int side = t.position.x < 0f ? -1 : 1;
                // 建筑朝街面固定在 ±(ROAD_HALF+2.6)，招牌贴着这个面往外 6cm
                float faceX = side * (ROAD_HALF + 2.6f + 0.06f);
                int count = 1 + rnd.Next(0, 2);
                for (int k = 0; k < count; k++)
                {
                    string path = BILL + boards[rnd.Next(0, boards.Length)];
                    float z = Mathf.Lerp(b.min.z + 2.5f, b.max.z - 2.5f, (float)rnd.NextDouble());
                    float h = Mathf.Lerp(5f, Mathf.Max(6f, b.max.y - 4f), (float)rnd.NextDouble());
                    var go = LevelKit.Place(path, new Vector3(faceX, h, z), side < 0 ? 90f : -90f, root, false);
                    if (go == null) continue;
                    float sc = 0.9f + (float)rnd.NextDouble() * 0.5f;
                    go.transform.localScale = new Vector3(sc, sc, sc);
                    n++;
                }
            }
            sb.AppendLine("[W4] 广告牌=" + n);
        }

        /// <summary>玩家可购置的公寓：墙体+门+室内家具，门牌可交互购买（800 信用点）。</summary>
        static void BuildPlayerHouse(Transform root, StringBuilder sb)
        {
            const float hx = -12.5f, hz = 3f;
            const float w = 5.5f, d = 7f, h = 3.2f;

            var house = new GameObject("PlayerHouse");
            house.transform.SetParent(root, false);
            house.transform.position = new Vector3(hx, 0f, hz);

            var wallMat = LevelKit.LoadMat(MAT + "Metallic/Metal_01.mat");
            if (wallMat == null) wallMat = LevelKit.LoadMat(MAT + "Concrete/Concrete_01.mat");
            var floorMat = LevelKit.LoadMat(MAT + "Metallic/Metal_02.mat");
            if (floorMat == null) floorMat = wallMat;

            // 地板 / 天花板
            LevelKit.Slab("HouseFloor", house.transform, new Vector3(hx, 0.05f, hz),
                new Vector3(w, 0.1f, d), floorMat);
            LevelKit.Slab("HouseCeiling", house.transform, new Vector3(hx, h, hz),
                new Vector3(w, 0.12f, d), wallMat);

            // 西 / 北 / 南 三面墙
            LevelKit.Slab("HouseWallW", house.transform, new Vector3(hx - w * 0.5f, h * 0.5f, hz),
                new Vector3(0.2f, h, d), wallMat);
            LevelKit.Slab("HouseWallN", house.transform, new Vector3(hx, h * 0.5f, hz + d * 0.5f),
                new Vector3(w, h, 0.2f), wallMat);
            LevelKit.Slab("HouseWallS", house.transform, new Vector3(hx, h * 0.5f, hz - d * 0.5f),
                new Vector3(w, h, 0.2f), wallMat);

            // 东墙（朝街）：中间留 2m 门洞
            LevelKit.Slab("HouseWallE1", house.transform, new Vector3(hx + w * 0.5f, h * 0.5f, hz + 2.2f),
                new Vector3(0.2f, h, 2.4f), wallMat);
            LevelKit.Slab("HouseWallE2", house.transform, new Vector3(hx + w * 0.5f, h * 0.5f, hz - 2.2f),
                new Vector3(0.2f, h, 2.4f), wallMat);
            LevelKit.Slab("HouseLintel", house.transform, new Vector3(hx + w * 0.5f, 2.85f, hz),
                new Vector3(0.2f, 0.7f, 2.2f), wallMat);

            // 门扇（锁着，购置后解锁）
            var doorGo = LevelKit.Slab("HouseDoor", house.transform,
                new Vector3(hx + w * 0.5f, 1.15f, hz), new Vector3(0.14f, 2.3f, 2.0f), wallMat);
            var door = doorGo.AddComponent<MedusaHorror.Gameplay.DoorInteractable>();
            door.closedHint = "开门";
            door.openedHint = "关门";
            door.lockedHint = "门锁着 · 先去门牌购置";
            door.openAngle = 88f;
            door.Lock();

            // 门牌：可交互购买
            var plaque = LevelKit.Slab("HousePlaque", house.transform,
                new Vector3(hx + w * 0.5f + 0.14f, 1.75f, hz + 1.55f), new Vector3(0.08f, 0.55f, 0.85f), wallMat);
            var pr = plaque.GetComponent<MeshRenderer>();
            if (pr != null)
            {
                var pm = new Material(Shader.Find("Standard"));
                pm.color = new Color(1f, 0.72f, 0.25f);
                pm.EnableKeyword("_EMISSION");
                pm.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.15f) * 2.4f);
                pr.sharedMaterial = pm;
            }
            var hp = plaque.AddComponent<MedusaHorror.Gameplay.HousePurchase>();
            hp.houseName = "霓虹公寓 7B";
            hp.houseId = "W4_Apartment_7B";
            hp.price = 800;
            hp.door = door;

            // 室内陈设
            LevelKit.Slab("HouseBed", house.transform, new Vector3(hx - 1.6f, 0.35f, hz - 2.0f),
                new Vector3(1.8f, 0.5f, 2.2f), wallMat);
            LevelKit.Slab("HouseBedPillow", house.transform, new Vector3(hx - 1.6f, 0.68f, hz - 2.85f),
                new Vector3(1.5f, 0.16f, 0.5f), floorMat);
            LevelKit.Slab("HouseDesk", house.transform, new Vector3(hx + 0.9f, 0.4f, hz + 2.3f),
                new Vector3(1.3f, 0.75f, 1.3f), wallMat);
            LevelKit.Slab("HouseStool", house.transform, new Vector3(hx + 0.9f, 0.25f, hz + 1.4f),
                new Vector3(0.6f, 0.45f, 0.6f), wallMat);

            LevelKit.AddPointLight(house.transform, "HouseLamp",
                new Vector3(hx, 2.7f, hz), new Color(1f, 0.78f, 0.5f), 1.9f, 8f);

            // 门牌上方的小灯，提示这是可买的房子
            LevelKit.AddPointLight(house.transform, "PlaqueLight",
                new Vector3(hx + w * 0.5f + 0.5f, 2.2f, hz + 1.55f), new Color(1f, 0.65f, 0.2f), 1.4f, 5f);

            // 房屋功能化：存档点 / 仓库 / 家具购买点 / 租客任务
            var bedT = house.transform.Find("HouseBed");
            var savePoint = (bedT != null ? bedT.gameObject : house.gameObject).AddComponent<MedusaHorror.Gameplay.HouseSavePoint>();
            savePoint.houseId = "W4_Apartment_7B";

            // 自由布置家具：屋内按 B 打开建造菜单
            var buildMode = house.AddComponent<MedusaHorror.Gameplay.HouseBuildMode>();
            buildMode.houseId = "W4_Apartment_7B";
            buildMode.houseCenter = house.transform.Find("HouseFloor").GetComponent<Collider>().bounds.center;
            buildMode.houseCenter += Vector3.up * 0.05f;
            buildMode.houseRadius = Mathf.Min(w, d) * 0.5f;

            var chest = LevelKit.Slab("HouseChest", house.transform, new Vector3(hx - 2.0f, 0.35f, hz + 2.6f),
                new Vector3(0.9f, 0.7f, 0.6f), floorMat);
            var storage = chest.AddComponent<MedusaHorror.Gameplay.HouseStorage>();
            storage.houseId = "W4_Apartment_7B";

            var sofa = LevelKit.Slab("HouseSofa", house.transform, new Vector3(hx + 1.5f, 0.42f, hz - 1.4f),
                new Vector3(2.2f, 0.8f, 0.9f), floorMat);
            sofa.SetActive(false);
            var sofaShop = LevelKit.Slab("HouseSofaShop", house.transform, new Vector3(hx + 1.5f, 0.3f, hz - 1.4f),
                new Vector3(0.5f, 0.6f, 0.5f), wallMat);
            var fs = sofaShop.AddComponent<MedusaHorror.Gameplay.FurnitureShop>();
            fs.houseId = "W4_Apartment_7B";
            fs.furnitureId = "sofa";
            fs.furnitureName = "沙发";
            fs.price = 150;
            fs.furnitureObject = sofa;

            var shelf = LevelKit.Slab("HouseShelf", house.transform, new Vector3(hx - 2.0f, 1.0f, hz + 1.2f),
                new Vector3(0.5f, 1.9f, 1.6f), wallMat);
            shelf.SetActive(false);
            var shelfShop = LevelKit.Slab("HouseShelfShop", house.transform, new Vector3(hx - 2.0f, 0.3f, hz + 1.2f),
                new Vector3(0.5f, 0.6f, 0.5f), wallMat);
            var fs2 = shelfShop.AddComponent<MedusaHorror.Gameplay.FurnitureShop>();
            fs2.houseId = "W4_Apartment_7B";
            fs2.furnitureId = "shelf";
            fs2.furnitureName = "书架";
            fs2.price = 200;
            fs2.furnitureObject = shelf;

            var npcPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Kazuko/Prefab/Kazuko.prefab");
            if (npcPf != null)
            {
                var npcGo = (GameObject)PrefabUtility.InstantiatePrefab(npcPf, house.transform);
                npcGo.name = "HouseTenant";
                npcGo.transform.position = new Vector3(hx + 0.2f, 0.3f, hz - 2.4f);
                npcGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                var nq = npcGo.AddComponent<MedusaHorror.Gameplay.NpcQuestGiver>();
                nq.def = new MedusaHorror.Gameplay.QuestDef
                {
                    id = "w4_home_light", npcName = "租客", title = "修好屋里的灯",
                    desc = "灯坏了，帮我找 2 个零件回来。", kind = MedusaHorror.Gameplay.QuestKind.Collect,
                    targetTag = "house_part", need = 2, rewardCoins = 300, chain = "home"
                };
            }

            for (int i = 0; i < 2; i++)
            {
                var part = LevelKit.Slab("HousePart" + i, house.transform,
                    new Vector3(hx - 1.5f + i * 2.4f, 0.35f, hz + 1.6f),
                    new Vector3(0.28f, 0.28f, 0.28f), floorMat, false);
                var qp = part.AddComponent<MedusaHorror.Gameplay.QuestPickup>();
                qp.itemTag = "house_part";
                qp.itemName = "旧零件";
            }

            // 快速旅行终端（门内侧）
            var ftGo = LevelKit.Slab("FastTravelPoint", house.transform,
                new Vector3(hx + 2.0f, 0.85f, hz + 0.9f),
                new Vector3(0.34f, 1.0f, 0.34f), floorMat);
            ftGo.AddComponent<MedusaHorror.Gameplay.FastTravelPoint>();

            sb.AppendLine("[W4] 可购公寓 pos=(" + hx + "," + hz + ") 价格=800");
        }

        /// <summary>街边两家店：武器强化店与义体店。</summary>
        static void BuildShops(Transform root, StringBuilder sb)
        {
            var mat = LevelKit.LoadMat(MAT + "Metallic/Metal_01.mat");
            if (mat == null) mat = LevelKit.LoadFirstMaterial(MAT + "Rough", null);

            var weapon = LevelKit.Slab("WeaponShop", root, new Vector3(-9.0f, 0.5f, -18f),
                new Vector3(1.6f, 1.0f, 2.4f), mat);
            var ws = weapon.AddComponent<MedusaHorror.Gameplay.ShopPoint>();
            ws.kind = MedusaHorror.Gameplay.ShopPoint.ShopKind.Weapon;
            ws.shopName = "黑刃工坊";

            var cyber = LevelKit.Slab("CyberShop", root, new Vector3(9.0f, 0.5f, 2f),
                new Vector3(1.6f, 1.0f, 2.4f), mat);
            var cs = cyber.AddComponent<MedusaHorror.Gameplay.ShopPoint>();
            cs.kind = MedusaHorror.Gameplay.ShopPoint.ShopKind.Cyber;
            cs.shopName = "义体门诊";

            sb.AppendLine("[W4] 商店：黑刃工坊(-9,-18) 义体门诊(9,2)");
        }

        /// <summary>街上走动的行人：沿人行道来回巡逻。</summary>
        static void BuildCrowd(Transform root, StringBuilder sb)
        {
            var npcPf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Suriyun/Kazuko/Prefab/Kazuko.prefab");
            if (npcPf == null) { sb.AppendLine("[W4] 人群：未找到 Kazuko 预制体"); return; }

            float[] zs = { -38f, -26f, -12f, 0f, 10f, 22f, 34f, 42f };
            int n = 0;
            for (int i = 0; i < zs.Length; i++)
            {
                float side = (i % 2 == 0) ? -1f : 1f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(npcPf, root);
                go.name = "CrowdWalker" + i;
                float x = side * 9.2f;
                go.transform.position = new Vector3(x, 0.3f, zs[i]);
                go.transform.rotation = Quaternion.Euler(0f, side > 0f ? 180f : 0f, 0f);
                var cw = go.AddComponent<MedusaHorror.Gameplay.CrowdWalker>();
                cw.pointA = new Vector3(x, 0.3f, zs[i] - 6f);
                cw.pointB = new Vector3(x, 0.3f, zs[i] + 6f);
                cw.speed = 1.2f + (i % 3) * 0.2f;
                n++;
            }
            sb.AppendLine("[W4] 人群=" + n);
        }

        /// <summary>霓虹雨：跟随玩家的雨幕与地面水花。</summary>
        static void BuildWeather(Transform root, StringBuilder sb)
        {
            var go = new GameObject("NeonRain");
            go.transform.SetParent(root, false);
            go.AddComponent<MedusaHorror.Gameplay.NeonRain>();
            sb.AppendLine("[W4] 天气：霓虹雨");
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
