using UnityEngine;

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

        void Awake()
        {
            if (input == null) input = FindObjectOfType<InputReader>();
            if (yawSource == null && input != null) yawSource = input.transform;

            _targetPitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
            _pitch = _targetPitch;
        }

        void Update()
        {
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
