using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>W1 objective state: collect 5 key items, then the exit stargate unlocks.</summary>
    public static class W1ObjectiveState
    {
        public const int Total = 5;
        public static int Count { get; private set; }
        public static bool Complete { get { return Count >= Total; } }

        public static void Reset() { Count = 0; }

        public static void Collect(string label)
        {
            Count++;
            LootToast.Show("获得：" + label + "（" + Count + "/" + Total + "）");
            if (Count < Total) return;
            var portals = Object.FindObjectsOfType<MedusaHorror.Gameplay.ExitPortal>(true);
            foreach (var p in portals) p.requireActivation = false;
            LootToast.Show("星门已激活 · 前往出口");
        }
    }

    /// <summary>Trigger pickup that feeds W1ObjectiveState.</summary>
    [RequireComponent(typeof(Collider))]
    public class W1ObjectivePickup : MonoBehaviour
    {
        public string label = "线索";
        public float spinSpeed = 60f;
        bool _taken;

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            if (!other.CompareTag("Player")) return;
            _taken = true;
            W1ObjectiveState.Collect(label);
            gameObject.SetActive(false);
        }
    }
}
