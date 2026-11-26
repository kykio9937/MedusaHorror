using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>家中的仓库：E 存入 100，Shift+E 取出 100。</summary>
    public class HouseStorage : MonoBehaviour, IInteractable
    {
        public string houseId = "W4_Apartment_7B";
        public int step = 100;

        InputReader _input;

        void Awake()
        {
            _input = GetComponentInParent<InputReader>();
            if (_input == null) _input = Object.FindObjectOfType<InputReader>();
        }

        bool Owned
        {
            get
            {
                var l = SaveSystem.Data.houses;
                for (int i = 0; i < l.Count; i++) if (l[i] == houseId) return true;
                return false;
            }
        }

        public string Hint
        {
            get
            {
                if (!Owned) return "私人仓库 · 先在门口购置";
                return "仓库 · 已存 " + SaveSystem.Data.houseStorage + "（E 存入 / Shift+E 取出）";
            }
        }

        public bool CanInteract { get { return true; } }

        public void Interact()
        {
            if (!Owned) { LootToast.Show("先在门口买下这套公寓。"); return; }
            bool take = _input != null && _input.RunHeld;
            if (take)
            {
                if (SaveSystem.Data.houseStorage < step) { LootToast.Show("仓库里没有可取的信用点"); return; }
                SaveSystem.Data.houseStorage -= step;
                Wallet.Add(step);
                SaveSystem.Save();
                LootToast.Show("取出 " + step + " 信用点 · 仓库余 " + SaveSystem.Data.houseStorage);
            }
            else
            {
                if (!Wallet.CanAfford(step)) { LootToast.Show("信用点不足 · 存入需要 " + step); return; }
                Wallet.Spend(step);
                SaveSystem.Data.houseStorage += step;
                SaveSystem.Save();
                LootToast.Show("存入 " + step + " 信用点 · 仓库共 " + SaveSystem.Data.houseStorage);
            }
        }
    }
}
