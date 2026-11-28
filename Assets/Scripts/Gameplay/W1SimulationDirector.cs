using UnityEngine;
using MedusaHorror.Core;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    public class W1SimulationDirector : MonoBehaviour
    {
        public MedusaBoss boss;
        public GameObject clueBarrier, roofBarrier;
        public ExitPortal roofPortal;
        Transform _player;
        int _stage=-1;
        bool _cluesAnnounced,_bossStarted,_passed;
        public bool Passed => _passed;
        Animator _bossAnimator;
        bool _speedParameter,_groundParameter;
        void Start()
        {
            var player=FindObjectOfType<ThirdPersonController>();if(player!=null)_player=player.transform;
            if(boss!=null){boss.minimumArenaY=6.5f;boss.wakeByFloorTrigger=true;boss.ApplyStats(680,3.15f,0.35f,2.1f,1.65f);boss.pulseRadius=8;boss.pulsePetrify=17;boss.pulseInterval=5.8f;}
            if(boss!=null&&boss.GetComponent<HospitalBossChallenge>()==null)boss.gameObject.AddComponent<HospitalBossChallenge>();
            if(boss!=null)_bossAnimator=boss.GetComponentInChildren<Animator>();
            if(_bossAnimator!=null)foreach(var parameter in _bossAnimator.parameters)
            {
                if(parameter.name=="Speed"&&parameter.type==AnimatorControllerParameterType.Float)_speedParameter=true;
                if(parameter.name=="IsGrounded"&&parameter.type==AnimatorControllerParameterType.Bool)_groundParameter=true;
            }
        }
        void Update()
        {
            if(_player==null)return;
            if(SaveSystem.Data.hospitalTrainingPassed && CheckpointSystem.Restoring)return;
            if(_bossAnimator!=null&&boss!=null)
            {
                if(_speedParameter)_bossAnimator.SetFloat("Speed",boss.IsAwake&&!boss.IsDead?2f:0f,0.15f,Time.deltaTime);
                if(_groundParameter)_bossAnimator.SetBool("IsGrounded",true);
            }
            bool clues=W1ObjectiveState.Complete;
            bool routeReady=clues&&HospitalExploration.NorthRouteReady;
            if(clueBarrier!=null)clueBarrier.SetActive(!routeReady);
            if(roofBarrier!=null)roofBarrier.SetActive(!_passed);
            if(InputReader.blocked)return;
            var p=_player.position;
            if(_stage<0)
            {
                _stage=0;
                StoryBanner.Show("序章 · 苏醒","记忆一片模糊，四周只有消毒水的气味。先找到能用的物资，再寻找离开这所医院的路。",9f);
            }
            if(p.z>20&&p.y<5&&_stage<1)
            {
                _stage=1;StoryBanner.Show("走廊深处","房门似乎与几件遗失的物品有关。靠近可疑物品，用准星对准后按 E 拾取。",7f);
            }
            if(clues&&!HospitalExploration.NorthRouteReady&&!_cluesAnnounced)
            {
                _cluesAnnounced=true;ObjectiveHud.Ensure().Show("五件线索已齐 · 调查西侧档案与东侧配电记录",0,0);
                StoryBanner.Show("还缺一段解锁线索",HospitalExploration.MissingRouteHint+"。侧区入口在值班室和后勤病区的外侧。",6);
            }
            if(routeReady&&_stage<2)
            {
                _stage=2;
                _cluesAnnounced=true;
                ObjectiveHud.Ensure().Show("北侧通道已打开 · 沿楼梯前往二楼",0,0);
                StoryBanner.Show("门锁松动了","几件物品拼出了通道的线索。北侧楼梯已经能够通行，但楼上传来沉重的脚步声。",7f);
            }
            if(routeReady&&!_bossStarted&&p.y>6.8f&&p.y<10&&p.z<130&&p.z>87)
            {
                _bossStarted=true;boss.WakeNow();
                ObjectiveHud.Ensure().Show("击败二楼的守望者 · 利用立柱躲避凝视",0,0);
                StoryBanner.Show("无声的守望者","持续正面暴露会积累石化。利用立柱打断视线，在安全间隙靠近攻击；注意二阶段脉冲。",7f);
            }
            if(!_passed&&boss!=null&&boss.IsDead)
            {
                _passed=true;if(roofBarrier!=null)roofBarrier.SetActive(false);
                SaveSystem.Data.hospitalTrainingPassed=true;SaveSystem.Save();
                ObjectiveHud.Ensure().Show("天台通道已打开 · 沿东侧楼梯继续向上",0,0);
                StoryBanner.Show("寂静重新降临","守望者倒下了。走廊深处的门锁发出轻响，东侧楼梯通往天台。那里也许有离开的办法。",9f);
            }
            if(_passed&&p.y>13.8f&&_stage<3)
            {
                _stage=3;StoryBanner.Show("天台 · 微弱的光","风声突然远去。光门另一端看不清景象，但这是你找到的唯一出口。",7f);
            }
        }
    }
}
