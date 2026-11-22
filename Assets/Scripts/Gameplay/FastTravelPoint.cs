using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>公寓内的快速旅行点：列出已点亮的传送门区域，按数字键直接前往。</summary>
    public class FastTravelPoint : MonoBehaviour, IInteractable
    {
        public string houseId = "W4_Apartment_7B";

        bool _open;
        Canvas _canvas;
        Text _body;
        readonly List<string> _targets = new List<string>();

        bool Owned
        {
            get
            {
                var l = SaveSystem.Data.houses;
                for (int i = 0; i < l.Count; i++) if (l[i] == houseId) return true;
                return false;
            }
        }

        public string Hint { get { return Owned ? "快速旅行 · 前往已点亮区域" : "快速旅行 · 先在门口购置公寓"; } }
        public bool CanInteract { get { return true; } }
        public bool IsOpen { get { return _open; } }
        public int TargetCount { get { return _targets.Count; } }

        void Awake() { BuildUi(); }

        public void Interact()
        {
            if (!Owned) { LootToast.Show("先在门口买下这套公寓，才能使用快速旅行。"); return; }
            Open();
        }

        void Open()
        {
            _targets.Clear();
            var list = SaveSystem.Data.activatedPortals;
            string cur = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            for (int i = 0; i < list.Count; i++)
            {
                string scene = list[i];
                int bar = scene.IndexOf('|');
                if (bar > 0) scene = scene.Substring(0, bar);
                if (string.IsNullOrEmpty(scene) || scene == cur) continue;
                if (!_targets.Contains(scene)) _targets.Add(scene);
            }
            _open = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshText();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void TravelTo(int index)
        {
            if (index < 0 || index >= _targets.Count) return;
            string scene = _targets[index];
            Close();
            SceneLoader.Load(scene, SceneLoader.DisplayName(scene));
        }

        void Update()
        {
            if (!_open) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            for (int i = 0; i < _targets.Count && i < 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) { TravelTo(i); return; }
            }
        }

        void RefreshText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("快速旅行");
            sb.AppendLine();
            if (_targets.Count == 0) sb.AppendLine("还没有点亮任何传送门。");
            for (int i = 0; i < _targets.Count; i++)
                sb.AppendLine("[" + (i + 1) + "] " + SceneLoader.DisplayName(_targets[i]));
            sb.AppendLine();
            sb.AppendLine("按数字键前往 · Esc 关闭");
            if (_body != null) _body.text = sb.ToString();
        }

        void BuildUi()
        {
            var go = new GameObject("FastTravelCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 200;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var brt = bg.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;
            var img = bg.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.05f, 0.97f);

            var txtGo = new GameObject("Body", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(900f, 700f);
            _body = txtGo.AddComponent<Text>();
            _body.font = MedusaHorror.UI.CjkFont.Get();
            _body.fontSize = 34;
            _body.alignment = TextAnchor.MiddleLeft;
            _body.color = new Color(0.92f, 0.96f, 1f);
            _body.lineSpacing = 1.35f;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;

            go.SetActive(false);
        }
    }
}
