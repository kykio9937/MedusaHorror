using UnityEngine;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 可购置的房屋：靠近按 E 花信用点买下，买下后门解锁并写入存档，永久归玩家所有。
    /// </summary>
    [DisallowMultipleComponent]
    public class HousePurchase : MonoBehaviour, IInteractable
    {
        [Header("房屋")]
        public string houseId = "W4_Apartment_7B";
        public string houseName = "霓虹公寓 7B";
        public int price = 800;

        [Header("联动")]
        [Tooltip("买下后解锁的门；可留空")]
        public DoorInteractable door;

        bool Owned
        {
            get
            {
                var l = SaveSystem.Data.houses;
                for (int i = 0; i < l.Count; i++) if (l[i] == houseId) return true;
                return false;
            }
        }

        void Awake()
        {
            if (Owned && door != null) door.Unlock();
        }

        public string Hint
        {
            get
            {
                if (Owned) return houseName + " · 已购置";
                return "购买「" + houseName + "」· " + price + " 信用点";
            }
        }

        public bool CanInteract { get { return !Owned; } }

        public void Interact()
        {
            if (Owned) return;
            if (!Wallet.CanAfford(price))
            {
                LootToast.Show("信用点不足 · 需要 " + price + "，现有 " + Wallet.Coins);
                var a0 = AudioDirector.Instance;
                if (a0 != null) a0.PlayAt(SfxId.DoorLocked, transform.position, 0.7f, 1f);
                return;
            }

            Wallet.Spend(price);
            SaveSystem.Data.houses.Add(houseId);
            SaveSystem.Save();
            if (door != null) door.Unlock();

            LootToast.Show("已购置「" + houseName + "」· 门已解锁");
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.9f, 1.1f);
        }
    }
}
