using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using MedusaHorror.Player;
using MedusaHorror.Gameplay;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace MedusaHorror.UI
{
    /// <summary>全关卡左上角小地图：北向、楼层切片、位置标签、目标与缩放。</summary>
    public class LevelMinimap : MonoBehaviour
    {
        static LevelMinimap _inst;
        public static LevelMinimap Instance => _inst;
        public static LevelMinimap Ensure(string sceneName)
        {
            bool playable=sceneName=="W2_Stronghold"||sceneName=="W0_Base"||sceneName=="W1_Hospital"||sceneName=="W2_Wasteland"||sceneName=="W3_Subway"||sceneName=="W4_NeonStreet"||sceneName=="W5_AlienGate";
            if(!playable){if(_inst!=null){_inst._available=false;_inst.SetShown(false);}return null;}
            if(_inst==null){var go=new GameObject("LevelMinimapRuntime");DontDestroyOnLoad(go);go.AddComponent<LevelMinimap>();}
            _inst._available=true;_inst.SetScene(sceneName);_inst.SetShown(true);return _inst;
        }
        const float ViewSize=256, Width=300;
        const float MapRefreshInterval=0.125f;
        string _scene;
        Camera _cam;
        RenderTexture _rt;
        RectTransform _panel,_viewRt,_iconRoot,_arrow,_info;
        CanvasGroup _group;
        RawImage _view;
        Text _title,_area,_coordinates,_legend,_scale;
        Transform _player;
        Vector3 _playerPos,_mapCenter;
        MapLocationResolver _location;
        float _renderTimer,_scanTimer,_positionTimer;
        float _ortho=42,_baseOrtho=42,_zoom=1;
        int _floor=-1;
        bool _visible=true,_available=true,_collapsed;
        enum Kind { Enemy, Exit, Train, Shop, Item }
        class MapIcon { public Transform target; public Image image; public Kind kind; public Monster monster; public MedusaBoss boss; }
        readonly List<MapIcon> _icons=new List<MapIcon>();
        static Sprite _dot,_tri;
        bool _fogSaved,_savedFog;
        UnityEngine.Rendering.AmbientMode _savedAmbMode;
        Color _savedAmb;
        public string SceneName => _scene;
        public string CurrentArea => _location==null||_player==null?"定位中":_location.AreaName(_player.position);
        public string PositionText => _coordinates!=null?_coordinates.text:"";
        public float VisibleRadius => _ortho;
        public bool Collapsed => _collapsed;
        public Camera MapCamera => _cam;
        public RectTransform Panel => _panel;
        public int IconCount => _icons.Count;
        void Awake()
        {
            if(_inst!=null&&_inst!=this){Destroy(gameObject);return;}_inst=this;BuildUi();BuildCamera();
        }
        void OnDestroy()
        {
            if(_inst==this)_inst=null;
            if(_cam!=null)_cam.targetTexture=null;
            if(_rt!=null){_rt.Release();Destroy(_rt);}
        }
        public void SetShown(bool show){_visible=show;if(_panel!=null)_panel.gameObject.SetActive(show);_renderTimer=0;}
        public void SetCollapsed(bool value)
        {
            _collapsed=value;_viewRt.gameObject.SetActive(!value);_legend.gameObject.SetActive(!value);_scale.gameObject.SetActive(!value);
            _info.anchoredPosition=new Vector2(0,value?-34:-316);
            _panel.sizeDelta=new Vector2(Width,value?144:448);_renderTimer=0;
        }
        public void Zoom(float factor)
        {
            _zoom=Mathf.Clamp(_zoom*factor,0.5f,2);_ortho=_baseOrtho*_zoom;_cam.orthographicSize=_ortho;_renderTimer=0;
            _scale.text="范围 "+Mathf.RoundToInt(_ortho*2)+"m · 滚轮缩放";
        }
        public void HandleScroll(float steps)
        {
            if(!_available||!_visible||_collapsed||InputReader.blocked||MedusaHorror.Core.SceneLoader.IsLoading||InteractionPanelInput.ClosingThisFrame)return;
            if(Mathf.Abs(steps)>0.001f)Zoom(Mathf.Pow(0.85f,Mathf.Clamp(steps,-4,4)));
        }
        void SetScene(string scene)
        {
            _scene=scene;_player=null;_floor=-1;_zoom=1;_scanTimer=_positionTimer=_renderTimer=0;
            ClearIcons();_location=new MapLocationResolver(scene);
            _baseOrtho=scene=="W1_Hospital"?19:scene=="W3_Subway"?19:scene=="W4_NeonStreet"?48:scene=="W5_AlienGate"?50:scene=="W0_Base"?25:42;
            _ortho=_baseOrtho;_cam.orthographicSize=_ortho;
            var theme=SceneNoticeTheme.For(scene);
            _area.color=theme.accent;_title.text="北 ↑     区域地图     M 折叠";
            _info.GetComponent<Image>().sprite=theme.Background;
            _info.GetComponent<Image>().color=new Color(0.12f,0.15f,0.16f,0.98f);
            _area.text=_location.SceneName+" / 定位中";_coordinates.text="等待玩家位置";
            Zoom(1);SetCollapsed(_collapsed);
        }
        static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));var rt=go.GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=anchor;rt.pivot=pivot;rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
        }
        static Image Image(RectTransform rt,Color color)
        {
            var image=rt.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        static Text Text(string name,Transform parent,int size,Vector2 position,Vector2 dimensions)
        {
            var rt=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),position,dimensions);
            var text=rt.gameObject.AddComponent<Text>();text.font=CjkFont.Get();text.fontSize=size;text.color=new Color(0.86f,0.9f,0.91f);
            text.alignment=TextAnchor.MiddleLeft;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.raycastTarget=false;return text;
        }
        void BuildUi()
        {
            var go=new GameObject("MinimapCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));go.transform.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=118;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=0.5f;
            _panel=Rect("MinimapPanel",go.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-24),new Vector2(Width,448));
            Image(_panel,new Color(0.025f,0.04f,0.05f,0.94f));_group=_panel.gameObject.AddComponent<CanvasGroup>();_group.interactable=_group.blocksRaycasts=false;
            _title=Text("MapTitle",_panel,17,new Vector2(16,-4),new Vector2(256,26));
            _viewRt=Rect("View",_panel,new Vector2(0.5f,1),new Vector2(0.5f,1),new Vector2(0,-34),new Vector2(ViewSize,ViewSize));
            _view=_viewRt.gameObject.AddComponent<RawImage>();_view.raycastTarget=false;
            _viewRt.gameObject.AddComponent<RectMask2D>();
            _iconRoot=Rect("Icons",_viewRt,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(ViewSize,ViewSize));
            _arrow=Rect("PlayerArrow",_viewRt,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(16,20));
            Image(_arrow,new Color(1,0.94f,0.45f)).sprite=TriSprite();
            _info=Rect("LocationInfo",_panel,new Vector2(0.5f,1),new Vector2(0.5f,1),new Vector2(0,-316),new Vector2(Width-16,96));
            Image(_info,new Color(0.04f,0.07f,0.08f,0.95f));
            _area=Text("SceneAndRoom",_info,18,new Vector2(10,-6),new Vector2(130,84));
            _area.alignment=TextAnchor.UpperLeft;
            _coordinates=Text("LivePosition",_info,15,new Vector2(152,-6),new Vector2(122,84));
            _coordinates.alignment=TextAnchor.UpperLeft;
            Image(Rect("Divider",_info,new Vector2(0,1),new Vector2(0,1),new Vector2(142,-8),new Vector2(1,78)),new Color(0.4f,0.6f,0.65f,0.4f));
            _scale=Text("Scale",_panel,15,new Vector2(16,-292),new Vector2(256,20));
            _legend=Text("Legend",_panel,14,new Vector2(12,-416),new Vector2(276,28));_legend.fontSize=13;_legend.text="绿 出口/列车 · 红 敌人\n蓝 商店 · 金 任务物品";
        }
        void BuildCamera()
        {
            var go=new GameObject("MinimapCam");go.transform.SetParent(transform,false);_cam=go.AddComponent<Camera>();
            _cam.enabled=false;_cam.orthographic=true;_cam.orthographicSize=_ortho;_cam.nearClipPlane=0.1f;_cam.farClipPlane=160;
            _cam.clearFlags=CameraClearFlags.SolidColor;_cam.backgroundColor=new Color(0.04f,0.06f,0.07f);
            _cam.allowHDR=_cam.allowMSAA=false;_cam.transform.rotation=Quaternion.Euler(90,0,0);
            _rt=new RenderTexture(320,320,16,RenderTextureFormat.ARGB32){name="LevelMinimapRT"};_rt.Create();_cam.targetTexture=_rt;_view.texture=_rt;
        }
        void PreRender()
        {
            _savedFog=RenderSettings.fog;_savedAmbMode=RenderSettings.ambientMode;_savedAmb=RenderSettings.ambientLight;_fogSaved=true;
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(0.78f,0.81f,0.86f);
        }
        void PostRender()
        {
            if(!_fogSaved)return;RenderSettings.fog=_savedFog;RenderSettings.ambientMode=_savedAmbMode;RenderSettings.ambientLight=_savedAmb;_fogSaved=false;
        }
        void LateUpdate()
        {
            if(!_available||!_visible)return;
            bool blocked=InputReader.blocked||MedusaHorror.Core.SceneLoader.IsLoading;
            _group.alpha=blocked?0:1;if(blocked)return;
            if(InteractionPanelInput.Pressed(KeyCode.M))SetCollapsed(!_collapsed);
            if(!_collapsed)
            {
                float wheel=0;
#if ENABLE_INPUT_SYSTEM
                if(Mouse.current!=null)wheel=Mouse.current.scroll.ReadValue().y/120f;
#else
                wheel=Input.mouseScrollDelta.y;
#endif
                HandleScroll(wheel);
            }
            if(_player==null){var p=FindObjectOfType<ThirdPersonController>();if(p!=null)_player=p.transform;else return;}
            _playerPos=_player.position;
            _positionTimer-=Time.unscaledDeltaTime;
            if(_positionTimer<=0)
            {
                _positionTimer=0.1f;
                _area.text=_location.SceneName+"\n"+_location.AreaName(_playerPos);
                _coordinates.text=_location.FloorName(_playerPos)+" · 朝"+MapLocationResolver.Heading(_player.eulerAngles.y)+"\nX "+_playerPos.x.ToString("F1")+"\nZ "+_playerPos.z.ToString("F1")+"\n高度 "+_playerPos.y.ToString("F1")+"m";
            }
            if(_collapsed)return;
            _arrow.localRotation=Quaternion.Euler(0,0,-_player.eulerAngles.y);
            _renderTimer-=Time.unscaledDeltaTime;
            int floor=_scene=="W1_Hospital"?(_playerPos.y>=13.5f?2:_playerPos.y>=6.5f?1:0):0;
            if(floor!=_floor){_floor=floor;_renderTimer=0;}
            if(_renderTimer<=0)
            {
                _renderTimer=MapRefreshInterval;_mapCenter=_playerPos;
                float y=_scene=="W0_Base"?_playerPos.y+6.5f:_scene=="W4_NeonStreet"&&CurrentArea=="霓虹公寓 7B"?3f:_scene=="W1_Hospital"?(_floor==0?4.4f:_floor==1?12.6f:25):_scene=="W3_Subway"?Mathf.Max(3.75f,_playerPos.y+2.5f):_playerPos.y+45;
                _cam.transform.position=new Vector3(_mapCenter.x,y,_mapCenter.z);
                try{PreRender();_cam.Render();}finally{PostRender();}
            }
            float ppm=ViewSize/(_ortho*2);
            _arrow.anchoredPosition=new Vector2((_playerPos.x-_mapCenter.x)*ppm,(_playerPos.z-_mapCenter.z)*ppm);
            _scanTimer-=Time.unscaledDeltaTime;if(_scanTimer<=0){_scanTimer=1.5f;ScanTargets();}
            UpdateIconPositions();
        }
        void ClearIcons(){foreach(var icon in _icons)if(icon.image!=null)Destroy(icon.image.gameObject);_icons.Clear();}
        void ScanTargets()
        {
            var targets=new Dictionary<Transform,Kind>();
            foreach(var p in FindObjectsOfType<ExitPortal>())targets[p.transform]=Kind.Exit;
            foreach(var p in FindObjectsOfType<BasePortal>())targets[p.transform]=Kind.Exit;
            foreach(var p in FindObjectsOfType<WorldTravelPoint>())targets[p.transform]=Kind.Exit;
            foreach(var p in FindObjectsOfType<CombatViewTraining>())targets[p.transform]=Kind.Shop;
            foreach(var p in FindObjectsOfType<TrainBoardPoint>())targets[p.transform]=Kind.Train;
            foreach(var p in FindObjectsOfType<ShopPoint>())targets[p.transform]=Kind.Shop;
            foreach(var p in FindObjectsOfType<W1ObjectivePickup>())if(p.CanInteract)targets[p.transform]=Kind.Item;
            foreach(var p in FindObjectsOfType<HospitalExploration>())targets[p.transform]=Kind.Item;
            foreach(var p in FindObjectsOfType<Monster>())if(!p.IsDead)targets[p.transform]=Kind.Enemy;
            foreach(var p in FindObjectsOfType<MedusaBoss>())if(!p.IsDead)targets[p.transform]=Kind.Enemy;
            // 保留现有图标，只增删目标变化项。
            for(int i=_icons.Count-1;i>=0;i--)
            {
                var icon=_icons[i];
                if(icon.target==null||!targets.ContainsKey(icon.target)){if(icon.image!=null)Destroy(icon.image.gameObject);_icons.RemoveAt(i);}
                else targets.Remove(icon.target);
            }
            foreach(var target in targets)AddIcon(target.Key,target.Value);
            _arrow.SetAsLastSibling();
        }
        void AddIcon(Transform target,Kind kind)
        {
            var rt=Rect(kind+"Icon",_iconRoot,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(kind==Kind.Enemy?9:13,kind==Kind.Enemy?9:13));
            Color color=kind==Kind.Enemy?new Color(1,0.25f,0.22f):kind==Kind.Shop?new Color(0.3f,0.7f,1):kind==Kind.Item?new Color(1,0.8f,0.25f):new Color(0.3f,1,0.55f);
            var image=Image(rt,color);image.sprite=kind==Kind.Exit||kind==Kind.Train?TriSprite():DotSprite();
            _icons.Add(new MapIcon{target=target,image=image,kind=kind,monster=target.GetComponent<Monster>(),boss=target.GetComponent<MedusaBoss>()});
        }
        void UpdateIconPositions()
        {
            float ppm=ViewSize/(_ortho*2),half=ViewSize/2-12;
            foreach(var icon in _icons)
            {
                bool active=icon.target!=null&&icon.target.gameObject.activeInHierarchy&&(icon.monster==null||!icon.monster.IsDead)&&(icon.boss==null||!icon.boss.IsDead);
                icon.image.enabled=active;if(!active)continue;
                var pos=icon.target.position;var p=new Vector2((pos.x-_mapCenter.x)*ppm,(pos.z-_mapCenter.z)*ppm);
                bool outside=Mathf.Abs(p.x)>half||Mathf.Abs(p.y)>half;
                // 仅导航目标贴边，远处敌人与物品不挤在边框上。
                bool navigation=icon.kind==Kind.Exit||icon.kind==Kind.Train;
                if(outside&&!navigation){icon.image.enabled=false;continue;}
                if(outside)p*=half/Mathf.Max(Mathf.Abs(p.x),Mathf.Abs(p.y));
                icon.image.rectTransform.anchoredPosition=p;
                icon.image.rectTransform.localRotation=navigation&&outside?Quaternion.Euler(0,0,-Mathf.Atan2(p.x,p.y)*Mathf.Rad2Deg):Quaternion.identity;
                var color=icon.image.color;color.a=Mathf.Abs(pos.y-_playerPos.y)>3?0.3f:outside?0.6f:1;icon.image.color=color;
            }
        }
        public string IconReport()
        {
            var sb=new System.Text.StringBuilder("scene="+_scene+" area="+CurrentArea+" radius="+_ortho+" icons="+_icons.Count+"\n");
            foreach(var icon in _icons)if(icon.target!=null)sb.AppendLine(icon.kind+" "+icon.target.name+" "+icon.target.position+" visible="+icon.image.enabled);
            return sb.ToString();
        }
        // ------------------------------------------------------------------ 贴图
        static Sprite DotSprite()
        {
            if (_dot != null) return _dot;
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(8f, 8f)) / 8f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, d <= 1f ? 1f : 0f));
                }
            tex.Apply();
            _dot = Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f));
            return _dot;
        }

        static Sprite TriSprite()
        {
            if (_tri != null) return _tri;
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float fx = (x + 0.5f) / 32f * 2f - 1f;
                    float fy = (y + 0.5f) / 32f;
                    bool inside = Mathf.Abs(fx) <= (1f - fy) * 0.95f && fy > 0.08f;
                    tex.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            tex.Apply();
            _tri = Sprite.Create(tex, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f));
            return _tri;
        }
    }
}
