using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 玩家进入 Boss 所在楼层的触发器：
    /// 轮询玩家脚部高度，一旦踏上目标楼层高度（例如 3F y=9.0）立刻唤醒 Boss。
    /// 用轮询而不是触发器碰撞体，避免 Boss 自身（玩家模型的副本）误触发。
    /// </summary>
    [DisallowMultipleComponent]
    public class BossFloorTrigger : MonoBehaviour
    {
        [Tooltip("目标 Boss；为空时自动查找 MedusaBoss")]
        public MedusaBoss boss;
        [Tooltip("Boss 所在楼层的地面高度（世界坐标 Y）")]
        public float floorY = 9.1f;
        [Tooltip("判定容差：玩家脚部高度达到 floorY - tolerance 即算进入该层")]
        public float tolerance = 1.0f;
        [Tooltip("触发后的提示语（空则不提示）")]
        public string toast = "三层封印震动 · 美杜莎苏醒！";

        Transform _player;
        bool _fired;

        void Update()
        {
            if (_fired) return;
            if (boss == null)
            {
                boss = Object.FindObjectOfType<MedusaBoss>();
                if (boss == null) return;
            }
            if (_player == null)
            {
                var pet = Object.FindObjectOfType<PlayerPetrify>();
                if (pet == null) return;
                _player = pet.transform;
            }
            if (_player.position.y >= floorY - tolerance)
            {
                _fired = true;
                if (!string.IsNullOrEmpty(toast)) LootToast.Show(toast);
                Debug.Log("[Boss] 玩家进入三层，触发 Boss 苏醒");
                boss.WakeNow();
            }
        }
    }
}
