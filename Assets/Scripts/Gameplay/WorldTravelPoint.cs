using UnityEngine;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    [RequireComponent(typeof(BoxCollider))]
    public class WorldTravelPoint : MonoBehaviour, IInteractable
    {
        public string destination;
        public string label;
        public int minimumLevel;
        public string Hint => Progression.Level < minimumLevel ? label + " · " + minimumLevel + " 级开放（当前 " + Progression.Level + "）" : label;
        public bool CanInteract => !SceneLoader.IsLoading;
        public bool Available => Progression.Level >= minimumLevel && CampaignRoute.CanTravel(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, destination, out _);
        public void Interact()
        {
            if (Progression.Level < minimumLevel) { LootToast.Show(Hint); return; }
            SceneLoader.Load(destination);
        }
    }
}
