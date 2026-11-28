using MedusaHorror.Player;
using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.Gameplay
{
    /// <summary>
    /// 玩家石化系统：
    /// 1) 被凝视时累积石化值，满值进入石化（禁用移动）；
    /// 2) 石化中连按跳跃/交互键挣扎，达到次数或超时自动解脱；
    /// 3) 表现层为程序化生成的全屏石化纹路 + 挣扎进度提示。
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPetrify : MonoBehaviour
    {
        [Tooltip("难度配置资产（唯一数据源）：运行时覆盖石化速率与挣脱参数")]
        public DifficultyProfile profile;

        [Header("石化积累")]
        public float maxValue = 100f;
        [Tooltip("基础凝视速率（点/秒），实际速率 = 该值 × 视锥权重（约 0.45~1.55）")]
        public float gazeRate = 12f;
        [Tooltip("离开视线后的回落速率（点/秒）")]
        public float recoverRate = 8f;

        [Header("挣脱")]
        public int struggleRequired = 5;
        public float autoRelease = 5.5f;

        [Header("致死（P0-2：石化是本作唯一失败条件）")]
        [Tooltip("勾选后石化满值即死亡：石化定格 + 失败面板，不再自动挣脱")]
        public bool petrifyIsLethal = true;
        [Tooltip("石化满值到失败面板出现的定格观察时长（秒）")]
        public float lethalHold = 0.30f;

        [Header("表现")]
        public Color stoneColor = new Color(0.76f, 0.77f, 0.80f, 1f);

        public float value;
        public bool petrified;
        public float petrifiedTime;
        public int struggle;

        // P0-2：石化致死标记（true 表示本局已失败，等待 GameFlow 弹出失败面板）
        public bool dead;
        public bool Dead { get { return dead; } }

        // 诊断：本次凝视的实际速率与累积时长，用于实测"被凝视到满石化"的秒数
        public float lastRate;
        public float gazeTime;
        public float lastFillSeconds;

        public float Value { get { return value; } }
        public float Normalized { get { return maxValue <= 0f ? 0f : Mathf.Clamp01(value / maxValue); } }
        public bool Petrified { get { return petrified; } }
        /// <summary>半石化：累积达到 70% 且尚未完全石化时移动减速。</summary>
        public bool IsHalfPetrified { get { return !petrified && Normalized >= 0.7f; } }
        public int StruggleCount { get { return struggle; } }
        public int StruggleRequired { get { return struggleRequired; } }

        CharacterController controller;
        InputReader input;
        float flash;
        float hudAlpha;
        float hurtFlash;
        float damageAlpha;

        Canvas canvas;
        CanvasGroup overlayGroup;
        Image crackImage;
        Image damageImage;
        RectTransform crackRect;
        Image gazeFill;
        RectTransform gazeFillRect;
        Text stateText;
        GameObject strugglePanel;
        RectTransform struggleFillRect;
        Image struggleFill;
        Text struggleTitle;
        RectTransform autoFillRect;
        Image autoFill;
        Text countText;

        static Sprite _crackSprite;

        void Awake()
        {
            if (profile == null) profile = DifficultyProfile.Active;
            if (profile != null) profile.ApplyToPetrify(this);
            maxValue += MedusaHorror.Core.PlayerStats.PetrifyCapBonus;   // 等级带来的上限提升

            controller = GetComponent<CharacterController>();
            input = GetComponent<InputReader>();
            BuildHud();
        }

        void Start()
        {
            UpdateHud(0f);
        }

        /// <summary>直接削减石化值（消耗品用，比如石英粉）。</summary>
        public void ReduceValue(float amount)
        {
            if (amount <= 0f) return;
            value = Mathf.Max(0f, value - amount);
            lastRate = 0f;
        }

        float _incomingTotal,_incomingUntil;
        public void ApplyImpact(float amount)
        {
            if(dead||petrified||amount<=0)return;
            float actual=Mathf.Min(maxValue-value,amount*(1-MedusaHorror.Core.PlayerStats.PetrifyResist));
            value+=actual;hurtFlash=1;MedusaHorror.UI.CombatNumbers.Show(transform.position,actual,true);
            if(value>=maxValue)EnterPetrified();
        }
        public void ApplyGaze(float weightedRate, float dt)
        {
            if (petrified) return;
            if (dt > 0.1f) dt = 0.1f;   // 防止掉帧单帧大幅跳变
            lastRate = weightedRate;
            if (weightedRate > 0f) gazeTime += dt;
            // 任务奖励的石化抗性按比例削减累积速度
            float resistScale = 1f - MedusaHorror.Core.PlayerStats.PetrifyResist;
            float before = value;
            value = Mathf.Min(maxValue, value + weightedRate * dt * resistScale);
            if (weightedRate > 0f && value - before > 0.55f) hurtFlash = 1f;
            _incomingTotal+=Mathf.Max(0,value-before);
            if(Time.time>=_incomingUntil&&_incomingTotal>0.1f){MedusaHorror.UI.CombatNumbers.Show(transform.position,_incomingTotal,true);_incomingTotal=0;_incomingUntil=Time.time+0.5f;}
            if (value >= maxValue) EnterPetrified();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (petrified)
            {
                petrifiedTime += dt;
                if (!dead && input != null && (input.JumpPressed || input.InteractPressed))
                {
                    struggle++;
                    flash = 1f;
                }
                // 致死模式下永不挣脱：石化即死亡，失败面板由 GameFlow 接管
                if (!dead && (struggle >= struggleRequired || petrifiedTime >= autoRelease)) Release();
            }
            else
            {
                if (value > 0f) value = Mathf.Max(0f, value - recoverRate * dt);
                if (value <= 0f) { value = 0f; gazeTime = 0f; }
            }
            if (flash > 0f) flash = Mathf.Max(0f, flash - dt * 3.4f);
            if (hurtFlash > 0f) hurtFlash = Mathf.Max(0f, hurtFlash - dt * 1.8f);
            UpdatePetrifyAudio();
            UpdateHud(dt);
        }

        /// <summary>石化累积音随进度升高音调与音量；石化瞬间停循环、交满值爆裂音。</summary>
        void UpdatePetrifyAudio()
        {
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio == null) return;
            float n = maxValue > 0f ? value / maxValue : 0f;
            if (petrified) audio.SetPetrifyIntensity(0f);
            else audio.SetPetrifyIntensity(n);

            // 低血量等价于高石化：>=55% 开始心跳，越接近满值越快
            if (!petrified && n >= 0.55f) audio.SetHeartbeatIntensity(Mathf.InverseLerp(0.55f, 1f, n));
            else audio.SetHeartbeatIntensity(0f);
        }

        void EnterPetrified()
        {
            petrified = true;
            value = maxValue;
            lastFillSeconds = gazeTime;
            struggle = 0;
            petrifiedTime = 0f;
            flash = 1f;
            if (controller != null) controller.enabled = false;

            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null)
            {
                audio.SetPetrifyIntensity(0f);
                audio.Play(MedusaHorror.Core.SfxId.PetrifyBreak, 0.95f, 0.92f);
            }

            if (petrifyIsLethal)
            {
                dead = true;
                Debug.Log("[Petrify] 石化满值 → 死亡（累积耗时 " + lastFillSeconds.ToString("F2") + " 秒）");
                Invoke("TriggerDeath", Mathf.Max(0f, lethalHold));
                return;
            }

            Debug.Log("[Petrify] 石化！本次累积耗时 " + lastFillSeconds.ToString("F2") + " 秒" +
                      "（连按跳跃键挣脱，需 " + struggleRequired + " 次 / 最长 " + autoRelease + " 秒）");
        }

        /// <summary>石化致死：交给 GameFlow 弹出失败面板（重开 / 退出）。</summary>
        void TriggerDeath()
        {
            if (!dead) return;
            MedusaHorror.Core.GameFlow.FailRun("你被石化了",
                "石化值已满 · 美杜莎的凝视将你凝固成石（累积 " + lastFillSeconds.ToString("F1") + " 秒）");
        }

        void Release()
        {
            petrified = false;
            value = maxValue * 0.3f;
            gazeTime = 0f;
            if (controller != null) controller.enabled = true;
            var audio = MedusaHorror.Core.AudioDirector.Instance;
            if (audio != null)
            {
                audio.Play(MedusaHorror.Core.SfxId.PetrifyBreak, 0.55f, 1.22f);
                audio.SetPetrifyIntensity(value / maxValue);
            }
            Debug.Log("[Petrify] 挣脱石化（挣扎 " + struggle + "/" + struggleRequired + "，耗时 " + petrifiedTime.ToString("F1") + " 秒）");
        }

        // ---------------- UI ----------------

        void BuildHud()
        {
            var canvasGo = new GameObject("PetrifyCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 全屏石化纹路
            var crackGo = new GameObject("PetrifyCrackOverlay", typeof(RectTransform));
            crackGo.transform.SetParent(canvasGo.transform, false);
            crackRect = crackGo.GetComponent<RectTransform>();
            Stretch(crackRect);
            crackImage = crackGo.AddComponent<Image>();
            crackImage.sprite = GetCrackSprite();
            crackImage.type = Image.Type.Simple;
            crackImage.raycastTarget = false;
            crackImage.preserveAspect = false;
            crackImage.color = new Color(stoneColor.r, stoneColor.g, stoneColor.b, 0f);
            overlayGroup = crackGo.AddComponent<CanvasGroup>();
            overlayGroup.blocksRaycasts = false;

            // 受击红屏（红色暗角）：石化快速累积 / 濒临石化时浮现
            var dmgGo = new GameObject("DamageVignette", typeof(RectTransform));
            dmgGo.transform.SetParent(canvasGo.transform, false);
            var drt = dmgGo.GetComponent<RectTransform>();
            Stretch(drt);
            damageImage = dmgGo.AddComponent<Image>();
            damageImage.sprite = GetDamageSprite();
            damageImage.type = Image.Type.Simple;
            damageImage.raycastTarget = false;
            damageImage.color = new Color(1f, 0.1f, 0.08f, 0f);

            // 凝视累积条
            var barBg = MakeImage("GazeBarBg", canvasGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(460f, 14f), new Vector2(0f, -110f), new Color(0f, 0f, 0f, 0.55f));
            var fillGo = new GameObject("GazeBarFill", typeof(RectTransform));
            fillGo.transform.SetParent(barBg.transform, false);
            gazeFillRect = fillGo.GetComponent<RectTransform>();
            gazeFillRect.anchorMin = new Vector2(0f, 0f);
            gazeFillRect.anchorMax = new Vector2(0f, 1f);
            gazeFillRect.pivot = new Vector2(0f, 0.5f);
            gazeFillRect.offsetMin = new Vector2(1f, 1f);
            gazeFillRect.offsetMax = new Vector2(1f, -1f);
            gazeFill = fillGo.AddComponent<Image>();
            gazeFill.raycastTarget = false;
            gazeFill.color = new Color(0.72f, 0.74f, 0.68f, 0.95f);

            // 状态文字
            var txtGo = new GameObject("PetrifyStateText", typeof(RectTransform));
            txtGo.transform.SetParent(canvasGo.transform, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 0.5f);
            trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(900f, 40f);
            trt.anchoredPosition = new Vector2(0f, -128f);
            stateText = txtGo.AddComponent<Text>();
            stateText.font = BuiltinFont();
            stateText.fontSize = 24;
            stateText.alignment = TextAnchor.MiddleCenter;
            stateText.horizontalOverflow = HorizontalWrapMode.Overflow;
            stateText.raycastTarget = false;
            stateText.color = new Color(0.9f, 0.9f, 0.86f, 0f);
            stateText.text = string.Empty;

            BuildStrugglePanel(canvasGo.transform);
        }

        void BuildStrugglePanel(Transform parent)
        {
            var panel = new GameObject("StrugglePanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(760f, 220f);
            prt.anchoredPosition = new Vector2(0f, -190f);
            strugglePanel = panel;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(panel.transform, false);
            var trt = titleGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(760f, 44f);
            trt.anchoredPosition = new Vector2(0f, 0f);
            struggleTitle = titleGo.AddComponent<Text>();
            struggleTitle.font = BuiltinFont();
            struggleTitle.fontSize = 34;
            struggleTitle.alignment = TextAnchor.MiddleCenter;
            struggleTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            struggleTitle.raycastTarget = false;
            struggleTitle.color = new Color(1f, 0.96f, 0.90f, 1f);
            struggleTitle.text = "石化了！连按「空格」挣脱";

            // 挣扎进度条
            var sBg = MakeImage("StruggleBarBg", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(680f, 26f), new Vector2(0f, -12f), new Color(0f, 0f, 0f, 0.6f));
            var sFillGo = new GameObject("StruggleBarFill", typeof(RectTransform));
            sFillGo.transform.SetParent(sBg.transform, false);
            struggleFillRect = sFillGo.GetComponent<RectTransform>();
            struggleFillRect.anchorMin = new Vector2(0f, 0f);
            struggleFillRect.anchorMax = new Vector2(0f, 1f);
            struggleFillRect.pivot = new Vector2(0f, 0.5f);
            struggleFillRect.offsetMin = new Vector2(2f, 2f);
            struggleFillRect.offsetMax = new Vector2(2f, -2f);
            struggleFill = sFillGo.AddComponent<Image>();
            struggleFill.raycastTarget = false;
            struggleFill.color = new Color(0.98f, 0.82f, 0.36f, 0.96f);

            // 自动解脱倒计时条
            var aBg = MakeImage("AutoBarBg", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(680f, 8f), new Vector2(0f, -46f), new Color(0f, 0f, 0f, 0.45f));
            var aFillGo = new GameObject("AutoBarFill", typeof(RectTransform));
            aFillGo.transform.SetParent(aBg.transform, false);
            autoFillRect = aFillGo.GetComponent<RectTransform>();
            autoFillRect.anchorMin = new Vector2(0f, 0f);
            autoFillRect.anchorMax = new Vector2(1f, 1f);
            autoFillRect.pivot = new Vector2(0f, 0.5f);
            autoFillRect.offsetMin = new Vector2(1f, 1f);
            autoFillRect.offsetMax = new Vector2(-1f, -1f);
            autoFill = aFillGo.AddComponent<Image>();
            autoFill.raycastTarget = false;
            autoFill.color = new Color(0.62f, 0.66f, 0.70f, 0.8f);

            var cntGo = new GameObject("Count", typeof(RectTransform));
            cntGo.transform.SetParent(panel.transform, false);
            var crt = cntGo.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(680f, 36f);
            crt.anchoredPosition = new Vector2(0f, 0f);
            countText = cntGo.AddComponent<Text>();
            countText.font = BuiltinFont();
            countText.fontSize = 24;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.horizontalOverflow = HorizontalWrapMode.Overflow;
            countText.raycastTarget = false;
            countText.color = new Color(0.86f, 0.88f, 0.90f, 1f);

            panel.SetActive(false);
        }

        Image MakeImage(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot,
            Vector2 size, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            return img;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static Font BuiltinFont()
        {
            return MedusaHorror.UI.CjkFont.Get();
        }

        void UpdateHud(float dt)
        {
            float n = Normalized;

            // 凝视条
            if (gazeFillRect != null)
            {
                float w = 456f * n;
                gazeFillRect.offsetMax = new Vector2(w + 1f, -1f);
            }
            if (gazeFill != null)
            {
                gazeFill.color = petrified
                    ? new Color(0.95f, 0.55f, 0.25f, 0.95f)
                    : Color.Lerp(new Color(0.72f, 0.74f, 0.68f, 0.9f), new Color(0.92f, 0.32f, 0.24f, 0.95f), n);
            }

            // 全屏石化纹路：随累积逐渐浮现，石化时满覆盖
            float targetAlpha = petrified ? 1f : Mathf.SmoothStep(0f, 0.92f, Mathf.Clamp01((n - 0.18f) / 0.82f));
            hudAlpha = Mathf.MoveTowards(hudAlpha, targetAlpha, dt * (petrified ? 6f : 2.4f));
            if (crackImage != null)
            {
                Color c = petrified ? new Color(0.62f, 0.63f, 0.66f, 1f) : stoneColor;
                c.a = Mathf.Clamp01(hudAlpha + flash * 0.35f);
                crackImage.color = c;
            }
            if (crackRect != null)
            {
                float s = 1f + flash * 0.035f;
                crackRect.localScale = new Vector3(s, s, 1f);
            }
            if (overlayGroup != null) overlayGroup.alpha = Mathf.Clamp01(hudAlpha + flash * 0.3f);

            // 受击红屏：濒危度 + 突增脉冲
            float danger = petrified ? 1f : Mathf.Clamp01((n - 0.45f) / 0.55f);
            float dmgTarget = danger * 0.5f + hurtFlash * 0.5f;
            damageAlpha = Mathf.MoveTowards(damageAlpha, dmgTarget, dt * (hurtFlash > 0.4f ? 7f : 2.2f));
            if (damageImage != null)
            {
                var dc = damageImage.color;
                dc.a = Mathf.Clamp01(damageAlpha);
                damageImage.color = dc;
            }

            // 状态文字
            if (stateText != null)
            {
                if (petrified && dead)
                {
                    stateText.text = "石化已满 · 你无法动弹";
                    stateText.color = new Color(1f, 0.42f, 0.30f, 1f);
                }
                else if (petrified)
                {
                    stateText.text = "石化中 · 连按空格挣脱";
                    stateText.color = new Color(1f, 0.72f, 0.42f, 1f);
                }
                else if (IsHalfPetrified)
                {
                    stateText.text = "半石化 · 移动迟缓 " + Mathf.RoundToInt(n * 100f) + "%（快移出视线！）";
                    stateText.color = new Color(1f, 0.55f, 0.40f, 0.85f);
                }
                else if (n > 0.02f)
                {
                    stateText.text = "被凝视 · 石化 " + Mathf.RoundToInt(n * 100f) + "%";
                    stateText.color = new Color(0.90f, 0.90f, 0.86f, 0.5f + 0.5f * n);
                }
                else
                {
                    stateText.text = string.Empty;
                    stateText.color = new Color(0.9f, 0.9f, 0.86f, 0f);
                }
            }

            // 挣扎面板
            if (strugglePanel != null)
            {
                bool show = petrified && !dead;
                if (strugglePanel.activeSelf != show) strugglePanel.SetActive(show);
                if (show)
                {
                    float p = struggleRequired <= 0 ? 0f : Mathf.Clamp01((float)struggle / struggleRequired);
                    if (struggleFillRect != null) struggleFillRect.offsetMax = new Vector2(676f * p + 2f, -2f);
                    if (struggleFill != null)
                        struggleFill.color = Color.Lerp(new Color(0.98f, 0.82f, 0.36f, 0.96f),
                            new Color(1f, 0.42f, 0.28f, 0.98f), p);
                    float auto = autoRelease <= 0f ? 0f : Mathf.Clamp01(1f - petrifiedTime / autoRelease);
                    if (autoFillRect != null)
                    {
                        autoFillRect.anchorMin = new Vector2(0f, 0f);
                        autoFillRect.anchorMax = new Vector2(auto, 1f);
                        autoFillRect.offsetMin = new Vector2(1f, 1f);
                        autoFillRect.offsetMax = new Vector2(-1f, -1f);
                    }
                    if (countText != null)
                        countText.text = "挣扎 " + struggle + " / " + struggleRequired +
                                         "　·　" + Mathf.Max(0f, autoRelease - petrifiedTime).ToString("F1") + "s 后自动解脱";
                    if (struggleTitle != null)
                    {
                        float pulse = 0.86f + 0.14f * Mathf.Sin(Time.time * 8f);
                        struggleTitle.color = new Color(1f, 0.96f * pulse, 0.90f * pulse, 1f);
                    }
                }
            }
        }

        // ---------------- 程序化石化纹路 ----------------

        static Sprite _damageSprite;

        static Sprite GetDamageSprite()
        {
            if (_damageSprite != null) return _damageSprite;
            var tex = BuildDamageTexture(256);
            _damageSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _damageSprite.name = "DamageVignette";
            return _damageSprite;
        }

        static Texture2D BuildDamageTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x / (float)size - 0.5f) * 2f;
                    float v = (y / (float)size - 0.5f) * 2f;
                    float rad = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.Clamp01((rad - 0.42f) / 0.58f);
                    a = a * a * 1.15f;
                    px[y * size + x] = new Color32(255, 40, 32, (byte)(Mathf.Clamp01(a) * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static Sprite GetCrackSprite()
        {
            if (_crackSprite != null) return _crackSprite;
            var tex = BuildCrackTexture(512);
            _crackSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            _crackSprite.name = "PetrifyCrack";
            return _crackSprite;
        }

        static Texture2D BuildCrackTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var crack = new float[size * size];
            var rnd = new System.Random(20261119);

            // 主裂缝：从随机边缘出发随机游走，带少量分支
            int mainLines = 7;
            for (int i = 0; i < mainLines; i++)
            {
                float x = (float)rnd.NextDouble() * size;
                float y = (float)rnd.NextDouble() * size;
                float ang = (float)(rnd.NextDouble() * Mathf.PI * 2f);
                int steps = size * 2;
                for (int s = 0; s < steps; s++)
                {
                    ang += (float)(rnd.NextDouble() - 0.5) * 0.55f;
                    x += Mathf.Cos(ang) * 1.2f;
                    y += Mathf.Sin(ang) * 1.2f;
                    if (x < 0 || y < 0 || x >= size || y >= size) break;
                    Stamp(crack, size, (int)x, (int)y, 1, 1f);
                    if (rnd.NextDouble() < 0.05)
                    {
                        // 分支
                        float bx = x, by = y, ba = ang + (float)(rnd.NextDouble() - 0.5) * 1.8f;
                        int bsteps = (int)(size * 0.5f);
                        for (int b = 0; b < bsteps; b++)
                        {
                            ba += (float)(rnd.NextDouble() - 0.5) * 0.6f;
                            bx += Mathf.Cos(ba) * 1.1f;
                            by += Mathf.Sin(ba) * 1.1f;
                            if (bx < 0 || by < 0 || bx >= size || by >= size) break;
                            Stamp(crack, size, (int)bx, (int)by, 1, 0.55f);
                        }
                    }
                }
            }

            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    float u = (x / (float)size - 0.5f) * 2f;
                    float v = (y / (float)size - 0.5f) * 2f;
                    float rad = Mathf.Sqrt(u * u + v * v);
                    float vig = Mathf.Clamp01((rad - 0.42f) / 0.58f);

                    float shade = Mathf.PerlinNoise(x * 0.014f + 11.3f, y * 0.014f + 7.7f) * 0.55f
                                + Mathf.PerlinNoise(x * 0.052f + 3.1f, y * 0.052f + 19.4f) * 0.30f
                                + Mathf.PerlinNoise(x * 0.160f + 31.7f, y * 0.160f + 5.2f) * 0.15f;
                    shade = Mathf.Clamp01(0.45f + shade * 0.55f);

                    float a = Mathf.Clamp01(crack[i] * 0.9f + vig * 0.62f + 0.045f);
                    byte r = (byte)(Mathf.Clamp01(shade * 0.90f) * 255f);
                    byte g = (byte)(Mathf.Clamp01(shade * 0.91f) * 255f);
                    byte bl = (byte)(Mathf.Clamp01(shade * 0.95f) * 255f);
                    px[i] = new Color32(r, g, bl, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static void Stamp(float[] buf, int size, int cx, int cy, int radius, float strength)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= size || y >= size) continue;
                    int i = y * size + x;
                    if (buf[i] < strength) buf[i] = strength;
                }
            }
        }
    }
}
