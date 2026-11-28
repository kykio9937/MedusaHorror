using System;
using System.Collections.Generic;
using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 角色成长：击杀给经验、升级加属性、按等级解锁技能 / 枪械 / 坐骑。
    /// 所有加成都是真实生效的数值，面板里能直接看到。
    /// </summary>
    public static class Progression
    {
        public static event Action Changed;
        public static event Action<int> LeveledUp;

        public const int MaxLevel = 30;

        /// <summary>一条成长解锁项。</summary>
        public class Unlock
        {
            public int level;
            public string id;
            public string name;
            public string desc;
        }

        /// <summary>等级解锁表（按等级排序）。</summary>
        public static readonly Unlock[] Unlocks =
        {
            new Unlock { level = 2,  id = "dash",       name = "疾行",       desc = "冲刺冷却缩短，移速 +3%" },
            new Unlock { level = 3,  id = "gun_mag",    name = "弹匣扩容",   desc = "弹匣容量 +4 发" },
            new Unlock { level = 4,  id = "combo",      name = "连击精通",   desc = "三段连击伤害 +15%" },
            new Unlock { level = 5,  id = "pulse",      name = "脉冲步枪",   desc = "解锁第三把武器：脉冲步枪" },
            new Unlock { level = 6,  id = "skin2",      name = "石肤强化",   desc = "石化上限 +15" },
            new Unlock { level = 8,  id = "mount",      name = "赛博摩托",   desc = "解锁坐骑，移动速度 +20%" },
            new Unlock { level = 10, id = "blade",      name = "裂空斩",     desc = "近战伤害 +12%" },
            new Unlock { level = 12, id = "resist2",    name = "抗石膜",     desc = "石化抗性 +12%" },
            new Unlock { level = 15, id = "vitality",   name = "强健体魄",   desc = "移速 +5%，石化上限 +10" },
            new Unlock { level = 20, id = "station_access", name = "远行许可", desc = "荒原堡垒交通门开放，可前往列车中转站" },
        };

        public static int Level { get { return Mathf.Clamp(SaveSystem.Data.level, 1, MaxLevel); } }
        public static float Xp { get { return SaveSystem.Data.xp; } }

        /// <summary>升到下一级需要的总经验。</summary>
        public static float XpForLevel(int level)
        {
            if (level < 1) level = 1;
            return Mathf.Round(60f * Mathf.Pow(level, 1.45f));
        }

        public static float XpToNext { get { return XpForLevel(Level); } }
        public static float Xp01 { get { return XpToNext <= 0.01f ? 0f : Mathf.Clamp01(Xp / XpToNext); } }

        // ---------------- 真实生效的加成 ----------------

        /// <summary>等级带来的伤害加成（每级 +3%）。</summary>
        public static float LevelDamageBonus
        {
            get
            {
                float v = (Level - 1) * 0.03f;
                if (Has("combo")) v += 0.15f;
                if (Has("blade")) v += 0.12f;
                return v;
            }
        }

        /// <summary>等级带来的移速加成。</summary>
        public static float LevelSpeedBonus
        {
            get
            {
                float v = (Level - 1) * 0.01f;
                if (Has("dash")) v += 0.03f;
                if (Has("mount")) v += 0.20f;
                if (Has("vitality")) v += 0.05f;
                return v;
            }
        }

        /// <summary>等级带来的石化抗性加成。</summary>
        public static float LevelResistBonus
        {
            get
            {
                float v = 0f;
                if (Has("resist2")) v += 0.12f;
                return v;
            }
        }

        /// <summary>石化上限加成（点数）。</summary>
        public static float LevelPetrifyCapBonus
        {
            get
            {
                float v = 0f;
                if (Has("skin2")) v += 15f;
                if (Has("vitality")) v += 10f;
                return v;
            }
        }

        /// <summary>弹匣扩容（发）。</summary>
        public static int LevelMagBonus { get { return Has("gun_mag") ? 4 : 0; } }

        public static bool Has(string id)
        {
            var l = SaveSystem.Data.unlockedSkills;
            if (l == null) return false;
            for (int i = 0; i < l.Count; i++) if (l[i] == id) return true;
            return false;
        }

        public static bool IsUnlocked(int level)
        {
            return Level >= level;
        }

        // ---------------- 经验 ----------------

        public static void AddXp(float amount)
        {
            if (amount <= 0f) return;
            var d = SaveSystem.Data;
            if (d.level < 1) d.level = 1;
            if (d.level >= MaxLevel) { d.xp = 0f; SaveSystem.Save(); return; }

            d.xp += amount;
            int guard = 0;
            while (d.level < MaxLevel && d.xp >= XpForLevel(d.level) && guard++ < 64)
            {
                d.xp -= XpForLevel(d.level);
                d.level++;
                GrantUnlocksFor(d.level);
                var h = LeveledUp;
                if (h != null) h(d.level);
            }
            if (d.level >= MaxLevel) d.xp = 0f;
            SaveSystem.Save();
            Notify();
        }

        static void GrantUnlocksFor(int level)
        {
            var d = SaveSystem.Data;
            if (d.unlockedSkills == null) d.unlockedSkills = new List<string>();
            for (int i = 0; i < Unlocks.Length; i++)
            {
                var u = Unlocks[i];
                if (u.level != level) continue;
                if (!d.unlockedSkills.Contains(u.id)) d.unlockedSkills.Add(u.id);
            }
        }

        /// <summary>老存档补齐：按当前等级补发所有应得解锁。</summary>
        public static void SyncUnlocks()
        {
            var d = SaveSystem.Data;
            if (d.unlockedSkills == null) d.unlockedSkills = new List<string>();
            for (int i = 0; i < Unlocks.Length; i++)
            {
                var u = Unlocks[i];
                if (Level >= u.level && !d.unlockedSkills.Contains(u.id)) d.unlockedSkills.Add(u.id);
            }
        }

        public static string UnlockName(string id)
        {
            for (int i = 0; i < Unlocks.Length; i++) if (Unlocks[i].id == id) return Unlocks[i].name;
            return id;
        }

        public static string UnlockDesc(string id)
        {
            for (int i = 0; i < Unlocks.Length; i++) if (Unlocks[i].id == id) return Unlocks[i].desc;
            return "";
        }

        static void Notify()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
