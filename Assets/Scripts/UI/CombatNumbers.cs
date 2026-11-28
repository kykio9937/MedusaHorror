using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MedusaHorror.UI
{
    public class CombatNumbers:MonoBehaviour
    {
        static CombatNumbers _instance;
        class Number{public Text text;public Vector3 world;public float life;public bool incoming;}
        readonly List<Number> _numbers=new List<Number>();RectTransform _canvas;
        public static void Show(Vector3 position,float amount,bool incoming=false)
        {
            if(amount<=0)return;
            if(_instance==null){var go=new GameObject("CombatNumbers");_instance=go.AddComponent<CombatNumbers>();}
            _instance.Add(position,amount,incoming);
        }
        void Awake()
        {
            var go=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=125;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);_canvas=go.GetComponent<RectTransform>();
        }
        void Add(Vector3 position,float amount,bool incoming)
        {
            if(_numbers.Count>=20){Destroy(_numbers[0].text.gameObject);_numbers.RemoveAt(0);}
            var go=new GameObject("Damage",typeof(RectTransform),typeof(Text));go.transform.SetParent(_canvas,false);
            var text=go.GetComponent<Text>();text.font=CjkFont.Get();text.fontSize=incoming?28:30;text.fontStyle=FontStyle.Bold;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
            text.text=incoming?"石化 +"+amount.ToString("0.#"):"-"+amount.ToString("0.#");text.color=incoming?new Color(1,0.35f,0.3f):new Color(1,0.85f,0.5f);text.rectTransform.sizeDelta=new Vector2(240,50);
            go.AddComponent<Shadow>().effectColor=new Color(0,0,0,0.9f);
            _numbers.Add(new Number{text=text,world=position,life=1,incoming=incoming});
        }
        void Update()
        {
            for(int i=_numbers.Count-1;i>=0;i--)
            {
                var n=_numbers[i];n.life-=Time.deltaTime;if(n.life<=0){Destroy(n.text.gameObject);_numbers.RemoveAt(i);continue;}
                var camera=Camera.main;if(camera==null)continue;
                Vector3 screen=n.incoming?new Vector3(Screen.width*0.5f,Screen.height*0.39f,1):camera.WorldToScreenPoint(n.world);
                n.text.enabled=screen.z>0&&!MedusaHorror.Player.InputReader.blocked;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas,screen,null,out var point);n.text.rectTransform.anchoredPosition=point+new Vector2((i%3-1)*18,(1-n.life)*65);
                var c=n.text.color;c.a=Mathf.Clamp01(n.life*2);n.text.color=c;
            }
        }
        void OnDestroy(){if(_instance==this)_instance=null;}
    }
}
