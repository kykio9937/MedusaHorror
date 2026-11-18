using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// W1 第三人称角色控制器（CharacterController 驱动，不走 Rigidbody，也不依赖 root motion）。
    /// 职责：WASD 移动、Shift 走/跑切换、重力与跳跃、鼠标水平转身、驱动 Animator 的
    /// Speed / IsGrounded / Jump 三个参数。
    /// 垂直俯仰由 ThirdPersonCameraInput 负责，本类只处理水平朝向。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class ThirdPersonController : MonoBehaviour
    {
        [Header("依赖")]
        [Tooltip("留空则自动从本物体上获取")]
        public InputReader input;

        [Header("移动速度（需与 Animator 混合树阈值保持一致：0 / 2 / 5.2）")]
        public float walkSpeed = 2.0f;
        public float runSpeed = 5.2f;

        [Header("加减速")]
        public float acceleration = 16f;
        public float deceleration = 20f;

        [Header("转身")]
        [Tooltip("鼠标水平转身的平滑时间，越小越跟手")]
        public float turnSmoothTime = 0.06f;

        [Header("重力与跳跃")]
        public float gravity = -20f;
        [Tooltip("跳跃最高点高度（米）")]
        public float jumpHeight = 1.15f;
        [Tooltip("贴地时施加的向下速度，避免走下缓坡时离地抖动")]
        public float groundStickVelocity = -2f;
        [Tooltip("离地后允许再次起跳的宽限时间（秒），0 表示不允许")]
        public float coyoteTime = 0.10f;

        [Header("Animator 参数名")]
        public string speedParam = "Speed";
        public string groundedParam = "IsGrounded";
        public string jumpParam = "Jump";

        [Header("动画速度平滑")]
        public float animationSpeedDamp = 0.08f;

        CharacterController _cc;
        Animator _anim;

        float _targetYaw;      // 鼠标累积出的目标水平朝向
        float _currentYaw;
        float _yawVelocity;
        float _planarSpeed;    // 当前水平速度（米/秒）
        float _verticalVelocity;
        float _animSpeed;
        float _animSpeedVelocity;
        float _lastGroundedTime;

        bool _hasSpeedParam;
        bool _hasGroundedParam;
        bool _hasJumpParam;

        // 脚步节拍：按移动速度换算步频，跑动更快更密
        float _footTimer;

        void Awake()
        {
            CacheComponents();
        }

        void CacheComponents()
        {
            if (_cc == null) _cc = GetComponent<CharacterController>();
            if (_anim == null) _anim = GetComponent<Animator>();
            if (input == null) input = GetComponent<InputReader>();

            _targetYaw = transform.eulerAngles.y;
            _currentYaw = _targetYaw;

            if (_anim != null && _anim.runtimeAnimatorController != null)
            {
                AnimatorControllerParameter[] ps = _anim.parameters;
                for (int i = 0; i < ps.Length; i++)
                {
                    if (ps[i].name == speedParam) _hasSpeedParam = true;
                    else if (ps[i].name == groundedParam) _hasGroundedParam = true;
                    else if (ps[i].name == jumpParam) _hasJumpParam = true;
                }
            }
        }

        void Update()
        {
            if (input == null) return;

            ApplyHorizontalTurning();
            ApplyMotion();
        }

        /// <summary>鼠标水平转身：累积 yaw 后平滑套用到角色根节点</summary>
        void ApplyHorizontalTurning()
        {
            _targetYaw += input.Look.x;
            if (_targetYaw > 360f) _targetYaw -= 360f;
            else if (_targetYaw < -360f) _targetYaw += 360f;

            _currentYaw = Mathf.SmoothDampAngle(_currentYaw, _targetYaw, ref _yawVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, _currentYaw, 0f);
        }

        /// <summary>位移、重力、跳跃与动画参数</summary>
        void ApplyMotion()
        {
            // 石化期间 PlayerPetrify 会禁用 CharacterController，此时不得再调用 Move，否则每帧刷告警
            if (_cc == null || !_cc.enabled) return;

            Vector2 moveInput = input.Move;
            bool hasInput = moveInput.sqrMagnitude > 0.01f;

            // 目标速度：有输入时按 Shift 在走/跑之间切换
            float targetSpeed = 0f;
            if (hasInput) targetSpeed = input.RunHeld ? runSpeed : walkSpeed;

            float rate = _planarSpeed < targetSpeed ? acceleration : deceleration;
            _planarSpeed = Mathf.MoveTowards(_planarSpeed, targetSpeed, rate * Time.deltaTime);

            // 移动方向以角色自身朝向为基准（角色朝向已由鼠标水平转身决定）
            Vector3 direction = Vector3.zero;
            if (hasInput)
            {
                direction = transform.forward * moveInput.y + transform.right * moveInput.x;
                if (direction.sqrMagnitude > 1f) direction.Normalize();
            }

            bool grounded = _cc.isGrounded;
            if (grounded) _lastGroundedTime = Time.time;

            if (grounded && _verticalVelocity < 0f) _verticalVelocity = groundStickVelocity;

            bool canJump = grounded || (coyoteTime > 0f && Time.time - _lastGroundedTime <= coyoteTime);
            if (canJump && input.JumpPressed)
            {
                _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
                if (_hasJumpParam && _anim != null) _anim.SetTrigger(jumpParam);
            }

            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = direction * _planarSpeed + Vector3.up * _verticalVelocity;
            _cc.Move(velocity * Time.deltaTime);

            UpdateAnimator();
            TickFootsteps();
        }

        /// <summary>地面移动时按速度决定步频，脚步声交由 AudioDirector 播放（3D 定位）。</summary>
        void TickFootsteps()
        {
            bool moving = _cc.isGrounded && _planarSpeed > 0.6f;
            if (!moving)
            {
                _footTimer = 0f;
                return;
            }

            _footTimer -= Time.deltaTime;
            if (_footTimer > 0f) return;

            float stride = Mathf.Lerp(0.46f, 0.28f, Mathf.InverseLerp(walkSpeed, runSpeed, _planarSpeed));
            _footTimer = stride;
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio == null) return;
            float vol = Mathf.Lerp(0.30f, 0.55f, Mathf.InverseLerp(walkSpeed, runSpeed, _planarSpeed));
            audio.PlayAt(MedusaHorror.Core.SfxId.Footstep, transform.position, vol, Random.Range(0.88f, 1.12f));
        }

        void UpdateAnimator()
        {
            if (_anim == null) return;

            // 用控制器实测速度驱动混合树，撞墙时会自动把速度压下来
            Vector3 v = _cc.velocity;
            float planar = new Vector2(v.x, v.z).magnitude;
            _animSpeed = Mathf.SmoothDamp(_animSpeed, planar, ref _animSpeedVelocity, animationSpeedDamp);

            if (_hasSpeedParam) _anim.SetFloat(speedParam, _animSpeed);
            if (_hasGroundedParam) _anim.SetBool(groundedParam, _cc.isGrounded);
        }
    }
}
