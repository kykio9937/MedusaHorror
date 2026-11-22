using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 地铁随机遭遇：逐层深入的迷你 roguelike。
    /// 每层随机商人/流浪者/怪物，风险与收益随层数上升，可随时撤退。
    /// </summary>
    public class SubwayEncounter : MonoBehaviour, IInteractable
    {
        public string encounterName = "废弃站台";
        public int maxDepth = 5;

        int _depth;
        int _event;
        bool _open;
        bool _resolved;
        Canvas _canvas;
        Text _body;
        System.Random _rng = new System.Random();

        public string Hint { get { return encounterName + " · 按 E 探索"; } }
        public bool CanInteract { get { return true; } }
        public bool IsOpen { get { return _open; } }
        public int Depth { get { return _depth; } }

        public void Interact() { Open(); }

        void Open()
        {
            _open = true;
            _resolved = false;
            _depth = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            NextLayer();
            if (_canvas != null) _canvas.gameObject.SetActive(true);
        }

        public void Close()
        {
            _open = false;
            if (_canvas != null) _canvas.gameObject.SetActive(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>调试用：强制当前层事件类型（0 商人 1 流浪者 2 怪物）。</summary>
        public void ForceEvent(int e)
        {
            _event = Mathf.Clamp(e, 0, 2);
            Refresh();
        }

        void NextLayer()
        {
            _depth++;
            if (_depth > maxDepth) { Finish(true); return; }
            _event = _rng.Next(0, 3);
            Refresh();
        }

        /// <summary>选择当前层的选项（0 或 1）。</summary>
        public void Choose(int option)
        {
            if (!_open || _resolved) return;

            if (_event == 0) ChooseMerchant(option);
            else if (_event == 1) ChooseWanderer(option);
            else ChooseMonster(option);
        }

        void ChooseMerchant(int option)
        {
            if (option == 0)
            {
                int price = Pricing.Apply(150);
                if (!Wallet.Spend(price)) { LootToast.Show("信用点不够，贩子把布收了回去。"); return; }
                PlayerStats.AddPetrifyResist(0.05f);
                Inventory.Add("quartz", "石英粉", 1, "研磨过的石英粉，贴着皮肤能压住石化的蔓延", "消耗");
                LootToast.Show("买到一罐石英粉 · 石化抗性 +5%");
            }
            else
            {
                LootToast.Show("你摆摆手，继续往前走。");
            }
            NextLayer();
        }

        void ChooseWanderer(int option)
        {
            if (option == 0)
            {
                int gain = 100 + _rng.Next(0, 121);
                Wallet.Add(gain);
                LootToast.Show("他从口袋里摸出几张皱钞塞给你 · +" + gain);
            }
            else
            {
                PlayerStats.AddSpeed(0.04f);
                LootToast.Show("他讲了一条近路 · 移动速度 +4%");
            }
            NextLayer();
        }

        void ChooseMonster(int option)
        {
            if (option == 0)
            {
                int reward = 200 + _depth * 60;
                // 层数越深越危险：成功概率随深度下降
                int safe = Mathf.Clamp(75 - _depth * 9, 30, 75);
                if (_rng.Next(0, 100) < safe)
                {
                    Wallet.Add(reward);
                    LootToast.Show("你从它身上摸走了东西 · +" + reward);
                }
                else
                {
                    var pz = FindObjectOfType<PlayerPetrify>();
                    if (pz != null) pz.value = Mathf.Min(pz.maxValue - 1f, pz.value + 28f);
                    LootToast.Show("它的眼睛扫过你 · 石化 +28");
                }
                NextLayer();
            }
            else
            {
                Finish(false);
            }
        }

        void Finish(bool cleared)
        {
            _resolved = true;
            var d = SaveSystem.Data;
            int reached = Mathf.Min(_depth, maxDepth);
            if (reached > d.subwayBestDepth) { d.subwayBestDepth = reached; SaveSystem.Save(); }
            if (cleared)
            {
                Wallet.Add(200);
                LootToast.Show("走到站台尽头 · 额外的 200 信用点");
            }
            if (_body != null)
                _body.text = (cleared ? "站台尽头是一道铁门，推不开。\n\n" : "你退回了亮处，心跳还在耳朵里。\n\n") +
                             "最深处：" + SaveSystem.Data.subwayBestDepth + " 层\n\n—— 按 Esc 离开 ——";
        }

        void Update()
        {
            if (!_open) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(); return; }
            if (_resolved) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Choose(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) Choose(1);
        }

        void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(encounterName + " · 第 " + _depth + " / " + maxDepth + " 层");
            sb.AppendLine();

            if (_event == 0)
            {
                sb.AppendLine("站台角落有人支起一块防水布，摆着几罐没标签的补给。");
                sb.AppendLine();
                sb.AppendLine("[1] 买一罐石英粉 · " + Pricing.Apply(150) + " 信用点（石化抗性 +5%）");
                sb.AppendLine("[2] 摆摆手走过去");
            }
            else if (_event == 1)
            {
                sb.AppendLine("一个裹着毯子的人坐在长椅上，盯着你看。");
                sb.AppendLine();
                sb.AppendLine("[1] 伸手要钱");
                sb.AppendLine("[2] 听他说说这条隧道");
            }
            else
            {
                sb.AppendLine("隧道深处传来爪子刮混凝土的声音，越来越近。");
                sb.AppendLine();
                sb.AppendLine("[1] 迎着声音走过去（有风险，第 " + _depth + " 层奖励 " + (200 + _depth * 60) + "）");
                sb.AppendLine("[2] 退回亮处，结束这次探索");
            }

            sb.AppendLine();
            sb.AppendLine("按数字键选择 · Esc 离开");
            if (_body != null) _body.text = sb.ToString();
        }

        void Awake() { BuildUi(); }

        void BuildUi()
        {
            var go = new GameObject("EncounterCanvas", typeof(RectTransform));
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
            trt.sizeDelta = new Vector2(1100f, 700f);
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
