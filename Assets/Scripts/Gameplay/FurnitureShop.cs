using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
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
