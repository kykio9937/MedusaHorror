using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 玩家属性加成（来自任务奖励）。只做小幅百分比加成，不改基础战斗数值。
    /// </summary>
    public static class PlayerStats
    {
        public static event System.Action Changed;

        /// <summary>移速倍率（1.0 = 原速，上限 +50%）。</summary>
        public static float SpeedMultiplier { get { return 1f + Mathf.Clamp(SaveSystem.Data.speedBonus, 0f, 0.5f); } }

        /// <summary>攻击倍率（1.0 = 原伤害，上限 +80%）。</summary>
        public static float DamageMultiplier { get { return 1f + Mathf.Clamp(SaveSystem.Data.damageBonus, 0f, 0.8f); } }

        /// <summary>石化抗性（0~0.6，石化累积速度按 (1 - resist) 缩放）。</summary>
        public static float PetrifyResist { get { return Mathf.Clamp(SaveSystem.Data.petrifyResist, 0f, 0.6f); } }

        public static void AddSpeed(float v)
        {
            if (v <= 0f) return;
            SaveSystem.Data.speedBonus += v;
            SaveSystem.Save();
            Notify();
        }

        public static void AddDamage(float v)
        {
            if (v <= 0f) return;
            SaveSystem.Data.damageBonus += v;
            SaveSystem.Save();
            Notify();
        }

        public static void AddPetrifyResist(float v)
        {
            if (v <= 0f) return;
            SaveSystem.Data.petrifyResist += v;
            SaveSystem.Save();
            Notify();
        }

        static void Notify()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
