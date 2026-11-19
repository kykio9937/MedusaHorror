using UnityEngine;
using MedusaHorror.Gameplay;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 跌落保护：玩家掉出关卡地面（走出边界、穿过缝隙）时，自动送回本关出生点，
    /// 避免无限下坠把存档状态带坏。每个关卡构建时挂在玩家身上并指定 respawnPoint。
    /// </summary>
    [DisallowMultipleComponent]
    public class FallRecovery : MonoBehaviour
    {
        [Tooltip("低于这个高度判定为掉出世界")]
        public float killY = -12f;

        [Tooltip("复位目标位置（一般就是本关出生点）")]
        public Vector3 respawnPoint;

        [Tooltip("两次复位之间的最小间隔，防止连续触发")]
        public float cooldown = 1.0f;

        float _nextAllowed;

        void Update()
        {
            if (Time.time < _nextAllowed) return;
            if (transform.position.y >= killY) return;

            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = respawnPoint;
            if (cc != null) cc.enabled = true;

            var body = GetComponent<Rigidbody>();
            if (body != null) body.velocity = Vector3.zero;

            _nextAllowed = Time.time + cooldown;
            LootToast.Show("脚下一空 · 回到安全位置");
            Debug.Log("[FallRecovery] 玩家跌出地面，已复位到 " + respawnPoint.ToString("F1"));
        }
    }
}
