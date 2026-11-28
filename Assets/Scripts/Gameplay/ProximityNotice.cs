using UnityEngine;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    /// <summary>首次靠近区域显示屏幕提示，无场景悬浮文字，也无需触发器。</summary>
    public class ProximityNotice : MonoBehaviour
    {
        public string title;
        [TextArea] public string description;
        public float radius=5f;
        public float verticalOffset;
        Transform _player;
        bool _shown;
        void Update()
        {
            if(_shown||InputReader.blocked)return;
            if(_player==null){var player=FindObjectOfType<ThirdPersonController>();if(player!=null)_player=player.transform;return;}
            var d=_player.position-(transform.position-Vector3.up*verticalOffset);
            if(Mathf.Abs(d.y)>3f)return;d.y=0;
            if(d.sqrMagnitude>radius*radius)return;
            _shown=true;StoryBanner.Show(title,description,5f);
        }
    }
}
