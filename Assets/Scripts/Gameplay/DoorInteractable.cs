using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 可交互门：关闭姿态 / 开启姿态由装配器写入，运行时只用插值旋转。
    /// 新增锁定能力：锁住时无法交互，并强制回到关闭姿态（Boss 战区域封锁用）。
    /// </summary>
    [DisallowMultipleComponent]
    public class DoorInteractable : MonoBehaviour, IInteractable
    {
        [Header("开门参数")]
        public float openAngle = 85f;
        public float openSpeed = 3.4f;

        [Header("提示文案")]
        public string closedHint = "开门";
        public string openedHint = "关门";
        public string lockedHint = "被石化之力封住了";

        [Header("锁状态")]
        [SerializeField] bool locked;

        Quaternion closedRot;
        Quaternion openRot;
        bool initialized;
        bool isOpen;
        float t;

        public bool IsOpen { get { return isOpen; } }
        public bool IsAnimating { get { return t > 0.0001f && t < 0.9999f; } }
        public bool Locked { get { return locked; } }
        public bool Initialized { get { return initialized; } }

        public string Hint
        {
            get
            {
                if (locked) return lockedHint;
                return isOpen ? openedHint : closedHint;
            }
        }

        /// <summary>锁住的门也允许交互（会播放上锁音效并提示），所以恒为 true。</summary>
        public bool CanInteract { get { return true; } }

        public void Configure(Quaternion closed, Quaternion open)
        {
            closedRot = closed;
            openRot = open;
            transform.rotation = closed;
            isOpen = false;
            t = 0f;
            initialized = true;
        }

        /// <summary>锁门：立即标记为锁定，并让门扇在下一帧开始回到关闭姿态。</summary>
        public void Lock()
        {
            locked = true;
            isOpen = false;
        }

        /// <summary>解锁：门可以重新交互。</summary>
        public void Unlock()
        {
            locked = false;
        }

        /// <summary>不经过交互直接设定开合目标（用于脚本强制关闭）。</summary>
        public void SetOpen(bool open)
        {
            isOpen = open;
        }

        void Start()
        {
            // 编辑器装配时写入的姿态未序列化，运行时按当前朝向自恢复
            if (!initialized) AutoInit();
        }

        void AutoInit()
        {
            closedRot = transform.rotation;
            openRot = closedRot * Quaternion.AngleAxis(openAngle, Vector3.up);
            t = 0f;
            isOpen = false;
            initialized = true;
        }

        public void Interact()
        {
            var audio = Core.AudioDirector.Instance;
            if (locked)
            {
                if (audio != null) audio.PlayAt(Core.SfxId.DoorLocked, transform.position, 0.8f, 1f);
                return;
            }
            if (IsAnimating) return;
            isOpen = !isOpen;
            if (audio != null)
                audio.PlayAt(isOpen ? Core.SfxId.DoorOpen : Core.SfxId.DoorClose, transform.position, 0.75f, 1f);
        }

        void Update()
        {
            if (!initialized) return;
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, openSpeed * Time.deltaTime);
            float s = Mathf.SmoothStep(0f, 1f, t);
            transform.rotation = Quaternion.Slerp(closedRot, openRot, s);
        }
    }
}
