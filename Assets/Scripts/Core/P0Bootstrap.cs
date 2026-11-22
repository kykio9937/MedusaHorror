using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using MedusaHorror.UI;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.Core
{
    /// <summary>
    /// P0 阶段运行时自举入口：不依赖场景序列化改动，场景加载后按需装配各子系统。
    /// </summary>
    public static class P0Bootstrap
    {
        public const string BaseScene = "W0_Base";
        public const string HospitalScene = "W1_Hospital";
        public const string WastelandScene = "W2_Wasteland";
        public const string SubwayScene = "W3_Subway";
        public const string NeonStreetScene = "W4_NeonStreet";
        public const string AlienGateScene = "W5_AlienGate";
        public const string MenuScene = "MainMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply(scene);
        }

        static void Apply(Scene scene)
        {
            // P0-1 UI 中文字体
            int fixedText = CjkFont.Apply();
            if (fixedText > 0)
                Debug.Log("[P0] 中文字体：已把 " + fixedText + " 个 UI.Text 切换到随包中文字体");

            // P0-3 音频框架：任何场景都保证有 AudioMixer 总线 + 合成音效库 + BGM
            var audio = AudioDirector.Ensure();
            if (audio != null)
            {
                audio.StartMusic();
                int adopted = 0;
                if (scene.name == HospitalScene) audio.AdoptSceneAmbience();
                adopted = audio.AdoptedAmbienceCount;
                Debug.Log("[P0] 音频：mixer=" + (audio.MixerLoaded ? "loaded" : "missing") +
                          " bgm=playing ambienceAdopted=" + adopted);
            }

            // P0-2 失败 / 重开闭环：战斗关卡都需要局内流程中枢
            if (scene.name == BaseScene || scene.name == HospitalScene || scene.name == WastelandScene
                || scene.name == SubwayScene || scene.name == NeonStreetScene || scene.name == AlienGateScene)
                GameFlow.Ensure();

            // 每次进入关卡都从「未拿钥匙、Boss 未死」开始，避免基地往返后门禁残留
            MedusaHorror.Gameplay.ExitPortal.ResetStaticState();

            // 关卡目标 HUD + 目标点（探索关卡占位，运行时装配，不改场景资产）
            SetupObjectives(scene);

            // 探索关卡没有钥匙来源，进关即视为持有钥匙，传送门直接可用
            if (System.Array.IndexOf(ExploreScenes, scene.name) >= 0)
            {
                MedusaHorror.Gameplay.ExitPortal.GrantKey();

                // 一进关就面向传送门，避免出生时背对关卡纵深
                var pc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
                var po = Object.FindObjectOfType<ExitPortal>(true);
                if (pc != null && po != null)
                {
                    Vector3 d = po.transform.position - pc.transform.position;
                    d.y = 0f;
                    if (d.sqrMagnitude > 1f) pc.SetYaw(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
                }
            }

            // 小地图 + 传送门导航罗盘（W1 医院继续用专用 MinimapRig，这里自动跳过）
            LevelMinimap.Ensure(scene.name);
            PortalCompass.Ensure();

            // 信用点 + 任务追踪 HUD（主菜单隐藏）
            var wallet = CurrencyHud.Ensure();
            var questHud = QuestHud.Ensure();
            bool inMenu = scene.name == MenuScene;
            wallet.SetShown(!inMenu);
            questHud.SetShown(!inMenu);

            // 反射探针：整搬进来的场景带实时探针，每帧渲染 6 面非常吃性能。
            // 统一改为按需刷新，并在加载后只渲染一次。
            var probes = Object.FindObjectsOfType<ReflectionProbe>(true);
            for (int i = 0; i < probes.Length; i++)
            {
                var p = probes[i];
                if (p == null || p.mode != UnityEngine.Rendering.ReflectionProbeMode.Realtime) continue;
                p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                p.RenderProbe();
            }
        }

        // ---------------- 关卡目标（探索关卡占位） ----------------

        static readonly string[] ExploreScenes =
        {
            WastelandScene, SubwayScene, NeonStreetScene, AlienGateScene
        };

        static void SetupObjectives(Scene scene)
        {
            if (System.Array.IndexOf(ExploreScenes, scene.name) < 0)
            {
                // 非探索关卡（基地/医院/主菜单）：清掉上一关残留的目标 HUD
                if (ObjectiveHud.Instance != null) ObjectiveHud.Instance.Hide();
                return;
            }

            // 同一场景重复进入时不重复装配
            if (LevelObjective.Active != null) return;

            string title;
            switch (scene.name)
            {
                case WastelandScene: title = "走访荒废村庄 · 抵达村口传送门"; break;
                case SubwayScene: title = "穿过地下铁 · 抵达传送门"; break;
                case NeonStreetScene: title = "穿过霓虹街区 · 抵达传送门"; break;
                default: title = "抵达外星星门"; break;
            }

            var root = new GameObject("LevelObjectiveRuntime");
            var objective = root.AddComponent<LevelObjective>();
            objective.objectiveTitle = title;

            int placed = 0;
            bool hasMarker = false;

            // 1) 路径标记：沿路给几个引导点，避免一路枯燥
            var markers = GameObject.Find(scene.name.Substring(0, 2) + "_PathMarkers");
            if (markers != null && markers.transform.childCount > 0)
            {
                hasMarker = true;
                int total = markers.transform.childCount;
                int take = Mathf.Min(total, 4);
                for (int i = 0; i < take; i++)
                {
                    int idx = total == 1 ? 0 : Mathf.RoundToInt(i * (total - 1) / Mathf.Max(1, take - 1));
                    var child = markers.transform.GetChild(idx);
                    if (child == null) continue;
                    AddGoalPoint(root.transform, child.position + Vector3.up * 0.9f,
                                 GoalKind.Reach, "路径点 " + (i + 1), 4.0f, false);
                    placed++;
                }
            }

            var portal = Object.FindObjectOfType<ExitPortal>(true);

            // 2) 没有路径标记的场景：沿「出生点 → 传送门」补两个中途探索点
            if (!hasMarker && portal != null)
            {
                var spawn = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
                if (spawn != null)
                {
                    Vector3 a = spawn.transform.position;
                    Vector3 b = portal.transform.position;
                    for (int i = 1; i <= 2; i++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, i / 3f);
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(p, out hit, 14f, NavMesh.AllAreas)) p = hit.position;
                        AddGoalPoint(root.transform, p + Vector3.up * 0.9f,
                                     GoalKind.Reach, "探索点 " + i, 4.5f, false);
                        placed++;
                    }
                }
            }

            // 3) 出口传送门：本关必需目标
            if (portal != null)
            {
                AddGoalPoint(root.transform, portal.transform.position + Vector3.up * 0.9f,
                             GoalKind.Reach, "传送门", 2.8f, true);
                placed++;
            }
            else if (placed > 0)
            {
                // 找不到传送门时，把最后一个引导点作为必需目标，避免永远无法完成
                var all = root.GetComponentsInChildren<LevelGoalPoint>(true);
                if (all.Length > 0) all[all.Length - 1].required = true;
            }

            if (placed == 0)
            {
                Object.Destroy(root);
                Debug.Log("[P0] " + scene.name + " 未找到可用目标点，跳过目标 HUD");
                return;
            }

            // Awake 时场景里还没有子目标点，这里补一次统计
            objective.RefreshPoints();
            Debug.Log("[P0] " + scene.name + " 目标点 " + placed + " 个，标题「" + title + "」");
        }

        static void AddGoalPoint(Transform parent, Vector3 worldPos, GoalKind kind, string label, float radius, bool required)
        {
            var go = new GameObject("Goal_" + label);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var pt = go.AddComponent<LevelGoalPoint>();
            pt.kind = kind;
            pt.label = label;
            pt.radius = radius;
            pt.required = required;
        }
    }
}
