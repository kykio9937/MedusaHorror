using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Player;
using MedusaHorror.UI;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 近战武器装配工具（调试通道调用）：
    /// 1) Probe：探测玩家层级、手部骨骼候选、剑模型包围盒；
    /// 2) Build：把 Claymore 模型挂到玩家右手，装配 MeleeWeapon / WeaponLoadout / WeaponHud / CameraShake；
    /// 3) Info / Swing / Swap：Play 模式下的实测诊断入口。
    /// </summary>
    public static class MeleeSetup
    {
        public const string ScenePath = "Assets/Scenes/W1_Hospital.unity";
        public const string SwordPrefabPath = "Assets/Art/Weapons/Claymore/Kazuko_claymore.prefab";
        public const string SwordFbxPath = "Assets/Art/Weapons/Claymore/Kazuko_claymore.fbx";
        public const string RigName = "SwordRig";
        public const string BladeName = "SwordBlade";

        // ---------------- 探测 ----------------

        /// <summary>设置近身石化减压倍率并保存场景，保证改完即生效且持久。</summary>
        public static string SetClose(float f)
        {
            var all = Object.FindObjectsOfType<MedusaHorror.Gameplay.MedusaGaze>();
            if (all == null || all.Length == 0) return "[SETCLOSE] NO_GAZE";
            float v = Mathf.Clamp(f, 0.05f, 1f);
            for (int i = 0; i < all.Length; i++)
            {
                all[i].closeWeightFactor = v;
                UnityEditor.EditorUtility.SetDirty(all[i]);
            }
            if (!EditorApplication.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            }
            return "[SETCLOSE] gazes=" + all.Length + " closeWeightFactor=" + v.ToString("F2")
                + " saved=" + (!EditorApplication.isPlaying);
        }
        /// <summary>多目标验证用：克隆 Boss 生成无 AI 的受击靶（关闭 MedusaBoss/MedusaGaze 逻辑，仅保留碰撞体）。</summary>
        public static string SpawnDummy(int count)
        {
            var boss = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            if (boss == null) return "[DUMMY] NO_BOSS";
            Vector3 p = boss.transform.position;
            int made = 0;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                var go = Object.Instantiate(boss.gameObject);
                go.name = "MeleeDummy_" + i;
                float off = (i % 2 == 0 ? 1f : -1f) * (0.9f + 0.6f * (i / 2));
                go.transform.position = new Vector3(p.x, p.y, p.z + off);
                go.transform.rotation = boss.transform.rotation;
                var mb = go.GetComponent<MedusaHorror.Gameplay.MedusaBoss>();
                if (mb != null) mb.enabled = false;
                var gz = go.GetComponentInChildren<MedusaHorror.Gameplay.MedusaGaze>();
                if (gz != null) gz.enabled = false;
                made++;
            }
            return "[DUMMY] spawned=" + made + " at boss=" + boss.name;
        }

        /// <summary>反射调用 PlayerWeapon 的私有 TryFire，验证切枪后射击通路仍可用。</summary>
        public static string ForceFire(int times)
        {
            var player = FindPlayer(out string findLog);
            if (player == null) return "[FIREGUN] PLAYER_NOT_FOUND " + findLog;
            var gun = player.GetComponent<PlayerWeapon>();
            if (gun == null) return "[FIREGUN] NO_PLAYERWEAPON";
            var m = typeof(PlayerWeapon).GetMethod("TryFire",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (m == null) return "[FIREGUN] NO_TRYFIRE";
            var boss = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            float before = boss != null ? boss.Health : -1f;
            int n = Mathf.Max(1, times);
            for (int i = 0; i < n; i++) m.Invoke(gun, null);
            float after = boss != null ? boss.Health : -1f;
            return "[FIREGUN] calls=" + n + " shots=" + gun.ShotsFired + " mag=" + gun.AmmoInMagazine
                + " hpBefore=" + before.ToString("F1") + " hpAfter=" + after.ToString("F1")
                + " gunActive=" + gun.inputEnabled;
        }

        /// <summary>碰撞体诊断：Boss 碰撞体层级/包围盒 + 玩家周围 OverlapSphere 命中列表。</summary>
        public static string ColliderDbg()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[COLLIDERDBG] playing=" + EditorApplication.isPlaying);
            var bosses = Object.FindObjectsOfType<MedusaHorror.Gameplay.MedusaBoss>();
            sb.AppendLine("bosses=" + bosses.Length);
            for (int i = 0; i < bosses.Length; i++)
            {
                var b = bosses[i];
                sb.AppendLine("boss=" + b.name + " pos=" + Fmt(b.transform.position)
                    + " layer=" + b.gameObject.layer + " scale=" + Fmt(b.transform.lossyScale));
                var cols = b.GetComponentsInChildren<Collider>(true);
                sb.AppendLine("  colliders=" + cols.Length);
                for (int j = 0; j < cols.Length; j++)
                {
                    var c = cols[j];
                    sb.AppendLine("   - " + Path(c.transform) + " " + c.GetType().Name
                        + " trig=" + c.isTrigger + " enabled=" + c.enabled + " layer=" + c.gameObject.layer
                        + " center=" + Fmt(c.bounds.center) + " ext=" + Fmt(c.bounds.extents));
                }
            }

            var player = FindPlayer(out string findLog);
            if (player == null) { sb.AppendLine("player=NOT_FOUND " + findLog); return sb.ToString(); }
            Vector3 o = player.transform.position + Vector3.up * 1.05f;
            var hits = Physics.OverlapSphere(o, 3.0f, ~0, QueryTriggerInteraction.Collide);
            sb.AppendLine("sphere o=" + Fmt(o) + " r=3.00 cols=" + hits.Length);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i];
                if (c == null) continue;
                var bb = c.GetComponentInParent<MedusaHorror.Gameplay.MedusaBoss>();
                sb.AppendLine("   > " + c.name + " layer=" + c.gameObject.layer + (bb != null ? " BOSS=" + bb.name : ""));
            }
            return sb.ToString();
        }

        public static string Probe()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MELEEPROBE] scene=" + EditorSceneManager.GetActiveScene().name + " playing=" + EditorApplication.isPlaying);

            var player = FindPlayer(out string findLog);
            sb.AppendLine("find=" + findLog);
            if (player == null) { sb.AppendLine("PLAYER=NOT_FOUND"); return sb.ToString(); }

            sb.AppendLine("player=" + Path(player.transform) + " pos=" + Fmt(player.transform.position));
            AppendTree(sb, player.transform, 0, 3);

            sb.AppendLine("-- hand candidates --");
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("hand") || n.Contains("wrist") || n.Contains("palm") || n.Contains("finger"))
                    sb.AppendLine("  " + Path(t) + " pos=" + Fmt(t.position));
            }

            sb.AppendLine("-- existing components --");
            sb.AppendLine("  PlayerWeapon=" + (player.GetComponent<PlayerWeapon>() != null));
            sb.AppendLine("  MeleeWeapon=" + (player.GetComponent<MeleeWeapon>() != null));
            sb.AppendLine("  WeaponLoadout=" + (player.GetComponent<WeaponLoadout>() != null));
            sb.AppendLine("  WeaponHud=" + (player.GetComponent<WeaponHud>() != null));
            sb.AppendLine("  InputReader=" + (player.GetComponent<InputReader>() != null));
            var cam = Camera.main;
            sb.AppendLine("  Camera=" + (cam == null ? "null" : Path(cam.transform)) + " CameraShake=" + (cam != null && cam.GetComponent<CameraShake>() != null));

            sb.AppendLine("-- prefab bounds --");
            sb.AppendLine(SwordBoundsInfo());

            sb.AppendLine("-- gun rig --");
            var gun = player.GetComponent<PlayerWeapon>();
            if (gun != null)
            {
                var holder = player.transform.Find("WeaponRig");
                sb.AppendLine("  WeaponRig=" + (holder == null ? "null" : "localPos=" + Fmt(holder.localPosition) + " localRot=" + Fmt(holder.localEulerAngles)));
            }

            return sb.ToString();
        }

        public static string SwordBoundsInfo()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
            if (prefab == null) return "prefab=NOT_FOUND path=" + SwordPrefabPath;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(0f, -500f, 0f);
            Bounds b = default(Bounds);
            bool has = false;
            int renderers = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            var sb = new StringBuilder();
            sb.AppendLine("  prefab=" + prefab.name + " renderers=" + renderers + " children=" + go.transform.childCount);
            if (has)
            {
                sb.AppendLine("  worldSize=" + Fmt(b.size) + " localScale=" + Fmt(go.transform.localScale));
                sb.AppendLine("  maxDim=" + Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)).ToString("F3"));
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                sb.AppendLine("   node " + Path(t) + " localScale=" + Fmt(t.localScale));
            Object.DestroyImmediate(go);
            return sb.ToString();
        }

        // ---------------- 装配 ----------------

        public static string Build(string[] parts)
        {
            if (EditorApplication.isPlaying) return "[MELEEBUILD] 播放中拒绝，请先 STOPPLAY";

            // 可选参数：MELEEBUILD|rotX|rotY|rotZ|posX|posY|posZ|length|alignHand
            float rotX = ArgF(parts, 1, 0f), rotY = ArgF(parts, 2, 0f), rotZ = ArgF(parts, 3, 0f);
            float posX = ArgF(parts, 4, 0f), posY = ArgF(parts, 5, 0f), posZ = ArgF(parts, 6, 0f);
            float length = ArgF(parts, 7, 1.30f);
            bool alignHand = ArgF(parts, 8, 1f) > 0.5f;

            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) EditorSceneManager.OpenScene(ScenePath);

            var player = FindPlayer(out string findLog);
            if (player == null) return "[MELEEBUILD] PLAYER_NOT_FOUND " + findLog;

            var sb = new StringBuilder();
            sb.AppendLine("[MELEEBUILD] player=" + Path(player.transform));

            var cam = Camera.main;
            if (cam == null) return "[MELEEBUILD] NO_MAIN_CAMERA";
            var shake = cam.GetComponent<CameraShake>();
            if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();
            sb.AppendLine("CameraShake=" + shake.GetType().Name + " positionScale=" + shake.positionScale);

            var input = player.GetComponent<InputReader>();
            if (input == null) input = player.gameObject.AddComponent<InputReader>();

            var gun = player.GetComponent<PlayerWeapon>();
            if (gun == null) gun = player.gameObject.AddComponent<PlayerWeapon>();
            gun.inputEnabled = true;

            // ---- 剑体 ----
            var hand = FindHand(player.transform);
            Transform parentForRig = hand != null ? hand : player.transform;

            // 幂等：先清掉玩家层级内所有同名 SwordRig（旧实现重复 Build 会叠出多把剑）
            var stale = new List<GameObject>();
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == RigName) stale.Add(t.gameObject);
            for (int i = 0; i < stale.Count; i++)
                if (stale[i] != null) Object.DestroyImmediate(stale[i]);
            if (stale.Count > 0) sb.AppendLine("staleRigsRemoved=" + stale.Count);

            var rigGo = new GameObject(RigName);
            rigGo.transform.SetParent(parentForRig, false);

            var bladeTr = rigGo.transform.Find(BladeName);
            if (bladeTr == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordPrefabPath);
                if (prefab == null) return "[MELEEBUILD] SWORD_PREFAB_NOT_FOUND " + SwordPrefabPath;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.name = BladeName;
                inst.transform.SetParent(rigGo.transform, false);
                bladeTr = inst.transform;
                FitSword(bladeTr, length);
                sb.AppendLine("blade=created scale=" + Fmt(bladeTr.localScale) + " rot=" + Fmt(bladeTr.localEulerAngles));
            }
            else sb.AppendLine("blade=existing");

            if (hand != null)
            {
                rigGo.transform.localPosition = new Vector3(posX, posY, posZ);
                if (alignHand)
                {
                    // 剑身长轴为本地 +Z，把它对齐到「手腕 → 中指根」方向，保证剑指向手部前方
                    var finger = FindMiddleFinger(hand);
                    Quaternion baseRot = rigGo.transform.localRotation;
                    if (finger != null)
                    {
                        Vector3 dir = finger.position - hand.position;
                        if (dir.sqrMagnitude > 0.000001f)
                            baseRot = Quaternion.FromToRotation(Vector3.forward, dir.normalized);
                    }
                    rigGo.transform.rotation = baseRot * Quaternion.Euler(rotX, rotY, rotZ);
                }
                else rigGo.transform.localRotation = Quaternion.Euler(rotX, rotY, rotZ);
            }
            else
            {
                // 没有可用手骨：挂到玩家根节点右前侧
                rigGo.transform.localPosition = new Vector3(0.28f + posX, 1.15f + posY, 0.25f + posZ);
                rigGo.transform.localRotation = Quaternion.Euler(-10f + rotX, rotY, rotZ);
            }
            sb.AppendLine("rigParent=" + Path(rigGo.transform.parent) + " localPos=" + Fmt(rigGo.transform.localPosition)
                + " worldRot=" + Fmt(rigGo.transform.eulerAngles));
            sb.AppendLine("bladeTip=" + Fmt(BladeTip(bladeTr)) + " hand=" + (hand == null ? "none" : Path(hand)));

            // ---- 组件 ----
            var hud = player.GetComponent<WeaponHud>();
            if (hud == null) hud = player.gameObject.AddComponent<WeaponHud>();

            var melee = player.GetComponent<MeleeWeapon>();
            if (melee == null) melee = player.gameObject.AddComponent<MeleeWeapon>();
            melee.attackRange = 2.2f;
            melee.attackAngle = 100f;
            melee.damage = 55f;
            melee.attackInterval = 0.55f;
            melee.maxTargets = 4;
            melee.maxHeightDiff = 2.0f;
            melee.input = input;
            melee.bladeRoot = bladeTr;
            melee.modelRoot = rigGo.transform;
            melee.shake = shake;
            melee.hud = hud;
            melee.firearm = gun;

            var loadout = player.GetComponent<WeaponLoadout>();
            if (loadout == null) loadout = player.gameObject.AddComponent<WeaponLoadout>();
            loadout.sword = melee;
            loadout.gun = gun;
            loadout.input = input;
            loadout.hud = hud;
            loadout.startWeapon = WeaponKind.Sword;
            loadout.useNumberKeys = true;
            loadout.useToggleKey = true;

            hud.sword = melee;
            hud.gun = gun;

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(cam.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            sb.AppendLine("saved=" + EditorSceneManager.GetActiveScene().path);
            sb.AppendLine("melee range=" + melee.attackRange + " angle=" + melee.attackAngle + " dmg=" + melee.damage + " interval=" + melee.attackInterval);

            // 幂等自检：装配完成后玩家层级下应当只剩一个 SwordRig
            int rigCount = 0;
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == RigName) rigCount++;
            sb.AppendLine("rigCount=" + rigCount);
            return sb.ToString();
        }

        static float ArgF(string[] parts, int index, float def)
        {
            if (parts == null || parts.Length <= index) return def;
            float v;
            return float.TryParse(parts[index], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out v) ? v : def;
        }

        static Transform FindMiddleFinger(Transform hand)
        {
            foreach (var t in hand.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("middlefinger1")) return t;
            }
            return hand.childCount > 0 ? hand.GetChild(0) : null;
        }

        static Vector3 BladeTip(Transform blade)
        {
            Bounds b = default(Bounds);
            bool has = false;
            foreach (var r in blade.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) return blade.position;
            Vector3 f = blade.forward;
            Vector3 e = b.extents;
            float proj = Mathf.Abs(f.x) * e.x + Mathf.Abs(f.y) * e.y + Mathf.Abs(f.z) * e.z;
            return b.center + f * proj;
        }

        static void FitSword(Transform blade, float targetLength)
        {
            Bounds b = default(Bounds);
            bool has = false;
            foreach (var r in blade.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (!has) return;
            float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            if (maxDim < 0.0001f) return;
            float s = targetLength / maxDim;
            blade.localScale = blade.localScale * s;
        }

        static Transform FindHand(Transform root)
        {
            Transform best = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("finger") || n.Contains("thumb")) continue;
                bool right = n.EndsWith("_r") || n.Contains("_right") || n.Contains("right");
                bool handLike = n.Contains("wrist") || n.Contains("hand") || n.Contains("palm");
                if (handLike && right) { best = t; break; }
            }
            return best;
        }

        static Transform FindPlayer(out string log)
        {
            var byTag = GameObject.FindGameObjectWithTag("Player");
            if (byTag != null)
            {
                log = "tag=Player path=" + Path(byTag.transform);
                return byTag.transform;
            }
            var weapons = Object.FindObjectsOfType<PlayerWeapon>(true);
            if (weapons != null && weapons.Length > 0)
            {
                log = "byPlayerWeapon path=" + Path(weapons[0].transform);
                return weapons[0].transform;
            }
            var controllers = Object.FindObjectsOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (controllers != null && controllers.Length > 0)
            {
                log = "byController path=" + Path(controllers[0].transform);
                return controllers[0].transform;
            }
            log = "no player";
            return null;
        }

        // ---------------- 运行时诊断 ----------------

        public static string Info()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[MELEEINFO] playing=" + EditorApplication.isPlaying + " scene=" + EditorSceneManager.GetActiveScene().name);
            var player = FindPlayer(out string findLog);
            if (player == null) return sb.AppendLine("player=" + findLog).ToString();

            var loadout = player.GetComponent<WeaponLoadout>();
            var melee = player.GetComponent<MeleeWeapon>();
            var gun = player.GetComponent<PlayerWeapon>();
            var hud = player.GetComponent<WeaponHud>();
            var petrify = player.GetComponent<MedusaHorror.Gameplay.PlayerPetrify>();
            var flow = MedusaHorror.Core.GameFlow.Instance;

            if (loadout != null)
                sb.AppendLine("loadout current=" + loadout.Current + " switches=" + loadout.SwitchCount + " last=" + loadout.LastSwitchSource);
            else sb.AppendLine("loadout=null");

            if (melee != null)
            {
                sb.AppendLine("melee enabled=" + melee.inputEnabled + " ready=" + melee.Ready
                    + " cd=" + melee.CooldownRemaining.ToString("F2")
                    + " swings=" + melee.SwingCount + " hits=" + melee.TotalHits
                    + " lastTargets=" + melee.LastSwingTargets + " lastDmg=" + melee.LastSwingDamage
                    + " result=" + melee.LastSwingResult);
                sb.AppendLine("diag=" + melee.LastSwingDiag);
            sb.AppendLine("melee modelActive=" + (melee.modelRoot != null && melee.modelRoot.gameObject.activeInHierarchy)
                    + " bladeBound=" + (melee.bladeRoot != null));
            }
            else sb.AppendLine("melee=null");

            if (gun != null)
                sb.AppendLine("gun inputEnabled=" + gun.inputEnabled + " mag=" + gun.AmmoInMagazine + " reserve=" + gun.ReserveAmmo + " reloading=" + gun.IsReloading);

            if (hud != null) sb.AppendLine("hud kind=" + hud.CurrentKind + " weapon=" + hud.WeaponLabel + " state=" + hud.StateLabel);

            if (petrify != null)
                sb.AppendLine("petrify value=" + petrify.Value.ToString("F1") + " petrified=" + petrify.Petrified);

            sb.AppendLine("flow=" + (flow == null ? "null" : flow.State.ToString()));

            var boss = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            if (boss != null)
            {
                sb.AppendLine("boss hp=" + boss.Health.ToString("F1") + "/" + boss.maxHealth + " rage=" + boss.IsRaging + " dead=" + boss.IsDead + " state=" + boss.Current);
                var gaze = boss.GetComponent<MedusaHorror.Gameplay.MedusaGaze>();
                if (gaze != null)
                    sb.AppendLine("gaze dist=" + gaze.LastDist.ToString("F2") + " ang=" + gaze.LastAngle.ToString("F1")
                        + " weight=" + gaze.LastWeight.ToString("F3") + " closeFactor=" + gaze.LastCloseFactor.ToString("F3")
                        + " rate=" + gaze.LastRate.ToString("F2") + " block=" + gaze.LastBlock);
            }
            else sb.AppendLine("boss=null");

            if (melee != null && melee.transform != null) { }
            sb.AppendLine("cursor=" + Cursor.lockState);
            return sb.ToString();
        }

        public static string ForceSwing(int times)
        {
            var player = FindPlayer(out string findLog);
            if (player == null) return "[SWING] player=" + findLog;
            var melee = player.GetComponent<MeleeWeapon>();
            if (melee == null) return "[SWING] melee=null";
            MeleeWeapon.debugSwingPulse = true;
            return "[SWING] pulse=true times=" + times + " ready=" + melee.Ready + " cd=" + melee.CooldownRemaining.ToString("F2");
        }

        public static string VisFix()
        {
            var player = FindPlayer(out string findLog);
            if (player == null) return "[VISFIX] player=" + findLog;
            var melee = player.GetComponent<MeleeWeapon>();
            if (melee == null || melee.modelRoot == null) return "[VISFIX] modelRoot=null";
            var keep = melee.modelRoot;
            var parent = keep.parent;
            var victims = new System.Collections.Generic.List<GameObject>();
            foreach (var tr in parent.GetComponentsInChildren<Transform>(true))
            {
                if (tr == null || tr == keep) continue;
                if (tr.parent != parent) continue;
                if (tr.name != keep.name) continue;
                victims.Add(tr.gameObject);
            }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[VISFIX] keep=" + keep.name + " fileId=" + keep.gameObject.GetInstanceID()
                + " siblings=" + victims.Count);
            foreach (var v in victims)
            {
                sb.AppendLine("remove=" + v.name + " instance=" + v.GetInstanceID());
                UnityEngine.Object.DestroyImmediate(v);
            }
            if (victims.Count > 0)
            {
                var scene = keep.gameObject.scene;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                sb.AppendLine("saved=" + ScenePath);
            }
            return sb.ToString();
        }

        public static string VisCheck()
        {
            var player = FindPlayer(out string findLog);
            if (player == null) return "[VIS] player=" + findLog;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[VIS] player=" + player.name);
            var melee = player.GetComponent<MeleeWeapon>();
            if (melee != null)
            {
                sb.AppendLine("melee.modelRoot=" + PathOf(player.transform, melee.modelRoot)
                    + " active=" + (melee.modelRoot != null && melee.modelRoot.gameObject.activeInHierarchy));
                sb.AppendLine("melee.bladeRoot=" + PathOf(player.transform, melee.bladeRoot)
                    + " active=" + (melee.bladeRoot != null && melee.bladeRoot.gameObject.activeInHierarchy));
            }
            var gun = player.GetComponent<PlayerWeapon>();
            if (gun != null) sb.AppendLine("gun.enabled=" + gun.enabled + " go=" + gun.gameObject.name);
            sb.AppendLine("-- 名称含 Sword/Weapon/Gun 的层级节点 --");
            foreach (var tr in player.GetComponentsInChildren<Transform>(true))
            {
                if (tr == null) continue;
                string nm = tr.name;
                if (nm.IndexOf("Sword", System.StringComparison.OrdinalIgnoreCase) < 0
                    && nm.IndexOf("Weapon", System.StringComparison.OrdinalIgnoreCase) < 0
                    && nm.IndexOf("Gun", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                int total = 0, visible = 0;
                foreach (var r in tr.GetComponentsInChildren<Renderer>(true))
                {
                    total++;
                    if (r.enabled && r.gameObject.activeInHierarchy) visible++;
                }
                sb.AppendLine(PathOf(player.transform, tr) + " selfActive=" + tr.gameObject.activeSelf
                    + " inHierarchy=" + tr.gameObject.activeInHierarchy
                    + " renderers=" + total + " visible=" + visible);
            }
            var loadout = player.GetComponent<WeaponLoadout>();
            if (loadout != null) sb.AppendLine("loadout current=" + loadout.Current);
            int allTotal = 0, allVisible = 0;
            var visNames = new System.Text.StringBuilder();
            foreach (var r in player.GetComponentsInChildren<Renderer>(true))
            {
                allTotal++;
                if (r.enabled && r.gameObject.activeInHierarchy)
                {
                    allVisible++;
                    string mn = r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null
                        ? r.GetComponent<MeshFilter>().sharedMesh.name : "-";
                    if (mn.IndexOf("claymore", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || mn.IndexOf("kazuko", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || mn.IndexOf("Sword", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || mn.IndexOf("Cube", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        visNames.Append(r.name + "[" + mn + "] ");
                }
            }
            sb.AppendLine("playerRenderers total=" + allTotal + " visible=" + allVisible);
            sb.AppendLine("visibleWeaponLike=" + (visNames.Length > 0 ? visNames.ToString() : "none"));
            return sb.ToString();
        }

        static string PathOf(Transform root, Transform target)
        {
            if (target == null) return "null";
            var stack = new System.Collections.Generic.List<string>();
            var cur = target;
            while (cur != null && cur != root) { stack.Add(cur.name); cur = cur.parent; }
            stack.Reverse();
            return string.Join("/", stack.ToArray());
        }

        public static string AutoSwing(float seconds)
        {
            var player = FindPlayer(out string findLog);
            if (player == null) return "[AUTO] player=" + findLog;
            var melee = player.GetComponent<MeleeWeapon>();
            if (melee == null) return "[AUTO] melee=null";
            float sec = Mathf.Clamp(seconds <= 0f ? 20f : seconds, 0.5f, 300f);
            MeleeWeapon.debugAutoSwingSeconds = sec;
            return "[AUTO] seconds=" + sec.ToString("F2") + " swingsBefore=" + melee.SwingCount
                + " interval=" + melee.attackInterval.ToString("F2") + " ready=" + melee.Ready;
        }

        public static string ForceSwap(int kind)
        {
            var player = FindPlayer(out string findLog);
            if (player == null) return "[SWAP] player=" + findLog;
            var loadout = player.GetComponent<WeaponLoadout>();
            if (loadout == null) return "[SWAP] loadout=null";
            WeaponLoadout.debugSwitchTo = kind;
            return "[SWAP] request=" + kind;
        }

        // ---------------- 石化速率采样 ----------------

        static bool _petRunning;
        static float _petT0;
        static float _petStartVal;
        static float _petMax;
        static float _petLastSample;
        static StringBuilder _petLog;

        public static string PetStart(float maxSeconds)
        {
            if (!EditorApplication.isPlaying) return "[PETSTART] NOT_PLAYING";
            var pet = Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerPetrify>();
            if (pet == null) return "[PETSTART] NO_PLAYER_PETRIFY";
            var gaze = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaGaze>();
            _petRunning = true;
            _petT0 = Time.time;
            _petStartVal = pet.Value;
            _petMax = Mathf.Clamp(maxSeconds, 1f, 600f);
            _petLastSample = -1f;
            _petLog = new StringBuilder();
            _petLog.AppendLine("[PETTEST] maxSec=" + _petMax.ToString("F1")
                + " startVal=" + _petStartVal.ToString("F1")
                + " closeFactor=" + (gaze != null ? gaze.closeWeightFactor.ToString("F2") : "?"));
            EditorApplication.update -= PetTick;
            EditorApplication.update += PetTick;
            return "[PETSTART] running startVal=" + _petStartVal.ToString("F1")
                + " closeFactor=" + (gaze != null ? gaze.closeWeightFactor.ToString("F2") : "?");
        }

        static void PetTick()
        {
            if (!_petRunning) { EditorApplication.update -= PetTick; return; }
            if (!EditorApplication.isPlaying) { FinishPet("PLAY_END"); return; }
            float el = Time.time - _petT0;
            if (el - _petLastSample < 0.2f) return;
            _petLastSample = el;
            var pet = Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerPetrify>();
            if (pet == null) { FinishPet("NO_PETRIFY"); return; }
            var gaze = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaGaze>();
            _petLog.AppendLine("t=" + el.ToString("F2") + " v=" + pet.Value.ToString("F1")
                + " pet=" + pet.Petrified
                + " d=" + (gaze != null ? gaze.LastDist.ToString("F2") : "?")
                + " rate=" + (gaze != null ? gaze.LastRate.ToString("F2") : "?"));
            if (pet.Petrified || pet.Value >= 99.5f) { FinishPet("FULL"); return; }
            if (el >= _petMax) { FinishPet("TIMEOUT"); }
        }

        static void FinishPet(string reason)
        {
            _petRunning = false;
            EditorApplication.update -= PetTick;
            var pet = Object.FindObjectOfType<MedusaHorror.Gameplay.PlayerPetrify>();
            float dur = pet != null ? Time.time - _petT0 : _petMax;
            float lastVal = pet != null ? pet.Value : 0f;
            _petLog.AppendLine("RESULT reason=" + reason + " dur=" + dur.ToString("F2")
                + "s start=" + _petStartVal.ToString("F1") + " end=" + lastVal.ToString("F1"));
        }

        public static string PetResult()
        {
            if (_petLog == null) return "[PETTEST] no data";
            var s = _petLog.ToString();
            return s.Length > 6000 ? s.Substring(s.Length - 6000) : s;
        }

        public static string PosInfo()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[POSINFO] playing=" + EditorApplication.isPlaying);
            var player = FindPlayer(out string findLog);
            var boss = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            if (player != null)
                sb.AppendLine("player=" + Fmt(player.transform.position) + " fwd=" + Fmt(player.transform.forward) + " " + findLog);
            if (boss != null)
                sb.AppendLine("boss=" + Fmt(boss.transform.position) + " dist=" + Vector3.Distance(
                    player != null ? player.transform.position : Vector3.zero, boss.transform.position).ToString("F2"));
            var gaze = Object.FindObjectOfType<MedusaHorror.Gameplay.MedusaGaze>();
            if (gaze != null)
                sb.AppendLine("gaze closeWeightFactor=" + gaze.closeWeightFactor.ToString("F2")
                    + " lastRate=" + gaze.LastRate.ToString("F2")
                    + " dist=" + gaze.LastDist.ToString("F2")
                    + " weight=" + gaze.LastWeight.ToString("F3"));
            return sb.ToString();
        }

        // ---------------- 工具 ----------------

        static string Path(Transform t)
        {
            if (t == null) return "null";
            var sb = new StringBuilder(t.name);
            var p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 32) { sb.Insert(0, p.name + "/"); p = p.parent; }
            return sb.ToString();
        }

        static string Fmt(Vector3 v) { return "(" + v.x.ToString("F3") + "," + v.y.ToString("F3") + "," + v.z.ToString("F3") + ")"; }

        static void AppendTree(StringBuilder sb, Transform t, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;
            string pad = new string(' ', depth * 2);
            var smr = t.GetComponent<SkinnedMeshRenderer>();
            var mr = t.GetComponent<MeshRenderer>();
            sb.AppendLine("  " + pad + t.name + (smr != null ? " [skinned]" : (mr != null ? " [mesh]" : "")));
            for (int i = 0; i < t.childCount; i++) AppendTree(sb, t.GetChild(i), depth + 1, maxDepth);
        }
    }
}
