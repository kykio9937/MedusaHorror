using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>街上的行人：在两点之间来回走，只做表现层，不参与战斗与交互。</summary>
    public class CrowdWalker : MonoBehaviour
    {
        public Vector3 pointA;
        public Vector3 pointB;
        public float speed = 1.4f;
        public float pauseAtEnd = 0.6f;

        int _dir = 1;
        float _wait;
        Animator _anim;
        bool _hasSpeedParam;
        static readonly int SpeedHash = Animator.StringToHash("Speed");

        void Awake()
        {
            _anim = GetComponentInChildren<Animator>();
            if (_anim != null)
            {
                // 位移由脚本控制，root motion 会把 NPC 带偏
                _anim.applyRootMotion = false;
                var ps = _anim.parameters;
                for (int i = 0; i < ps.Length; i++) if (ps[i].nameHash == SpeedHash) _hasSpeedParam = true;
            }
            // 关掉模型包自带的玩家脚本，避免行人也响应键鼠
            var mbs = GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < mbs.Length; i++)
            {
                var mb = mbs[i];
                if (mb == null || mb == this) continue;
                if (mb is MedusaHorror.Player.ThirdPersonController || mb is MedusaHorror.Player.InputReader)
                    mb.enabled = false;
            }
        }

        void Update()
        {
            if (_wait > 0f) { _wait -= Time.deltaTime; return; }

            Vector3 target = _dir > 0 ? pointB : pointA;
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 0.25f)
            {
                _dir = -_dir;
                _wait = pauseAtEnd;
                return;
            }

            Vector3 step = d.normalized * speed * Time.deltaTime;
            transform.position += step;
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(step.normalized), 6f * Time.deltaTime);

            if (_anim != null && _hasSpeedParam) _anim.SetFloat(SpeedHash, speed);
        }
    }
}
