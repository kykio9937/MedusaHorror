using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 玩家属性加成：任务奖励（存档里的 speedBonus 等）+ 等级成长（Progression）合并。
    /// 只做百分比加成，不改基础战斗数值。
    /// </summary>
    public static class PlayerStats
    {
        public static event System.Action Changed;

        /// <summary>移速倍率（1.0 = 原速，任务加成上限 +50%，等级加成额外叠加）。</summary>
        public static float SpeedMultiplier
        {
            get { return 1f + Mathf.Clamp(SaveSystem.Data.speedBonus, 0f, 0.5f) + Progression.LevelSpeedBonus; }
        }

        /// <summary>攻击倍率（1.0 = 原伤害，任务加成上限 +80%，等级加成额外叠加）。</summary>
        public static float DamageMultiplier
        {
            get { return 1f + Mathf.Clamp(SaveSystem.Data.damageBonus, 0f, 0.8f) + Progression.LevelDamageBonus; }
        }

        /// <summary>石化抗性（任务 + 等级，上限 75%）。</summary>
        public static float PetrifyResist
        {
            get { return Mathf.Clamp(SaveSystem.Data.petrifyResist + Progression.LevelResistBonus, 0f, 0.75f); }
        }

        /// <summary>石化上限加成（点数，来自等级）。</summary>
        public static float PetrifyCapBonus { get { return Progression.LevelPetrifyCapBonus; } }

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
