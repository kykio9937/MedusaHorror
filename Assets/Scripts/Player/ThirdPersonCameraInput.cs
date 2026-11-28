using UnityEngine;
using Cinemachine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// W1 第三人称相机输入：只负责鼠标垂直俯仰角，结果直接写进 Cinemachine 虚拟相机的旋转。
    /// 虚拟相机配置为 Body = Framing Transposer、Aim = Do Nothing，因此相机位置由 Cinemachine
    /// 计算、朝向由本脚本决定，天然绕开旧版 Input 轴（Cinemachine 内置输入依赖旧 Input Manager，
    /// 本工程 Active Input Handling 仅为 Input System，所以不走 Axis 驱动）。
    /// 水平方向不在这里处理：Yaw 取自 yawSource（Player 根节点），由 ThirdPersonController 的
    /// 鼠标水平转身带动，相机因此始终在角色背后。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class ThirdPersonCameraInput : MonoBehaviour
    {
        [Header("依赖")]
        [Tooltip("留空则从场景里查找 InputReader")]
        public InputReader input;

        [Tooltip("水平朝向基准，通常填 Player 根节点；留空则用 input 所在物体的朝向")]
        public Transform yawSource;

        [Header("俯仰角限制")]
        public float initialPitch = 12f;
        [Tooltip("向下俯视的最大角度")]
        public float maxPitch = 55f;
        [Tooltip("向上仰视的最大角度（负值）")]
        public float minPitch = -25f;

        [Header("手感")]
        [Tooltip("俯仰平滑时间，0 为完全跟随")]
        public float pitchSmoothTime = 0.05f;
        public bool invertY = false;

        [Header("开火后坐")]
        [Tooltip("每发后坐抬枪角度（度），0 表示不接入后坐")]
        public float recoilPerShot = 1.6f;
        [Tooltip("后坐回复速度（度/秒）")]
        public float recoilRecoverRate = 7f;

        float _pitch;
        float _targetPitch;
        float _pitchVelocity;
        float _recoil;
        CinemachineVirtualCamera _virtualCamera;
        CinemachineFramingTransposer _framing;
        WeaponLoadout _loadout;
        float _defaultDistance, _defaultScreenX, _defaultFov;
        float _aimBlend;
        Vector3 _defaultOffset, _defaultDamping;
        Renderer[] _bodyRenderers;
        UnityEngine.Rendering.ShadowCastingMode[] _bodyShadows;
        bool _firstPerson;
        float _perspectiveBlend;
        public bool IsFirstPerson => _firstPerson;

        void Awake()
        {
            if (input == null) input = FindObjectOfType<InputReader>();
            if (yawSource == null && input != null) yawSource = input.transform;

            _targetPitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
            _pitch = _targetPitch;
            _virtualCamera = GetComponent<CinemachineVirtualCamera>();
            if (_virtualCamera != null)
            {
                _framing = _virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
                _defaultFov = _virtualCamera.m_Lens.FieldOfView;
                if (_framing != null)
                {
                    _defaultDistance = _framing.m_CameraDistance;
                    _defaultScreenX = _framing.m_ScreenX;
                    _defaultOffset = _framing.m_TrackedObjectOffset;
                    _defaultDamping = new Vector3(_framing.m_XDamping, _framing.m_YDamping, _framing.m_ZDamping);
                }
            }
        }

        void Update()
        {
            UpdateWeaponView();
            if (input != null)
            {
                float delta = invertY ? input.Look.y : -input.Look.y;
                _targetPitch = Mathf.Clamp(_targetPitch + delta, minPitch, maxPitch);
            }

            _pitch = pitchSmoothTime > 0f
                ? Mathf.SmoothDamp(_pitch, _targetPitch, ref _pitchVelocity, pitchSmoothTime)
                : _targetPitch;

            float yaw = yawSource != null ? yawSource.eulerAngles.y : transform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(_pitch, yaw, 0f);

            // 后坐回复：把抬起的角度按固定速度收回去
            if (_recoil > 0.0001f && recoilRecoverRate > 0f)
            {
                float back = Mathf.Min(_recoil, recoilRecoverRate * Time.deltaTime);
                _recoil -= back;
                _targetPitch = Mathf.Clamp(_targetPitch + back, minPitch, maxPitch);
            }
        }

        void UpdateWeaponView()
        {
            if (_virtualCamera == null || _framing == null) return;
            if (_loadout == null && input != null) _loadout = input.GetComponent<WeaponLoadout>();
            if (PlayerViewMode.TogglePressed()) PlayerViewMode.Toggle(_loadout);
            bool firearm = PlayerViewMode.IsFirstPerson;
            bool aiming = firearm && _loadout != null && _loadout.Current != WeaponKind.Sword && input != null && input.AimHeld && !_loadout.IsSwitching;
            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            _aimBlend = Mathf.Lerp(_aimBlend, aiming ? 1f : 0f, blend);
            if (firearm != _firstPerson)
            {
                _firstPerson = firearm;
            }
            _perspectiveBlend = Mathf.MoveTowards(_perspectiveBlend, firearm ? 1f : 0f, Time.deltaTime / 0.3f);
            float perspective = Mathf.SmoothStep(0f, 1f, _perspectiveBlend);
            SetBodyVisibility(_perspectiveBlend < 0.8f);
            _framing.m_ScreenX = Mathf.Lerp(_defaultScreenX, 0.5f, perspective);
            _framing.m_CameraDistance = Mathf.Lerp(_defaultDistance, 0.05f, perspective);
            Vector3 eyeOffset = input != null && _virtualCamera.Follow != null
                ? _virtualCamera.Follow.InverseTransformPoint(input.transform.TransformPoint(new Vector3(0f, 1.75f, 0.08f)))
                : _defaultOffset;
            _framing.m_TrackedObjectOffset = Vector3.Lerp(_defaultOffset, eyeOffset, perspective);
            bool transitioning = _perspectiveBlend > 0f && _perspectiveBlend < 1f;
            _framing.m_XDamping = firearm || transitioning ? 0f : _defaultDamping.x;
            _framing.m_YDamping = firearm || transitioning ? 0f : _defaultDamping.y;
            _framing.m_ZDamping = firearm || transitioning ? 0f : _defaultDamping.z;
            _virtualCamera.m_Lens.FieldOfView = Mathf.Lerp(_defaultFov, Mathf.Lerp(65f, 48f, _aimBlend), perspective);
        }

        void SetBodyVisibility(bool visible)
        {
            if (_bodyRenderers == null && input != null)
            {
                var animator = input.GetComponentInChildren<Animator>();
                if (animator == null) return;
                _bodyRenderers = animator.GetComponentsInChildren<Renderer>(true);
                _bodyShadows = new UnityEngine.Rendering.ShadowCastingMode[_bodyRenderers.Length];
                for (int i = 0; i < _bodyRenderers.Length; i++) _bodyShadows[i] = _bodyRenderers[i].shadowCastingMode;
            }
            if (_bodyRenderers == null) return;
            for (int i = 0; i < _bodyRenderers.Length; i++)
                if (_bodyRenderers[i] != null)
                    _bodyRenderers[i].shadowCastingMode = visible ? _bodyShadows[i] : UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        }

        void OnDisable()
        {
            SetBodyVisibility(!PlayerViewMode.IsFirstPerson);
        }

        /// <summary>开火后坐：枪口上抬（俯仰角减小）并记录待回复量。</summary>
        public void AddRecoil(float degrees)
        {
            if (degrees <= 0f) return;
            _recoil += degrees;
            _targetPitch = Mathf.Clamp(_targetPitch - degrees, minPitch, maxPitch);
        }

        /// <summary>当前累计未回复的后坐角度（度）。</summary>
        public float RecoilDegrees { get { return _recoil; } }

        /// <summary>当前俯仰目标角（诊断用）。</summary>
        public float TargetPitch { get { return _targetPitch; } }
    }
}
