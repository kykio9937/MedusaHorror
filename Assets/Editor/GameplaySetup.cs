using System.Collections.Generic;
using System.Text;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
using MedusaHorror.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// W1 玩法装配器：把门交互、玩家交互器、石化系统、Medusa 凝视实体一次性装进当前场景。
    /// 幂等：重复执行会复用已有组件、重建 Medusa 实体，不会产生重复对象。
    /// </summary>
    public static class GameplaySetup
    {
        const float DoorWidthProbe = 1.7f;
        const float ProbeStep = 0.05f;

        public static string InstallAll()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Gameplay] 装配开始");
            sb.AppendLine(InstallDoors());
            sb.AppendLine(InstallPlayer());
            sb.AppendLine(InstallMedusa());
            sb.AppendLine(InstallBoss());
            sb.AppendLine(InstallArena());
            sb.AppendLine(ExitPortalSetup.Install());

            var sc = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            sb.AppendLine("[Gameplay] 场景已保存");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ 门
        public static string InstallDoors()
        {
            var sb = new StringBuilder();
            var pivots = new List<Transform>();
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (t == null) continue;
                if (!t.name.StartsWith("DoorPivot")) continue;
                if (t.gameObject.scene.name == null) continue;     // 排除 prefab 资源
                pivots.Add(t);
            }

            int ok = 0, sidePos = 0, sideNeg = 0;
            foreach (var pivot in pivots)
            {
                // 1) 反推门所在的墙侧：生成时局部角 = -openDeg * side，openDeg ∈ [38,71]
                float yaw = pivot.eulerAngles.y;
                int side;
                if (yaw > 180f && yaw < 355f) side = 1;                 // yaw = 360 - openDeg
                else if (yaw > 5f && yaw <= 180f) side = -1;            // yaw = openDeg
                else side = pivot.position.z < 0f ? -1 : 1;             // 已关闭态：按墙侧兜底

                // 2) 关闭姿态 = 局部 0°，门扇从门轴沿 +X 铺满门洞；开门朝房间内侧 88°
                Quaternion closed = Quaternion.identity;
                Quaternion open = Quaternion.Euler(0f, -88f * side, 0f);

                pivot.rotation = closed;
                var door = pivot.GetComponent<DoorInteractable>();
                if (door == null) door = pivot.gameObject.AddComponent<DoorInteractable>();
                door.openAngle = 88f;
                door.Configure(closed, open);

                // 3) 门扇碰撞体确保存在（关着的门要能挡住玩家）
                EnsureDoorCollider(pivot);

                if (side > 0) sidePos++; else sideNeg++;
                ok++;
            }

            sb.AppendLine("[Gameplay] 门：共 " + pivots.Count + " 扇，已装配 " + ok +
                           "（南墙 " + sideNeg + " / 北墙 " + sidePos + "），初始全部关闭");
            return sb.ToString();
        }

        static void EnsureDoorCollider(Transform pivot)
        {
            for (int i = 0; i < pivot.childCount; i++)
            {
                var leaf = pivot.GetChild(i);
                if (!leaf.name.StartsWith("DoorLeaf")) continue;
                if (leaf.GetComponent<Collider>() == null)
                {
                    var bc = leaf.gameObject.AddComponent<BoxCollider>();
                    bc.center = Vector3.zero;
                    bc.size = Vector3.one;
                }
            }
        }

        /// <summary>该点是否被「非本门」的实体碰撞体占据（即墙体）</summary>
        static bool WallAt(Vector3 p, Transform pivot)
        {
            var hits = Physics.OverlapSphere(p, 0.10f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h == null || h.isTrigger) continue;
                if (h.transform.IsChildOf(pivot)) continue;
                if (h.GetComponent<CharacterController>() != null) continue;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ 玩家
        public static string InstallPlayer()
        {
            var sb = new StringBuilder();
            var player = FindPlayer();
            if (player == null)
            {
                sb.AppendLine("[Gameplay] 玩家：未找到 Kazuko_W1_Player，跳过");
                return sb.ToString();
            }

            if (player.GetComponent<PlayerInteractor>() == null)
                player.gameObject.AddComponent<PlayerInteractor>();
            if (player.GetComponent<PlayerPetrify>() == null)
                player.gameObject.AddComponent<PlayerPetrify>();

            sb.AppendLine("[Gameplay] 玩家：" + player.name + " 已挂 PlayerInteractor + PlayerPetrify");
            return sb.ToString();
        }

        static Transform FindPlayer()
        {
            var go = GameObject.Find("Kazuko_W1_Player");
            if (go != null) return go.transform;

            // 重建后的玩家对象可能不叫 Kazuko_W1_Player，优先按玩家控制器组件定位，
            // 并排除 Boss（Medusa_W1 也带 CharacterController，直接取第一个 CC 会误挂到 Boss 身上）
            var tpc = Object.FindObjectOfType<ThirdPersonController>();
            if (tpc != null && !IsBoss(tpc.gameObject)) return tpc.transform;

            var all = Object.FindObjectsOfType<CharacterController>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || IsBoss(all[i].gameObject)) continue;
                return all[i].transform;
            }
            return null;
        }

        static bool IsBoss(GameObject go)
        {
            return go != null && (go.name.StartsWith("Medusa") || go.GetComponentInParent<MedusaBoss>() != null);
        }

        // ------------------------------------------------------------------ Medusa
        public static string InstallMedusa()
        {
            var sb = new StringBuilder();

            var old = GameObject.Find("Medusa_W1");
            if (old != null) Object.DestroyImmediate(old);

            var player = FindPlayer();
            if (player == null)
            {
                sb.AppendLine("[Gameplay] Medusa：缺少可复制的角色模型，跳过");
                return sb.ToString();
            }

            var root = GameObject.Find("Hospital_Rebuild");
            var medusa = Object.Instantiate(player.gameObject);
            medusa.name = "Medusa_W1";
            medusa.transform.SetParent(root != null ? root.transform : null, true);
            // 刷新点：3F（回字形顶层大平层）中央，面朝扶梯出口（-Z）；3F 地面 y=9.1
            medusa.transform.position = new Vector3(0f, 9.13f, 4f);
            medusa.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // 剥离玩家控制相关组件，只留模型
            Strip<InputReader>(medusa);
            Strip<ThirdPersonController>(medusa);
            Strip<ThirdPersonCameraInput>(medusa);
            Strip<PlayerInteractor>(medusa);
            Strip<PlayerPetrify>(medusa);
            Strip<CharacterController>(medusa);
            // 玩家身上的武器/血条必须剥离，否则 Boss 副本会带上并抢走 FindObjectOfType 的结果
            Strip<PlayerWeapon>(medusa);
            Strip<BossHud>(medusa);

            // 暗化材质，做出石化巫女的灰暗质感
            foreach (var r in medusa.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_Color"))
                        m.color = new Color(0.32f, 0.34f, 0.33f, 1f);
                    if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }

            // 碰撞体（供射线/触发器使用，避免玩家穿过）
            var cap = medusa.GetComponent<CapsuleCollider>();
            if (cap == null) cap = medusa.AddComponent<CapsuleCollider>();
            cap.radius = 0.35f;
            cap.height = 1.75f;
            cap.center = new Vector3(0f, 0.9f, 0f);

            var gaze = medusa.GetComponent<MedusaGaze>();
            if (gaze == null) gaze = medusa.AddComponent<MedusaGaze>();
            gaze.eyeHeight = 1.62f;
            gaze.range = 22f;
            gaze.halfFov = 34f;

            // 眼睛红光：作为凝视状态的可视反馈
            var lightGo = new GameObject("GazeLight");
            lightGo.transform.SetParent(medusa.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.62f, 0.22f);
            var lt = lightGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.range = 7f;
            lt.intensity = 0.9f;
            lt.color = new Color(0.55f, 0.10f, 0.10f);
            gaze.eyeLight = lt;

            sb.AppendLine("[Gameplay] Medusa：已生成 Medusa_W1 @ (0, 9.13, 4) 三层大平层中央，面朝 -Z（扶梯出口），视锥 ±34°，射程 22m");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Boss
        public static string InstallBoss()
        {
            var sb = new StringBuilder();

            var medusa = GameObject.Find("Medusa_W1");
            var player = FindPlayer();
            if (medusa == null || player == null)
            {
                sb.AppendLine("[Boss] 缺少 Medusa_W1 或玩家，跳过");
                return sb.ToString();
            }

            // 1) 碰撞体换为 CharacterController，供追猎移动使用
            var cap = medusa.GetComponent<CapsuleCollider>();
            if (cap != null) Object.DestroyImmediate(cap);
            var cc = medusa.GetComponent<CharacterController>();
            if (cc == null) cc = medusa.AddComponent<CharacterController>();
            cc.radius = 0.36f;
            cc.height = 1.78f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;

            // 2) Boss 主控
            var boss = medusa.GetComponent<MedusaBoss>();
            if (boss == null) boss = medusa.AddComponent<MedusaBoss>();
            boss.maxHealth = 900f;
            boss.huntSpeed = 2.8f;
            boss.rageSpeed = 4.6f;
            boss.rageThreshold = 0.55f;
            // 3F 刷新：不再靠贴身距离/视线唤醒（中庭挑空会隔层误判），改由 BossFloorTrigger 在玩家踏上三层时唤醒
            boss.wakeRadius = 2f;
            boss.wakeSightRange = 2f;
            boss.wakeByFloorTrigger = true;

            // 3) 玩家武器（先清掉 Boss 身上可能残留的玩家武器/血条，避免诊断与输入抢挂）
            var strayW = medusa.GetComponent<PlayerWeapon>();
            if (strayW != null) Object.DestroyImmediate(strayW);
            var strayH = medusa.GetComponent<BossHud>();
            if (strayH != null) Object.DestroyImmediate(strayH);

            if (player.GetComponent<PlayerWeapon>() == null)
            {
                var w = player.gameObject.AddComponent<PlayerWeapon>();
                w.damage = 34f;
                w.roundsPerSecond = 3.2f;
                w.range = 70f;
            }
            var cam = Camera.main;
            if (cam != null)
            {
                var w = player.GetComponent<PlayerWeapon>();
                if (w != null) w.viewCamera = cam;
            }

            // 4) Boss HUD
            if (player.GetComponent<BossHud>() == null)
            {
                var hud = player.gameObject.AddComponent<BossHud>();
                hud.boss = boss;
            }

            sb.AppendLine("[Boss] 美杜莎 HP " + boss.maxHealth.ToString("0") +
                           "，追猎速度 " + boss.huntSpeed.ToString("0.0") + "/" + boss.rageSpeed.ToString("0.0") +
                           "，三层入场触发唤醒（BossFloorTrigger）；玩家已配手枪 34 伤害 × 3.2 发/秒；Boss 血条已挂载");
            return sb.ToString();
        }


        // ------------------------------------------------------- Arena / 难度
        public static string InstallArena()
        {
            var sb = new StringBuilder();
            var medusa = GameObject.Find("Medusa_W1");
            if (medusa == null)
            {
                sb.AppendLine("[Arena] 缺少 Medusa_W1，跳过");
                return sb.ToString();
            }

            var bossComp = medusa.GetComponent<MedusaBoss>();
            var gate = medusa.GetComponent<BossArenaGate>();
            if (gate == null) gate = medusa.AddComponent<BossArenaGate>();
            gate.center = medusa.transform;
            gate.arenaRadius = 16f;
            gate.layerTolerance = 3f;
            // 3F 是开放大平层：屏障沿 Z 轴横在扶梯出口侧与屋顶楼梯侧，宽度拉满整层 X 进深
            gate.axis = Vector3.forward;
            gate.halfSpan = 9.4f;
            gate.barrierWidth = 52f;
            gate.barrierHeight = 4.2f;
            gate.groundY = 9.1f;

            // 玩家踏上三层（3F 地面 y=9.1）即触发 Boss 苏醒
            var floorTrigger = medusa.GetComponent<BossFloorTrigger>();
            if (floorTrigger == null) floorTrigger = medusa.AddComponent<BossFloorTrigger>();
            floorTrigger.boss = bossComp;
            floorTrigger.floorY = 9.1f;
            floorTrigger.tolerance = 1.0f;

            var loot = medusa.GetComponent<BossLoot>();
            if (loot == null) loot = medusa.AddComponent<BossLoot>();
            loot.spawnAt = medusa.transform;
            loot.offset = new Vector3(0f, 0.75f, 0f);
            loot.itemName = "锈蚀钥匙";
            loot.gate = gate;

            if (bossComp != null)
            {
                bossComp.arenaGate = gate;
                bossComp.loot = loot;
            }

            gate.CollectArenaDoors();

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine("[Arena] 战场内门 " + gate.ArenaDoorCount + " 扇；屏障 z=" +
                (medusa.transform.position.z - gate.halfSpan).ToString("0.0") + " / " +
                (medusa.transform.position.z + gate.halfSpan).ToString("0.0") +
                "，宽 " + gate.barrierWidth.ToString("0.0") + "m，高度 " + gate.barrierHeight.ToString("0.0") +
                "m 贴地 y=" + gate.groundY.ToString("0.0") +
                "；Boss 挂钩 gate=" + (bossComp != null && bossComp.arenaGate != null) +
                " loot=" + (bossComp != null && bossComp.loot != null) + "；场景已保存");
            return sb.ToString();
        }

        public static string ApplyDifficulty(float hp, float gaze, float recover, float rage, float rageHp,
            float hunt = -1f, float stop = -1f, int struggle = -1, float autoRel = -1f)
        {
            var sb = new StringBuilder();

            // 单一数据源：数值先落到 DifficultyProfile 资产，再由资产推给场景实例，
            // 避免「脚本默认值」与「场景序列化值」两份硬编码各改各的。
            var profile = DifficultySetup.EnsureAsset(out string assetLog);
            if (profile != null)
            {
                if (hp > 0f) profile.maxHealth = hp;
                if (gaze > 0f) profile.gazeRate = gaze;
                if (recover > 0f) profile.recoverRate = recover;
                if (rage > 0f) profile.rageSpeed = rage;
                if (rageHp > 0f) profile.rageThreshold = Mathf.Clamp01(rageHp);
                if (hunt > 0f) profile.huntSpeed = hunt;
                if (stop > 0f) profile.stopDistance = stop;
                if (struggle > 0) profile.struggleRequired = struggle;
                if (autoRel > 0f) profile.autoRelease = autoRel;
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                sb.Append(assetLog);
                sb.AppendLine("[Diff] 配置写入 " + DifficultyProfile.ResourcePath);
            }

            var medusa = GameObject.Find("Medusa_W1");
            if (medusa == null) { return "[Diff] 缺少 Medusa_W1"; }
            var boss = medusa.GetComponent<MedusaBoss>();
            if (boss != null)
            {
                boss.ApplyStats(hp, rage, rageHp, hunt, stop);
                if (profile != null) { profile.ApplyToBoss(boss); boss.profile = profile; EditorUtility.SetDirty(boss); }
                sb.AppendLine("[Diff] Boss HP " + boss.maxHealth.ToString("0") + "，狂暴移速 " + boss.rageSpeed.ToString("0.0") +
                              "，狂暴阈值 " + (boss.rageThreshold * 100f).ToString("0") + "%");
            }
            var gazeComp = medusa.GetComponent<MedusaGaze>();
            if (gazeComp == null) gazeComp = medusa.GetComponentInChildren<MedusaGaze>(true);
            if (gazeComp != null && profile != null)
            {
                profile.ApplyToGaze(gazeComp);
                gazeComp.profile = profile;
                EditorUtility.SetDirty(gazeComp);
            }
            var pet = Object.FindObjectOfType<PlayerPetrify>();
            if (pet != null)
            {
                pet.gazeRate = gaze;
                pet.recoverRate = recover;
                if (struggle > 0) pet.struggleRequired = struggle;
                if (autoRel > 0f) pet.autoRelease = autoRel;
                if (profile != null) { profile.ApplyToPetrify(pet); pet.profile = profile; EditorUtility.SetDirty(pet); }
                sb.AppendLine("[Diff] 凝视 " + pet.gazeRate.ToString("0") + "/s，恢复 " + pet.recoverRate.ToString("0") + "/s，" +
                              "挣脱 " + pet.struggleRequired + " 次 / " + pet.autoRelease.ToString("0.0") + "s");
            }
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            return sb.ToString();
        }

        public static string ArenaDiag()
        {
            var sb = new StringBuilder();
            var medusa = GameObject.Find("Medusa_W1");
            if (medusa == null) return "[Arena] 缺少 Medusa_W1";
            var gate = medusa.GetComponent<BossArenaGate>();
            if (gate == null) return "[Arena] 未挂 BossArenaGate";
            sb.AppendLine("[Arena] center=" + (gate.center != null ? gate.center.position.ToString("F2") : "null") +
                          " halfSpan=" + gate.halfSpan.ToString("F1") + " gate=" + gate.GateStateText() +
                          " 战场门=" + gate.ArenaDoorCount);
            var doors = Object.FindObjectsOfType<MedusaHorror.Gameplay.DoorInteractable>();
            int lockedCount = 0;
            foreach (var d in doors) if (d.Locked) lockedCount++;
            sb.AppendLine("[Arena] 全场景门 " + doors.Length + " 扇，已锁 " + lockedCount);
            var loot = medusa.GetComponent<BossLoot>();
            sb.AppendLine("[Arena] loot=" + (loot != null ? ("item=" + loot.itemName + " dropped=" + loot.dropped) : "null"));
            return sb.ToString();
        }

        static void Strip<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c != null) Object.DestroyImmediate(c);
        }
    }
}
