using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.UI;
using MedusaHorror.Gameplay;

namespace MedusaHorror.Player
{
    public enum WeaponKind
    {
        Sword = 0,
        Gun = 1,
        /// <summary>脉冲步枪：等级 5 解锁的第三把武器。</summary>
        Rifle = 2
    }

    /// <summary>
    /// 武器切换中枢：剑（MeleeWeapon）/ 枪（PlayerWeapon）共用同一套输入（左键）与 HUD。
    /// 数字键 1 切剑、2 切枪，Q 在两者间来回切换；同一时刻只有一把武器可见且响应输入。
    /// 枪械封锁：gunUnlocked=false 时数字键 2 / Q 切枪一律驳回，枪体不显示、HUD 无枪位；
    /// Boss 被击杀（IsDead）后自动解锁，恢复 2 / Q 切枪。
    /// </summary>
    [DisallowMultipleComponent]
    public class WeaponLoadout : MonoBehaviour
    {
        public MeleeWeapon sword;
        public PlayerWeapon gun;
        [Tooltip("脉冲步枪（等级 5 解锁），留空则运行时自动创建")]
        public PlayerWeapon rifle;
        public InputReader input;
        public WeaponHud hud;
        public WeaponKind startWeapon = WeaponKind.Sword;
        public bool useNumberKeys = true;
        public bool useToggleKey = true;

        [Header("枪械封锁（本关只允许用剑，通关解锁）")]
        [Tooltip("false = 枪械封存：数字键 2 与 Q 切枪无效、Pistol Rig 隐藏、HUD 不显示枪位")]
        public bool gunUnlocked = false;
        [Tooltip("Boss 被击杀后自动解锁枪械")]
        public bool autoUnlockOnBossDefeat = true;
        [Tooltip("探测 Boss 死亡状态的轮询间隔（秒）")]
        public float bossPollInterval = 0.5f;

        public WeaponKind Current { get; private set; }
        public int SwitchCount { get; private set; }
        public int BlockedSwitches { get; private set; }
        public string LastSwitchSource { get; private set; }
        public string LastBlockedSource { get; private set; }
        public bool GunUnlocked { get { return gunUnlocked; } }

        /// <summary>诊断用：>-1 时下一帧切到该武器（0 剑 / 1 枪），与真实输入走同一判定。</summary>
        public static int debugSwitchTo = -1;

        /// <summary>诊断用：1=数字键 1，2=数字键 2，3=Q，下一帧按真实输入路径处理一次。</summary>
        public static int debugPressKey = 0;

        bool _bossSeen;
        float _bossPollTimer;
        float _lastToastTime = -10f;

        void Awake()
        {
            if (input == null) input = GetComponent<InputReader>();
            if (sword == null) sword = GetComponent<MeleeWeapon>();
            if (gun == null) gun = GetComponent<PlayerWeapon>();
            if (hud == null) hud = GetComponent<WeaponHud>();
            EnsureRifle();
            if (LastSwitchSource == null) LastSwitchSource = "初始化";
        }

        /// <summary>宽松解析引用：场景重建后序列化引用可能失效，按组件层级就近重取。</summary>
        void ResolveRefs()
        {
            if (input == null) input = GetComponent<InputReader>();
            if (sword == null)
            {
                sword = GetComponent<MeleeWeapon>();
                if (sword == null) sword = GetComponentInChildren<MeleeWeapon>(true);
            }
            if (gun == null)
            {
                gun = GetComponent<PlayerWeapon>();
                if (gun == null) gun = GetComponentInChildren<PlayerWeapon>(true);
            }
            if (hud == null)
            {
                hud = GetComponent<WeaponHud>();
                if (hud == null) hud = GetComponentInChildren<WeaponHud>(true);
            }
            EnsureRifle();
            if (LastSwitchSource == null) LastSwitchSource = "初始化";
        }

        /// <summary>运行时创建脉冲步枪：独立枪模、独立弹道参数，与手枪共用输入。</summary>
        void EnsureRifle()
        {
            if (rifle != null) return;
            var go = new GameObject("PulseRifle");
            go.transform.SetParent(transform, false);
            var w = go.AddComponent<PlayerWeapon>();
            w.damage = 24f;
            w.roundsPerSecond = 8.5f;
            w.magazineSize = 30;
            w.reserveAmmo = 300;
            w.reloadTime = 1.9f;
            w.range = 90f;
            w.spreadBase = 0.55f;
            w.spreadPerShot = 0.42f;
            w.spreadMax = 3.6f;
            w.spreadRecoverPerSecond = 5.5f;
            w.recoilPerShot = 0.9f;
            w.aimHeight = 1.35f;
            w.bodyLength = 0.46f;
            w.bodyHeight = 0.10f;
            w.bodyWidth = 0.075f;
            w.bodyColor = new Color(0.12f, 0.16f, 0.22f, 1f);
            w.gripColor = new Color(0.07f, 0.09f, 0.12f, 1f);
            w.emissive = true;
            w.emissiveColor = new Color(0.25f, 0.85f, 1f, 1f);
            w.inputEnabled = false;
            rifle = w;
        }

        void Start()
        {
            ResolveRefs();
            Equip(startWeapon, "初始化");
        }

