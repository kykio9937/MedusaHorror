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
        int _bagFilter;
        int _bagSel = -1;
        bool _open;
        bool _shown = true;
        Canvas _canvas;
        Text _body;
        Text _hint;

        // 素材化 UI
        Image[] _tabImgs;
        RectTransform _bagGrid;
        Image[] _slotIcons;
        Text[] _slotTexts;
        Image[] _slotHighlights;
        RectTransform _bodyRect;
        const int SlotCount = 9;

        static readonly string[] PageNames = { "属性", "装备", "背包", "帮助" };
        static readonly string[] BagFilters = { "全部", "消耗", "材料", "任务", "钥匙", "纪念" };

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

            // 背包页：F 切分类，数字键使用，Shift+数字丢弃
            if (_page == 2)
            {
                if (Input.GetKeyDown(KeyCode.F))
                {
                    _bagFilter = (_bagFilter + 1) % BagFilters.Length;
                    Refresh();
                    return;
                }
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                for (int i = 0; i < 9; i++)
                {
                    if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) continue;
                    _bagSel = i;
                    if (shift) DropItem(i); else UseItem(i);
                    return;
                }
            }
        }

        public void Open(int page = -1)
        {
            if (page >= 0) _page = Mathf.Clamp(page, 0, PageNames.Length - 1);
            _open = true;
            if (_bagSel < 0 && FilteredItems().Count > 0) _bagSel = 0;
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
        List<ItemStack> FilteredItems()
        {
            var all = Inventory.Items;
            if (_bagFilter <= 0) return all;
            string kind = BagFilters[_bagFilter];
            var result = new List<ItemStack>();
            for (int i = 0; i < all.Count; i++)
                if (all[i].kind == kind) result.Add(all[i]);
            return result;
        }

        /// <summary>丢弃背包第 index 件（按当前分类）。</summary>
        public bool DropItem(int index)
        {
            var list = FilteredItems();
            if (index < 0 || index >= list.Count) { LootToast.Show("这一格是空的。"); return false; }
            var it = list[index];
            if (it.kind == "任务" || it.kind == "钥匙")
            {
                LootToast.Show("「" + it.name + "」不能丢弃。");
                return false;
            }
            Inventory.Remove(it.id, 1);
            LootToast.Show("丢掉了「" + it.name + "」×1");
            Refresh();
            return true;
        }

        public bool UseItem(int index)
        {
            var list = FilteredItems();
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
            // 页签高亮：选中态用素材包的亮按钮，其余用暗按钮
            if (_tabImgs != null)
            {
                for (int i = 0; i < _tabImgs.Length; i++)
                {
                    if (_tabImgs[i] == null) continue;
                    _tabImgs[i].sprite = i == _page ? UiAtlas.ButtonOn : UiAtlas.ButtonOff;
                    _tabImgs[i].color = i == _page
                        ? new Color(0.55f, 0.95f, 1f, 1f)
                        : new Color(0.78f, 0.86f, 0.94f, 0.85f);
                }
            }

            bool bag = _page == 2;
            if (_bagGrid != null) _bagGrid.gameObject.SetActive(bag);
            if (_bodyRect != null)
            {
                // 背包页把文字区压到下半屏，给 3×3 格子让位
                if (bag)
                {
                    _bodyRect.offsetMin = new Vector2(240f, 108f);
                    _bodyRect.offsetMax = new Vector2(-240f, -470f);
                }
                else
                {
                    _bodyRect.offsetMin = new Vector2(240f, 108f);
                    _bodyRect.offsetMax = new Vector2(-240f, -132f);
                }
            }
            if (bag) FillBagGrid();

            if (_body != null) _body.text = BuildPage(_page);
            if (_hint != null)
                _hint.text = _page == 2
                    ? "点击格子或数字键 1-9 使用 · Shift+数字 丢弃 · F 切分类 · Q/E 换页 · Tab/Esc 关闭"
                    : "Q / E 或点击页签切换 · Tab / Esc 关闭";
        }

        /// <summary>刷新背包格子内容。</summary>
        void FillBagGrid()
        {
            var list = FilteredItems();
            for (int i = 0; i < SlotCount; i++)
            {
                bool has = i < list.Count;
                if (_slotIcons[i] != null)
                    _slotIcons[i].color = has
                        ? new Color(0.55f, 0.82f, 1f, 0.55f)
                        : new Color(0.50f, 0.60f, 0.70f, 0.16f);
                if (_slotTexts[i] != null)
                    _slotTexts[i].text = has ? (list[i].name + "\n×" + list[i].count) : "";
                if (_slotHighlights[i] != null)
                    _slotHighlights[i].enabled = has && i == _bagSel;
            }
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
            sb.AppendLine("等级          Lv " + Progression.Level
                + (Progression.Level >= Progression.MaxLevel ? "（已满级）" : "　经验 " + Mathf.RoundToInt(Progression.Xp) + " / " + Mathf.RoundToInt(Progression.XpToNext)));
            sb.AppendLine("累计击杀      " + SaveSystem.Data.killCount);
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
            sb.AppendLine();
            sb.AppendLine("已解锁能力");
            if (d.unlockedSkills == null || d.unlockedSkills.Count == 0)
            {
                sb.AppendLine("  还没有。打怪升级会陆续解锁。");
            }
            else
            {
                for (int i = 0; i < d.unlockedSkills.Count; i++)
                {
                    string id = d.unlockedSkills[i];
                    sb.AppendLine("  · " + Progression.UnlockName(id) + "　—　" + Progression.UnlockDesc(id));
                }
            }
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
            sb.AppendLine("  三段连击：斜劈 → 反手横扫 → 重斩（伤害 100% / 118% / 162%）");
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
            var list = FilteredItems();

            var ft = new StringBuilder();
            for (int i = 0; i < BagFilters.Length; i++)
            {
                if (i > 0) ft.Append(" · ");
                ft.Append(i == _bagFilter ? "【" + BagFilters[i] + "】" : BagFilters[i]);
            }
            sb.AppendLine("背包   " + ft);
            sb.AppendLine("共 " + Inventory.Items.Count + " 种 · " + Inventory.TotalCount + " 件　（F 切换分类）");
            sb.AppendLine();

            if (list.Count == 0)
            {
                sb.AppendLine(_bagFilter == 0 ? "空空如也。" : "这个分类里还没有东西。");
                sb.AppendLine();
                sb.AppendLine("任务物品、钥匙、石英粉之类的东西会自动放进这里。");
                return sb.ToString();
            }

            // 有格子视图时文字区只显示选中物品的详情，避免和格子重复
            if (_slotTexts != null)
            {
                sb.AppendLine();
                if (_bagSel >= 0 && _bagSel < list.Count)
                {
                    var sel = list[_bagSel];
                    sb.AppendLine("已选 [" + (_bagSel + 1) + "]  [" + sel.kind + "]  " + sel.name + " ×" + sel.count);
                    if (!string.IsNullOrEmpty(sel.desc)) sb.AppendLine("  " + sel.desc);
                    sb.AppendLine();
                    sb.AppendLine("数字键或点击格子使用 · Shift+数字丢弃（任务物品和钥匙不能丢）");
                }
                else
                {
                    sb.AppendLine("点击格子或按数字键 1-9 选择物品。");
                }
                if (list.Count > 9) sb.AppendLine("……还有 " + (list.Count - 9) + " 种未显示");
                return sb.ToString();
            }

            for (int i = 0; i < list.Count && i < 9; i++)
            {
                var it = list[i];
                sb.AppendLine("[" + (i + 1) + "] [" + it.kind + "] " + it.name + " ×" + it.count);
                if (!string.IsNullOrEmpty(it.desc)) sb.AppendLine("        " + it.desc);
            }
            if (list.Count > 9) sb.AppendLine("……还有 " + (list.Count - 9) + " 种未显示");
            sb.AppendLine();
            sb.AppendLine("数字键使用 · Shift+数字丢弃（任务物品和钥匙不能丢）");
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
            sb.AppendLine("F            背包内切换分类（消耗/材料/任务/钥匙/纪念）");
            sb.AppendLine("Shift+数字   丢弃背包物品");
            sb.AppendLine("B            在自己家里布置家具");
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

            BuildTabs(go.transform, font);
            BuildArrows(go.transform);
            BuildBagGrid(go.transform, font);

            _body = MakeText(go.transform, font, 28, TextAnchor.UpperLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero,
                new Color(0.92f, 0.96f, 1f));
            _bodyRect = _body.rectTransform;
            _bodyRect.offsetMin = new Vector2(240f, 108f);
            _bodyRect.offsetMax = new Vector2(-240f, -132f);
            _body.lineSpacing = 1.22f;

            _hint = MakeText(go.transform, font, 26, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(1200f, 50f),
                new Color(0.62f, 0.70f, 0.80f));

            UiSkin.Apply(go.GetComponent<RectTransform>());
            go.SetActive(false);
        }

        /// <summary>顶部四个页签按钮，用素材包的按钮图（选中/常态两态）。</summary>
        void BuildTabs(Transform parent, Font font)
        {
            _tabImgs = new Image[PageNames.Length];
            const float w = 240f, h = 76f, gap = 16f;

            for (int i = 0; i < PageNames.Length; i++)
            {
                var b = new GameObject("Tab" + i, typeof(RectTransform));
                b.transform.SetParent(parent, false);
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - (PageNames.Length - 1) * 0.5f) * (w + gap), -76f);
                rt.sizeDelta = new Vector2(w, h);

                var img = b.AddComponent<Image>();
                img.sprite = UiAtlas.ButtonOff;
                img.type = Image.Type.Sliced;
                img.color = new Color(1f, 1f, 1f, 0.92f);
                _tabImgs[i] = img;

                int idx = i;
                var btn = b.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(delegate { _page = idx; Refresh(); });

                var t = MakeText(b.transform, font, 30, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                    new Color(0.86f, 0.95f, 1f));
                t.text = PageNames[i];
                t.raycastTarget = false;
            }
        }

        /// <summary>左右翻页箭头，用素材包的箭头图旋转。</summary>
        void BuildArrows(Transform parent)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                var a = new GameObject(d < 0 ? "ArrowLeft" : "ArrowRight", typeof(RectTransform));
                a.transform.SetParent(parent, false);
                var rt = a.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(d * 86f, 0f);
                rt.sizeDelta = new Vector2(84f, 84f);
                rt.localRotation = Quaternion.Euler(0f, 0f, d < 0 ? 90f : -90f);

                var img = a.AddComponent<Image>();
                img.sprite = UiAtlas.Arrow;
                img.color = new Color(0.75f, 0.93f, 1f, 0.8f);
                img.preserveAspect = true;

                int dir = d;
                var btn = a.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(delegate
                {
                    _page = (_page + dir + PageNames.Length) % PageNames.Length;
                    Refresh();
                });
            }
        }

        /// <summary>背包 3×3 物品格子，格底用素材包的图标框，选中格用取景框高亮。</summary>
        void BuildBagGrid(Transform parent, Font font)
        {
            const int cols = 3, rows = 3;
            const float cw = 210f, ch = 150f, gap = 18f;

            var grid = new GameObject("BagGrid", typeof(RectTransform));
            grid.transform.SetParent(parent, false);
            _bagGrid = grid.GetComponent<RectTransform>();
            _bagGrid.anchorMin = _bagGrid.anchorMax = new Vector2(0.5f, 0.5f);
            _bagGrid.pivot = new Vector2(0.5f, 0.5f);
            _bagGrid.sizeDelta = new Vector2(cols * cw + (cols - 1) * gap, rows * ch + (rows - 1) * gap);
            _bagGrid.anchoredPosition = new Vector2(0f, 70f);

            _slotIcons = new Image[SlotCount];
            _slotTexts = new Text[SlotCount];
            _slotHighlights = new Image[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                int col = i % cols, row = i / cols;

                var s = new GameObject("Slot" + i, typeof(RectTransform));
                s.transform.SetParent(_bagGrid, false);
                var rt = s.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cw, ch);
                rt.anchoredPosition = new Vector2(
                    (col - (cols - 1) * 0.5f) * (cw + gap),
                    -(row - (rows - 1) * 0.5f) * (ch + gap));

                var bgImg = s.AddComponent<Image>();
                bgImg.sprite = UiAtlas.Icon;
                bgImg.color = new Color(0.55f, 0.78f, 0.95f, 0.30f);
                bgImg.preserveAspect = true;
                bgImg.raycastTarget = true;
                _slotIcons[i] = bgImg;

                var hl = new GameObject("Highlight", typeof(RectTransform));
                hl.transform.SetParent(s.transform, false);
                var hrt = hl.GetComponent<RectTransform>();
                hrt.anchorMin = Vector2.zero;
                hrt.anchorMax = Vector2.one;
                hrt.offsetMin = new Vector2(-6f, -6f);
                hrt.offsetMax = new Vector2(6f, 6f);
                var hImg = hl.AddComponent<Image>();
                hImg.sprite = UiAtlas.Indicator;
                hImg.type = Image.Type.Sliced;
                hImg.color = new Color(0.32f, 0.92f, 1f, 0.95f);
                hImg.raycastTarget = false;
                _slotHighlights[i] = hImg;

                var t = MakeText(s.transform, font, 22, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14f, -10f),
                    new Color(0.94f, 0.98f, 1f));
                t.text = "";
                t.raycastTarget = false;
                _slotTexts[i] = t;

                int idx = i;
                var btn = s.AddComponent<Button>();
                btn.targetGraphic = bgImg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(delegate { _bagSel = idx; Refresh(); });
            }

            grid.SetActive(false);
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
