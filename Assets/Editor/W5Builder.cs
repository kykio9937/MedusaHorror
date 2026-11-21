using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W5「外星星门」一键搭建：外星荒原 + 异星植被岩石 + 巨石阵 + 坠毁飞船 + 星门离场。
    /// 素材来自 Assets/Space（Alien Sky 包）。
    /// </summary>
    public static class W5Builder
    {
        public const string ScenePath = "Assets/Scenes/W5_AlienGate.unity";
        public const string SceneName = "W5_AlienGate";

        const string PF = "Assets/Space/Prefabs/";
        const string MAT = "Assets/Space/Materials/";

        const float FIELD = 110f;   // 荒原半径
        const float GATE_Z = 44f;
        const float START_Z = -44f;

        public static string Build()
        {
            if (EditorApplication.isPlaying) return "[W5] 播放中拒绝，请先 STOPPLAY";
            EditorSceneManager.SaveOpenScenes();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("W5_Root").transform;
            var sb = new StringBuilder("[W5] 外星星门\n");

            BuildGround(root, sb);
            int flora = BuildFlora(root, sb);
            int rocks = BuildRocks(root, sb);
            BuildMonoliths(root, sb);
            BuildShipwreck(root, sb);
            BuildAtmosphere(root, sb);
            var player = LevelKit.BuildPlayer(root, new Vector3(0f, 0.25f, START_Z), 0f, sb, "W5");
            LevelKit.BuildCamera(player, "CM_W5_ThirdPerson", sb, "W5");
            BuildGate(root, sb);
            LevelKit.BuildIntro(root, "两个月亮挂在天上。荒原尽头的星门，是回去的唯一办法。", 1.8f);

            int stripped = StripPackageBehaviours(root);
            sb.AppendLine("[W5] 已移除包内控制脚本=" + stripped);

            sb.AppendLine("[W5] 植被=" + flora + " 岩石=" + rocks);
            LevelKit.SaveAndRegister(scene, ScenePath, "W4_NeonStreet.unity", sb, "W5");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 地面
        static void BuildGround(Transform root, StringBuilder sb)
        {
            var mat = LevelKit.LoadMat(MAT + "mudGray.mat");
            if (mat == null) mat = LevelKit.LoadMat(MAT + "debrisStone.mat");
            mat = LevelKit.MakeTinted("W5_Ground", mat, new Color(0.52f, 0.44f, 0.60f));
            LevelKit.Slab("AlienGround", root, new Vector3(0f, -0.5f, 0f),
                new Vector3(FIELD * 2.2f, 1f, FIELD * 2.2f), mat);

            // 兜底地面 + 边界隐形墙：防止跑到荒原边缘掉进虚空
            LevelKit.Slab("FallGuard", root, new Vector3(0f, -2.5f, 0f),
                new Vector3(460f, 0.5f, 460f), mat);
            for (int i = 0; i < 4; i++)
            {
                var w = new GameObject("BoundWall" + i);
                w.transform.SetParent(root, false);
                bool alongX = (i % 2) == 0;
                float sign = i < 2 ? -1f : 1f;
                float edge = FIELD + 2f;
                w.transform.position = alongX
                    ? new Vector3(sign * edge, 12f, 0f)
                    : new Vector3(0f, 12f, sign * edge);
                var bc = w.AddComponent<BoxCollider>();
                bc.size = alongX ? new Vector3(1f, 40f, FIELD * 2.2f) : new Vector3(FIELD * 2.2f, 40f, 1f);
            }

            // 星门前的石台
            var padMat = LevelKit.LoadMat(MAT + "monolith.mat");
            if (padMat == null) padMat = mat;
            LevelKit.Slab("GatePad", root, new Vector3(0f, -0.05f, GATE_Z),
                new Vector3(26f, 0.6f, 26f), padMat);

            // 起点后的落点平台
            LevelKit.Slab("StartPad", root, new Vector3(0f, -0.05f, START_Z),
                new Vector3(16f, 0.6f, 16f), padMat);
            sb.AppendLine("[W5] 地面铺设完成");
        }

        // ------------------------------------------------------------------ 植被
        static int BuildFlora(Transform root, StringBuilder sb)
        {
            string[] flora =
            {
                "foliage/xenoFern.prefab", "foliage/xenoFernFlower.prefab", "foliage/xenoTree2.prefab",
                "foliage/XenoTree.prefab", "foliage/xenoTree3.prefab", "foliage/xenoPalm.prefab",
                "foliage/alienTree.prefab", "foliage/Xeno_ cactus.prefab", "foliage/xenoBatat.prefab"
            };
            var rnd = new System.Random(5505);
            int n = 0;
            for (int i = 0; i < 70; i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float r = 14f + (float)rnd.NextDouble() * (FIELD - 22f);
                float x = Mathf.Cos(a) * r;
                float z = Mathf.Sin(a) * r;
                // 主路径与星门台地留空
                if (Mathf.Abs(x) < 13f && z > START_Z - 12f && z < GATE_Z + 22f) continue;
                if (Mathf.Abs(x) < 22f && Mathf.Abs(z - GATE_Z) < 20f) continue;
                string p = flora[rnd.Next(0, flora.Length)];
                if (LevelKit.Place(PF + p, new Vector3(x, 0f, z), (float)(rnd.NextDouble() * 360.0), root) != null) n++;
            }
            sb.AppendLine("[W5] 植被=" + n);
            return n;
        }

        static int BuildRocks(Transform root, StringBuilder sb)
        {
            string[] rocks =
            {
                "props/rockSmall.prefab", "props/RockSpike.prefab",
                "outerBodies/asteroidD.prefab", "outerBodies/asteroidB.prefab"
            };
            var rnd = new System.Random(6606);
            int n = 0;
            for (int i = 0; i < 34; i++)
            {
                float a = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                float r = 16f + (float)rnd.NextDouble() * (FIELD - 20f);
                float x = Mathf.Cos(a) * r;
                float z = Mathf.Sin(a) * r;
                if (Mathf.Abs(x) < 13f && z > START_Z - 12f && z < GATE_Z + 22f) continue;
                string p = rocks[rnd.Next(0, rocks.Length)];
                var go = LevelKit.Place(PF + p, new Vector3(x, 0f, z), (float)(rnd.NextDouble() * 360.0), root);
                if (go == null) continue;
                float s = 0.5f + (float)rnd.NextDouble() * 1.2f;
                go.transform.localScale = new Vector3(s, s, s);
                n++;
            }
            sb.AppendLine("[W5] 岩石=" + n);
            return n;
        }

        // ------------------------------------------------------------------ 巨石阵
        static void BuildMonoliths(Transform root, StringBuilder sb)
        {
            // 星门外圈巨石，形成仪式感
            for (int i = 0; i <= 4; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                float r = 12f;
                var go = LevelKit.Place(PF + "props/monolith.prefab",
                    new Vector3(Mathf.Cos(a) * r, 0f, GATE_Z + Mathf.Sin(a) * r),
                    -i * 45f, root);
                if (go != null) go.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
            }
            // 路径两侧的小石柱
            for (float z = START_Z + 10f; z < GATE_Z - 12f; z += 12f)
            {
                for (int side = -1; side <= 1; side += 2)
                    LevelKit.Place(PF + "props/pillar.prefab", new Vector3(side * 6.5f, 0f, z), 0f, root);
            }
            sb.AppendLine("[W5] 巨石阵完成");
        }

        // ------------------------------------------------------------------ 飞船残骸
        static void BuildShipwreck(Transform root, StringBuilder sb)
        {
            var ship = LevelKit.Place(PF + "spaceShip.prefab", new Vector3(-62f, 0f, 26f), 32f, root);
            if (ship != null)
            {
                ship.name = "CrashedShip";
                ship.transform.rotation = Quaternion.Euler(-6f, 32f, 9f);
            }
            LevelKit.Place(PF + "props/conatainer.prefab", new Vector3(-38f, 0f, 30f), 14f, root);
            LevelKit.Place(PF + "props/conatainerGreen.prefab", new Vector3(-34f, 0f, 20f), -22f, root);
            LevelKit.Place(PF + "props/pumpTunk.prefab", new Vector3(-44f, 0f, 12f), 68f, root);
            LevelKit.Place(PF + "props/barrel.prefab", new Vector3(-36f, 0f, 12f), 0f, root);
            LevelKit.Place(PF + "props/barrel.prefab", new Vector3(-35f, 0f, 10.6f), 40f, root);
            LevelKit.Place(PF + "props/crateA.prefab", new Vector3(-40f, 0f, 36f), 22f, root);
            LevelKit.Place(PF + "props/crateC.prefab", new Vector3(-41.4f, 0f, 35f), -14f, root);

            // 残骸火光
            LevelKit.AddPointLight(root, "WreckFire", new Vector3(-56f, 3f, 26f), new Color(1f, 0.45f, 0.18f), 2.6f, 16f);
            sb.AppendLine("[W5] 飞船残骸完成");
        }

        /// <summary>
        /// 去掉 Space 包预制体自带的控制脚本（Hover/SpaceShipInput 等会读 "Throttle" 等不存在的输入轴）。
        /// 这些物件在本关只是布景，不需要任何运行时逻辑。
        /// </summary>
        static int StripPackageBehaviours(Transform root)
        {
            int n = 0;
            var all = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var ms = MonoScript.FromMonoBehaviour(mb);
                if (ms == null) continue;
                string p = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(p)) continue;
                if (p.StartsWith("Assets/Space/") || p.StartsWith("Assets/Standard Assets/"))
                {
                    Object.DestroyImmediate(mb);
                    n++;
                }
            }
            return n;
        }

        // ------------------------------------------------------------------ 星门
        static void BuildGate(Transform root, StringBuilder sb)
        {
            var gateRoot = new GameObject("AlienGate");
            gateRoot.transform.SetParent(root, false);
            gateRoot.transform.position = new Vector3(0f, 0f, GATE_Z);

            var stoneMat = LevelKit.LoadMat(MAT + "monolith.mat");
            var glowMat = LevelKit.LoadMat(MAT + "forceField.mat");
            var glowMat2 = LevelKit.LoadMat(MAT + "forceFieldGlow.mat");

            // 星门拱架：两根石柱 + 横梁，比普通门更大
            for (int side = -1; side <= 1; side += 2)
            {
                var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
                col.name = "GatePillar" + (side < 0 ? "L" : "R");
                col.transform.SetParent(gateRoot.transform, false);
                col.transform.localPosition = new Vector3(side * 2.6f, 3.6f, 0f);
                col.transform.localScale = new Vector3(0.9f, 7.2f, 0.9f);
                LevelKit.ApplyMat(col, stoneMat);

                // 柱身发光纹路
                var vein = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vein.name = "GateVein";
                vein.transform.SetParent(gateRoot.transform, false);
                vein.transform.localPosition = new Vector3(side * 2.15f, 3.6f, 0f);
                vein.transform.localScale = new Vector3(0.08f, 6.4f, 0.16f);
                var vr = vein.GetComponent<MeshRenderer>();
                vr.sharedMaterial = glowMat != null ? glowMat : new Material(Shader.Find("Standard")) { color = new Color(0.35f, 0.95f, 1f) };
                Object.DestroyImmediate(vein.GetComponent<Collider>());
            }

            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.name = "GateLintel";
            lintel.transform.SetParent(gateRoot.transform, false);
            lintel.transform.localPosition = new Vector3(0f, 7.6f, 0f);
            lintel.transform.localScale = new Vector3(6.2f, 0.9f, 0.9f);
            LevelKit.ApplyMat(lintel, stoneMat);

            // 门内光幕（比普通门更大）
            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            veil.name = "GateVeil";
            veil.transform.SetParent(gateRoot.transform, false);
            veil.transform.localPosition = new Vector3(0f, 3.7f, 0f);
            veil.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            veil.transform.localScale = new Vector3(4.6f, 6.8f, 1f);
            var vr2 = veil.GetComponent<MeshRenderer>();
            if (glowMat2 != null) vr2.sharedMaterial = glowMat2;
            else
            {
                var m = new Material(Shader.Find("Standard"));
                m.color = new Color(0.35f, 0.95f, 1f, 0.5f);
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.renderQueue = 3000;
                vr2.sharedMaterial = m;
            }
            Object.DestroyImmediate(veil.GetComponent<Collider>());

            // 地面光环
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "GateRing";
            ring.transform.SetParent(gateRoot.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            ring.transform.localScale = new Vector3(6.4f, 0.02f, 6.4f);
            var rr = ring.GetComponent<MeshRenderer>();
            rr.sharedMaterial = glowMat != null ? glowMat : vr2.sharedMaterial;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            var glow = new GameObject("GateGlow");
            glow.transform.SetParent(gateRoot.transform, false);
            glow.transform.localPosition = new Vector3(0f, 3.6f, -0.8f);
            var gl = glow.AddComponent<Light>();
            gl.type = LightType.Point;
            gl.color = new Color(0.4f, 0.95f, 1f);
            gl.intensity = 4.2f;
            gl.range = 22f;

            // 复用标准离场门逻辑（无钥匙，自动取下一关 → 无 → 回主菜单）
            var portal = gateRoot.AddComponent<MedusaHorror.Gameplay.ExitPortal>();
            portal.door = null;
            portal.fxRoot = null;
            portal.requireKey = false;
            portal.nextSceneOverride = "";
            portal.halfWidth = 2.3f;
            portal.halfDepth = 1.4f;
            portal.lowerY = -0.3f;
            portal.upperY = 6.9f;

            // 让 ExitPortal 找得到光幕：直接把 veil 挂在 gateRoot 下（已在）
            sb.AppendLine("[W5] 星门就位 pos=" + gateRoot.transform.position.ToString("F1"));
        }

        // ------------------------------------------------------------------ 氛围
        static void BuildAtmosphere(Transform root, StringBuilder sb)
        {
            var sky = LevelKit.LoadMat(MAT + "skySpaceBox.mat");
            if (sky != null) RenderSettings.skybox = sky;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0055f;
            RenderSettings.fogColor = new Color(0.26f, 0.16f, 0.34f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.40f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.28f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.16f, 0.28f);

            // 双月：一紫一青
            var moonA = new GameObject("MoonA");
            moonA.transform.SetParent(root, false);
            moonA.transform.rotation = Quaternion.Euler(28f, -52f, 0f);
            var la = moonA.AddComponent<Light>();
            la.type = LightType.Directional;
            la.color = new Color(0.72f, 0.55f, 1f);
            la.intensity = 2.30f;
            la.shadows = LightShadows.Soft;

            var moonB = new GameObject("MoonB");
            moonB.transform.SetParent(root, false);
            moonB.transform.rotation = Quaternion.Euler(64f, 118f, 0f);
            var lb = moonB.AddComponent<Light>();
            lb.type = LightType.Directional;
            lb.color = new Color(0.45f, 0.85f, 0.95f);
            lb.intensity = 1.10f;
            lb.shadows = LightShadows.None;
            sb.AppendLine("[W5] 氛围：双月+紫雾 skybox=" + (sky != null ? sky.name : "none"));
        }
    }
}
