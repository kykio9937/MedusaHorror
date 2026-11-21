using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MedusaHorror.EditorTools
{
    /// <summary>基地/中继站场景生成器：一个封闭大厅 + 通往五关的传送门。</summary>
    public static class BaseBuilder
    {
        public const string ScenePath = "Assets/Scenes/W0_Base.unity";
        const string Tag = "BASE";

        struct GateDef
        {
            public string scene;
            public string label;
            public Color color;
            public GateDef(string s, string l, Color c) { scene = s; label = l; color = c; }
        }

        public static string Build()
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var rootGo = new GameObject("BASE_Root");
            var root = rootGo.transform;

            var concrete = Mat("Base_Concrete", new Color(0.33f, 0.34f, 0.35f), 0.12f);
            var darkMetal = Mat("Base_Metal", new Color(0.17f, 0.18f, 0.20f), 0.55f);
            var trim = Mat("Base_Trim", new Color(0.10f, 0.28f, 0.32f), 0.45f);
            var emit = Mat("Base_Emit", new Color(0.75f, 0.90f, 1f), 0.9f);
            emit.EnableKeyword("_EMISSION");
            emit.SetColor("_EmissionColor", new Color(0.55f, 0.80f, 1f) * 2.6f);
            emit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            // ---------- 地面 / 天花板 / 墙 ----------
            LevelKit.Slab("Base_Floor", root, new Vector3(0f, -0.5f, 0f), new Vector3(46f, 1f, 46f), concrete);
            LevelKit.Slab("Base_Ceiling", root, new Vector3(0f, 8.4f, 0f), new Vector3(46f, 0.5f, 46f), darkMetal);
            LevelKit.Slab("Base_Wall_N", root, new Vector3(0f, 4f, 22.5f), new Vector3(46f, 8.5f, 1f), darkMetal);
            LevelKit.Slab("Base_Wall_S", root, new Vector3(0f, 4f, -22.5f), new Vector3(46f, 8.5f, 1f), darkMetal);
            LevelKit.Slab("Base_Wall_E", root, new Vector3(22.5f, 4f, 0f), new Vector3(1f, 8.5f, 46f), darkMetal);
            LevelKit.Slab("Base_Wall_W", root, new Vector3(-22.5f, 4f, 0f), new Vector3(1f, 8.5f, 46f), darkMetal);

            // ---------- 中央地台 ----------
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Base_CenterDisc";
            disc.transform.SetParent(root, false);
            disc.transform.position = new Vector3(0f, 0.02f, 0f);
            disc.transform.localScale = new Vector3(7f, 0.02f, 7f);
            LevelKit.ApplyMat(disc, trim);

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Base_CenterRing";
            ring.transform.SetParent(root, false);
            ring.transform.position = new Vector3(0f, 0.05f, 0f);
            ring.transform.localScale = new Vector3(7.6f, 0.02f, 7.6f);
            LevelKit.ApplyMat(ring, emit);
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            // ---------- 立柱 ----------
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 17f, 4f, Mathf.Sin(a) * 17f);
                var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
                col.name = "Base_Pillar_" + i;
                col.transform.SetParent(root, false);
                col.transform.position = p;
                col.transform.localScale = new Vector3(1.1f, 8f, 1.1f);
                LevelKit.ApplyMat(col, concrete);
            }

            // ---------- 顶灯 ----------
            for (int i = 0; i < 5; i++)
            {
                float a = (90f + i * 72f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 13f, 7.4f, Mathf.Sin(a) * 13f);
                var lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.name = "Base_Lamp_" + i;
                lamp.transform.SetParent(root, false);
                lamp.transform.position = p;
                lamp.transform.localScale = new Vector3(3.2f, 0.18f, 0.9f);
                LevelKit.ApplyMat(lamp, emit);
                Object.DestroyImmediate(lamp.GetComponent<Collider>());

                LevelKit.AddPointLight(root, "Base_Light_" + i, p + Vector3.down * 0.6f,
                    new Color(0.86f, 0.92f, 1f), 2.6f, 20f);
            }

            // ---------- 五关传送门 ----------
            var gates = new[]
            {
                new GateDef("W1_Hospital", "1 · 废弃医院", new Color(0.55f, 0.88f, 0.95f, 0.45f)),
                new GateDef("W2_Wasteland", "2 · 城市废墟", new Color(1f, 0.62f, 0.28f, 0.45f)),
                new GateDef("W3_Subway", "3 · 地下铁", new Color(0.82f, 0.90f, 0.45f, 0.45f)),
                new GateDef("W4_NeonStreet", "4 · 霓虹街区", new Color(0.95f, 0.32f, 0.86f, 0.45f)),
                new GateDef("W5_AlienGate", "5 · 外星星门", new Color(0.62f, 0.48f, 1f, 0.45f)),
            };

            var font = MedusaHorror.UI.CjkFont.Get();
            for (int i = 0; i < gates.Length; i++)
            {
                float a = (90f + i * 72f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a) * 12f, 0f, Mathf.Sin(a) * 12f);
                float yaw = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;

                var gate = LevelKit.BuildExitPortal(root, "Base_Gate_" + gates[i].scene, p, yaw,
                    gates[i].color, gates[i].color * 1.2f, trim, gates[i].scene, sb, Tag);

                // 门框与光效沿用 BuildExitPortal 的装配，触发改用距离判定更稳的 BasePortal
                var oldPortal = gate.GetComponent<MedusaHorror.Gameplay.ExitPortal>();
                if (oldPortal != null) Object.DestroyImmediate(oldPortal);
                var bp = gate.AddComponent<MedusaHorror.Gameplay.BasePortal>();
                bp.targetScene = gates[i].scene;
                bp.prompt = "传送 · " + gates[i].label;
                bp.radius = 1.7f;

                var labelGo = new GameObject("Base_Gate_Label_" + i);
                labelGo.transform.SetParent(gate.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 4.3f, 0f);
                labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = gates[i].label;
                tm.font = font;
                tm.fontSize = 72;
                tm.characterSize = 0.075f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = new Color(0.92f, 0.94f, 0.96f);
                var mr = labelGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;

                // 门前的彩色地标条
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stripe.name = "Base_Gate_Stripe_" + i;
                stripe.transform.SetParent(gate.transform, false);
                stripe.transform.localPosition = new Vector3(0f, 0.02f, -1.9f);
                stripe.transform.localScale = new Vector3(3.4f, 0.03f, 1.6f);
                LevelKit.ApplyMat(stripe, Mat("Base_Stripe_" + i, gates[i].color, 0.7f));
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
            }

            // ---------- 玩家 / 相机 ----------
            var playerPos = new Vector3(0f, 0.1f, -2.5f);
            var player = LevelKit.BuildPlayer(root, playerPos, 0f, sb, Tag);
            LevelKit.BuildCamera(player, "CM_BASE_ThirdPerson", sb, Tag);

            // ---------- 环境 ----------
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.20f, 0.23f, 0.27f);
            RenderSettings.fogDensity = 0.010f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.30f, 0.33f, 0.38f);
            RenderSettings.ambientIntensity = 1f;

            LevelKit.BuildIntro(root, "中继站 · 选择一扇门", 1.6f);

            LevelKit.SaveAndRegister(scene, ScenePath, "MainMenu.unity", sb, Tag);
            sb.AppendLine("[" + Tag + "] 完成 " + ScenePath);
            return sb.ToString();
        }

        static Material Mat(string name, Color c, float smooth)
        {
            var m = new Material(Shader.Find("Standard"));
            m.name = name;
            m.color = c;
            m.SetFloat("_Glossiness", smooth);
            return m;
        }
    }
}
