using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace MedusaHorror.Player
{
    public static class PlayerViewMode
    {
        static bool _thirdPerson;
        public static bool ThirdPersonAllowed => SceneManager.GetActiveScene().name != CampaignRoute.Trial && SaveSystem.Data.thirdPersonTrainingUnlocked;
        public static bool IsFirstPerson => !ThirdPersonAllowed || !_thirdPerson;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { _thirdPerson = false; }
        public static void ResetForScene() { _thirdPerson = false; }
        public static void RequireFirstPerson() { _thirdPerson = false; }
        public static bool Toggle(WeaponLoadout loadout)
        {
            if (InputReader.blocked || SceneLoader.IsLoading || (loadout != null && loadout.IsSwitching)) return false;
            if (!ThirdPersonAllowed)
            {
                LootToast.Show(SceneManager.GetActiveScene().name == CampaignRoute.Trial ? "医院试炼固定第一人称" : "第三人称需要达到10级，并前往训练场完成训练"); return false;
            }
            _thirdPerson = !_thirdPerson;
            if (_thirdPerson && loadout != null && loadout.Current != WeaponKind.Sword) loadout.Equip(WeaponKind.Sword, "切换第三人称收枪");
            return true;
        }
        public static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.T);
#endif
        }
    }
}
