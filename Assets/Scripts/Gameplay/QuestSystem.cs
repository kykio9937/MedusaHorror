using System.Collections.Generic;
using UnityEngine;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    public enum QuestKind
    {
        /// <summary>收集指定 tag 的物品若干个。</summary>
        Collect = 0,
        /// <summary>到达指定 id 的坐标点。</summary>
        Reach = 1
    }

    /// <summary>任务定义。由 NPC 在运行时注册，id 全局唯一。</summary>
    [System.Serializable]
    public class QuestDef
    {
        public string id;
        public string npcName;
        public string title;
        public string desc;
        public QuestKind kind = QuestKind.Collect;
        [Tooltip("Collect 用物品 tag；Reach 用目标点 id")]
        public string targetTag = "";
        public int need = 3;
        public int rewardCoins = 100;
        public float rewardSpeed = 0f;
        public float rewardDamage = 0f;
        public float rewardPetrify = 0f;
    }

    /// <summary>
    /// 轻量任务系统：定义注册 + 状态存档 + 进度上报 + 发奖。
    /// 状态存在 SaveData.quests，跨场景持久。
    /// </summary>
    public static class QuestSystem
    {
        static readonly Dictionary<string, QuestDef> _defs = new Dictionary<string, QuestDef>();

        /// <summary>任务状态变化时触发，任务 HUD 订阅刷新。</summary>
        public static event System.Action Changed;

        public static void Register(QuestDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.id)) return;
            _defs[def.id] = def;
        }

        public static QuestDef Get(string id)
        {
            QuestDef d;
            return (id != null && _defs.TryGetValue(id, out d)) ? d : null;
        }

        public static QuestState State(string id)
        {
            var list = SaveSystem.Data.quests;
            for (int i = 0; i < list.Count; i++)
                if (list[i].id == id) return list[i];
            var s = new QuestState { id = id };
            list.Add(s);
            return s;
        }

        public static bool Accepted(string id) { return State(id).accepted; }
        public static bool Claimed(string id) { return State(id).claimed; }
        public static int Progress(string id) { return State(id).progress; }

        public static void Accept(string id)
        {
            var s = State(id);
            if (s.accepted) return;
            s.accepted = true;
            SaveSystem.Save();
            Notify();
        }

        /// <summary>上报进度：所有已接、未领奖、类型与 tag 匹配的任务都会推进。</summary>
        public static void Report(QuestKind kind, string tag, int amount = 1)
        {
            if (amount <= 0) return;
            var list = SaveSystem.Data.quests;
            bool dirty = false;
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                if (!s.accepted || s.claimed) continue;
                var d = Get(s.id);
                if (d == null || d.kind != kind) continue;
                if (!string.IsNullOrEmpty(d.targetTag) && d.targetTag != tag) continue;
                if (s.progress >= d.need) continue;
                s.progress = Mathf.Min(d.need, s.progress + amount);
                if (s.progress >= d.need) s.completed = true;
                dirty = true;
            }
            if (dirty) { SaveSystem.Save(); Notify(); }
        }

        public static bool Ready(string id)
        {
            var d = Get(id);
            var s = State(id);
            return d != null && s.accepted && !s.claimed && s.progress >= d.need;
        }

        /// <summary>领取奖励；未达条件或已领过返回 false。</summary>
        public static bool Claim(string id)
        {
            var d = Get(id);
            var s = State(id);
            if (d == null || !s.accepted || s.claimed) return false;
            if (s.progress < d.need) return false;

            s.claimed = true;
            s.completed = true;

            Wallet.Add(d.rewardCoins);
            if (d.rewardSpeed > 0f) PlayerStats.AddSpeed(d.rewardSpeed);
            if (d.rewardDamage > 0f) PlayerStats.AddDamage(d.rewardDamage);
            if (d.rewardPetrify > 0f) PlayerStats.AddPetrifyResist(d.rewardPetrify);

            SaveSystem.Save();
            Notify();
            return true;
        }

        static void Notify()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
