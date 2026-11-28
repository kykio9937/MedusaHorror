using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.Gameplay;

namespace MedusaHorror.Core
{
    public static class CampaignRoute
    {
        public const string City = "W2_Stronghold";
        public const string Trial = "W1_Hospital";
        public const string Wild = "W2_Wasteland";
        public const string Station = "W3_Subway";
        public const int StationLevel = 20;
        public static string PreviousScene { get; private set; }
        public static bool ReturnFromTrial { get; private set; }

        public static bool CanTravel(string source, string target, out string reason)
        {
            reason = "";
            if(CheckpointSystem.Restoring)return true;
            if (target == Station && Progression.Level < StationLevel)
                reason = "交通门需要达到 20 级 · 当前 " + Progression.Level + " 级";
            if ((target == "W4_NeonStreet" || target == "W5_AlienGate") && source != Station && source != "MainMenu" && source != target)
                reason = "远行需要从列车中转站选择目的地。";
            if (target == Trial && source != City && source != "MainMenu" && source != Trial)
                reason = "试炼入口位于荒原堡垒，先返回主城。";
            if (target == City && source == Trial && !SaveSystem.Data.hospitalTrainingPassed)
                reason = "先找到医院的出口。";
            return reason.Length == 0;
        }

        public static void BeginTravel(string source, string target)
        {
            PreviousScene = source;
            ReturnFromTrial = source == Trial && target == City;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { PreviousScene = null; ReturnFromTrial = false; }

        public static void Apply(Scene scene)
        {
            MedusaHorror.Player.PlayerViewMode.ResetForScene();
            if(scene.name==Trial)HospitalExploration.Reset();
            CheckpointSystem.Attach();
            if (scene.name == "W0_Base")
            {
                foreach(var portal in Object.FindObjectsOfType<BasePortal>())portal.gameObject.SetActive(false);
                foreach(var portal in Object.FindObjectsOfType<ExitPortal>())portal.gameObject.SetActive(false);
            }
            if (scene.name == Trial)
            {
                // 序章不能通过旧基地符文提前看到外界。
                foreach (var portal in Object.FindObjectsOfType<BasePortal>()) portal.gameObject.SetActive(false);
                foreach (var portal in Object.FindObjectsOfType<ExitPortal>()) portal.nextSceneOverride = City;
                if (Object.FindObjectOfType<TrialAwakening>() == null) new GameObject("TrialAwakening").AddComponent<TrialAwakening>();
            }
            if (scene.name == Wild)
            {
                foreach (var portal in Object.FindObjectsOfType<BasePortal>()) portal.targetScene = City;
                foreach (var portal in Object.FindObjectsOfType<ExitPortal>()) portal.nextSceneOverride = City;
            }
            if (scene.name == "W4_NeonStreet" || scene.name == "W5_AlienGate")
            {
                foreach (var portal in Object.FindObjectsOfType<ExitPortal>()) portal.nextSceneOverride = Station;
                foreach (var portal in Object.FindObjectsOfType<BasePortal>()) portal.targetScene = City;
            }
            if (scene.name == Station)
                foreach (var portal in Object.FindObjectsOfType<BasePortal>()) portal.targetScene = City;
        }
    }
}
