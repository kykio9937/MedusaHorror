using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 可拾取的任务物品：原地旋转浮动，玩家走近自动收取，并向任务系统上报进度。
    /// </summary>
    [DisallowMultipleComponent]
    public class QuestPickup : MonoBehaviour
    {
        [Header("物品")]
        public string itemTag = "scrap";
        public string itemName = "废铁";

        [Header("拾取")]
        public float pickupRadius = 1.9f;

        Vector3 _origin;
        Transform _player;
        float _t;
        bool _taken;

        void Start()
        {
            _origin = transform.position;
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        void Update()
        {
            _t += Time.deltaTime;
            transform.position = _origin + Vector3.up * (0.34f + Mathf.Sin(_t * 2.2f) * 0.09f);
            transform.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);

            if (_taken) return;
            if (_player == null)
            {
                var pc = Object.FindObjectOfType<ThirdPersonController>();
                if (pc != null) _player = pc.transform;
            }
            if (_player == null) return;

            Vector3 a = _player.position;
            Vector3 b = _origin;
            a.y = 0f;
            b.y = 0f;
            if ((a - b).sqrMagnitude <= pickupRadius * pickupRadius) Collect();
        }

        void Collect()
        {
            _taken = true;
            QuestSystem.Report(QuestKind.Collect, itemTag, 1);
            LootToast.Show("拾取 " + itemName);
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.7f, 1.2f);
            Destroy(gameObject);
        }
    }
}
