using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MedusaHorror.Gameplay;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W3「地下铁」一键搭建：4m 模块化站台走廊 + 荧光灯 + 铁轨隧道口 + 站尾离场门。
    /// 素材来自 Assets/Dark City 2/Subway。
    /// </summary>
    public static class W3Builder
    {
        public const string ScenePath = "Assets/Scenes/W3_Subway.unity";
        public const string SceneName = "W3_Subway";

        const string SUB = "Assets/Dark City 2/Subway/Prefabs/";
        const string MAT = "Assets/Dark City 2/Subway/Source/Models/Materials/";

        const float HALF_W = 4.4f;    // 走廊半宽
        const float CEIL_Y = 4.2f;    // 天花板底面高度
        const float Z0 = -34f;
        const float Z1 = 34f;

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "[W3] 播放中拒绝，请先 STOPPLAY";
            EditorSceneManager.SaveOpenScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("W3_Root").transform;
            var sb = new StringBuilder("[W3] 地下铁\n");

            var mats = new Mats();
            int floor = BuildFloor(root, mats, sb);
            int walls = BuildWalls(root, mats, sb);
            BuildCeiling(root, mats, sb);
            BuildSideCorridor(root, mats, sb);
            BuildPillarsAndProps(root, mats, sb);
            BuildTunnelEnds(root, mats, sb);
            BuildLighting(root, sb);
            // 出生点避开南端隧道口铁轨（铁轨顶面 0.64m，站上去会把人托起来）
            BuildEncounters(root, sb);
            BuildTrainDoor(root, mats, sb);
            var player = LevelKit.BuildPlayer(root, new Vector3(0f, 0.25f, Z0 + 10f), 0f, sb, "W3");
            LevelKit.BuildCamera(player, "CM_W3_ThirdPerson", sb, "W3");
            // 地铁出口：需要先点亮，且要买票（200 信用点）才能乘车去霓虹街区
            // 出口不再用传送门：站台东侧的列车门就是唯一出口（200 信用点上车，到站自动进 W4）
            LevelKit.BuildIntro(root, "地下的灯还在闪。走到站台东侧，刷信用点登上列车。", 1.6f);

            sb.AppendLine("[W3] 地砖=" + floor + " 墙段=" + walls);
            LevelKit.SaveAndRegister(scene, ScenePath, "W2_Wasteland.unity", sb, "W3");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 列车车门

        static void BuildTrainDoor(Transform root, Mats m, StringBuilder sb)
        {
            var doorRoot = new GameObject("W3_TrainDoor");
            doorRoot.transform.SetParent(root, false);
            Vector3 at = new Vector3(HALF_W - 1.1f, 0f, 6f);
            doorRoot.transform.position = at;

            // 门框（发光）
            var frameMat = new Material(Shader.Find("Standard"));
            frameMat.color = new Color(0.15f, 0.55f, 0.62f);
            frameMat.EnableKeyword("_EMISSION");
            frameMat.SetColor("_EmissionColor", new Color(0.15f, 0.7f, 0.8f) * 2.2f);

            LevelKit.Slab("TrainDoorLeft", doorRoot.transform, new Vector3(-0.85f, 1.3f, 0f),
                new Vector3(0.12f, 2.6f, 0.2f), frameMat);
            LevelKit.Slab("TrainDoorRight", doorRoot.transform, new Vector3(0.85f, 1.3f, 0f),
                new Vector3(0.12f, 2.6f, 0.2f), frameMat);
            LevelKit.Slab("TrainDoorTop", doorRoot.transform, new Vector3(0f, 2.62f, 0f),
                new Vector3(1.82f, 0.12f, 0.2f), frameMat);
            LevelKit.Slab("TrainDoorGlow", doorRoot.transform, new Vector3(0f, 1.3f, 0.08f),
                new Vector3(1.6f, 2.5f, 0.04f), frameMat, false);

            var col = doorRoot.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 1.3f, 0f);
            col.size = new Vector3(1.8f, 2.6f, 0.9f);

            var bp = doorRoot.AddComponent<TrainBoardPoint>();
            bp.stationReturn = new Vector3(HALF_W - 1.9f, 0.3f, 6f);

            sb.AppendLine("[W3] 列车车门已放置");
        }

        class Mats
        {
            public Material Floor, Wall, WallPanel, Ceil, Metal, GlowBlue;
            public Mats()
            {
                Floor = LevelKit.MakeTinted("W3_Floor", LevelKit.LoadMat(MAT + "Tiles_Subway_a.mat"), new Color(0.55f, 0.56f, 0.55f));
                Wall = LevelKit.MakeTinted("W3_Wall", LevelKit.LoadMat(MAT + "Concrete_Panels9_a.mat"), new Color(0.62f, 0.62f, 0.60f));
                WallPanel = LevelKit.LoadMat(MAT + "Subway_wall_panel1.mat");
                if (WallPanel == null) WallPanel = LevelKit.LoadMat(MAT + "Subway_panel1_a.mat");
                Ceil = LevelKit.MakeTinted("W3_Ceil", LevelKit.LoadMat(MAT + "Subway_panel3_a.mat"), new Color(0.46f, 0.47f, 0.47f));
                Metal = LevelKit.LoadMat(MAT + "Metal_Panel8_a.mat");
                if (Metal == null) Metal = LevelKit.LoadMat(MAT + "Subway_Rails_a.mat");
                GlowBlue = LevelKit.LoadMat(MAT + "Solid_Glow_blue.mat");
            }
        }

        // ------------------------------------------------------------------ 地面
        static int BuildFloor(Transform root, Mats m, StringBuilder sb)
        {
            LevelKit.Slab("FloorSlab", root,
                new Vector3(0f, -0.2f, (Z0 + Z1) * 0.5f),
                new Vector3(HALF_W * 2f + 1.6f, 0.4f, Z1 - Z0 + 6f), m.Floor);

            // 兜底大地面：万一从墙缝/端头挤出去，也不会无限坠落
            LevelKit.Slab("FallGuard", root, new Vector3(0f, -1.8f, 0f),
                new Vector3(200f, 0.4f, 200f), m.Floor);

            // 站台边缘黄色警示线
            for (int side = -1; side <= 1; side += 2)
            {
                var warn = LevelKit.Slab("WarnLine" + (side < 0 ? "L" : "R"), root,
                    new Vector3(side * (HALF_W - 0.35f), 0.012f, (Z0 + Z1) * 0.5f),
                    new Vector3(0.28f, 0.02f, Z1 - Z0), null, false);
                var r = warn.GetComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.85f, 0.72f, 0.10f);
                r.sharedMaterial = mat;
            }

            // 包内地砖点缀，每 8m 一块，居中交替
            int n = 0;
            for (float z = Z0 + 4f; z < Z1 - 4f; z += 8f)
            {
                var go = LevelKit.Place(SUB + "Subway_Tiles3.prefab", new Vector3(0f, 0.005f, z), 0f, root);
                if (go != null) n++;
            }
            sb.AppendLine("[W3] 地砖点缀=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 墙体
        static int BuildWalls(Transform root, Mats m, StringBuilder sb)
        {
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (HALF_W + 0.3f);
                for (float z = Z0; z < Z1; z += 4f)
                {
                    float cz = z + 2f;
                    // 左侧留两个 8m 宽门洞（z≈-14 / z≈+10），通向平行侧廊，形成可绕行的回路
                    // 门洞边界正好落在立柱之间（立柱在 -26,-18,-10,-2,6,14,...），中间不挡路
                    if (side < 0 && (Mathf.Abs(cz + 16f) < 0.5f || Mathf.Abs(cz + 12f) < 0.5f
                                  || Mathf.Abs(cz - 8f) < 0.5f || Mathf.Abs(cz - 12f) < 0.5f)) continue;
                    var w = LevelKit.Place(SUB + "Subway_wall_Large1_Half_B.prefab",
                        new Vector3(x, 0f, cz), 90f, root);
                    if (w != null) n++;
                }
                // 腰线灯带
                for (float z = Z0 + 2f; z < Z1; z += 8f)
                {
                    if (side < 0 && (Mathf.Abs(z + 14f) < 3f || Mathf.Abs(z - 10f) < 3f)) continue;
                    var p = LevelKit.Place(SUB + "Subway_wall_panel1.prefab",
                        new Vector3(side * (HALF_W - 0.05f), 1.1f, z), 90f, root, false);
                    if (p != null) p.transform.SetParent(root, true);
                }
            }
            sb.AppendLine("[W3] 墙段=" + n);
            return n;
        }

        static void BuildCeiling(Transform root, Mats m, StringBuilder sb)
        {
            LevelKit.Slab("CeilingSlab", root,
                new Vector3(0f, CEIL_Y + 0.15f, (Z0 + Z1) * 0.5f),
                new Vector3(HALF_W * 2f + 1.6f, 0.3f, Z1 - Z0), m.Ceil);

            for (float z = Z0 + 4f; z < Z1; z += 8f)
                LevelKit.Place(SUB + "Subway_ceiling1.prefab", new Vector3(0f, CEIL_Y, z), 0f, root);
            sb.AppendLine("[W3] 天花板铺设完成");
        }

        // ------------------------------------------------------------------ 立柱 / 杂物
        static void BuildPillarsAndProps(Transform root, Mats m, StringBuilder sb)
        {
            for (float z = Z0 + 8f; z < Z1 - 4f; z += 8f)
            {
                for (int side = -1; side <= 1; side += 2)
                    LevelKit.Place(SUB + "Subway_Pillar1.prefab", new Vector3(side * (HALF_W - 1.0f), 0f, z), 0f, root);
            }

            // 长椅沿墙
            for (float z = Z0 + 10f; z < Z1 - 8f; z += 12f)
            {
                int side = (Mathf.RoundToInt(z) % 24 == 0) ? -1 : 1;
                LevelKit.Place(SUB + "Subway_Bench1.prefab",
                    new Vector3(side * (HALF_W - 0.9f), 0f, z), side < 0 ? 90f : -90f, root);
            }

            // 垃圾桶 / 警示条 / 管道
            var rnd = new System.Random(3117);
            string[] props = { "Subway_Trash_Bin.prefab", "Subway_Bar1A.prefab", "Subway_Vents1.prefab", "Subway_cables_Box1.prefab" };
            for (float z = Z0 + 6f; z < Z1 - 6f; z += 9f)
            {
                if (rnd.NextDouble() > 0.7) continue;
                string p = props[rnd.Next(0, props.Length)];
                float x = (float)(rnd.NextDouble() * (HALF_W * 2f - 2.4f) - (HALF_W - 1.2f));
                LevelKit.Place(SUB + p, new Vector3(x, 0f, z), (float)(rnd.NextDouble() * 360.0), root);
            }
            sb.AppendLine("[W3] 立柱/长椅/杂物布置完成");
        }

        // ------------------------------------------------------------------ 隧道端头
        static void BuildTunnelEnds(Transform root, Mats m, StringBuilder sb)
        {
            // 南端（玩家身后）：铁轨隧道洞
            LevelKit.Place(SUB + "Subway_Rails_Tunel1.prefab", new Vector3(0f, 0f, Z0 - 2f), 90f, root);
            LevelKit.Place(SUB + "Subway_Rails1.prefab", new Vector3(-HALF_W + 0.6f, 0f, Z0 + 3f), 90f, root, false);

            // 端头封板，防止走空
            LevelKit.Slab("CapSouth", root, new Vector3(0f, 2.2f, Z0 - 0.6f),
                new Vector3(HALF_W * 2f + 1.6f, 4.4f, 0.6f), m.Wall);

            // 北端：门洞两侧封板，中间留给传送门
            LevelKit.Slab("CapNorth_L", root, new Vector3(-3.3f, 2.2f, Z1 + 0.2f),
                new Vector3(3.0f, 4.4f, 0.6f), m.Wall);
            LevelKit.Slab("CapNorth_R", root, new Vector3(3.3f, 2.2f, Z1 + 0.2f),
                new Vector3(3.0f, 4.4f, 0.6f), m.Wall);
            LevelKit.Slab("CapNorth_Top", root, new Vector3(0f, 3.9f, Z1 + 0.2f),
                new Vector3(3.6f, 1.0f, 0.6f), m.Wall);
            // 门洞后方封板：门是传送门，玩家进门前就会离场，不需要真的穿过
            LevelKit.Slab("CapNorth_Mid", root, new Vector3(0f, 2.2f, Z1 + 0.6f),
                new Vector3(3.6f, 4.4f, 0.6f), m.Wall);

            sb.AppendLine("[W3] 隧道端头完成");
        }

        /// <summary>站台随机遭遇点：主走廊与侧廊各一处。</summary>
        static void BuildEncounters(Transform root, StringBuilder sb)
        {
            var mat = LevelKit.LoadFirstMaterial("Assets/Dark City 2/Subway/Materials", null);
            if (mat == null) mat = LevelKit.LoadFirstMaterial("Assets/Dark City 2/Assets/Source/Materials/Rough", null);

            var a = LevelKit.Slab("SubwayEncounterA", root, new Vector3(3.2f, 0.85f, 8f),
                new Vector3(0.6f, 1.5f, 1.2f), mat);
            var ea = a.AddComponent<MedusaHorror.Gameplay.SubwayEncounter>();
            ea.encounterName = "废弃站台";

            var b = LevelKit.Slab("SubwayEncounterB", root, new Vector3(-9.45f, 0.85f, -8f),
                new Vector3(0.6f, 1.5f, 1.2f), mat);
            var eb = b.AddComponent<MedusaHorror.Gameplay.SubwayEncounter>();
            eb.encounterName = "侧廊深处";

            sb.AppendLine("[W3] 随机遭遇点=2");
        }

        // ------------------------------------------------------------------ 平行侧廊（回路）
        static void BuildSideCorridor(Transform root, Mats m, StringBuilder sb)
        {
            const float CX = -9.45f;   // 侧廊中心 X
            const float W = 9.5f;      // 侧廊宽
            const float CZ0 = -18f, CZ1 = 18f;
            float len = CZ1 - CZ0;

            LevelKit.Slab("SideFloor", root, new Vector3(CX, -0.2f, (CZ0 + CZ1) * 0.5f),
                new Vector3(W, 0.4f, len), m.Floor);
            LevelKit.Slab("SideCeil", root, new Vector3(CX, CEIL_Y + 0.15f, (CZ0 + CZ1) * 0.5f),
                new Vector3(W, 0.3f, len), m.Ceil);
            LevelKit.Slab("SideOuterWall", root, new Vector3(CX - W * 0.5f - 0.2f, 2.2f, 0f),
                new Vector3(0.4f, 4.4f, len + 0.8f), m.Wall);
            LevelKit.Slab("SideEndS", root, new Vector3(CX, 2.2f, CZ0 - 0.2f),
                new Vector3(W, 4.4f, 0.4f), m.Wall);
            LevelKit.Slab("SideEndN", root, new Vector3(CX, 2.2f, CZ1 + 0.2f),
                new Vector3(W, 4.4f, 0.4f), m.Wall);

            for (float z = CZ0 + 5f; z < CZ1 - 2f; z += 8f)
            {
                LevelKit.Place(SUB + "Subway_Lamp1.prefab", new Vector3(CX, CEIL_Y - 0.35f, z), 90f, root, false);
                LevelKit.AddPointLight(root, "SideLamp_" + Mathf.RoundToInt(z),
                    new Vector3(CX, CEIL_Y - 0.6f, z), new Color(0.68f, 0.96f, 0.90f), 3.6f, 14f);
            }
            // 侧廊中央补一盏大范围顶灯，避免两端之间出现死黑
            LevelKit.AddPointLight(root, "SideLampMid", new Vector3(CX, CEIL_Y - 0.6f, 0f),
                new Color(0.70f, 0.96f, 0.92f), 3.2f, 18f);

            var rnd = new System.Random(3141);
            string[] props = { "Subway_Trash_Bin.prefab", "Subway_Bar1A.prefab", "Subway_Vents1.prefab", "Subway_cables_Box1.prefab" };
            for (float z = CZ0 + 4f; z < CZ1 - 4f; z += 7f)
            {
                if (rnd.NextDouble() > 0.6) continue;
                string p = props[rnd.Next(0, props.Length)];
                float x = CX + (float)(rnd.NextDouble() * (W - 2.6f) - (W * 0.5f - 1.3f));
                LevelKit.Place(SUB + p, new Vector3(x, 0f, z), (float)(rnd.NextDouble() * 360.0), root);
            }

            sb.AppendLine("[W3] 平行侧廊完成 x=" + CX + " 长=" + len);
        }

        // ------------------------------------------------------------------ 灯光
        static void BuildLighting(Transform root, StringBuilder sb)
        {
            // 地下无天空：极低环境光 + 深青雾
            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.030f;
            RenderSettings.fogColor = new Color(0.03f, 0.05f, 0.05f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.18f, 0.18f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.13f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.03f, 0.03f);

            int lamps = 0;
            for (float z = Z0 + 6f; z < Z1 - 4f; z += 8f)
            {
                LevelKit.Place(SUB + "Subway_Lamp1.prefab", new Vector3(0f, CEIL_Y - 0.35f, z), 90f, root, false);
                LevelKit.AddPointLight(root, "SubLamp_" + Mathf.RoundToInt(z),
                    new Vector3(0f, CEIL_Y - 0.6f, z), new Color(0.62f, 0.95f, 0.88f), 2.6f, 11f);
                lamps++;
            }
            // 红色应急灯
            for (float z = Z0 + 14f; z < Z1; z += 24f)
                LevelKit.AddPointLight(root, "Emergency_" + Mathf.RoundToInt(z),
                    new Vector3(HALF_W - 0.5f, 3.2f, z), new Color(1f, 0.22f, 0.18f), 1.2f, 6f);

            sb.AppendLine("[W3] 灯=" + lamps);
        }
    }
}
