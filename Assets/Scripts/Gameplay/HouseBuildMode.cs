using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.Gameplay
{
    /// <summary>家具类型表：建造菜单、几何体生成、功能都从这里取。</summary>
    public static class FurnitureKit
    {
        public class Def
        {
            public string id;
            public string name;
            public int price;
            public string func;
            public Color color;
        }

        public static readonly Def[] All =
        {
            new Def { id = "cabinet", name = "储物柜", price = 180, func = "存 / 取信用点", color = new Color(0.34f, 0.26f, 0.18f) },
            new Def { id = "bed",     name = "床",     price = 220, func = "休息：存档并清空石化", color = new Color(0.55f, 0.20f, 0.26f) },
            new Def { id = "bench",   name = "工作台", price = 320, func = "花 300 信用点永久 +5% 伤害", color = new Color(0.28f, 0.30f, 0.34f) },
            new Def { id = "lamp",    name = "落地灯", price = 120, func = "照明", color = new Color(1f, 0.86f, 0.55f) },
            new Def { id = "shelf",   name = "书架",   price = 200, func = "查看收集统计", color = new Color(0.30f, 0.22f, 0.14f) },
            new Def { id = "plant",   name = "盆栽",   price = 90,  func = "移速 +1%（最多 5 盆）", color = new Color(0.20f, 0.45f, 0.24f) },
        };

        public static Def Find(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].id == id) return All[i];
            return null;
        }

        public static GameObject Build(string id, Transform parent, Vector3 pos, float yaw, string houseId)
        {
            var def = Find(id);
            if (def == null) return null;

            var root = new GameObject("Furniture_" + id);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            switch (id)
            {
                case "cabinet": BuildCabinet(root, def.color); break;
                case "bed": BuildBed(root, def.color); break;
                case "bench": BuildBench(root, def.color); break;
                case "lamp": BuildLamp(root, def.color); break;
                case "shelf": BuildShelf(root, def.color); break;
                default: BuildPlant(root, def.color); break;
            }

            var f = root.AddComponent<HouseFurniture>();
            f.furnitureId = id;
            f.houseId = houseId;
            return root;
        }

        static Material Mat(Color c, float emission = 0f, float smooth = 0.2f)
        {
            var sh = Shader.Find("Standard");
            var m = new Material(sh != null ? sh : Shader.Find("Diffuse"));
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (emission > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emission);
            }
            return m;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static void AddBody(GameObject root, Vector3 center, Vector3 size)
        {
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = center;
            col.size = size;
        }

        static void BuildCabinet(GameObject root, Color c)
        {
            var m = Mat(c);
            Box(root.transform, "Body", new Vector3(0f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 0.6f), m);
            var drawer = Mat(c * 1.25f);
            for (int i = 0; i < 3; i++)
                Box(root.transform, "Drawer" + i, new Vector3(0f, 0.35f + i * 0.55f, 0.31f), new Vector3(1.0f, 0.42f, 0.04f), drawer);
            AddBody(root, new Vector3(0f, 0.9f, 0f), new Vector3(1.2f, 1.8f, 0.7f));
        }

        static void BuildBed(GameObject root, Color c)
        {
            var frame = Mat(new Color(0.25f, 0.20f, 0.18f));
            var sheet = Mat(c);
            Box(root.transform, "Frame", new Vector3(0f, 0.22f, 0f), new Vector3(1.5f, 0.36f, 2.1f), frame);
            Box(root.transform, "Mattress", new Vector3(0f, 0.46f, 0f), new Vector3(1.42f, 0.18f, 2.0f), sheet);
            Box(root.transform, "Pillow", new Vector3(0f, 0.58f, 0.75f), new Vector3(0.9f, 0.14f, 0.4f), Mat(Color.white));
            Box(root.transform, "Head", new Vector3(0f, 0.62f, -1.0f), new Vector3(1.5f, 0.9f, 0.1f), frame);
            AddBody(root, new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1.0f, 2.1f));
        }

        static void BuildBench(GameObject root, Color c)
        {
            var m = Mat(c);
            Box(root.transform, "Top", new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 0.1f, 0.8f), m);
            var leg = Mat(c * 0.8f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.72f : 0.72f;
                float z = (i < 2) ? -0.32f : 0.32f;
                Box(root.transform, "Leg" + i, new Vector3(x, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), leg);
            }
            Box(root.transform, "Tool", new Vector3(0.45f, 1.02f, 0f), new Vector3(0.35f, 0.14f, 0.25f), Mat(new Color(0.6f, 0.62f, 0.66f), 0f, 0.6f));
            AddBody(root, new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 0.9f));
        }

        static void BuildLamp(GameObject root, Color c)
        {
            var pole = Mat(new Color(0.3f, 0.3f, 0.32f), 0f, 0.5f);
            Box(root.transform, "Base", new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.08f, 0.5f), pole);
            Box(root.transform, "Pole", new Vector3(0f, 0.85f, 0f), new Vector3(0.08f, 1.7f, 0.08f), pole);
            Box(root.transform, "Shade", new Vector3(0f, 1.78f, 0f), new Vector3(0.55f, 0.42f, 0.55f), Mat(c, 1.8f));

            var lightGo = new GameObject("LampLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 6f;
            l.intensity = 1.4f;
            l.color = new Color(1f, 0.88f, 0.62f);
            l.shadows = LightShadows.None;

            AddBody(root, new Vector3(0f, 0.9f, 0f), new Vector3(0.6f, 1.9f, 0.6f));
        }

        static void BuildShelf(GameObject root, Color c)
        {
            var m = Mat(c);
            Box(root.transform, "Back", new Vector3(0f, 1.0f, -0.18f), new Vector3(1.4f, 2.0f, 0.06f), m);
            for (int i = 0; i < 4; i++)
                Box(root.transform, "Board" + i, new Vector3(0f, 0.1f + i * 0.6f, 0f), new Vector3(1.4f, 0.06f, 0.38f), m);
            Box(root.transform, "SideL", new Vector3(-0.67f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.38f), m);
            Box(root.transform, "SideR", new Vector3(0.67f, 1.0f, 0f), new Vector3(0.06f, 2.0f, 0.38f), m);

            var rnd = new System.Random(7);
            Color[] cols = { new Color(0.7f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.7f), new Color(0.7f, 0.65f, 0.3f), new Color(0.4f, 0.6f, 0.4f) };
            for (int s = 0; s < 3; s++)
            {
                float x = -0.55f;
                while (x < 0.55f)
                {
                    float w = 0.06f + (float)rnd.NextDouble() * 0.06f;
                    float h = 0.28f + (float)rnd.NextDouble() * 0.16f;
                    Box(root.transform, "Book", new Vector3(x, 0.14f + s * 0.6f + h * 0.5f, 0f),
                        new Vector3(w, h, 0.24f), Mat(cols[rnd.Next(cols.Length)]));
                    x += w + 0.02f;
                }
            }
            AddBody(root, new Vector3(0f, 1.0f, 0f), new Vector3(1.4f, 2.0f, 0.45f));
        }

        static void BuildPlant(GameObject root, Color c)
        {
            var pot = Mat(new Color(0.42f, 0.26f, 0.20f));
            Box(root.transform, "Pot", new Vector3(0f, 0.18f, 0f), new Vector3(0.44f, 0.36f, 0.44f), pot);
            var leaf = Mat(c, 0.15f);
            for (int i = 0; i < 4; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Leaf" + i;
                s.transform.SetParent(root.transform, false);
                float a = i * Mathf.PI * 0.5f;
                s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.16f, 0.55f + i * 0.06f, Mathf.Sin(a) * 0.16f);
                s.transform.localScale = new Vector3(0.34f, 0.4f, 0.34f);
                Object.Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = leaf;
            }
            AddBody(root, new Vector3(0f, 0.45f, 0f), new Vector3(0.55f, 1.0f, 0.55f));
        }
    }

    /// <summary>已放置的家具：每种都有自己的功能，按 E 使用。</summary>
    public class HouseFurniture : MonoBehaviour, IInteractable
    {
        public string furnitureId = "cabinet";
        public string houseId = "W4_Apartment_7B";

        InputReader _input;

        void Awake()
        {
            _input = GetComponentInParent<InputReader>();
        }

        InputReader Input2
        {
            get
            {
                if (_input == null) _input = Object.FindObjectOfType<InputReader>();
                return _input;
            }
        }

        public string Hint
        {
            get
            {
                var def = FurnitureKit.Find(furnitureId);
                string n = def != null ? def.name : furnitureId;
                switch (furnitureId)
                {
                    case "cabinet": return n + " · E 存入 100 / Shift+E 取出（已存 " + SaveSystem.Data.houseStorage + "）";
                    case "bed": return n + " · E 休息存档";
                    case "bench": return n + " · E 强化武器（300 信用点 / +5% 伤害）";
                    case "shelf": return n + " · E 查看收藏";
                    case "plant": return n + " · 移速加成已生效";
                    default: return n + " · E 开关灯";
                }
            }
        }

        public bool CanInteract { get { return true; } }

        public void Interact()
        {
            switch (furnitureId)
            {
                case "cabinet": DoCabinet(); break;
                case "bed": DoBed(); break;
                case "bench": DoBench(); break;
                case "shelf": DoShelf(); break;
                case "plant": LootToast.Show("这盆绿植让你在屋里走得更轻快。"); break;
                default: DoLamp(); break;
            }
        }

        void DoCabinet()
        {
            const int step = 100;
            bool take = Input2 != null && Input2.RunHeld;
            if (take)
            {
                if (SaveSystem.Data.houseStorage < step) { LootToast.Show("柜子里没有可取的信用点"); return; }
                SaveSystem.Data.houseStorage -= step;
                Wallet.Add(step);
                SaveSystem.Save();
                LootToast.Show("取出 " + step + " · 柜内余 " + SaveSystem.Data.houseStorage);
            }
            else
            {
                if (!Wallet.CanAfford(step)) { LootToast.Show("信用点不足 · 需要 " + step); return; }
                Wallet.Spend(step);
                SaveSystem.Data.houseStorage += step;
                SaveSystem.Save();
                LootToast.Show("存入 " + step + " · 柜内共 " + SaveSystem.Data.houseStorage);
            }
        }

        void DoBed()
        {
            SaveSystem.Save();
            var pz = Object.FindObjectOfType<PlayerPetrify>();
            if (pz != null)
            {
                pz.value = 0f;
                pz.petrified = false;
                pz.dead = false;
            }
            LootToast.Show("睡了一觉 · 石化清空，进度已保存");
            AudioDirector.Instance?.PlayAt(SfxId.Pickup, transform.position, 0.7f, 0.85f);
        }

        void DoBench()
        {
            const int cost = 300;
            if (!Wallet.CanAfford(cost)) { LootToast.Show("信用点不足 · 强化需要 " + cost); return; }
            if (SaveSystem.Data.damageBonus >= 0.8f) { LootToast.Show("武器已经强化到极限了。"); return; }
            Wallet.Spend(cost);
            PlayerStats.AddDamage(0.05f);
            LootToast.Show("武器强化完成 · 伤害 +5%（累计 +" + Mathf.RoundToInt(SaveSystem.Data.damageBonus * 100f) + "%）");
            AudioDirector.Instance?.PlayAt(SfxId.UiClick, transform.position, 0.9f, 0.8f);
        }

        void DoShelf()
        {
            var d = SaveSystem.Data;
            var sb = new StringBuilder();
            sb.Append("收藏 · 击杀 ").Append(d.killCount)
              .Append(" · 通关 ").Append(SaveSystem.ClearedCount)
              .Append(" · 结局 ").Append(d.endingsSeen.Count)
              .Append(" · 家具 ").Append(d.furniturePlaced.Count);
            LootToast.Show(sb.ToString());
        }

        void DoLamp()
        {
            var l = GetComponentInChildren<Light>();
            if (l == null) return;
            l.enabled = !l.enabled;
            LootToast.Show(l.enabled ? "灯亮了。" : "灯灭了。");
        }
    }

    /// <summary>
    /// 房屋建造模式：站在自己屋里按 B 打开建造菜单，数字键选家具，直接摆到身前。
    /// 摆放记录写进存档，下次进屋自动恢复。
    /// </summary>
    public class HouseBuildMode : MonoBehaviour
    {
        public string houseId = "W4_Apartment_7B";
        public Vector3 houseCenter;
        public float houseRadius = 12f;

        static HouseBuildMode _instance;

        Canvas _canvas;
        Text _title;
        Text _body;
        Text _hint;
        bool _open;

        void Awake()
        {
            _instance = this;
            if (houseCenter == Vector3.zero) houseCenter = transform.position;
            Restore();
            Build();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_open) Close();
        }

        public bool Owned
        {
            get
            {
                var l = SaveSystem.Data.houses;
                for (int i = 0; i < l.Count; i++) if (l[i] == houseId) return true;
                return false;
            }
        }

        bool PlayerInside
        {
            get
            {
                var p = PlayerRef.Transform;
                if (p == null) return false;
                Vector3 d = p.position - houseCenter; d.y = 0f;
                return d.magnitude <= houseRadius;
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.B))
            {
                if (_open) { Close(); return; }
                if (!Owned) { LootToast.Show("先买下这套房子，才能布置家具。"); return; }
                if (!PlayerInside) { LootToast.Show("回到自己的屋里再布置。"); return; }
                Open();
                return;
            }
            if (!_open) return;

            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }

            for (int i = 0; i < FurnitureKit.All.Length && i < 9; i++)
            {
                if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) continue;
                Place(FurnitureKit.All[i]);
                return;
            }
        }

        void Place(FurnitureKit.Def def)
        {
            if (def.id == "plant")
            {
                int count = 0;
                for (int i = 0; i < SaveSystem.Data.furniturePlaced.Count; i++)
                    if (SaveSystem.Data.furniturePlaced[i].Contains("|plant|")) count++;
                if (count >= 5) { LootToast.Show("盆栽已经够多了（上限 5 盆）。"); return; }
            }

            int cost = Pricing.Apply(def.price);
            if (!Wallet.CanAfford(cost)) { LootToast.Show("信用点不足 · 需要 " + cost); return; }

            var player = PlayerRef.Transform;
            if (player == null) return;

            Vector3 fwd = player.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 pos = player.position + fwd.normalized * 2.2f;
            pos.y = houseCenter.y;
            float yaw = player.eulerAngles.y + 180f;

            Wallet.Spend(cost);
            var go = FurnitureKit.Build(def.id, transform, pos, yaw, houseId);
            SavePlacement(def.id, pos, yaw);
            SaveSystem.Save();

            if (def.id == "plant") PlayerStats.AddSpeed(0.01f);

            LootToast.Show("摆好了「" + def.name + "」 · -" + cost + " 信用点");
            AudioDirector.Instance?.PlayAt(SfxId.UiClick, pos, 0.9f, 1.1f);
            Refresh();
        }

        void SavePlacement(string id, Vector3 pos, float yaw)
        {
            string rec = houseId + "|" + id + "|" + pos.x.ToString("F2") + "|" + pos.y.ToString("F2") + "|"
                         + pos.z.ToString("F2") + "|" + yaw.ToString("F1");
            SaveSystem.Data.furniturePlaced.Add(rec);
        }

        void Restore()
        {
            var list = SaveSystem.Data.furniturePlaced;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var parts = list[i].Split('|');
                if (parts.Length < 6) continue;
                if (parts[0] != houseId) continue;
                Vector3 pos;
                float yaw;
                if (!float.TryParse(parts[2], out pos.x)) continue;
                if (!float.TryParse(parts[3], out pos.y)) continue;
                if (!float.TryParse(parts[4], out pos.z)) continue;
                if (!float.TryParse(parts[5], out yaw)) continue;
                FurnitureKit.Build(parts[1], transform, pos, yaw, houseId);
            }
        }

        // ---------------------------------------------------------------- 调试入口

        public bool DebugOpen() { if (!Owned) return false; Open(); return _open; }
        public bool DebugClose() { Close(); return !_open; }
        public bool DebugPlace(int i)
        {
            if (i < 0 || i >= FurnitureKit.All.Length) return false;
            Place(FurnitureKit.All[i]);
            return true;
        }

        // ---------------------------------------------------------------- UI

        void Open()
        {
            _open = true;
            InputReader.blocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Refresh();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        void Close()
        {
            _open = false;
            InputReader.blocked = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Refresh()
        {
            if (_body == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("按数字键把家具摆到身前，直接扣信用点。");
            sb.AppendLine();
            for (int i = 0; i < FurnitureKit.All.Length; i++)
            {
                var d = FurnitureKit.All[i];
                sb.AppendLine("[" + (i + 1) + "] " + d.name + "　" + Pricing.Apply(d.price) + " 信用点");
                sb.AppendLine("        " + d.func);
            }
            sb.AppendLine();
            sb.AppendLine("已有信用点 " + Wallet.Coins + " · 已摆家具 " + SaveSystem.Data.furniturePlaced.Count + " 件");
            _body.text = sb.ToString();
            if (_title != null) _title.text = "布置房间";
        }

        void Build()
        {
            var go = new GameObject("BuildCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 210;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var brt = bg.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var img = bg.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.05f, 0.97f);

            var font = MedusaHorror.UI.CjkFont.Get();
            _title = Text(go.transform, font, 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 60f), new Color(1f, 0.85f, 0.35f));
            _body = Text(go.transform, font, 28, TextAnchor.UpperLeft, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1100f, 700f), new Color(0.92f, 0.96f, 1f));
            _hint = Text(go.transform, font, 24, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1200f, 40f), new Color(0.62f, 0.70f, 0.80f));
            _hint.text = "1-6 选择家具 · B / Esc 关闭";

            MedusaHorror.UI.UiSkin.Apply(go.GetComponent<RectTransform>());
            go.SetActive(false);
        }

        static Text Text(Transform parent, Font font, int size, TextAnchor anchor, Vector2 anchorPos, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorPos; rt.anchorMax = anchorPos;
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