        void Update()
        {
            if (gun == null || sword == null || hud == null) ResolveRefs();

            if (autoUnlockOnBossDefeat && !gunUnlocked) PollBossDefeat();

            if (debugSwitchTo >= 0)
            {
                var k = debugSwitchTo == 0 ? WeaponKind.Sword
                      : (debugSwitchTo == 2 ? WeaponKind.Rifle : WeaponKind.Gun);
                debugSwitchTo = -1;
                Equip(k, "诊断命令");
                return;
            }

            if (debugPressKey != 0)
            {
                int key = debugPressKey;
                debugPressKey = 0;
                HandlePress(key, "诊断按键");
                return;
            }

            if (input == null) return;

            // 暂停 / 结算面板下不响应切换（避免 Q 与暂停面板的退出键冲突）
            var flow = GameFlow.Instance;
            if (flow != null && !flow.IsRunning) return;

            if (useNumberKeys)
            {
                if (input.Weapon1Pressed) { HandlePress(1, "数字键 1"); return; }
                if (input.Weapon2Pressed) { HandlePress(2, "数字键 2"); return; }
                if (input.Weapon3Pressed) { HandlePress(3, "数字键 3"); return; }
            }
            if (useToggleKey && input.SwitchWeaponPressed) HandlePress(3, "Q 切换");
        }

        /// <summary>统一按键入口：数字键 1 / 2 与 Q 都经此判定，封锁态下切枪请求被驳回并回落剑。</summary>
        void HandlePress(int key, string source)
        {
            var flow = GameFlow.Instance;
            if (flow != null && !flow.IsRunning) return;

            switch (key)
            {
                case 1:
                    Equip(WeaponKind.Sword, source);
                    break;
                case 2:
                    Equip(WeaponKind.Gun, source);
                    break;
                case 3:
                    Equip(WeaponKind.Rifle, source);
                    break;
                default:
                    Equip(NextWeapon(), source);
                    break;
            }
        }

        /// <summary>Q 键循环：剑 → 手枪 → 脉冲步枪 → 剑（未解锁的跳过）。</summary>
        WeaponKind NextWeapon()
        {
            if (Current == WeaponKind.Sword)
                return gunUnlocked ? WeaponKind.Gun : WeaponKind.Sword;
            if (Current == WeaponKind.Gun)
                return (gunUnlocked && Progression.Has("pulse")) ? WeaponKind.Rifle : WeaponKind.Sword;
            return WeaponKind.Sword;
        }

        /// <summary>轮询 Boss 死亡状态，作为「关卡通过」信号自动解锁枪械。</summary>
        void PollBossDefeat()
        {
            _bossPollTimer -= Time.deltaTime;
            if (_bossPollTimer > 0f) return;
            _bossPollTimer = Mathf.Max(0.1f, bossPollInterval);

            var boss = FindObjectOfType<MedusaHorror.Gameplay.MedusaBoss>();
            if (boss != null)
            {
                _bossSeen = true;
                if (boss.IsDead) UnlockGun("Boss 已击杀");
                return;
            }
            if (_bossSeen) UnlockGun("Boss 已击杀（对象已离场）");
        }

        /// <summary>解锁枪械：之后数字键 2 / Q 可正常切枪，Pistol Rig 与 HUD 枪位恢复。</summary>
        public void UnlockGun(string reason = "关卡通过")
        {
            if (gunUnlocked) return;
            gunUnlocked = true;
            Debug.Log("[Weapon] 枪械解锁：" + reason);
            LootToast.Show("枪械已解锁 · 按 2 或 Q 切换");
            if (hud != null) hud.SetHintText(null);
        }

        /// <summary>装备指定武器：显隐模型、切换输入开关、刷新 HUD。封锁态下枪的请求回落为剑。</summary>
        public void Equip(WeaponKind kind, string source = "切换")
        {
            bool isFirearm = kind == WeaponKind.Gun || kind == WeaponKind.Rifle;
            if (isFirearm && !gunUnlocked)
            {
                BlockedSwitches++;
                LastBlockedSource = source;
                Debug.Log("[Weapon] 枪械未解锁 · 已忽略「" + source + "」（本关只允许用剑，击杀 Boss 后解锁）");
                if (Time.time - _lastToastTime > 2.5f)
                {
                    _lastToastTime = Time.time;
                    LootToast.Show("枪械被封存 · 本关只允许用剑");
                }
                kind = WeaponKind.Sword;
                source = source + "→回落剑";
            }
            else if (kind == WeaponKind.Rifle && !Progression.Has("pulse"))
            {
                BlockedSwitches++;
                LastBlockedSource = source;
                if (Time.time - _lastToastTime > 2.5f)
                {
                    _lastToastTime = Time.time;
                    LootToast.Show("脉冲步枪 · 等级 5 解锁");
                }
                kind = WeaponKind.Sword;
                source = source + "→回落剑";
            }

            EnsureRifle();
            Current = kind;
            SwitchCount++;
            LastSwitchSource = source;

            if (sword != null) sword.SetActiveState(kind == WeaponKind.Sword);
            if (gun != null) gun.SetActiveState(kind == WeaponKind.Gun);
            if (rifle != null) rifle.SetActiveState(kind == WeaponKind.Rifle);

            if (hud != null)
            {
                hud.SetWeapon(kind, this, sword, kind == WeaponKind.Rifle ? rifle : gun);
                hud.SetHintText(gunUnlocked ? null : "1 剑 · 枪械已封存（击杀 Boss 解锁）");
            }

            var audio = AudioDirector.Instance;
            if (audio != null) audio.Play(SfxId.WeaponSwitch, 0.55f, kind == WeaponKind.Sword ? 1.06f : 0.94f);
        }
    }
}
