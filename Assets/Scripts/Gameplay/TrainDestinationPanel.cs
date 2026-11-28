using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    public class TrainDestinationPanel : MonoBehaviour
    {
        static TrainDestinationPanel _instance;
        readonly InteractionPanelInput _input = new InteractionPanelInput();
        Vector3 _return;
        int _fare;
        bool _chosen;
        public static bool IsOpen => _instance != null;
        public static void Open(Vector3 stationReturn, int fare)
        {
            if (_instance != null || DialogueSystem.IsOpen) return;
            _instance = new GameObject("TrainDestinationPanel").AddComponent<TrainDestinationPanel>();
            _instance._return = stationReturn; _instance._fare = fare; _instance.Build();
        }
        void Build()
        {
            if(UnityEngine.EventSystems.EventSystem.current==null)
            {
                var events=new GameObject("TrainUIEvents",typeof(UnityEngine.EventSystems.EventSystem));events.transform.SetParent(transform,false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
#else
                events.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
            }
            _input.Open();
            var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); go.transform.SetParent(transform, false);
            go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; go.GetComponent<Canvas>().sortingOrder = 500;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
            var panel = new GameObject("Destinations", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(go.transform, false);
            var rt = panel.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(680, 460);
            panel.GetComponent<Image>().color = new Color(0.025f, 0.06f, 0.075f, 0.98f);
            Label(rt, "列车中转站 · 选择目的地", 32, new Vector2(0, 175), new Vector2(620, 50));
            Label(rt, "每次车票 " + _fare + " 信用点 · 当前余额 " + Wallet.Coins, 22, new Vector2(0, 118), new Vector2(620, 40));
            Button(rt, "1 · 霓虹街区", 45, () => Select("W4_NeonStreet"));
            Button(rt, "2 · 异星之门", -25, () => Select("W5_AlienGate"));
            Button(rt, "3 · 荒原堡垒（免费返回）", -95, () => Select(CampaignRoute.City));
            Button(rt, "取消 / Esc", -175, Close);
        }
        static Text Label(Transform parent, string value, int size, Vector2 position, Vector2 dimensions)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = dimensions; rt.anchoredPosition = position;
            var text = go.GetComponent<Text>(); text.font = CjkFont.Get(); text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = value; text.raycastTarget = false; return text;
        }
        static void Button(Transform parent, string label, float y, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject("Destination", typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(580, 55); rt.anchoredPosition = new Vector2(0, y);
            go.GetComponent<Image>().color = new Color(0.07f, 0.23f, 0.28f); go.GetComponent<Button>().onClick.AddListener(action);
            Label(rt, label, 24, Vector2.zero, new Vector2(560, 50));
        }
        public void Select(string destination)
        {
            if (_chosen || (destination != "W4_NeonStreet" && destination != "W5_AlienGate" && destination != CampaignRoute.City)) return;
            int fare = destination == CampaignRoute.City ? 0 : _fare;
            if (!Wallet.CanAfford(fare)) { LootToast.Show("信用点不足 · 车票需要 " + fare); return; }
            // 先建立车厢，再扣票，避免失败时吞掉信用点。
            var train = TrainRide.Ensure(_return);
            if (!Wallet.Spend(fare)) return;
            _chosen = true;
            train.nextScene = destination; Close(); train.Board();
        }
        public void Close() { _input.Close(); Destroy(gameObject); }
        void Update()
        {
            if (InteractionPanelInput.Pressed(KeyCode.Escape)) Close();
            else if (InteractionPanelInput.Pressed(KeyCode.Alpha1)) Select("W4_NeonStreet");
            else if (InteractionPanelInput.Pressed(KeyCode.Alpha2)) Select("W5_AlienGate");
            else if (InteractionPanelInput.Pressed(KeyCode.Alpha3)) Select(CampaignRoute.City);
        }
        void OnDestroy() { _input.Close(); if (_instance == this) _instance = null; }
    }
}
