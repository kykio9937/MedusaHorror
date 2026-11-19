using UnityEngine;
using UnityEngine.SceneManagement;

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
    public class ExitPortal : MonoBehaviour
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
        Transform _player;

        public static bool KeyGranted { get { return s_keyGranted; } }
        public static bool BossDefeated { get { return s_bossDefeated; } }

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
            if (fxRoot != null) fxRoot.SetActive(s_bossDefeated);
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
            if (requireKey && !s_keyGranted) return;
            if (door != null && !door.IsOpen) return;
            if (!PlayerInside()) return;
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
            LootToast.Show("离场 · " + label);
            Debug.Log("[Exit] 玩家走入传送门，开始离场 → " + label);
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
