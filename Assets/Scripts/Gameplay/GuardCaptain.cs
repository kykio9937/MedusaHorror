using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    public class GuardCaptain : MonoBehaviour, IInteractable
    {
        Animator _animator;
        void Start()
        {
            _animator=GetComponentInChildren<Animator>();
            foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="Kazuko_claymore")t.gameObject.SetActive(false);
            var player=FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
            if(player!=null){var d=player.transform.position-transform.position;d.y=0;if(d.sqrMagnitude>0.01f)transform.rotation=Quaternion.LookRotation(d);}
        }
        void LateUpdate()
        {
            if(_animator!=null&&_animator.transform!=transform){_animator.transform.localPosition=new Vector3(0,0.16f,0);_animator.transform.localRotation=Quaternion.identity;}
        }
        public string Hint => SaveSystem.Data.captainBriefingDone ? "保安队队长 · 交谈 / 巡防委托" : "保安队队长 · 听他说明情况";
        public bool CanInteract => !DialogueSystem.IsOpen && !SceneLoader.IsLoading;
        public static readonly string[] FirstBriefing =
        {
            "醒了？别急着站起来。这里是荒原堡垒，我是保安队队长。你已经回来了。",
            "你刚才经历的医院，并不在城外。那是我们留下的虚拟试炼，用来教你搜索物资、使用武器，也教你面对恐惧时怎样活下来。",
            "你是在这里长大的孩子，但城墙不能护着你一辈子。荒原上的怪物会袭击村庄，我们希望你有一天能独立生存，也能保护身边的人。",
            "现在先在堡垒里认认路。西侧出口通往荒原村道和旧村庄，那些地方能探索，也会遇到真正的危险。记得回来补给，别逞强。",
            "想再练习，就去城内单独的试炼门。只有在那里才能重新进入那所医院；试炼结束会回到这里。",
            "保安队东侧入口通往独立训练场。达到 10 级后，在场内教官训练台学习第三人称观察。剑支持两种视角，枪只在第一人称使用。",
            "等你成长到 20 级，东侧交通门才允许通行。门后是列车中转站，乘车可以去霓虹街区和其他地方。先把眼前这段路走稳。"
        };
        public void Interact()
        {
            if (!CanInteract) return;
            if (!SaveSystem.Data.captainBriefingDone && SaveSystem.Data.hospitalTrainingPassed)
            {
                DialogueSystem.Ensure().Play("保安队队长", FirstBriefing, CompleteBriefing);
                return;
            }
            if (!SaveSystem.Data.hospitalTrainingPassed)
            {
                DialogueSystem.Ensure().Play("保安队队长", new[] { "先去城内的试炼门熟悉武器。回来以后，我会带你认识这片荒原。" }); return;
            }
            CaptainPanel.Open(this);
        }
        public void DiscussPatrol()
        {
            if(!SaveSystem.Data.firstWildPatrolAccepted&&!SaveSystem.Data.firstWildPatrolDone)
            {
                DialogueSystem.Ensure().Play("保安队队长",new[]{"第一次巡防不要求你逞英雄。去西门外的旧村庄，确认中央广场的情况，再平安回到这里。","委托：旧村侦察。报告奖励100信用点与120经验。准备好了，就从西门出发。"},()=>{SaveSystem.Data.firstWildPatrolAccepted=true;SaveSystem.Save();ShowObjective();LootToast.Show("已接取委托 · 旧村侦察");});return;
            }
            if (SaveSystem.Data.firstWildPatrolDone && !SaveSystem.Data.firstWildPatrolClaimed)
            {
                DialogueSystem.Ensure().Play("保安队队长", new[] { "去过旧村庄了？能平安回来就是第一步。", "这是巡防补给，拿好。荒原没有固定的安全路线；先照顾好自己，再去帮助别人。" }, () =>
                {
                    if (SaveSystem.Data.firstWildPatrolClaimed) return;
                    SaveSystem.Data.firstWildPatrolClaimed = true; Wallet.Add(100); Progression.AddXp(120); SaveSystem.Save();
                    LootToast.Show("首次巡防完成 · 信用点 +100 · 经验 +120"); ShowObjective();
                }); return;
            }
            DialogueSystem.Ensure().Play("保安队队长", new[]
            {
                "城内可以休整，西门外是野外探索区。先去旧村庄查看情况，再平安回来向我报告。",
                Progression.Level < 20 ? "交通门还需要 " + (20 - Progression.Level) + " 级。巡防、完成当地委托和对付怪物都能帮助你成长。" : "你已经达到 20 级，东侧交通门已开放。列车票价会在上车前显示，确认目的地再出发。"
            });
        }
        static void CompleteBriefing()
        {
            SaveSystem.Data.captainBriefingDone = true; SaveSystem.Save(); ShowObjective();
        }
        public static void ShowObjective()
        {
            var d = SaveSystem.Data;
            string text = !d.captainBriefingDone ? "苏醒之后 · 与保安队队长交谈" : d.firstWildPatrolDone && !d.firstWildPatrolClaimed ? "首次巡防 · 返回主城向队长报告" : !d.firstWildPatrolDone ? "首次巡防 · 从西门探索旧村庄" : Progression.Level < 20 ? "荒原成长 · 探索与委托，20 级开放交通门" : "交通门已开放 · 可前往列车中转站";
            ObjectiveHud.Ensure().Show(text, 0, 0);
        }
    }
}
