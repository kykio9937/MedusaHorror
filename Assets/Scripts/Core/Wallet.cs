using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>
    /// 玩家货币（废土信用点）。数值落在存档里，跨场景、跨局持久。
    /// </summary>
    public static class Wallet
    {
        /// <summary>余额变化时触发，HUD 订阅后即时刷新。</summary>
        public static event System.Action Changed;

        public static int Coins { get { return SaveSystem.Data.coins; } }

        public static void Add(int amount)
        {
            if (amount <= 0) return;
            SaveSystem.Data.coins += amount;
            SaveSystem.Save();
            Notify();
        }

        public static bool CanAfford(int amount)
        {
            return amount <= 0 || SaveSystem.Data.coins >= amount;
        }

        /// <summary>扣款；余额不足返回 false 且不扣。</summary>
        public static bool Spend(int amount)
        {
            if (amount <= 0) return true;
            if (SaveSystem.Data.coins < amount) return false;
            SaveSystem.Data.coins -= amount;
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
