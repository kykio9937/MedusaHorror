using UnityEngine;

namespace MedusaHorror.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class W4FallRecovery : MonoBehaviour
    {
        CharacterController _controller;
        ThirdPersonController _movement;
        Vector3 _safePosition;
        float _groundedTime;
        public int Recoveries { get; private set; }

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _movement = GetComponent<ThirdPersonController>();
            _safePosition = transform.position;
        }

        void LateUpdate()
        {
            if (!_controller.enabled || (_movement != null && _movement.suspended)) return;
            if (transform.position.y < -5f)
            {
                _controller.enabled = false;
                transform.position = _safePosition + Vector3.up * 0.15f;
                if (_movement != null) _movement.ResetVerticalMotion();
                _controller.enabled = true;
                Recoveries++;
                MedusaHorror.Gameplay.LootToast.Show("已返回最近的安全位置");
                return;
            }
            if (_controller.isGrounded && transform.position.y > 0f)
            {
                _groundedTime += Time.deltaTime;
                if (_groundedTime > 0.25f) _safePosition = transform.position;
            }
            else _groundedTime = 0;
        }
    }
}
