using System.Collections.Generic;

namespace MedusaHorror.Core
{
    /// <summary>背包：物品堆叠按 id 累加，落在存档里跨场景保留。</summary>
    public static class Inventory
    {
        public static event System.Action Changed;

        public static List<ItemStack> Items { get { return SaveSystem.Data.items; } }

        public static void Add(string id, string name, int count = 1, string desc = "", string kind = "材料")
        {
            if (string.IsNullOrEmpty(id) || count <= 0) return;
            var list = SaveSystem.Data.items;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id != id) continue;
                list[i].count += count;
                if (!string.IsNullOrEmpty(name)) list[i].name = name;
                if (!string.IsNullOrEmpty(desc)) list[i].desc = desc;
                SaveSystem.Save();
                Notify();
                return;
            }
            list.Add(new ItemStack { id = id, name = name, count = count, desc = desc, kind = kind });
            SaveSystem.Save();
            Notify();
        }

        public static int Count(string id)
        {
            var list = SaveSystem.Data.items;
            for (int i = 0; i < list.Count; i++) if (list[i].id == id) return list[i].count;
            return 0;
        }

        /// <summary>扣除物品；不足返回 false 且不改动。</summary>
        public static bool Remove(string id, int count = 1)
        {
            if (count <= 0) return true;
            var list = SaveSystem.Data.items;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].id != id) continue;
                if (list[i].count < count) return false;
                list[i].count -= count;
                if (list[i].count <= 0) list.RemoveAt(i);
                SaveSystem.Save();
                Notify();
                return true;
            }
            return false;
        }

        public static int TotalCount
        {
            get
            {
                var list = SaveSystem.Data.items;
                int n = 0;
                for (int i = 0; i < list.Count; i++) n += list[i].count;
                return n;
            }
        }

        static void Notify()
        {
            var h = Changed;
            if (h != null) h();
        }
    }
}
