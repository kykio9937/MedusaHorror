using UnityEngine;
using MedusaHorror.UI;
using MedusaHorror.Player;
namespace MedusaHorror.Gameplay
{
    public class W1RouteGuide : MonoBehaviour
    {
        Transform _player;
        int _stage=-1;
        bool _exitShown;
        void Update()
        {
            if (_player == null) { var p=FindObjectOfType<ThirdPersonController>(); if(p!=null)_player=p.transform; }
            if(_player==null || InputReader.blocked)return;
            int stage=_player.position.z<18?0:_player.position.z<56?1:_player.position.z<86?2:3;
            if(stage>_stage)
            {
                _stage=stage;
                string[] titles={"接诊大厅","住院区 · 搜寻线索","物资区 · 留意走廊","手术区 · 最后一件线索"};
                string[] hints={"前往住院部；先在左侧值班室寻找钥匙。Tab 可打开档案与背包。","值班室、检查室、药房各有一件线索；靠近后按 E 拾取。","右侧物资库保存着保险丝。清理过的敌人不会反复补刷。","在手术室寻找密码本；集齐五件后继续向北侧星门前进。"};
                StoryBanner.Show(titles[stage],hints[stage],5f);
            }
            if(!_exitShown && W1ObjectiveState.Complete && LevelObjective.Active!=null && LevelObjective.Active.IsDone)
            {
                _exitShown=true;
                ObjectiveHud.Ensure().Show("五件线索已齐 · 前往北侧星门离开医院",0,0);
                StoryBanner.Show("星门已启动","沿主走廊向北，通过手术区后的星门前往下一站。",5f);
            }
        }
    }
}
