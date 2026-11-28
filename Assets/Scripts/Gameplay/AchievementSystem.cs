using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Player;
using MedusaHorror.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 成就系统：轮询存档与运行时状态判定条件，解锁时弹提示并落盘；Tab 打开成就列表。
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public struct Def
        {
            public string id;
            public string name;
            public string desc;
            public Def(string id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
        }

        public static readonly Def[] All =
        {
            new Def("first_blood", "初次见血", "击杀第一个怪物"),
            new Def("slayer50",    "清道夫",   "累计击杀 50 个怪物"),
            new Def("level10",     "渐入佳境", "角色达到 10 级"),
            new Def("level20",     "老练猎人", "角色达到 20 级"),
            new Def("boss_slain",  "凝视终结", "击杀美杜莎"),
            new Def("all_skills",  "全面武装", "解锁全部等级技能"),
            new Def("homeowner",   "安身之所", "在霓虹街区购置房屋"),
            new Def("rider",       "赛博骑士", "首次召唤并骑乘摩托"),
            new Def("rich",        "小有积蓄", "信用点持有量达到 5000"),
            new Def("explorer",    "抵达星门", "在外星星门做出抉择"),
        };

        static AchievementSystem _inst;
        public static AchievementSystem Instance { get { return _inst; } }

        public static AchievementSystem Ensure()
        {
            if (_inst != null) return _inst;
            var go = new GameObject("AchievementSystem");
            _inst = go.AddComponent<AchievementSystem>();
            return _inst;
        }

        float _pollTimer;
        bool _panelOpen;
        Canvas _panelCanvas;
        Text _panelBody;
        int _lastUnlockFrame = -1;
        public string LastUnlocked { get; private set; }
        public int UnlockCount { get; private set; }

        void Awake()
        {
            if (_inst != null && _inst != this) { Destroy(gameObject); return; }
            _inst = this;
            BuildPanel();
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab)) TogglePanel();

            _pollTimer -= Time.deltaTime;
            if (_pollTimer > 0f) return;
            _pollTimer = 1f;
            Evaluate();
        }

        public static bool Has(string id)
        {
            var list = SaveSystem.Data.achievements;
            return list != null && list.Contains(id);
        }

        public int UnlockedCount
        {
            get
            {
                var list = SaveSystem.Data.achievements;
                if (list == null) return 0;
                int n = 0;
                for (int i = 0; i < All.Length; i++) if (list.Contains(All[i].id)) n++;
                return n;
            }
        }

        void Evaluate()
        {
            var d = SaveSystem.Data;

            if (d.killCount >= 1) Unlock("first_blood");
            if (d.killCount >= 50) Unlock("slayer50");
            if (d.level >= 10) Unlock("level10");
            if (d.level >= 20) Unlock("level20");
            if (d.coins >= 5000) Unlock("rich");
            if (d.houses != null && d.houses.Count > 0) Unlock("homeowner");
            if (d.endingsSeen != null && d.endingsSeen.Count > 0) Unlock("explorer");

            if (d.unlockedSkills != null && d.unlockedSkills.Count >= 9) Unlock("all_skills");

            var boss = FindObjectOfType<MedusaBoss>();
            if (boss != null && boss.IsDead) Unlock("boss_slain");

            var mount = FindObjectOfType<MountSystem>();
            if (mount != null && mount.IsRiding) Unlock("rider");
        }

        /// <summary>解锁一个成就：已解锁则忽略；否则记录、提示、落盘。</summary>
        public bool Unlock(string id)
        {
            if (Has(id)) return false;
            var d = SaveSystem.Data;
            if (d.achievements == null) d.achievements = new List<string>();
            d.achievements.Add(id);
            SaveSystem.Save();

            LastUnlocked = id;
            UnlockCount++;
            _lastUnlockFrame = Time.frameCount;

            string name = DisplayName(id);
            LootToast.Show("成就解锁 · " + name);
            var audio = AudioDirector.Instance;
            if (audio != null) audio.Play(SfxId.Pickup, 0.8f, 1.35f);
            Debug.Log("[Achievement] 解锁：" + id + " " + name);
            if (_panelOpen) RefreshPanel();
            return true;
        }

        public static string DisplayName(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return All[i].name;
            return id;
        }

        // ---------------- 成就面板（Tab） ----------------

        void TogglePanel()
        {
            _panelOpen = !_panelOpen;
            if (_panelCanvas != null) _panelCanvas.gameObject.SetActive(_panelOpen);
            if (_panelOpen)
            {
                RefreshPanel();
                InputReader.blocked = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                InputReader.blocked = false;
            }
        }

        void RefreshPanel()
        {
            if (_panelBody == null) return;
            var d = SaveSystem.Data;
            var list = d.achievements ?? new List<string>();
            var sb = new System.Text.StringBuilder();
            sb.Append("成就　").Append(UnlockedCount).Append(" / ").Append(All.Length).Append("\n\n");
            for (int i = 0; i < All.Length; i++)
            {
                bool got = list.Contains(All[i].id);
                if (!got && !SaveSystem.Data.captainBriefingDone) { sb.AppendLine("？ 未知成就"); continue; }
                sb.Append(got ? "◆ " : "◇ ")
                  .Append(All[i].name)
                  .Append("　—　")
                  .Append(got ? All[i].desc : "未解锁")
                  .Append("\n");
            }
            _panelBody.text = sb.ToString();
        }

        void BuildPanel()
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(transform, false);
            _panelCanvas = canvasGo.AddComponent<Canvas>();
            _panelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _panelCanvas.sortingOrder = 110;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760f, 620f);
            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.94f);
            bg.raycastTarget = false;

            var bar = new GameObject("Bar", typeof(RectTransform));
            bar.transform.SetParent(panel.transform, false);
            var brt = (RectTransform)bar.transform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.sizeDelta = new Vector2(0f, 4f);
            var bimg = bar.AddComponent<Image>();
            bimg.color = new Color(0.32f, 0.92f, 1f);
            bimg.raycastTarget = false;

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(panel.transform, false);
            var wrt = (RectTransform)bodyGo.transform;
            wrt.anchorMin = Vector2.zero;
            wrt.anchorMax = Vector2.one;
            wrt.offsetMin = new Vector2(36f, 36f);
            wrt.offsetMax = new Vector2(-36f, -30f);
            _panelBody = bodyGo.AddComponent<Text>();
            _panelBody.font = CjkFont.Get();
            _panelBody.fontSize = 22;
            _panelBody.alignment = TextAnchor.UpperLeft;
            _panelBody.color = new Color(0.93f, 0.94f, 0.97f);
            _panelBody.raycastTarget = false;
            _panelBody.lineSpacing = 1.45f;

            _panelCanvas.gameObject.SetActive(false);
        }

        public void SetShown(bool show)
        {
            if (_panelCanvas != null) _panelCanvas.gameObject.SetActive(show && _panelOpen);
        }
    }
}
