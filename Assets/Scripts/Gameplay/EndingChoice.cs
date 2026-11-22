using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>星门控制台：按条件解锁多结局，选一个走进去。</summary>
    public class EndingChoice : MonoBehaviour, IInteractable
    {
        class Ending
        {
            public string id;
            public string title;
            public string text;
            public string hint;
            public System.Func<bool> unlocked;
        }

        readonly List<Ending> _endings = new List<Ending>();
        bool _open;
        bool _showing;
        string _shownId;
        Canvas _canvas;
        Text _body;

        public string Hint { get { return "星门控制台 · 选择你的结局"; } }
        public bool CanInteract { get { return true; } }
        public bool IsOpen { get { return _open || _showing; } }
        public int EndingCount { get { return _endings.Count; } }

        void Awake()
        {
            BuildEndings();
            BuildUi();
        }

        static int ClaimedQuests()
        {
            int n = 0;
            var l = SaveSystem.Data.quests;
            for (int i = 0; i < l.Count; i++) if (l[i].claimed) n++;
            return n;
        }

        void BuildEndings()
        {
            _endings.Add(new Ending
            {
                id = "stone", title = "石之归乡",
                text = "你把美杜莎的眼睛留在了黑暗里。\n回程的船上，你的手还在抖，但它是热的。",
                hint = "通关即达成",
                unlocked = () => true
            });
            _endings.Add(new Ending
            {
                id = "village", title = "村庄之子",
                text = "荒废村庄的灯一盏盏亮起来。\n有人在门口等你，桌上有热的饭。你终于有了回去的地方。",
                hint = "完成村庄支线并买下公寓",
                unlocked = () => SaveSystem.Data.villageDiscount > 0f && SaveSystem.Data.houses.Count > 0
            });
            _endings.Add(new Ending
            {
                id = "ascend", title = "星门共鸣",
                text = "星门认得你身上的每一块义体。\n它没有送你回家，而是把你送去了更远的地方。",
                hint = "信用点 ≥ 3000 且完成 5 个任务",
                unlocked = () => SaveSystem.Data.coins >= 3000 && ClaimedQuests() >= 5
            });
        }

        public void Interact() { Open(); }

        void Open()
        {
            _showing = false;
            _open = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            RefreshList();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            _open = false;
            _showing = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>选择结局；未解锁返回 false。</summary>
        public bool Choose(int index)
        {
            if (index < 0 || index >= _endings.Count) return false;
            var e = _endings[index];
            if (!e.unlocked()) { LootToast.Show("这个结局还没解锁：" + e.hint); return false; }

            _shownId = e.id;
            _showing = true;
            var seen = SaveSystem.Data.endingsSeen;
            if (!seen.Contains(e.id)) { seen.Add(e.id); SaveSystem.Save(); }

            if (_body != null)
                _body.text = e.title + "\n\n" + e.text + "\n\n—— 按 R 回到主菜单 ——";
            return true;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (_showing)
            {
                if (Input.GetKeyDown(KeyCode.R)) { Close(); SceneLoader.Load("MainMenu", "回到主菜单"); }
                return;
            }
            for (int i = 0; i < _endings.Count && i < 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) { Choose(i); return; }
            }
        }

        void RefreshList()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("星门控制台");
            sb.AppendLine();
            for (int i = 0; i < _endings.Count; i++)
            {
                var e = _endings[i];
                bool ok = e.unlocked();
                sb.AppendLine("[" + (i + 1) + "] " + e.title + (ok ? "" : "（未解锁）") +
                              " — " + (ok ? "已解锁" : e.hint));
            }
            sb.AppendLine();
            sb.AppendLine("按数字键选择结局 · Esc 离开");
            if (_body != null) _body.text = sb.ToString();
        }

        void BuildUi()
        {
            var go = new GameObject("EndingCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 95;
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
            img.color = new Color(0.01f, 0.02f, 0.04f, 0.90f);

            var txtGo = new GameObject("Body", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(1100f, 700f);
            _body = txtGo.AddComponent<Text>();
            _body.font = MedusaHorror.UI.CjkFont.Get();
            _body.fontSize = 32;
            _body.alignment = TextAnchor.MiddleCenter;
            _body.color = new Color(0.92f, 0.96f, 1f);
            _body.lineSpacing = 1.4f;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;

            go.SetActive(false);
        }
    }
}
