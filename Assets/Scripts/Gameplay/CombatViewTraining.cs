using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Core;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    /// <summary>保安队10级战术观察训练，位于W0训练场；医院始终第一人称。</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CombatViewTraining : MonoBehaviour, IInteractable
    {
        public const int RequiredLevel = 10;
        public static bool ThirdPersonAllowed => MedusaHorror.Player.PlayerViewMode.ThirdPersonAllowed;
        public string Hint => SaveSystem.Data.thirdPersonTrainingUnlocked ? "战术观察训练 · 已完成" : Progression.Level < RequiredLevel ? "保安队战术训练 · 10 级开放（当前 " + Progression.Level + " 级）" : "保安队战术训练 · 解锁第三人称";
        public bool CanInteract => !DialogueSystem.IsOpen && !SceneLoader.IsLoading;
        public void Interact()
        {
            if (SceneManager.GetActiveScene().name != "W0_Base") return;
            if (SaveSystem.Data.thirdPersonTrainingUnlocked) { LootToast.Show("第三人称已解锁，按 T 切换。枪械仅能在第一人称使用。医院试炼始终为第一人称。"); return; }
            if (Progression.Level < RequiredLevel) { LootToast.Show(Hint); return; }
            DialogueSystem.Ensure().Play("保安队 · 射击教官", new[]
            {
                "你已经有了一些实战经验。今天学习从更广的视角判断自己的位置、身后的空间和敌人的包围路线。",
                "完成讲解后，可以按 T 在第一人称和第三人称之间切换。剑能在两种视角使用；枪只能在第一人称使用，切入第三人称时会先收枪换剑。",
                "医院试炼仍固定第一人称。离开训练场后，这次学习的观察方式会保留下来。"
            }, Complete);
        }
        static void Complete()
        {
            if (SaveSystem.Data.thirdPersonTrainingUnlocked || Progression.Level < RequiredLevel || SceneManager.GetActiveScene().name != "W0_Base") return;
            SaveSystem.Data.thirdPersonTrainingUnlocked = true; SaveSystem.Save();
            StoryBanner.Show("战术观察训练完成", "第三人称已解锁 · 按 T 切换视角 · 枪仅限第一人称", 5);
        }
    }
}
