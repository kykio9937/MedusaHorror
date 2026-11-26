using UnityEngine;

namespace MedusaHorror.Gameplay
{
    /// <summary>吸收 Space 素材包动画里的 footStep 事件，避免 "has no receiver" 报错。</summary>
    public class FootStepReceiver : MonoBehaviour
    {
        void footStep() { }
    }
}
