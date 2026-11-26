using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MedusaHorror.Core;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

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
        RectTransform _bodyViewport;
        ScrollRect _bodyScroll;
        Text _summary;
        int _displayedPage = -1;
        const int SlotCount = 9;
        int _bagPage;
        string _dropConfirmation;
        GameObject _bagControls;
        Text _bagPageText;
        Button _useButton, _dropButton, _previousBag, _nextBag;
        Text _dropLabel;
        Image[] _filterImages;
        bool _previousBlocked;
        CursorLockMode _previousCursor;
        bool _previousCursorVisible;

        void OnEnable() { Inventory.Changed += OnInventoryChanged; }
        void OnDisable() { Inventory.Changed -= OnInventoryChanged; Close(); }
        void OnInventoryChanged() { if (_open) Refresh(); }

        static bool KeyDown(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (code >= KeyCode.Alpha1 && code <= KeyCode.Alpha9)
                return keyboard[(Key)((int)Key.Digit1 + (int)code - (int)KeyCode.Alpha1)].wasPressedThisFrame;
            switch (code)
            {
                case KeyCode.Tab: return keyboard.tabKey.wasPressedThisFrame;
                case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                case KeyCode.Q: return keyboard.qKey.wasPressedThisFrame;
                case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
                case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey.wasPressedThisFrame;
                case KeyCode.RightArrow: return keyboard.rightArrowKey.wasPressedThisFrame;
            }
            return false;
#else
            return Input.GetKeyDown(code);
#endif
        }

        static bool ShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }

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

            if (KeyDown(KeyCode.Tab))
            {
                if (!_open && InputReader.blocked) return;
                if (_open) Close(); else Open();
                return;
            }
            if (!_open) return;

            if (KeyDown(KeyCode.Escape)) { Close(); return; }

            if (KeyDown(KeyCode.Q)) { _page = (_page + PageNames.Length - 1) % PageNames.Length; Refresh(); return; }
            if (KeyDown(KeyCode.E)) { _page = (_page + 1) % PageNames.Length; Refresh(); return; }

            // 背包页：F 切分类，数字键使用，Shift+数字丢弃
            if (_page == 2)
            {
                if (KeyDown(KeyCode.F))
                {
                    SelectFilter((_bagFilter + 1) % BagFilters.Length);
                    return;
                }
                if (KeyDown(KeyCode.LeftArrow)) { ChangeBagPage(-1); return; }
                if (KeyDown(KeyCode.RightArrow)) { ChangeBagPage(1); return; }
                bool shift = ShiftHeld();
                for (int i = 0; i < 9; i++)
                {
                    if (!KeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) continue;
                    _bagSel = _bagPage * SlotCount + i;
                    if (shift) DropItem(_bagSel); else UseItem(_bagSel);
                    return;
                }
            }
        }

        public void Open(int page = -1)
        {
            if (EventSystem.current == null && FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("PlayerPanelEventSystem", typeof(EventSystem));
                events.transform.SetParent(transform, false);
#if ENABLE_INPUT_SYSTEM
                events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
                events.AddComponent<StandaloneInputModule>();
#endif
            }
#if ENABLE_INPUT_SYSTEM
            var eventSystem = EventSystem.current != null ? EventSystem.current : FindObjectOfType<EventSystem>();
            if (eventSystem != null)
            {
                var module = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                foreach (var old in eventSystem.GetComponents<BaseInputModule>()) if (old != module) old.enabled = false;
                module.enabled = true;
                if (module.actionsAsset == null) module.AssignDefaultActions();
            }
#endif
            if (!_open)
            {
                _previousBlocked = InputReader.blocked;
                _previousCursor = Cursor.lockState;
                _previousCursorVisible = Cursor.visible;
            }
            if (page >= 0) _page = Mathf.Clamp(page, 0, PageNames.Length - 1);
            _open = true;
            if (_bagSel < 0 && FilteredItems().Count > 0) _bagSel = 0;
            InputReader.blocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            if (_bodyScroll != null) _bodyScroll.verticalNormalizedPosition = 1f;
        }

        void LateUpdate()
        {
            if (!_open) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            if (!_open) return;
            _open = false;
            InteractionPanelInput.MarkClosed();
            InputReader.blocked = _previousBlocked;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = _previousCursor;
            Cursor.visible = _previousCursorVisible;
            _dropConfirmation = null;
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
            if (_dropConfirmation != it.id)
            {
                _dropConfirmation = it.id;
                _bagSel = index;
                Refresh();
                LootToast.Show("再次点击丢弃，确认丢弃「" + it.name + "」×1");
                return false;
            }
            _dropConfirmation = null;
            if (!Inventory.Remove(it.id, 1)) return false;
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
                if (!Inventory.Remove(it.id, 1)) return false;
                pz.ReduceValue(pz.maxValue * 0.15f);
                LootToast.Show("用掉一罐石英粉 · 石化 " + Mathf.RoundToInt(before) + " → " + Mathf.RoundToInt(pz.value));
                var audio = AudioDirector.Instance;
                if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.8f, 1.05f);
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
                    _tabImgs[i].sprite = null;
                    _tabImgs[i].color = i == _page
                        ? new Color(0.12f, 0.35f, 0.39f, 1f)
                        : new Color(0.065f, 0.12f, 0.15f, 1f);
                }
            }

            bool bag = _page == 2;
            if (_bagGrid != null) _bagGrid.gameObject.SetActive(bag);
            if (_bagControls != null) _bagControls.SetActive(bag);
            if (_bodyViewport != null)
            {
                _bodyViewport.anchorMin = bag ? new Vector2(0.58f, 0.28f) : new Vector2(0.07f, 0.18f);
                _bodyViewport.anchorMax = new Vector2(0.94f, 0.73f);
                _bodyViewport.offsetMin = _bodyViewport.offsetMax = Vector2.zero;
            }
            if (bag) FillBagGrid();

            if (_body != null) _body.text = BuildPage(_page);
            if (_displayedPage != _page && _bodyScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                _bodyScroll.verticalNormalizedPosition = 1f;
                _displayedPage = _page;
            }
            if (_summary != null) _summary.text = "等级 " + Progression.Level + "     信用点 " + Wallet.Coins + "     随身物品 " + Inventory.TotalCount + " 件";
            if (_hint != null)
                _hint.text = _page == 2
                    ? "点击选择物品 · 数字键 1—9 使用 · ←/→ 翻页 · F 分类 · Tab/Esc 关闭"
                    : "点击页签切换 · 鼠标滚轮阅读 · Q / E 切页 · Tab / Esc 返回";
        }

        /// <summary>刷新背包格子内容。</summary>
        void FillBagGrid()
        {
            var list = FilteredItems();
            int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)SlotCount));
            _bagPage = Mathf.Clamp(_bagPage, 0, pages - 1);
            if (list.Count == 0) _bagSel = -1;
            else _bagSel = Mathf.Clamp(_bagSel, _bagPage * SlotCount, Mathf.Min(list.Count - 1, (_bagPage + 1) * SlotCount - 1));
            if (_bagPageText != null) _bagPageText.text = "第 " + (_bagPage + 1) + " / " + pages + " 页";
            _previousBag.interactable = _bagPage > 0;
            _nextBag.interactable = _bagPage + 1 < pages;
            var selected = _bagSel >= 0 ? list[_bagSel] : null;
            _useButton.interactable = selected != null && selected.id == "quartz";
            _dropButton.interactable = selected != null && selected.kind != "任务" && selected.kind != "钥匙";
            _dropLabel.text = selected != null && selected.id == _dropConfirmation ? "确认丢弃 ×1" : "丢弃 ×1";
            for (int f = 0; f < _filterImages.Length; f++)
                _filterImages[f].color = f == _bagFilter ? new Color(0.12f, 0.48f, 0.55f) : new Color(0.09f, 0.15f, 0.21f);
            for (int i = 0; i < SlotCount; i++)
            {
                int index = _bagPage * SlotCount + i;
                bool has = index < list.Count;
                if (_slotIcons[i] != null)
                    _slotIcons[i].color = has
                        ? new Color(0.09f, 0.18f, 0.21f, 1f)
                        : new Color(0.055f, 0.09f, 0.11f, 1f);
                if (_slotTexts[i] != null)
                    _slotTexts[i].text = has ? ((i + 1) + "  [" + list[index].kind + "]\n" + list[index].name + "\n×" + list[index].count) : "";
                if (_slotHighlights[i] != null)
                    _slotHighlights[i].enabled = has && index == _bagSel;
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

            var controller = FindObjectOfType<ThirdPersonController>();
            var loadout = controller != null ? controller.GetComponent<WeaponLoadout>() : null;
            string cur = loadout == null ? "石刃（近战）" : (loadout.Current == WeaponKind.Sword ? "石刃（近战）" : loadout.Current == WeaponKind.Rifle ? "脉冲步枪" : "科幻枪械");
            sb.AppendLine("装备        当前手持：" + cur);
            sb.AppendLine();

            // 有实例就用实时数值，没有就用标准值，保证任何场景都能看到完整装备
            var melee = loadout != null ? loadout.sword : null;
            float meleeDmg = melee != null ? melee.damage : BaseMeleeDamage;
            float meleeRange = melee != null ? melee.attackRange : BaseMeleeRange;
            float meleeInterval = melee != null ? melee.attackInterval : BaseMeleeInterval;
            int meleeTargets = melee != null ? melee.maxTargets : BaseMeleeTargets;

            sb.AppendLine("近战 · 石刃");
            sb.AppendLine("  伤害 " + meleeDmg.ToString("F0") + "  →  实际 " + Mathf.RoundToInt(meleeDmg * dmgMul));
            sb.AppendLine("  攻击距离 " + meleeRange.ToString("F1") + " m · 挥砍 " + meleeInterval.ToString("F2") + " s · 最多命中 " + meleeTargets);
            sb.AppendLine("  三段连击：斜劈 → 反手横扫 → 重斩（伤害 100% / 118% / 162%）");
            sb.AppendLine();

            var gun = loadout != null ? (loadout.Current == WeaponKind.Rifle ? loadout.rifle : loadout.gun) : null;
            float gunDmg = gun != null ? gun.damage : BaseGunDamage;
            float gunRange = gun != null ? gun.range : BaseGunRange;
            float gunReload = gun != null ? gun.reloadTime : BaseGunReload;
            int gunMag = gun != null ? gun.MagazineSize : BaseGunMag;
            int gunReserve = gun != null ? gun.ReserveAmmo : BaseGunReserve;

            sb.AppendLine("远程 · 科幻枪械");
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

            sb.AppendLine("物品详情");
            sb.AppendLine("共 " + Inventory.Items.Count + " 种 · " + Inventory.TotalCount + " 件");
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
                    sb.AppendLine(sel.name + " ×" + sel.count);
                    sb.AppendLine("分类　" + sel.kind);
                    if (!string.IsNullOrEmpty(sel.desc)) sb.AppendLine("  " + sel.desc);
                    sb.AppendLine();
                    sb.AppendLine(sel.id == "quartz" ? "使用后降低 15% 最大石化值。" : sel.kind == "任务" || sel.kind == "钥匙" ? "任务所需物品，已保护，无法丢弃。" : "此物品无法直接使用。");
                }
                else
                {
                    sb.AppendLine("点击格子或按数字键 1-9 选择物品。");
                }
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
            sb.AppendLine("鼠标右键     按住瞄准（持枪时）");
            sb.AppendLine("R            换弹");
            sb.AppendLine("1 / 2        切换武器");
            sb.AppendLine("E            交互（拾取、对话、传送门、商店、快速旅行）");
            sb.AppendLine("Tab          本面板（打开时角色停止操作）");
            sb.AppendLine("Q / E        面板内切换页签");
            sb.AppendLine("F            背包内切换分类（消耗/材料/任务/钥匙/纪念）");
            sb.AppendLine("← / →        背包物品翻页");
            sb.AppendLine("Shift+数字   请求丢弃，再按一次确认");
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
            go.AddComponent<GraphicRaycaster>();
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
            img.color = new Color(0.01f, 0.02f, 0.025f, 0.80f);

            var font = CjkFont.Get();
            var shell = new GameObject("FieldRecord", typeof(RectTransform), typeof(Image));
            var shellRt = shell.GetComponent<RectTransform>();shellRt.SetParent(go.transform,false);
            shellRt.anchorMin = new Vector2(0.06f,0.06f);shellRt.anchorMax = new Vector2(0.94f,0.94f);
            shellRt.offsetMin = shellRt.offsetMax = Vector2.zero;
            shell.GetComponent<Image>().color = new Color(0.035f,0.065f,0.08f,0.99f);
            var title=MakeText(shellRt,font,34,TextAnchor.MiddleLeft,new Vector2(0.06f,0.9f),new Vector2(0.06f,0.9f),Vector2.zero,new Vector2(440,60),Color.white);
            title.rectTransform.pivot=new Vector2(0,0.5f);title.text="行动档案 / 随身终端";title.raycastTarget=false;
            _summary=MakeText(shellRt,font,19,TextAnchor.MiddleRight,new Vector2(0.82f,0.91f),new Vector2(0.82f,0.91f),Vector2.zero,new Vector2(650,44),new Color(0.6f,0.77f,0.8f));_summary.rectTransform.pivot=new Vector2(1,0.5f);_summary.raycastTarget=false;
            var close=BagButton(shellRt,font,"关闭 ×",new Vector2(0.93f,0.91f),Vector2.zero,new Vector2(125,46));close.name="ClosePanel";close.onClick.AddListener(Close);
            BuildTabs(shellRt, font);
            BuildBagGrid(shellRt, font);
            BuildBagControls(shellRt, font);
            var viewport=new GameObject("ReadingArea",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
            _bodyViewport=viewport.GetComponent<RectTransform>();_bodyViewport.SetParent(shellRt,false);
            viewport.GetComponent<Image>().color=new Color(0.02f,0.035f,0.045f,0.45f);
            _bodyScroll=viewport.GetComponent<ScrollRect>();_bodyScroll.horizontal=false;_bodyScroll.scrollSensitivity=36;
            _bodyScroll.movementType=ScrollRect.MovementType.Clamped;_bodyScroll.viewport=_bodyViewport;
            _body = MakeText(_bodyViewport, font, 23, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-38,0),
                new Color(0.87f, 0.93f, 0.94f));
            _bodyRect = _body.rectTransform;
            _bodyRect.pivot=new Vector2(0.5f,1);_bodyRect.anchoredPosition=new Vector2(0,-14);
            _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            _bodyScroll.content=_bodyRect;
            _body.lineSpacing = 1.18f;
            _body.raycastTarget = false;

            _hint = MakeText(shellRt, font, 19, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.065f), new Vector2(0.5f, 0.065f), Vector2.zero, new Vector2(1300f, 44f),
                new Color(0.62f, 0.70f, 0.80f));
            _hint.raycastTarget=false;

            UiSkin.Apply(shellRt, false);
            go.SetActive(false);
        }

        /// <summary>顶部四个页签按钮，用素材包的按钮图（选中/常态两态）。</summary>
        void BuildTabs(Transform parent, Font font)
        {
            _tabImgs = new Image[PageNames.Length];
            const float w = 230f, h = 54f, gap = 16f;

            for (int i = 0; i < PageNames.Length; i++)
            {
                var b = new GameObject("Tab" + i, typeof(RectTransform));
                b.transform.SetParent(parent, false);
                var rt = b.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.81f);
                rt.anchorMax = new Vector2(0.5f, 0.81f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2((i - (PageNames.Length - 1) * 0.5f) * (w + gap), 0);
                rt.sizeDelta = new Vector2(w, h);

                var img = b.AddComponent<Image>();
                img.sprite = null;
                img.color = new Color(1f, 1f, 1f, 0.92f);
                _tabImgs[i] = img;

                int idx = i;
                var btn = b.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;
                btn.onClick.AddListener(delegate { _page = idx; Refresh(); });

                var t = MakeText(b.transform, font, 25, TextAnchor.MiddleCenter,
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
                rt.anchoredPosition = new Vector2(d * 850f, 0f);
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
            const float cw = 178f, ch = 124f, gap = 14f;

            var grid = new GameObject("BagGrid", typeof(RectTransform));
            grid.transform.SetParent(parent, false);
            _bagGrid = grid.GetComponent<RectTransform>();
            _bagGrid.anchorMin = _bagGrid.anchorMax = new Vector2(0.30f, 0.49f);
            _bagGrid.pivot = new Vector2(0.5f, 0.5f);
            _bagGrid.sizeDelta = new Vector2(cols * cw + (cols - 1) * gap, rows * ch + (rows - 1) * gap);
            _bagGrid.anchoredPosition = new Vector2(0f, -5f);

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
                bgImg.sprite = null;
                bgImg.color = new Color(0.09f, 0.18f, 0.21f, 1f);
                bgImg.preserveAspect = false;
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

                AddAssetBorder(s.transform);
                var t = MakeText(s.transform, font, 20, TextAnchor.MiddleCenter,
                    Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14f, -10f),
                    new Color(0.94f, 0.98f, 1f));
                t.text = "";
                t.raycastTarget = false;
                _slotTexts[i] = t;

                int idx = i;
                var btn = s.AddComponent<Button>();
                btn.targetGraphic = bgImg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(delegate { _bagSel = _bagPage * SlotCount + idx; _dropConfirmation = null; Refresh(); });
            }

            grid.SetActive(false);
        }

        void SelectFilter(int index)
        {
            _bagFilter = index;
            _bagPage = 0;
            _bagSel = 0;
            _dropConfirmation = null;
            Refresh();
        }

        void ChangeBagPage(int direction)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(FilteredItems().Count / (float)SlotCount));
            _bagPage = Mathf.Clamp(_bagPage + direction, 0, pages - 1);
            _bagSel = _bagPage * SlotCount;
            _dropConfirmation = null;
            Refresh();
        }

        Button BagButton(Transform parent, Font font, string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var obj = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var background = obj.GetComponent<Image>();
            background.sprite = null;
            background.color = new Color(0.09f, 0.22f, 0.29f);
            var button = obj.GetComponent<Button>();
            button.targetGraphic = background;
            var colors = button.colors;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.45f);
            button.colors = colors;
            var text = MakeText(obj.transform, font, 20, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
            text.text = label;
            text.raycastTarget = false;
            AddAssetBorder(obj.transform);
            return button;
        }

        static void AddAssetBorder(Transform parent)
        {
            var edge=new GameObject("AssetFrame",typeof(RectTransform),typeof(Image));
            edge.transform.SetParent(parent,false);var rt=edge.GetComponent<RectTransform>();
            rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            var image=edge.GetComponent<Image>();image.sprite=UiAtlas.Frame;image.type=Image.Type.Sliced;
            image.color=new Color(0.3f,0.57f,0.62f,0.65f);image.raycastTarget=false;
        }

        void BuildBagControls(Transform parent, Font font)
        {
            _bagControls = new GameObject("BagControls", typeof(RectTransform));
            _bagControls.transform.SetParent(parent, false);
            var rect = _bagControls.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _filterImages = new Image[BagFilters.Length];
            for (int i = 0; i < BagFilters.Length; i++)
            {
                int filter = i;
                var button = BagButton(rect, font, BagFilters[i], new Vector2(0.3f, 0.74f), new Vector2((i - 2.5f) * 94f, 0), new Vector2(88, 40));
                button.onClick.AddListener(() => SelectFilter(filter));
                _filterImages[i] = button.GetComponent<Image>();
            }
            _previousBag = BagButton(rect, font, "上一页", new Vector2(0.3f, 0.20f), new Vector2(-215, 0), new Vector2(130, 48));
            _previousBag.onClick.AddListener(() => ChangeBagPage(-1));
            _nextBag = BagButton(rect, font, "下一页", new Vector2(0.3f, 0.20f), new Vector2(215, 0), new Vector2(130, 48));
            _nextBag.onClick.AddListener(() => ChangeBagPage(1));
            _bagPageText = MakeText(rect, font, 24, TextAnchor.MiddleCenter, new Vector2(0.3f, 0.20f), new Vector2(0.3f, 0.20f), Vector2.zero, new Vector2(220, 48), Color.white);
            _useButton = BagButton(rect, font, "使用物品", new Vector2(0.66f, 0.20f), Vector2.zero, new Vector2(200, 52));
            _useButton.onClick.AddListener(() => { _dropConfirmation = null; UseItem(_bagSel); });
            _dropButton = BagButton(rect, font, "丢弃 ×1", new Vector2(0.84f, 0.20f), Vector2.zero, new Vector2(200, 52));
            _dropButton.onClick.AddListener(() => DropItem(_bagSel));
            _dropLabel = _dropButton.GetComponentInChildren<Text>();
            _bagControls.SetActive(false);
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
