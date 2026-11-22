using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>家中的存档点：购置后按 E 手动保存。</summary>
    public class HouseSavePoint : MonoBehaviour, IInteractable
    {
        public string houseId = "W4_Apartment_7B";

        bool Owned
        {
            get
            {
                var l = SaveSystem.Data.houses;
                for (int i = 0; i < l.Count; i++) if (l[i] == houseId) return true;
                return false;
            }
        }

        public string Hint { get { return Owned ? "休息 · 保存进度" : "私人住宅 · 先在门口购置"; } }
        public bool CanInteract { get { return true; } }

        public void Interact()
        {
            if (!Owned) { LootToast.Show("这是别人的家，先在门口买下它。"); return; }
            SaveSystem.Save();
            LootToast.Show("你在家中休息 · 进度已保存");
            var a = AudioDirector.Instance;
            if (a != null) a.PlayAt(SfxId.Pickup, transform.position, 0.8f, 1.2f);
        }
    }

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

    /// <summary>家具购买点：买下后激活对应家具模型。</summary>
    public class FurnitureShop : MonoBehaviour, IInteractable
    {
        public string houseId = "W4_Apartment_7B";
        public string furnitureId = "sofa";
        public string furnitureName = "沙发";
        public int price = 150;
        public GameObject furnitureObject;

        bool Owned
        {
            get
            {
                var l = SaveSystem.Data.houses;
                for (int i = 0; i < l.Count; i++) if (l[i] == houseId) return true;
                return false;
            }
        }

        bool Bought
        {
            get
            {
                var l = SaveSystem.Data.furniture;
                for (int i = 0; i < l.Count; i++) if (l[i] == furnitureId) return true;
                return false;
            }
        }

        void Awake()
        {
            if (Bought && furnitureObject != null) furnitureObject.SetActive(true);
        }

        public string Hint
        {
            get
            {
                if (Bought) return furnitureName + " · 已购置";
                if (!Owned) return "家具 · 先在门口购置公寓";
                return "购买「" + furnitureName + "」· " + Pricing.Apply(price) + " 信用点";
            }
        }

        public bool CanInteract { get { return !Bought; } }

        public void Interact()
        {
            if (Bought) return;
            if (!Owned) { LootToast.Show("先在门口买下公寓，再添置家具。"); return; }
            int cost = Pricing.Apply(price);
            if (!Wallet.CanAfford(cost)) { LootToast.Show("信用点不足 · 需要 " + cost); return; }
            Wallet.Spend(cost);
            SaveSystem.Data.furniture.Add(furnitureId);
            SaveSystem.Save();
            if (furnitureObject != null) furnitureObject.SetActive(true);
            LootToast.Show("已添置「" + furnitureName + "」");
            var a = AudioDirector.Instance;
            if (a != null) a.PlayAt(SfxId.Pickup, transform.position, 0.9f, 1.15f);
            gameObject.SetActive(false);
        }
    }
}
