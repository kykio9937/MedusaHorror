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
            StoryBanner.Show("第一章 · 荒原人生","模拟已结束，眼前是需要你守护的真实村庄。先寻找村民，了解石化怪物的动向；探索与帮助村民将为旅途积累信用点。",10f);
            Destroy(gameObject);
        }
    }
}
