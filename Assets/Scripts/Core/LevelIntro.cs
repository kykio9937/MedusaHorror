using System.Collections;
using UnityEngine;

namespace MedusaHorror.Core
{
    /// <summary>关卡开场提示：进入场景后延迟弹一条氛围/目标文案。</summary>
    [DisallowMultipleComponent]
    public class LevelIntro : MonoBehaviour
    {
        public string toast = "";
        public float delay = 1.4f;

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(toast)) yield break;
            yield return new WaitForSeconds(delay);
            Gameplay.LootToast.Show(toast);
        }
    }
}
