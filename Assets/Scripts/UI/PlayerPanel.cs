using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 玩家面板（Tab）：属性 / 装备 / 背包 / 帮助 四页。
    /// 装备页展示全局装备，不依赖当前场景是否存在武器实例；背包页可按数字键使用消耗品。
    /// </summary>
    public class PlayerPanel : MonoBehaviour
    {
        static PlayerPanel _instance;

        // 武器标准数值：场景里没有武器实例时用它做基准，保证装备页在任何关卡都有内容
        const float BaseMeleeDamage = 55f;
        const float BaseMeleeRange = 2.2f;
        const float BaseMeleeInterval = 0.55f;
        const int BaseMeleeTargets = 4;
        const float BaseGunDamage = 34f;
        const float BaseGunRange = 70f;
        const float BaseGunReload = 1.55f;
        const int BaseGunMag = 12;
        const int BaseGunReserve = 240;

        static readonly Dictionary<string, string[]> PurchaseInfo = new Dictionary<string, string[]>
        {
            { "w_ed1",    new[] { "刃口强化 I",   "近战/射击伤害 +10%" } },
            { "w_ed2",    new[] { "刃口强化 II",  "近战/射击伤害 再 +10%" } },
            { "w_grip",   new[] { "轻量化握柄",   "移动速度 +8%" } },
            { "c_jump",   new[] { "神经反射插件", "解锁空中二段跳" } },
            { "c_skin",   new[] { "石英表皮",     "石化抗性 +10%" } },
            { "c_muscle", new[] { "肌纤维强化",   "移动速度 +8%" } },
        };

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

            if (Input.GetKeyDown(KeyCode.Q)) { _page = (_page + PageNames.Length - 1) % PageNames.Length; Refresh(); return; }
            if (Input.GetKeyDown(KeyCode.E)) { _page = (_page + 1) % PageNames.Length; Refresh(); return; }

            // 背包页：数字键使用对应物品
            if (_page == 2)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) continue;
                    UseItem(i);
                    return;
                }
            }
        }

        public void Open(int page = -1)
        {
            if (page >= 0) _page = Mathf.Clamp(page, 0, PageNames.Length - 1);
            _open = true;
            InputReader.blocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            InputReader.blocked = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>使用背包第 index 件物品。</summary>
        public bool UseItem(int index)
        {
            var list = Inventory.Items;
            if (index < 0 || index >= list.Count)
            {
                LootToast.Show("这一格是空的。");
                return false;
            }

            var it = list[index];
            if (it.id == "quartz")
            {
                var pz = FindObjectOfType<PlayerPetrify>();
                if (pz == null) { LootToast.Show("现在用不上。"); return false; }
                if (pz.value <= 0.5f) { LootToast.Show("身上还没有石化痕迹，先留着。"); return false; }

                float before = pz.value;
                Inventory.Remove(it.id, 1);
                pz.ReduceValue(pz.maxValue * 0.15f);
                LootToast.Show("用掉一罐石英粉 · 石化 " + Mathf.RoundToInt(before) + " → " + Mathf.RoundToInt(pz.value));
                var audio = AudioDirector.Instance;
                if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.8f, 1.05f);
                Refresh();
                return true;
            }

            if (it.kind == "消耗")
            {
                Inventory.Remove(it.id, 1);
                LootToast.Show("用掉了「" + it.name + "」");
                Refresh();
                return true;
            }

            LootToast.Show("「" + it.name + "」不能直接使用。");
            return false;
        }

        // ---------------------------------------------------------------- 内容

        void Refresh()
        {
            if (_tabText != null)
            {
                var tb = new StringBuilder();
                tb.Append("◀ Q     ");
                for (int i = 0; i < PageNames.Length; i++)
                {
                    if (i > 0) tb.Append("   ·   ");
                    tb.Append(i == _page ? "【" + PageNames[i] + "】" : PageNames[i]);
                }
                tb.Append("     E ▶");
                _tabText.text = tb.ToString();
            }
            if (_body != null) _body.text = BuildPage(_page);
            if (_hint != null)
                _hint.text = _page == 2
                    ? "数字键 1-9 使用物品 · Q / E 切换页签 · Tab / Esc 关闭"
                    : "Q / E 切换页签 · Tab / Esc 关闭";
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
            float dmgMul = PlayerStats.DamageMultiplier;

            var loadout = FindObjectOfType<WeaponLoadout>(true);
            string cur = loadout == null ? "石刃（近战）" : (loadout.Current == WeaponKind.Sword ? "石刃（近战）" : "旧式手枪");
            sb.AppendLine("装备        当前手持：" + cur);
            sb.AppendLine();

            // 有实例就用实时数值，没有就用标准值，保证任何场景都能看到完整装备
            var melee = FindObjectOfType<MeleeWeapon>(true);
            float meleeDmg = melee != null ? melee.damage : BaseMeleeDamage;
            float meleeRange = melee != null ? melee.attackRange : BaseMeleeRange;
            float meleeInterval = melee != null ? melee.attackInterval : BaseMeleeInterval;
            int meleeTargets = melee != null ? melee.maxTargets : BaseMeleeTargets;

            sb.AppendLine("近战 · 石刃");
            sb.AppendLine("  伤害 " + meleeDmg.ToString("F0") + "  →  实际 " + Mathf.RoundToInt(meleeDmg * dmgMul));
            sb.AppendLine("  攻击距离 " + meleeRange.ToString("F1") + " m · 挥砍 " + meleeInterval.ToString("F2") + " s · 最多命中 " + meleeTargets);
            sb.AppendLine();

            var gun = FindObjectOfType<PlayerWeapon>(true);
            float gunDmg = gun != null ? gun.damage : BaseGunDamage;
            float gunRange = gun != null ? gun.range : BaseGunRange;
            float gunReload = gun != null ? gun.reloadTime : BaseGunReload;
            int gunMag = gun != null ? gun.MagazineSize : BaseGunMag;
            int gunReserve = gun != null ? gun.ReserveAmmo : BaseGunReserve;

            sb.AppendLine("远程 · 旧式手枪");
            sb.AppendLine("  伤害 " + gunDmg.ToString("F0") + "  →  实际 " + Mathf.RoundToInt(gunDmg * dmgMul));
            sb.AppendLine("  弹匣 " + gunMag + " · 备弹 " + gunReserve + " · 射程 " + gunRange.ToString("F0") + " m · 换弹 " + gunReload.ToString("F2") + " s");
            sb.AppendLine();

            var owned = SaveSystem.Data.shopPurchases;
            sb.AppendLine("已装强化 / 义体（" + owned.Count + " 件）");
            if (owned.Count == 0)
            {
                sb.AppendLine("  还没有装任何强化，去霓虹街区的武器店和义体门诊看看。");
            }
            else
            {
                for (int i = 0; i < owned.Count; i++)
                {
                    string[] info;
                    if (PurchaseInfo.TryGetValue(owned[i], out info))
                        sb.AppendLine("  " + info[0] + "　—　" + info[1]);
                    else
                        sb.AppendLine("  " + owned[i]);
                }
            }
            sb.AppendLine();
            sb.AppendLine("—— 加成合计 ——");
            sb.AppendLine("  伤害 +" + Mathf.RoundToInt(Mathf.Clamp(SaveSystem.Data.damageBonus, 0f, 0.8f) * 100f) + "%"
                        + " · 移速 +" + Mathf.RoundToInt(Mathf.Clamp(SaveSystem.Data.speedBonus, 0f, 0.5f) * 100f) + "%"
                        + " · 石化抗性 " + Mathf.RoundToInt(Mathf.Clamp(SaveSystem.Data.petrifyResist, 0f, 0.6f) * 100f) + "%"
                        + " · 二段跳 " + (SaveSystem.Data.doubleJump ? "已解锁" : "未解锁"));
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
                sb.AppendLine("[" + (i + 1) + "] [" + it.kind + "] " + it.name + " ×" + it.count);
                if (!string.IsNullOrEmpty(it.desc)) sb.AppendLine("        " + it.desc);
            }
            sb.AppendLine();
            sb.AppendLine("消耗品按对应数字键使用。");
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
            sb.AppendLine("Tab          本面板（打开时角色停止操作）");
            sb.AppendLine("Q / E        面板内切换页签");
            sb.AppendLine("Esc          关闭面板 / 打开游戏菜单");
            sb.AppendLine();
            sb.AppendLine("小提示：石化满了就完了，被盯住时先跑出视线；身上带着石英粉能救一次。");
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

            _body = MakeText(go.transform, font, 28, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                new Color(0.92f, 0.96f, 1f));
            var brt2 = _body.rectTransform;
            brt2.offsetMin = new Vector2(240f, 108f);
            brt2.offsetMax = new Vector2(-240f, -132f);
            _body.lineSpacing = 1.22f;

            _hint = MakeText(go.transform, font, 26, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(1200f, 50f),
                new Color(0.62f, 0.70f, 0.80f));

            UiSkin.Apply(go.GetComponent<RectTransform>());
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
