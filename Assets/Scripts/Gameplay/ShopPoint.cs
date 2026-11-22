using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>W4 商店柜台：武器店卖强化，义体店卖能力，按数字键购买。</summary>
    public class ShopPoint : MonoBehaviour, IInteractable
    {
        public enum ShopKind { Weapon = 0, Cyber = 1 }

        public ShopKind kind = ShopKind.Weapon;
        public string shopName = "黑刃工坊";

        class Goods
        {
            public string id;
            public string name;
            public string desc;
            public int price;
            public System.Action buy;
        }

        readonly List<Goods> _goods = new List<Goods>();
        bool _open;
        Canvas _canvas;
        Text _body;
        Text _title;

        public string Hint { get { return shopName + " · 按 E 进店"; } }
        public bool CanInteract { get { return true; } }
        public bool IsOpen { get { return _open; } }
        public int GoodsCount { get { return _goods.Count; } }

        void Awake()
        {
            BuildGoods();
            BuildUi();
        }

        void BuildGoods()
        {
            if (kind == ShopKind.Weapon)
            {
                shopName = string.IsNullOrEmpty(shopName) ? "黑刃工坊" : shopName;
                _goods.Add(new Goods
                {
                    id = "w_ed1", name = "刃口强化 I", desc = "近战伤害 +10%", price = 400,
                    buy = () => PlayerStats.AddDamage(0.1f)
                });
                _goods.Add(new Goods
                {
                    id = "w_ed2", name = "刃口强化 II", desc = "近战伤害 再 +10%", price = 700,
                    buy = () => PlayerStats.AddDamage(0.1f)
                });
                _goods.Add(new Goods
                {
                    id = "w_grip", name = "轻量化握柄", desc = "移动速度 +8%", price = 500,
                    buy = () => PlayerStats.AddSpeed(0.08f)
                });
            }
            else
            {
                shopName = string.IsNullOrEmpty(shopName) ? "义体门诊" : shopName;
                _goods.Add(new Goods
                {
                    id = "c_jump", name = "神经反射插件", desc = "解锁空中二段跳", price = 900,
                    buy = () => { SaveSystem.Data.doubleJump = true; }
                });
                _goods.Add(new Goods
                {
                    id = "c_skin", name = "石英表皮", desc = "石化抗性 +10%", price = 500,
                    buy = () => PlayerStats.AddPetrifyResist(0.1f)
                });
                _goods.Add(new Goods
                {
                    id = "c_muscle", name = "肌纤维强化", desc = "移动速度 +8%", price = 350,
                    buy = () => PlayerStats.AddSpeed(0.08f)
                });
            }
        }

        public void Interact() { Open(); }

        void Open()
        {
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

        public bool Buy(int index)
        {
            if (index < 0 || index >= _goods.Count) return false;
            var g = _goods[index];
            if (Owned(g.id)) { LootToast.Show("「" + g.name + "」已经装上了。"); return false; }
            int price = Pricing.Apply(g.price);
            if (!Wallet.CanAfford(price)) { LootToast.Show("信用点不够，还差 " + (price - Wallet.Coins)); return false; }

            if (!Wallet.Spend(price)) return false;
            SaveSystem.Data.shopPurchases.Add(g.id);
            g.buy();
            SaveSystem.Save();
            LootToast.Show("购入「" + g.name + "」 · -" + price);
            var audio = AudioDirector.Instance;
            if (audio != null) audio.PlayAt(SfxId.Pickup, transform.position, 0.9f, 1.1f);
            RefreshText();
            return true;
        }

        static bool Owned(string id)
        {
            var l = SaveSystem.Data.shopPurchases;
            for (int i = 0; i < l.Count; i++) if (l[i] == id) return true;
            return false;
        }

        void Update()
        {
            if (!_open) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            for (int i = 0; i < _goods.Count && i < 9; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) { Buy(i); return; }
            }
        }

        void RefreshText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(shopName);
            sb.AppendLine("信用点 " + Wallet.Coins + Pricing.Tag(0));
            sb.AppendLine();
            for (int i = 0; i < _goods.Count; i++)
            {
                var g = _goods[i];
                int price = Pricing.Apply(g.price);
                string state = Owned(g.id) ? "已装备" : price + " 信用点";
                sb.AppendLine("[" + (i + 1) + "] " + g.name + " — " + g.desc + " · " + state);
            }
            sb.AppendLine();
            sb.AppendLine("按数字键购买 · Esc 离开");
            if (_body != null) _body.text = sb.ToString();
        }

        void BuildUi()
        {
            var go = new GameObject("ShopCanvas", typeof(RectTransform));
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
            trt.sizeDelta = new Vector2(1000f, 700f);
            _body = txtGo.AddComponent<Text>();
            _body.font = MedusaHorror.UI.CjkFont.Get();
            _body.fontSize = 32;
            _body.alignment = TextAnchor.MiddleLeft;
            _body.color = new Color(0.92f, 0.96f, 1f);
            _body.lineSpacing = 1.4f;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;

            MedusaHorror.UI.UiSkin.Apply(go.GetComponent<RectTransform>());
            go.SetActive(false);
        }
    }
}
