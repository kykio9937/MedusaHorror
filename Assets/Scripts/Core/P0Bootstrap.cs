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
        public const string WastelandScene = "W2_Wasteland";
        public const string SubwayScene = "W3_Subway";
        public const string NeonStreetScene = "W4_NeonStreet";
        public const string AlienGateScene = "W5_AlienGate";
        public const string HospitalScene = "W1_Hospital";
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
                audio.ApplySceneMusic(scene.name);
                int adopted = 0;
                adopted = audio.AdoptedAmbienceCount;
                Debug.Log("[P0] 音频：mixer=" + (audio.MixerLoaded ? "loaded" : "missing") +
                          " bgm=playing bgmKey=" + audio.SceneMusicKey + " ambienceAdopted=" + adopted);
            }

            // P0-2 失败 / 重开闭环：战斗关卡都需要局内流程中枢
            if (scene.name == BaseScene || scene.name == WastelandScene
                || scene.name == SubwayScene || scene.name == NeonStreetScene || scene.name == AlienGateScene)
                GameFlow.Ensure();

            // 角色补光：UnityChan 卡通 shader 只吃平行光，没有平行光的场景角色会渲染成黑剪影
            EnsureCharacterLight();

            // 地下铁换成星空天空盒
            if (scene.name == SubwayScene) StarSkybox.Ensure();

            // 新手引导：首次进入 W1 自动播放操作提示（H 键随时重看）
            MedusaHorror.Gameplay.TutorialDirector.Ensure();
            MedusaHorror.Gameplay.TutorialDirector.MaybeShowOnW1(scene.name);

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

            // 星际传送门实体门框（Resources 里的 Teleport 模型，纯表现层）
            var portals = Object.FindObjectsOfType<ExitPortal>(true);
            for (int i = 0; i < portals.Length; i++)
                if (portals[i] != null) PortalFrame.Ensure(portals[i]);

            // 小地图 + 传送门导航罗盘（W1 医院继续用专用 MinimapRig，这里自动跳过）
            LevelMinimap.Ensure(scene.name);
            if (scene.name == WastelandScene && SaveSystem.Data.hospitalTrainingPassed)
                new GameObject("TrainingArrivalStory").AddComponent<TrainingArrival>();
            PortalCompass.Ensure();

            // 信用点 + 任务追踪 HUD（主菜单隐藏）
            var wallet = CurrencyHud.Ensure();
            var questHud = QuestHud.Ensure();
            bool inMenu = scene.name == MenuScene;
            wallet.SetShown(!inMenu);
            questHud.SetShown(!inMenu);

            // 玩家面板（属性/装备/背包/帮助，Tab 打开）
            var playerPanel = PlayerPanel.Ensure();
            playerPanel.SetShown(!inMenu);

            // NPC 外观差异化：同一个模型也能看出是不同的人
            SetupNpcVariation();

            // 玩家外观配色（V 键切换，不改骨骼与武器绑定）
            var pcSkin = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (pcSkin != null && pcSkin.GetComponent<MedusaHorror.Player.PlayerSkin>() == null)
                pcSkin.gameObject.AddComponent<MedusaHorror.Player.PlayerSkin>();

            // 赛博摩托坐骑（等级 8 解锁，G 键召唤）
            if (pcSkin != null && pcSkin.GetComponent<MedusaHorror.Gameplay.MountSystem>() == null)
                pcSkin.gameObject.AddComponent<MedusaHorror.Gameplay.MountSystem>();

            // 主动技能：F 裂空斩（10 级）/ V 石化震荡（12 级），HUD 右下角常驻
            if (pcSkin != null && pcSkin.GetComponent<MedusaHorror.Gameplay.ActiveSkill>() == null)
                pcSkin.gameObject.AddComponent<MedusaHorror.Gameplay.ActiveSkill>();
            var skillHud = MedusaHorror.UI.SkillHud.Ensure();
            skillHud.SetShown(!inMenu);

            // NPC 对话系统：常驻，按需弹出底部对话框
            MedusaHorror.Gameplay.DialogueSystem.Ensure();

            // 成就系统：常驻轮询解锁条件，Tab 打开成就列表
            MedusaHorror.Gameplay.AchievementSystem.Ensure();

            // 等级 / 经验：老存档按等级补齐解锁，HUD 常驻左下角
            Progression.SyncUnlocks();
            var expHud = ExpHud.Ensure();
            expHud.SetShown(!inMenu);

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

        // ---------------- 角色补光 ----------------

        /// <summary>
        /// 角色补光：UnityChan 卡通 shader 只吃平行光，室内关卡原有灯照不到角色时会渲染成黑剪影。
        /// 所以不管场景有没有平行光，都保证有一盏专供角色的补光。
        /// </summary>
        static void EnsureCharacterLight()
        {
            var exist = GameObject.Find("P0_CharacterFill");
            Light l;
            if (exist == null)
            {
                var go = new GameObject("P0_CharacterFill");
                l = go.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = new Color(0.92f, 0.95f, 1f);
                l.shadows = LightShadows.None;
                go.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            }
            else
            {
                l = exist.GetComponent<Light>();
                if (l == null) return;
            }
            l.intensity = 0.85f;

            // 室内关卡环境光普遍偏低，角色和地面都糊成一片黑，顺手抬一点
            if (RenderSettings.ambientIntensity < 0.65f)
                RenderSettings.ambientIntensity = 0.65f;
        }

        // ---------------- NPC 外观差异化 ----------------

        static void SetupNpcVariation()
        {
            var markers = new System.Collections.Generic.List<GameObject>();

            var quest = Object.FindObjectsOfType<MedusaHorror.Gameplay.NpcQuestGiver>(true);
            for (int i = 0; i < quest.Length; i++)
                if (quest[i] != null) markers.Add(quest[i].gameObject);

            var crowd = Object.FindObjectsOfType<MedusaHorror.Gameplay.CrowdWalker>(true);
            for (int i = 0; i < crowd.Length; i++)
                if (crowd[i] != null) markers.Add(crowd[i].gameObject);

            // 名字里带这些词的也算 NPC（Builder 生成的村民/租客）
            var all = Object.FindObjectsOfType<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == null) continue;
                string n = t.name;
                if (n.IndexOf("CrowdWalker", System.StringComparison.Ordinal) >= 0
                    || n.IndexOf("HouseTenant", System.StringComparison.Ordinal) >= 0
                    || n.IndexOf("Villager", System.StringComparison.Ordinal) >= 0
                    || n.IndexOf("Npc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (!markers.Contains(t.gameObject)) markers.Add(t.gameObject);
                }
            }

            int added = 0;
            for (int i = 0; i < markers.Count; i++)
            {
                var go = markers[i];
                if (go == null) continue;
                // 玩家自己有控制器，跳过
                if (go.GetComponent<MedusaHorror.Player.ThirdPersonController>() != null) continue;
                if (go.GetComponent<MedusaHorror.Gameplay.PlayerPetrify>() != null) continue;
                if (go.GetComponent<MedusaHorror.Gameplay.NpcVariation>() != null) continue;

                var v = go.AddComponent<MedusaHorror.Gameplay.NpcVariation>();
                // 用名字 + 位置生成稳定种子，避免每次进场景换脸
                var pos = go.transform.position;
                v.seed = go.name.GetHashCode()
                       ^ Mathf.RoundToInt(pos.x * 10f)
                       ^ (Mathf.RoundToInt(pos.z * 10f) << 8);
                added++;
            }
            if (added > 0) Debug.Log("[P0] NPC 外观差异化：" + added + " 个");
        }

        // ---------------- 关卡目标（探索关卡占位） ----------------

        static readonly string[] ExploreScenes =
        {
            WastelandScene, SubwayScene, NeonStreetScene, AlienGateScene
        };

        static void SetupObjectives(Scene scene)
        {
            if (scene.name == HospitalScene)
            {
                SetupHospitalObjectives();
                return;
            }

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
                case WastelandScene: title = "第一章 · 穿过荒废村庄 · 找到村口传送门"; break;
                case SubwayScene: title = "第二章 · 登上地铁列车 · 前往霓虹街区"; break;
                case NeonStreetScene: title = "第三章 · 探索商店与义体门诊 · 前往离场星门"; break;
                default: title = "终章 · 抵达外星星门 · 离开这颗星球"; break;
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

            // 没有传送门的关卡（地下铁）：出口就是列车车门
            Transform exitPoint = portal != null ? portal.transform : null;
            if (exitPoint == null)
            {
                var board = Object.FindObjectOfType<MedusaHorror.Gameplay.TrainBoardPoint>(true);
                if (board != null) exitPoint = board.transform;
            }

            // 2) 没有路径标记的场景：沿「出生点 → 出口」补两个中途探索点
            if (!hasMarker && exitPoint != null)
            {
                var spawn = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
                if (spawn != null)
                {
                    Vector3 a = spawn.transform.position;
                    Vector3 b = exitPoint.position;
                    for (int i = 1; i <= 2; i++)
                    {
                        if (!TryRoutePoint(a, b, i / 3f, out Vector3 p)) continue;
                        AddGoalPoint(root.transform, p + Vector3.up * 0.9f,
                                     GoalKind.Reach, "前往出口 · 路线 " + i, 4.5f, false);
                        placed++;
                    }
                }
            }

            // 3) 出口：本关必需目标
            if (exitPoint != null)
            {
                AddGoalPoint(root.transform, exitPoint.position + Vector3.up * 0.9f,
                             GoalKind.Reach, portal != null ? "传送门" : "列车", 2.8f, true);
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

        static bool TryRoutePoint(Vector3 from, Vector3 to, float fraction, out Vector3 point)
        {
            point = default;
            if (!NavMesh.SamplePosition(from, out var start, 5f, NavMesh.AllAreas) ||
                !NavMesh.SamplePosition(to, out var end, 3f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = path.corners;
            float length = 0;
            for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
            float remaining = length * fraction;
            for (int i = 1; i < corners.Length; i++)
            {
                float segment = Vector3.Distance(corners[i - 1], corners[i]);
                if (remaining <= segment)
                {
                    point = Vector3.Lerp(corners[i - 1], corners[i], remaining / Mathf.Max(segment, 0.001f));
                    return true;
                }
                remaining -= segment;
            }
            return false;
        }


        /// <summary>W1: collect the five clue items, then the stargate unlocks.</summary>
        static void SetupHospitalObjectives()
        {
            W1ObjectiveState.Reset();
            if (LevelObjective.Active != null) return;
            var root = new GameObject("LevelObjectiveRuntime");
            var objective = root.AddComponent<LevelObjective>();
            objective.objectiveTitle = "序章 · 在医院中搜寻 5 件线索 · 激活星门";

            var spots = new (string label, Vector3 pos)[]
            {
                ("值班室钥匙", new Vector3(-10f, 1.1f, 26f)),
                ("药房阀门",   new Vector3(-10f, 1.1f, 50f)),
                ("检查室门禁卡", new Vector3(10f, 1.1f, 38f)),
                ("储物间保险丝", new Vector3(10f, 1.1f, 74f)),
                ("手术室密码本", new Vector3(0f, 1.1f, 102f)),
            };
            foreach (var s in spots)
            {
                AddGoalPoint(root.transform, s.pos, GoalKind.Collect, s.label, 2.2f, true);
                var goal = root.transform.GetChild(root.transform.childCount - 1).GetComponent<LevelGoalPoint>();
                goal.manualCompletion = true;
                goal.hideWorldMarker = true;
                foreach (var pickup in Object.FindObjectsOfType<W1ObjectivePickup>())
                    if (pickup.label == s.label) { goal.transform.position = pickup.transform.position; pickup.goal = goal; }
            }
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
