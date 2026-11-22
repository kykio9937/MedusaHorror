using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 顶层离场传送门（击杀 Boss 后的唯一离场口）。
    /// 设计要点：
    /// 1) 门禁与 Boss 掉落的「锈蚀钥匙」强绑定：未拾取钥匙时门保持锁定（DoorInteractable.Lock），
    ///    交互只播放上锁音效与提示，玩家无法穿过；拾取钥匙后自动解封。
    /// 2) 玩家开门并走进门洞后触发离场：淡出 → 若有可切换的下一场景则切过去，否则回主菜单。
    /// 3) 与石化致死（GameFlow.Failed）完全无关，也不触发 Won 结算面板。
    /// </summary>
    [DisallowMultipleComponent]
    public class ExitPortal : MonoBehaviour, IInteractable
    {
        [Header("门体")]
        public DoorInteractable door;

        [Tooltip("传送门光效根节点：Boss 未死前关闭，Boss 死亡后点亮")]
        public GameObject fxRoot;

        [Header("离场")]
        [Tooltip("留空表示自动选择：优先取 Build Settings 中当前场景之后的首个非开发测试场景；没有则回主菜单")]
        public string nextSceneOverride = "";
        public float leaveDelay = 1.05f;
        [Tooltip("是否需要 Boss 掉落的钥匙才能开门；W2 这类没有 Boss 的关卡可关闭")]
        public bool requireKey = true;

        [Header("解锁方式")]
        [Tooltip("需要靠近按 E 点亮（幻兽帕鲁式）；点亮状态写入存档")]
        public bool requireActivation = false;
        [Tooltip("通过时扣多少信用点（0 = 免费）。余额不足会被拦下")]
        public int cost = 0;

        [Header("门洞判定（本地坐标，单位米）")]
        public float halfWidth = 1.25f;
        public float halfDepth = 1.30f;
        public float InnerEdge = -0.04f;
        public float lowerY = -0.20f;
        public float upperY = 2.60f;

        /// <summary>开发测试台场景，不作为「下一关」候选。</summary>
        static readonly string[] DevScenes = { "W1_Sandbox" };

        /// <summary>Boss 钥匙是否已到手。放在静态字段上，避免传送门与钥匙的对象生命周期耦合。</summary>
        static bool s_keyGranted;

        /// <summary>Boss 是否已被击杀（击杀后传送门启动）。</summary>
        static bool s_bossDefeated;

        bool _announced;
        bool _leaving;
        float _warnAt = -99f;
        Transform _player;

        public static bool KeyGranted { get { return s_keyGranted; } }
        public static bool BossDefeated { get { return s_bossDefeated; } }

        /// <summary>传送门存档 id：场景名|对象名。</summary>
        public string PortalId { get { return SceneManager.GetActiveScene().name + "|" + gameObject.name; } }

        /// <summary>是否已点亮（不要求点亮时恒为 true）。</summary>
        public bool Activated
        {
            get
            {
                if (!requireActivation) return true;
                var list = SaveSystem.Data.activatedPortals;
                string k = PortalId;
                for (int i = 0; i < list.Count; i++) if (list[i] == k) return true;
                return false;
            }
        }

        // ---------------- IInteractable ----------------

        public string Hint
        {
            get
            {
                if (requireActivation && !Activated) return "点亮传送门";
                int fare = MedusaHorror.Core.Pricing.Apply(cost);
                if (fare > 0)
                {
                    string tag = MedusaHorror.Core.Pricing.Tag(cost);
                    if (!Wallet.CanAfford(fare)) return "票价 " + fare + tag + " · 余额不足（" + Wallet.Coins + "）";
                    return "传送门已点亮 · 票价 " + fare + tag;
                }
                return "传送门已点亮";
            }
        }

        /// <summary>只有「未点亮」这一种状态需要按 E。</summary>
        public bool CanInteract { get { return requireActivation && !Activated; } }

        public void Interact()
        {
            if (requireActivation && !Activated) ActivatePortal();
        }

        void ActivatePortal()
        {
            var list = SaveSystem.Data.activatedPortals;
            string k = PortalId;
            if (!list.Contains(k)) { list.Add(k); SaveSystem.Save(); }
            if (fxRoot != null) fxRoot.SetActive(true);
            LootToast.Show("传送门已点亮 · 走进去即可出发");
            Debug.Log("[Exit] 传送门点亮 " + k);
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.9f, 1.1f);
        }

        /// <summary>Boss 死亡时调用：点亮传送门光效（门仍需钥匙才能开）。</summary>
        public static void NotifyBossDefeated()
        {
            if (s_bossDefeated) return;
            s_bossDefeated = true;
            var portal = Object.FindObjectOfType<ExitPortal>();
            if (portal != null) portal.ApplyDormant();
            Debug.Log("[Exit] Boss 已死亡 · 天台传送门启动");
        }

        void ApplyDormant()
        {
            if (fxRoot == null) return;
            // Boss 关还要等 Boss 死；其余关卡只看是否点亮
            bool show = requireKey ? (s_bossDefeated && Activated) : Activated;
            fxRoot.SetActive(show);
        }

        /// <summary>Boss 钥匙拾取时调用。</summary>
        public static void GrantKey()
        {
            s_keyGranted = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStaticState()
        {
            s_keyGranted = false;
            s_bossDefeated = false;
        }

        void Awake()
        {
            // 场景里装配态的门是「锁定」。若 Awake 时门仍锁定，说明是新一轮进入场景，
            // 必须把上一轮遗留的静态钥匙状态清零，否则新一局会跳过钥匙门禁直接解封。
            if (door != null && door.Locked) ResetStaticState();
            SyncLock();
            ApplyDormant();
        }

        void Update()
        {
            if (_leaving) return;
            SyncLock();
            if (requireActivation && !Activated) return;
            if (requireKey && !s_keyGranted) return;
            if (door != null && !door.IsOpen) return;
            if (!PlayerInside()) return;

            int fare = MedusaHorror.Core.Pricing.Apply(cost);
            if (fare > 0 && !Wallet.CanAfford(fare))
            {
                if (Time.unscaledTime - _warnAt > 2.5f)
                {
                    _warnAt = Time.unscaledTime;
                    LootToast.Show("票价 " + fare + " 信用点 · 当前只有 " + Wallet.Coins);
                    var a = AudioDirector.Instance;
                    if (a != null) a.PlayAt(SfxId.DoorLocked, transform.position, 0.7f, 1f);
                }
                return;
            }

            BeginLeave();
        }

        /// <summary>钥匙状态 → 门锁状态单向同步（幂等，可每帧调用）。</summary>
        void SyncLock()
        {
            if (door == null) return;
            if (!requireKey)
            {
                if (door.Locked) door.Unlock();
                return;
            }
            if (s_keyGranted)
            {
                if (door.Locked) door.Unlock();
                if (!_announced)
                {
                    _announced = true;
                    LootToast.Show("锈蚀钥匙契合封印 · 顶层传送门已解封");
                    Debug.Log("[Exit] 顶层传送门解封（钥匙已入手）");
                }
            }
            else if (!door.Locked)
            {
                door.Lock();
            }
        }

        /// <summary>玩家是否站在门洞里（本地坐标盒判定，不依赖触发器）。</summary>
        bool PlayerInside()
        {
            if (_player == null)
            {
                var pet = Object.FindObjectOfType<PlayerPetrify>();
                if (pet == null) return false;
                _player = pet.transform;
            }
            Vector3 local = transform.InverseTransformPoint(_player.position);
            // 必须真正跨过门扇平面（local.x >= InnerEdge），站在门外不算进入
            return local.x >= InnerEdge && local.x <= halfDepth
                   && Mathf.Abs(local.z) <= halfWidth
                   && local.y >= lowerY && local.y <= upperY;
        }

        void BeginLeave()
        {
            _leaving = true;
            string next = ResolveNextScene();
            string label = string.IsNullOrEmpty(next) ? MedusaHorror.Core.GameFlow.MenuScene : next;

            int fare = MedusaHorror.Core.Pricing.Apply(cost);
            if (fare > 0)
            {
                if (!Wallet.Spend(fare)) { _leaving = false; return; }
                LootToast.Show("购票 -" + fare + " 信用点 · 前往 " + label);
            }
            else
            {
                LootToast.Show("离场 · " + label);
            }

            Debug.Log("[Exit] 玩家走入传送门，开始离场 → " + label + (cost > 0 ? "（花费 " + cost + "）" : ""));
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null) audio.PlayAt(MedusaHorror.Core.SfxId.Pickup, transform.position, 0.9f, 0.85f);
            MedusaHorror.Core.GameFlow.LeaveRun(next);
        }

        /// <summary>解析离场目标场景；返回空串表示无可用下一关，由 GameFlow 回主菜单。</summary>
        public string ResolveNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneOverride)) return nextSceneOverride;

            var active = SceneManager.GetActiveScene();
            int n = SceneManager.sceneCountInBuildSettings;
            for (int i = active.buildIndex + 1; i < n; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.IsNullOrEmpty(path)) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(name)) continue;
                if (IsDevScene(name)) continue;
                return name;
            }
            return "";
        }

        static bool IsDevScene(string name)
        {
            for (int i = 0; i < DevScenes.Length; i++)
                if (string.Equals(DevScenes[i], name, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>诊断文本（命令通道 EXITSTATE 使用）。</summary>
        public string StateText()
        {
            string next = ResolveNextScene();
            return "key=" + s_keyGranted +
                   " door=" + (door == null ? "null" : (door.Locked ? "locked/open=" : "unlocked/open=") + door.IsOpen) +
                   " inside=" + PlayerInside() +
                   " leaving=" + _leaving +
                   " pos=" + transform.position.ToString("F2") +
                   " next=" + (string.IsNullOrEmpty(next) ? MedusaHorror.Core.GameFlow.MenuScene + "(回主菜单)" : next);
        }
    }
}
