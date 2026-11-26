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
}
