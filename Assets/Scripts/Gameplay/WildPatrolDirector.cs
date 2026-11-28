using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    public class WildPatrolDirector : MonoBehaviour
    {
        Transform _player;
        void Update()
        {
            if (!SaveSystem.Data.captainBriefingDone || !SaveSystem.Data.firstWildPatrolAccepted || SaveSystem.Data.firstWildPatrolDone || InputReader.blocked) return;
            if (_player == null) { var p = FindObjectOfType<ThirdPersonController>(); if (p != null) _player = p.transform; return; }
            var position = _player.position;
            if (new Vector2(position.x, position.z).sqrMagnitude > 14 * 14) return;
            SaveSystem.Data.firstWildPatrolDone = true; SaveSystem.Save();
            StoryBanner.Show("旧村庄 · 中央广场", "记录下这里的情况，返回堡垒向保安队队长报告。", 5);
            ObjectiveHud.Ensure().Show("巡防记录已完成 · 返回堡垒向队长报告", 0, 0);
        }
    }
}
