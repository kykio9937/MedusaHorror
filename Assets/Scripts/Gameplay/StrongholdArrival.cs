using System.Collections;
using UnityEngine;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    public class StrongholdArrival : MonoBehaviour
    {
        IEnumerator Start()
        {
            while (SceneLoader.IsLoading) yield return null;
            yield return new WaitForSecondsRealtime(1.2f);
            var player=FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
            if(player!=null && CampaignRoute.PreviousScene==CampaignRoute.Wild)
            {
                var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(-19,0.2f,0);cc.enabled=true;
            }
            GuardCaptain.ShowObjective();
            if (SaveSystem.Data.hospitalTrainingPassed && !SaveSystem.Data.captainBriefingDone)
            {
                while (DialogueSystem.IsOpen || MedusaHorror.Player.InputReader.blocked) yield return null;
                var captain = FindObjectOfType<GuardCaptain>();
                if (captain != null) captain.Interact();
            }
        }
    }
}
