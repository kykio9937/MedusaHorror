using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 赛博摩托坐骑：等级 8 解锁后按 G 召唤/收起，召唤即骑乘。
    /// 骑乘期间提高移动速度、关闭脚步音、车身随移动上下浮动与转向倾斜。
    /// </summary>
    [DisallowMultipleComponent]
    public class MountSystem : MonoBehaviour
    {
        public static MountSystem Instance { get; private set; }

        [Header("按键")]
        public KeyCode toggleKey = KeyCode.G;

        [Header("骑乘")]
        [Tooltip("骑乘时的移速倍率（相对跑步速度）")]
        public float rideSpeedMultiplier = 2.4f;
        [Tooltip("骑乘时玩家抬升高度，让角色站在车上")]
        public float rideHeight = 0.62f;

        [Header("悬浮表现")]
        public float bobAmplitude = 0.045f;
        public float bobSpeed = 2.4f;
        public float leanDegrees = 14f;
        public float leanDamp = 6f;

        [Header("资源")]
        public string prefabResource = "Mount/HoverBike";

        public bool IsRiding { get; private set; }
        public bool Unlocked { get { return Progression.Has("mount"); } }
        public int SummonCount { get; private set; }
        public string LastBlockReason { get; private set; } = "";

        GameObject _bike;
        ThirdPersonController _tpc;
        CharacterController _cc;
        InputReader _input;
        float _bobT;
        float _lean;

        void Awake()
        {
            Instance = this;
            _tpc = GetComponent<ThirdPersonController>();
            _cc = GetComponent<CharacterController>();
            _input = GetComponent<InputReader>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            // 石化时 CharacterController 被禁用，骑乘无法维持，强制下车
            if (IsRiding && _cc != null && !_cc.enabled) { Dismount(); return; }

            if (Input.GetKeyDown(toggleKey)) Toggle();
            if (IsRiding) TickRide();
        }

        public bool Toggle()
        {
            if (IsRiding) { Dismount(); return true; }
            return Summon();
        }

        public bool Summon()
        {
            if (IsRiding) return true;
            if (!Unlocked) { LastBlockReason = "未解锁：需要等级 8"; return false; }
            if (_cc != null && !_cc.enabled) { LastBlockReason = "石化中无法召唤"; return false; }

            var prefab = Resources.Load<GameObject>(prefabResource);
            if (prefab == null) { LastBlockReason = "缺少坐骑预制体 " + prefabResource; return false; }

            _bike = Instantiate(prefab, transform);
            _bike.name = "HoverBike";
            _bike.transform.localPosition = new Vector3(0f, -rideHeight + 0.02f, 0.06f);
            _bike.transform.localRotation = Quaternion.identity;

            if (_cc != null) _cc.Move(Vector3.up * rideHeight);
            IsRiding = true;
            SummonCount++;
            LastBlockReason = "";

            if (_tpc != null)
            {
                _tpc.suspended = true;
                _tpc.externalSpeedScale = rideSpeedMultiplier;
            }

            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.WeaponSwitch, transform.position, 0.5f, 1.25f);
            return true;
        }

        public void Dismount()
        {
            if (!IsRiding) return;

            // 下车时落到脚下地面，避免从车高悬空再自由落体
            int mask = ~(1 << gameObject.layer);
            Vector3 origin = transform.position + Vector3.up * 0.6f;
            RaycastHit hit;
            float drop = -rideHeight;
            if (Physics.Raycast(origin, Vector3.down, out hit, 10f, mask, QueryTriggerInteraction.Ignore))
                drop = hit.point.y - transform.position.y;

            if (_cc != null && _cc.enabled) _cc.Move(Vector3.up * drop);
            else transform.position += Vector3.up * drop;

            if (_bike != null) Destroy(_bike);
            _bike = null;

            IsRiding = false;
            _lean = 0f;
            if (_tpc != null)
            {
                _tpc.suspended = false;
                _tpc.externalSpeedScale = 1f;
            }

            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.WeaponSwitch, transform.position, 0.4f, 0.85f);
        }

        /// <summary>骑乘时的悬浮与转向倾斜（纯表现，不改物理）。</summary>
        void TickRide()
        {
            if (_bike == null) return;

            SnapToGround();

            _bobT += Time.deltaTime * bobSpeed;
            float bob = Mathf.Sin(_bobT) * bobAmplitude;
            _bike.transform.localPosition = new Vector3(0f, -rideHeight + 0.02f + bob, 0.06f);

            float lateral = _input != null ? _input.Move.x : 0f;
            float target = -lateral * leanDegrees;
            _lean = Mathf.Lerp(_lean, target, Time.deltaTime * leanDamp);
            _bike.transform.localRotation = Quaternion.Euler(0f, 0f, _lean);
        }

        /// <summary>骑乘时维持固定离地高度：跳过重力后必须自己贴地，否则走下坡会悬空。</summary>
        void SnapToGround()
        {
            if (_cc == null || !_cc.enabled) return;

            Vector3 origin = transform.position + Vector3.up * 0.6f;
            int mask = ~(1 << gameObject.layer);
            RaycastHit hit;
            if (!Physics.Raycast(origin, Vector3.down, out hit, 6f, mask, QueryTriggerInteraction.Ignore))
            {
                // 脚下没有地面（跑出平台边缘）：缓慢下沉，避免整台车浮在空中
                _cc.Move(Vector3.down * 3.5f * Time.deltaTime);
                return;
            }

            float targetY = hit.point.y + rideHeight;
            float dy = targetY - transform.position.y;
            if (Mathf.Abs(dy) < 0.02f) return;

            float step = Mathf.Clamp(dy, -0.6f, 0.6f) * Mathf.Min(1f, Time.deltaTime * 14f);
            _cc.Move(Vector3.up * step);
        }
    }
}
