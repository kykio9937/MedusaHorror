using UnityEngine;
using MedusaHorror.Player;
using MedusaHorror.UI;
namespace MedusaHorror.Gameplay
{
    /// <summary>有明确预警、可躲避的近身石化重击，随阶段提速。</summary>
    public class HospitalBossChallenge:MonoBehaviour
    {
        MedusaBoss _boss;Transform _player;float _next,_windup=-1;Vector3 _target;
        GameObject _warning;
        Material _warningMaterial;
        void Start(){_boss=GetComponent<MedusaBoss>();_next=Time.time+4;}
        void Update()
        {
            if(_boss==null||_boss.IsDead){if(_warning!=null)Destroy(_warning);return;}
            if(!_boss.IsAwake||InputReader.blocked)return;
            if(_player==null){var p=FindObjectOfType<ThirdPersonController>();if(p!=null)_player=p.transform;return;}
            if(Mathf.Abs(_player.position.y-transform.position.y)>3)return;
            if(_windup>=0)
            {
                _windup-=Time.deltaTime;
                if(_windup>0)return;
                if(Vector3.Distance(_player.position,_target)<3.4f)
                {
                    var petrify=_player.GetComponent<PlayerPetrify>();if(petrify!=null)petrify.ApplyImpact(_boss.Health01<0.35f?25:18);
                }
                ClearWarning();_windup=-1;_next=Time.time+(_boss.Health01<0.5f?4:6);return;
            }
            if(Time.time<_next||Vector3.Distance(_player.position,transform.position)>8)return;
            _target=_player.position;_windup=_boss.Health01<0.35f?1.05f:1.5f;
            _warning=GameObject.CreatePrimitive(PrimitiveType.Cylinder);_warning.name="BossAttackWarning";
            _warning.transform.position=_target+Vector3.up*0.05f;_warning.transform.localScale=new Vector3(6.8f,0.025f,6.8f);Destroy(_warning.GetComponent<Collider>());
            _warningMaterial=new Material(Shader.Find("Sprites/Default"));_warningMaterial.color=new Color(1,0.2f,0.1f,0.25f);_warning.GetComponent<Renderer>().sharedMaterial=_warningMaterial;
            StoryBanner.ShowUrgent("危险预兆", "脚下的石化波正在聚集，立刻离开红色区域！",2.5f);
        }
        void ClearWarning(){if(_warning!=null)Destroy(_warning);if(_warningMaterial!=null)Destroy(_warningMaterial);}
        void OnDestroy(){ClearWarning();}
    }
}
