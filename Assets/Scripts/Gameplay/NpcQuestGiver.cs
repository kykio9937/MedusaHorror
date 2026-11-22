using UnityEngine;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// NPC 任务发布者：靠近按 E 依次「接任务 → 查进度 → 交任务领奖」。
    /// 任务定义在 Awake 时注册进 QuestSystem，进度与领奖状态存进存档。
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcQuestGiver : MonoBehaviour, IInteractable
    {
        [Header("任务定义")]
        public QuestDef def = new QuestDef();

        [Header("朝向")]
        [Tooltip("NPC 默认面向的目标；留空则保持摆放朝向")]
        public Transform faceTo;

        [Header("站位")]
        [Tooltip("锁死 NPC 位置，防止动画 root motion 把人带跑")]
        public bool lockPosition = true;

        Vector3 _lockPos;

        void Awake()
        {
            _lockPos = transform.position;
            if (string.IsNullOrEmpty(def.id))
                def.id = gameObject.name + "_" + Mathf.Abs(GetInstanceID());
            QuestSystem.Register(def);
            if (faceTo != null) Face(faceTo.position);
        }

        void LateUpdate()
        {
            if (lockPosition && transform.position != _lockPos)
                transform.position = _lockPos;
        }

        void Face(Vector3 p)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
        }

        public string Hint
        {
            get
            {
                if (QuestSystem.Claimed(def.id)) return def.npcName + "（已交付）";
                if (!QuestSystem.Accepted(def.id)) return "交谈 · " + def.npcName;
                if (QuestSystem.Ready(def.id)) return "交付 · " + def.title;
                return "查看进度 · " + def.title;
            }
        }

        public bool CanInteract { get { return !QuestSystem.Claimed(def.id); } }

        public void Interact()
        {
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.UiClick, transform.position, 0.7f, 1f);

            if (!QuestSystem.Accepted(def.id))
            {
                QuestSystem.Accept(def.id);
                LootToast.Show("「" + def.npcName + "」：" + def.desc);
                return;
            }

            if (QuestSystem.Ready(def.id))
            {
                if (QuestSystem.Claim(def.id))
                {
                    var reward = "+" + def.rewardCoins + " 信用点";
                    if (def.rewardSpeed > 0f) reward += " · 移速 +" + Mathf.RoundToInt(def.rewardSpeed * 100f) + "%";
                    if (def.rewardDamage > 0f) reward += " · 攻击 +" + Mathf.RoundToInt(def.rewardDamage * 100f) + "%";
                    if (def.rewardPetrify > 0f) reward += " · 石化抗性 +" + Mathf.RoundToInt(def.rewardPetrify * 100f) + "%";
                    LootToast.Show("任务完成：" + def.title + " · " + reward);
                    if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.9f, 1.15f);
                }
                return;
            }

            LootToast.Show(def.title + " · 进度 " + QuestSystem.Progress(def.id) + "/" + def.need);
        }
    }
}
