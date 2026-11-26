using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>荒原净化结晶：玩家靠近即自动拾取，降低石化值。</summary>
    public class PetrifyPickup : MonoBehaviour
    {
        public float restore = 35f;
        public string label = "净化结晶";
        public float pickupRadius = 2.2f;

        bool used;
        PlayerPetrify target;

        void Start()
        {
            target = FindObjectOfType<PlayerPetrify>();
        }

        void Update()
        {
            if (used || target == null || target.value <= 0f) return;
            Vector3 a = new Vector3(target.transform.position.x, 0f, target.transform.position.z);
            Vector3 b = new Vector3(transform.position.x, 0f, transform.position.z);
            if (Vector3.Distance(a, b) <= pickupRadius) Take();
        }

        void Take()
        {
            used = true;
            target.value = Mathf.Max(0f, target.value - restore);
            LootToast.Show(label + " · 石化 -" + Mathf.RoundToInt(restore));
            Destroy(gameObject);
        }
    }
}
