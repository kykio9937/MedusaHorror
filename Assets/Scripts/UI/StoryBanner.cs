using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MedusaHorror.UI
{
    /// <summary>正上方场景标题、中间偏右的剧情提示；两条通道分别计时。</summary>
    public class StoryBanner : MonoBehaviour
    {
        static StoryBanner _instance;
        Notice _location, _event;
        string _scene;
        readonly Queue<Message> _pending = new Queue<Message>();
        struct Message { public string title, sub; public float hold; }
        class Notice
        {
            public CanvasGroup group;
            public Image backdrop, frame, accent;
            public Text label, title, sub;
            public float remaining, duration;
        }
        static StoryBanner Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("StoryBanner"); DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoryBanner>(); _instance.Build(); return _instance;
        }
        public static void Show(string title, string sub = "", float hold = 3.6f)
        {
            var ui = Ensure();
            var m = new Message { title = title, sub = sub, hold = Mathf.Clamp(hold, 2.5f, 7f) };
            if (ui._event.remaining > 0) { if (ui._pending.Count < 5) ui._pending.Enqueue(m); }
            else Push(ui._event, m.title, m.sub, m.hold);
        }
        public static void EnterScene(string scene, string title, string sub)
        {
            var ui = Ensure(); ui._scene = scene; ui._pending.Clear();
            ui._event.remaining = 0; ui._event.group.alpha = 0;
            ui.ApplyTheme(ui._location); ui.ApplyTheme(ui._event);
            Push(ui._location, title, sub, 4.5f);
        }
        public static void Clear()
        {
            if (_instance == null) return;
            _instance._pending.Clear(); _instance._location.remaining = _instance._event.remaining = 0;
            _instance._location.group.alpha = _instance._event.group.alpha = 0;
        }
        public static void ShowUrgent(string title,string sub,float hold=2.5f)
        {
            var ui=Ensure();ui._pending.Clear();Push(ui._event,title,sub,hold);
        }
        void Build()
        {
            var go = new GameObject("BannerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 95;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = 0.5f;
            _location = BuildNotice(go.transform,"SceneTitle",new Vector2(0.5f,1),new Vector2(0.5f,1),new Vector2(0,-52),new Vector2(700,154),true);
            _event = BuildNotice(go.transform,"RoomNotice",new Vector2(0.69f,0.51f),new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(500,218),false);
        }
        static Notice BuildNotice(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size,bool centered)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(CanvasGroup));
            var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=position;rt.sizeDelta=size;
            var n = new Notice { group=go.GetComponent<CanvasGroup>() };
            n.group.alpha=0;n.group.interactable=n.group.blocksRaycasts=false;
            ImagePart("Shade",rt,new Color(0.015f,0.02f,0.025f,0.94f));
            n.backdrop=ImagePart("SceneMaterial",rt,Color.white);
            n.frame=ImagePart("AssetFrame",rt,Color.white);n.frame.sprite=UiAtlas.Frame;n.frame.type=Image.Type.Sliced;
            n.accent=ImagePart("Accent",rt,Color.white);
            var line=n.accent.rectTransform;line.anchorMin=new Vector2(0,0);line.anchorMax=new Vector2(0,1);line.offsetMin=Vector2.zero;line.offsetMax=new Vector2(3,0);
            n.label=TextPart("Category",rt,16,28,14,size.x-56,22,centered);
            n.title=TextPart("Title",rt,centered?34:27,28,centered?40:43,size.x-56,centered?45:64,centered);n.title.fontStyle=FontStyle.Bold;
            n.sub=TextPart("Description",rt,centered?20:22,28,centered?94:116,size.x-56,centered?46:88,centered);n.sub.lineSpacing=1.18f;
            return n;
        }
        static Image ImagePart(string name,RectTransform parent,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        static Text TextPart(string name,Transform parent,int size,float x,float y,float width,float height,bool centered)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Text));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(width,height);
            var text=go.GetComponent<Text>();text.font=CjkFont.Get();text.fontSize=size;text.alignment=centered?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
            text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=size-3;text.resizeTextMaxSize=size;text.raycastTarget=false;return text;
        }
        void ApplyTheme(Notice n)
        {
            var theme=SceneNoticeTheme.For(_scene);n.backdrop.sprite=theme.Background;n.backdrop.color=new Color(1,1,1,0.22f);
            n.frame.color=theme.accent*new Color(1,1,1,0.55f);n.accent.color=theme.accent;n.label.color=theme.accent;n.label.text=theme.category;
            n.title.color=Color.Lerp(Color.white,theme.accent,0.16f);n.sub.color=new Color(0.83f,0.85f,0.84f);
        }
        static void Push(Notice n,string title,string sub,float hold)
        {
            n.title.text=title??"";n.sub.text=sub??"";n.duration=n.remaining=hold+0.9f;n.group.alpha=0;
        }
        void Update()
        {
            if(MedusaHorror.Player.InputReader.blocked || MedusaHorror.Core.SceneLoader.IsLoading){_location.group.alpha=_event.group.alpha=0;return;}
            Animate(_location);Animate(_event);
            if(_event.remaining<=0 && _pending.Count>0){var m=_pending.Dequeue();Push(_event,m.title,m.sub,m.hold);}
        }
        static void Animate(Notice n)
        {
            if(n.remaining<=0){n.group.alpha=0;return;}n.remaining=Mathf.Max(0,n.remaining-Time.unscaledDeltaTime);
            n.group.alpha=Mathf.Min(Mathf.Clamp01((n.duration-n.remaining)/0.35f),Mathf.Clamp01(n.remaining/0.55f));
        }
        void OnDestroy(){if(_instance==this)_instance=null;}
    }
}
