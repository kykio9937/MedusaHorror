using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>W1 objective state: collect 5 key items, then the exit stargate unlocks.</summary>
    public static class W1ObjectiveState
    {
        public const int Total = 5;
        static readonly System.Collections.Generic.HashSet<string> Collected = new System.Collections.Generic.HashSet<string>();
        public static int Count => Collected.Count;
        public static bool Complete { get { return Count >= Total; } }

        public static void Reset() { Collected.Clear(); }

        public static void Collect(string label)
        {
            if (!Collected.Add(label)) return;
            LootToast.Show("获得：" + label + "（" + Count + "/" + Total + "）");
            if (Count < Total) return;
            var portals = Object.FindObjectsOfType<MedusaHorror.Gameplay.ExitPortal>(true);
            foreach (var p in portals) p.requireActivation = false;
            LootToast.Show("五件训练凭证已齐 · 北侧二楼通道开放");
        }
    }

    /// <summary>Trigger pickup that feeds W1ObjectiveState.</summary>
    [RequireComponent(typeof(Collider))]
    public class W1ObjectivePickup : MonoBehaviour, IInteractable
    {
        public string label = "线索";
        public float spinSpeed = 60f;
        bool _taken;
        public LevelGoalPoint goal;
        public string Hint => "拾取「" + label + "」 · 医院线索";
        public bool CanInteract => !_taken && !MedusaHorror.Player.InputReader.blocked;

        void Reset() { GetComponent<Collider>().isTrigger = true; }

        void Update()
        {
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        public void Interact()
        {
            if (!CanInteract) return;
            _taken = true;
            W1ObjectiveState.Collect(label);
            if (goal != null && goal.owner != null) goal.owner.CompletePoint(goal);
            string id = "hospital_clue_" + label;
            if (MedusaHorror.Core.Inventory.Count(id) == 0)
                MedusaHorror.Core.Inventory.Add(id, label, 1, "在医院发现的关键线索。集齐五件后可启动离场星门。", "任务");
            gameObject.SetActive(false);
        }
    }
}
