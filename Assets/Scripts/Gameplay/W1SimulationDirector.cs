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
            if(boss!=null){boss.minimumArenaY=6.5f;boss.wakeByFloorTrigger=true;boss.ApplyStats(420,2.9f,0.35f,2.1f,1.65f);boss.pulseRadius=8;boss.pulsePetrify=12;boss.pulseInterval=7;}
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
            if(_bossAnimator!=null&&boss!=null)
            {
                if(_speedParameter)_bossAnimator.SetFloat("Speed",boss.IsAwake&&!boss.IsDead?2f:0f,0.15f,Time.deltaTime);
                if(_groundParameter)_bossAnimator.SetBool("IsGrounded",true);
            }
            bool clues=W1ObjectiveState.Complete;
            if(clueBarrier!=null)clueBarrier.SetActive(!clues);
            if(roofBarrier!=null)roofBarrier.SetActive(!_passed);
            if(InputReader.blocked)return;
            var p=_player.position;
            if(_stage<0)
            {
                _stage=0;
                StoryBanner.Show("序章 · 守村者适应训练","这所医院是荒原应对模拟场。搜集五件训练凭证，熟悉搜索、近战与石化规避；通过后，你将抵达需要守护的村庄。",9f);
            }
            if(p.z>20&&p.y<5&&_stage<1)
            {
                _stage=1;StoryBanner.Show("训练一 · 搜索与辨认","值班室、检查室、药房、物资库、手术室各有一件凭证。靠近后用准星对准物品，按 E 拾取。",7f);
            }
            if(clues&&!_cluesAnnounced)
            {
                _cluesAnnounced=true;
                ObjectiveHud.Ensure().Show("搜索训练完成 · 沿北侧楼梯前往二楼",0,0);
                StoryBanner.Show("训练二 · 对抗荒原威胁","五件凭证校验完成。北侧楼梯已开放；二楼的拟态守卫将检验你保护村民的能力。",7f);
            }
            if(clues&&!_bossStarted&&p.y>6.8f&&p.y<10&&p.z<130&&p.z>87)
            {
                _bossStarted=true;boss.WakeNow();
                ObjectiveHud.Ensure().Show("击败二楼拟态守卫 · 利用立柱躲避凝视",0,0);
                StoryBanner.Show("拟态守卫 · 石化训练","持续正面暴露会积累石化。利用立柱打断视线，在安全间隙靠近攻击；注意二阶段脉冲。",7f);
            }
            if(!_passed&&boss!=null&&boss.IsDead)
            {
                _passed=true;if(roofBarrier!=null)roofBarrier.SetActive(false);
                SaveSystem.Data.hospitalTrainingPassed=true;SaveSystem.Save();
                ObjectiveHud.Ensure().Show("模拟训练通过 · 东侧楼梯通往天台星门",0,0);
                StoryBanner.Show("训练通过 · 人生剧本即将开始","荒原的村庄正遭受石化怪物侵扰。天台星门已就绪：离开模拟场，去认识村民，并守住他们的家。",9f);
            }
            if(_passed&&p.y>13.8f&&_stage<3)
            {
                _stage=3;StoryBanner.Show("天台 · 荒原接入点","走入光门即可离开模拟场。接下来的选择、战斗与村庄，都将成为你真正的人生。",7f);
            }
        }
    }
}
