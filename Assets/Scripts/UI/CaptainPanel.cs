using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Gameplay;
using MedusaHorror.Core;
namespace MedusaHorror.UI
{
    public class CaptainPanel:MonoBehaviour
    {
        static CaptainPanel _instance;
        readonly InteractionPanelInput _input=new InteractionPanelInput();
        GuardCaptain _captain;
        public static void Open(GuardCaptain captain)
        {
            if(_instance!=null)return;_instance=new GameObject("CaptainPanel").AddComponent<CaptainPanel>();_instance._captain=captain;_instance.Build();
        }
        void Build()
        {
            _input.Open();
            var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=510;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var panel=new GameObject("CaptainChoices",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);var rt=panel.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(700,650);rt.anchorMin=rt.anchorMax=new Vector2(0.5f,0.5f);
            Label(rt,"保安队队长 · 有什么想问的？",250,30);
            Choice(rt,"巡防任务 / 交付报告",150,()=>{var captain=_captain;Close();captain.DiscussPatrol();});
            Choice(rt,"了解这个世界",65,()=>Talk(new[]{"石化灾害之后，许多道路与旧城市都被怪物占据。堡垒靠城墙、巡防队和彼此支援的居民维持生活。","城外的旧村庄保留着物资和幸存者的线索。凝视会积累石化；利用掩体断开视线，带上净化物资，永远为撤退留条路。","信用点用于补给、装备和列车。任务与探索帮助你成长，但不要只追着等级跑，活着回城才是巡防的第一条规矩。"}));
            Choice(rt,"第三人称如何解锁？",-20,()=>Talk(new[]{"从我东侧约十米处的训练场入口按 E 进入W0。场内正前方有教官训练台。","达到10级后，在训练台按 E 并完成讲解，才会解锁 T 切换第三人称。剑可用两种视角，枪只能第一人称。医院试炼始终锁定第一人称。"},()=>{
                var point=FindObjectsOfType<WorldTravelPoint>();foreach(var p in point)if(p.destination=="W0_Base")TrainingNavigation.Mark(p.transform);
            }));
            Choice(rt,"交通与试炼入口",-105,()=>Talk(new[]{"北侧试炼门返回医院，西门通往野外旧村庄。东侧交通门需要20级，门后是列车中转站。","列车上车前选择目的地；去霓虹或异星票价200信用点，返回堡垒免费。"}));
            Choice(rt,"保存安全检查点",-190,()=>{Close();if(CheckpointSystem.Capture("保安队驻地"))LootToast.Show("驻地检查点已保存");});
            Choice(rt,"结束交谈",-270,Close);MenuTheme.Apply(rt);
        }
        void Talk(string[] lines,System.Action completed=null){Close();DialogueSystem.Ensure().Play("保安队队长",lines,completed);}
        static void Label(Transform parent,string value,float y,int size)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(650,60);rt.anchoredPosition=new Vector2(0,y);var t=go.GetComponent<Text>();t.font=CjkFont.Get();t.fontSize=size;t.color=Color.white;t.text=value;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;
        }
        static void Choice(Transform parent,string value,float y,UnityEngine.Events.UnityAction action)
        {
            var go=new GameObject("Choice",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var rt=go.GetComponent<RectTransform>();rt.sizeDelta=new Vector2(600,62);rt.anchoredPosition=new Vector2(0,y);go.GetComponent<Button>().onClick.AddListener(action);Label(rt,value,0,24);
        }
        void Update(){if(InteractionPanelInput.Pressed(KeyCode.Escape))Close();}
        public void Close(){_input.Close();Destroy(gameObject);}
        void OnDestroy(){_input.Close();if(_instance==this)_instance=null;}
    }
    public class TrainingNavigation:MonoBehaviour
    {
        Transform _target;float _life=30;GameObject _marker;
        public static void Mark(Transform target)
        {
            var old=FindObjectOfType<TrainingNavigation>();if(old!=null)Destroy(old.gameObject);
            var go=new GameObject("TrainingNavigation");go.AddComponent<TrainingNavigation>()._target=target;
            StoryBanner.Show("训练场入口已标记","沿左上角地图的蓝色图标前往保安队东侧；入口前按 E 进入。",6);
        }
        void Update()
        {
            _life-=Time.deltaTime;if(_target==null||_life<=0){Destroy(gameObject);return;}
            if(_marker==null){_marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);_marker.transform.SetParent(transform,false);Destroy(_marker.GetComponent<Collider>());_marker.transform.localScale=Vector3.one*0.4f;_marker.GetComponent<Renderer>().material.color=Color.cyan;}
            _marker.transform.position=_target.position+Vector3.up*(2.5f+Mathf.Sin(Time.time*2)*0.15f);
        }
    }
}
