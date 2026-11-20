using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// 轻微屏幕抖动：不改相机逻辑，只在 Cinemachine Brain 写好相机变换之后（LateUpdate，order 10000）
    /// 叠加一次瞬时偏移。下一帧 Brain 会重设相机 transform，因此抖动不会累积漂移。
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public class CameraShake : MonoBehaviour
    {
        public Camera target;

        [Tooltip("位移比例：角度 x 该系数 = 米")]
        public float positionScale = 0.012f;

        float _degrees;
        float _duration;
        float _elapsed;

        /// <summary>累计触发次数（诊断用）</summary>
        public int ShakeCount { get; private set; }
        /// <summary>当前抖动强度（度，诊断用）</summary>
        public float CurrentDegrees { get; private set; }

        void Awake()
        {
            if (target == null) target = GetComponent<Camera>();
            if (target == null) target = Camera.main;
        }

        /// <summary>按角度幅度触发抖动（度，建议 1~4 度的轻微量）。</summary>
        public void Shake(float degrees, float seconds)
        {
            if (degrees <= 0f || seconds <= 0f) return;
            _degrees = Mathf.Max(_degrees, degrees);
            _duration = Mathf.Max(_duration, seconds);
            _elapsed = 0f;
            ShakeCount++;
        }

        void LateUpdate()
        {
            if (_duration <= 0f)
            {
                CurrentDegrees = 0f;
                return;
            }

            if (target == null) { _duration = 0f; return; }

            _elapsed += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(_elapsed / _duration);
            if (k <= 0f)
            {
                _duration = 0f;
                CurrentDegrees = 0f;
                return;
            }

            float m = _degrees * k * k;
            CurrentDegrees = m;
            float p = m * positionScale;

            target.transform.position += new Vector3(
                Random.Range(-p, p),
                Random.Range(-p, p),
                0f);
            target.transform.rotation *= Quaternion.Euler(
                Random.Range(-m, m) * 0.55f,
                Random.Range(-m, m) * 0.55f,
                Random.Range(-m, m) * 0.85f);
        }
    }
}
