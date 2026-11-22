using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 玩家面板（Tab）：属性 / 装备 / 背包 / 帮助 四页。
    /// 数据全部读存档与运行时组件，不额外维护副本。
    /// </summary>
    public class PlayerPanel : MonoBehaviour
    {
        static PlayerPanel _instance;

        int _page;
        bool _open;
        bool _shown = true;
        Canvas _canvas;
        Text _tabText;
        Text _body;
        Text _hint;

        static readonly string[] PageNames = { "属性", "装备", "背包", "帮助" };

        public bool IsOpen { get { return _open; } }
        public int Page { get { return _page; } }

        public static PlayerPanel Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("PlayerPanel");
            Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<PlayerPanel>();
            _instance.Build();
            return _instance;
        }

        public void SetShown(bool on)
        {
            _shown = on;
            if (!on) Close();
            if (_canvas != null && !_open) _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_shown) return;

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                if (_open) Close(); else Open();
                return;
            }
            if (!_open) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            for (int i = 0; i < PageNames.Length; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
                {
                    _page = i;
                    Refresh();
                    return;
                }
            }
        }

        public void Open(int page = -1)
        {
            if (page >= 0) _page = Mathf.Clamp(page, 0, PageNames.Length - 1);
            _open = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
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

        // ---------------------------------------------------------------- 内容

        void Refresh()
        {
            if (_tabText != null)
            {
                var tb = new StringBuilder();
                for (int i = 0; i < PageNames.Length; i++)
                {
                    if (i > 0) tb.Append("   ");
                    tb.Append(i == _page ? "【" + (i + 1) + " " + PageNames[i] + "】" : " " + (i + 1) + " " + PageNames[i] + " ");
                }
                _tabText.text = tb.ToString();
            }
            if (_body != null) _body.text = BuildPage(_page);
            if (_hint != null) _hint.text = "1-4 切换页签 · Tab / Esc 关闭";
        }

        string BuildPage(int page)
        {
            switch (page)
            {
                case 1: return BuildEquipment();
                case 2: return BuildInventory();
                case 3: return BuildHelp();
                default: return BuildStats();
            }
        }

        static int ClaimedQuests()
        {
            int n = 0;
            var l = SaveSystem.Data.quests;
            for (int i = 0; i < l.Count; i++) if (l[i].claimed) n++;
            return n;
        }

        static int AcceptedQuests()
        {
            int n = 0;
            var l = SaveSystem.Data.quests;
            for (int i = 0; i < l.Count; i++) if (l[i].accepted) n++;
            return n;
        }

        string BuildStats()
        {
            var d = SaveSystem.Data;
            var sb = new StringBuilder();
            sb.AppendLine("属性");
            sb.AppendLine();

            int spd = Mathf.RoundToInt(Mathf.Clamp(d.speedBonus, 0f, 0.5f) * 100f);
            int dmg = Mathf.RoundToInt(Mathf.Clamp(d.damageBonus, 0f, 0.8f) * 100f);
            int res = Mathf.RoundToInt(Mathf.Clamp(d.petrifyResist, 0f, 0.6f) * 100f);

            sb.AppendLine("移动速度      +" + spd + "%   （上限 +50%）");
            sb.AppendLine("近战/射击伤害  +" + dmg + "%   （上限 +80%）");
            sb.AppendLine("石化抗性      " + res + "%   （上限 60%）");
            sb.AppendLine("空中二段跳    " + (d.doubleJump ? "已解锁" : "未解锁"));
            sb.AppendLine();
            sb.AppendLine("信用点        " + Wallet.Coins);
            sb.AppendLine("已通关关卡    " + SaveSystem.ClearedCount + " / 5");
            sb.AppendLine("任务          已领 " + ClaimedQuests() + " · 已接 " + AcceptedQuests());
            sb.AppendLine("地铁最深处    " + d.subwayBestDepth + " 层");
            sb.AppendLine("已解锁结局    " + d.endingsSeen.Count + " / 3");
            sb.AppendLine("房屋          " + (d.houses.Count > 0 ? "已购置" : "未购置"));
            sb.AppendLine("已购强化/义体 " + d.shopPurchases.Count + " 件");
            sb.AppendLine("村庄折扣      " + (d.villageDiscount > 0f ? "8.5 折" : "未解锁"));
            return sb.ToString();
        }

        string BuildEquipment()
        {
            var sb = new StringBuilder();
            sb.AppendLine("装备");
            sb.AppendLine();

            var loadout = FindObjectOfType<WeaponLoadout>();
            string cur = loadout != null ? (loadout.Current == WeaponKind.Sword ? "石刃（近战）" : "旧式手枪") : "未知";
            sb.AppendLine("当前手持：" + cur);
            sb.AppendLine();

            float dmgMul = PlayerStats.DamageMultiplier;

            var melee = FindObjectOfType<MeleeWeapon>(true);
            if (melee != null)
            {
                int actual = Mathf.RoundToInt(melee.damage * dmgMul);
                sb.AppendLine("石刃");
                sb.AppendLine("  基础伤害 " + melee.damage.ToString("F0") + "  →  实际 " + actual);
                sb.AppendLine("  攻击距离 " + melee.attackRange.ToString("F1") + " m · 挥砍 " + melee.attackInterval.ToString("F2") + " s");
                sb.AppendLine("  单次最多命中 " + melee.maxTargets + " 个目标");
                sb.AppendLine();
            }

            var gun = FindObjectOfType<PlayerWeapon>(true);
            if (gun != null)
            {
                int actual = Mathf.RoundToInt(gun.damage * dmgMul);
                sb.AppendLine("旧式手枪");
                sb.AppendLine("  基础伤害 " + gun.damage.ToString("F0") + "  →  实际 " + actual);
                sb.AppendLine("  弹匣 " + gun.AmmoInMagazine + " / " + gun.MagazineSize + " · 备弹 " + gun.ReserveAmmo);
                sb.AppendLine("  射程 " + gun.range.ToString("F0") + " m · 换弹 " + gun.reloadTime.ToString("F2") + " s");
                sb.AppendLine();
            }

            if (melee == null && gun == null)
                sb.AppendLine("当前场景没有可用武器。");

            sb.AppendLine("—— 加成来源 ——");
            sb.AppendLine("  伤害 +" + Mathf.RoundToInt(Mathf.Clamp(SaveSystem.Data.damageBonus, 0f, 0.8f) * 100f) + "%  ·  来自商店强化与任务奖励");
            return sb.ToString();
        }

        string BuildInventory()
        {
            var sb = new StringBuilder();
            var list = Inventory.Items;
            sb.AppendLine("背包   （" + list.Count + " 种 · " + Inventory.TotalCount + " 件）");
            sb.AppendLine();
            if (list.Count == 0)
            {
                sb.AppendLine("空空如也。");
                sb.AppendLine();
                sb.AppendLine("任务物品、钥匙、石英粉之类的东西会自动放进这里。");
                return sb.ToString();
            }
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                sb.AppendLine("[" + it.kind + "] " + it.name + " ×" + it.count);
                if (!string.IsNullOrEmpty(it.desc)) sb.AppendLine("    " + it.desc);
            }
            return sb.ToString();
        }

        string BuildHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("操作");
            sb.AppendLine();
            sb.AppendLine("W A S D      移动");
            sb.AppendLine("Shift        奔跑");
            sb.AppendLine("空格         跳跃（购买义体后可二段跳）");
            sb.AppendLine("鼠标         视角 / 转身");
            sb.AppendLine("鼠标左键     攻击 / 射击");
            sb.AppendLine("R            换弹");
            sb.AppendLine("1 / 2        切换武器");
            sb.AppendLine("E            交互（拾取、对话、传送门、商店、快速旅行）");
            sb.AppendLine("Tab          本面板");
            sb.AppendLine("Esc          关闭面板 / 打开游戏菜单");
            sb.AppendLine();
            sb.AppendLine("小提示：石化满了就完了，被盯住时先跑出视线。");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- UI

        void Build()
        {
            var go = new GameObject("PanelCanvas", typeof(RectTransform));
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
            img.color = new Color(0.02f, 0.03f, 0.05f, 0.985f);

            var font = CjkFont.Get();

            _tabText = MakeText(go.transform, font, 36, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1500f, 70f),
                new Color(1f, 0.85f, 0.35f));

            _body = MakeText(go.transform, font, 32, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                new Color(0.92f, 0.96f, 1f));
            var brt2 = _body.rectTransform;
            brt2.offsetMin = new Vector2(260f, 130f);
            brt2.offsetMax = new Vector2(-260f, -140f);
            _body.lineSpacing = 1.32f;

            _hint = MakeText(go.transform, font, 26, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(1200f, 50f),
                new Color(0.62f, 0.70f, 0.80f));

            go.SetActive(false);
        }

        static Text MakeText(Transform parent, Font font, int size, TextAnchor anchor,
            Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
