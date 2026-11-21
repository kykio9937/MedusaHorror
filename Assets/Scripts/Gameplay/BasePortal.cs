using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 符文传送点：玩家站上圆盘即异步切换到目标场景。
    /// 与 ExitPortal 的区别是不需要钥匙、不需要开门，适合基地往返这类自由传送。
    /// </summary>
    [DisallowMultipleComponent]
    public class BasePortal : MonoBehaviour
    {
        [Tooltip("目标场景名，必须在 Build Settings 里")]
        public string targetScene = "W0_Base";
        [Tooltip("提示文案")]
        public string prompt = "传送";
        [Tooltip("触发半径（米）")]
        public float radius = 0.95f;
        [Tooltip("两次触发之间的冷却（秒）")]
        public float cooldown = 1.5f;

        Transform _player;
        float _cd;

        void Update()
        {
            if (_cd > 0f) { _cd -= Time.unscaledDeltaTime; return; }
            if (MedusaHorror.Core.SceneLoader.IsLoading) return;

            if (_player == null)
            {
                var pet = Object.FindObjectOfType<PlayerPetrify>();
                if (pet == null) return;
                _player = pet.transform;
            }

            Vector3 a = _player.position;
            Vector3 b = transform.position;
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            if (dx * dx + dz * dz > radius * radius) return;
            if (Mathf.Abs(a.y - b.y) > 2.5f) return;

            _cd = cooldown;
            LootToast.Show(prompt + " · " + targetScene);
            Debug.Log("[BasePortal] " + name + " 触发 → " + targetScene);
            MedusaHorror.Core.SceneLoader.Load(targetScene);
        }
    }
}
