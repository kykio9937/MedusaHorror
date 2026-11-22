using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// Boss 掉落器：Boss 死亡后在指定位置生成「锈蚀钥匙」道具。
    /// 钥匙是通往下一区域的凭证，拾取后由 BossArenaGate 打开进度门。
    /// </summary>
    public class BossLoot : MonoBehaviour
    {
        [Header("投放位置")]
        public Transform spawnAt;
        public Vector3 offset = new Vector3(0f, 0.65f, 0f);
        [Tooltip("沿尸体背离玩家方向再挪开的距离，避免钥匙落在玩家脚下被瞬间拾取")]
        public float dropDistance = 2.2f;

        [Header("联动")]
        public BossArenaGate gate;

        [Header("道具")]
        public string itemName = "锈蚀钥匙";
        [Tooltip("击杀 Boss 一并获得的信用点")]
        public int coinReward = 300;
        public Color glowColor = new Color(1f, 0.82f, 0.35f, 1f);

        public bool dropped;

        public BossKey Drop()
        {
            if (dropped) return null;
            dropped = true;
            Vector3 basePos = spawnAt != null ? spawnAt.position : transform.position;
            Vector3 away = basePos;
            var pet = Object.FindObjectOfType<PlayerPetrify>();
            if (pet != null)
            {
                Vector3 d = basePos - pet.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.04f) away = basePos + d.normalized * dropDistance;
            }
            Vector3 p = away + offset;
            var key = BossKey.Spawn(p, itemName, glowColor, gate);
            if (coinReward > 0)
            {
                MedusaHorror.Core.Wallet.Add(coinReward);
                LootToast.Show("战利品 · +" + coinReward + " 信用点");
            }
            Debug.Log("[Loot] Boss 掉落「" + itemName + "」于 " + p.ToString("F2") + " · 信用点 +" + coinReward);
            return key;
        }

        public void ResetLoot()
        {
            dropped = false;
        }
    }
}
