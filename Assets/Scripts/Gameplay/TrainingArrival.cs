using System.Collections;
using UnityEngine;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    public class TrainingArrival : MonoBehaviour
    {
        public static bool LastArrivalShown;
        IEnumerator Start()
        {
            LastArrivalShown=false;
            yield return new WaitForSeconds(3);
            LastArrivalShown=true;
            StoryBanner.Show("野外 · 旧村庄","这里在堡垒城墙之外。留意道路与撤退方向，探索后可以返回主城休整。",5f);
            Destroy(gameObject);
        }
    }
}
