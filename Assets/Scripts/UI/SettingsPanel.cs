using System;
using UnityEngine;
using UnityEngine.UI;
using MedusaHorror.Core;
using MedusaHorror.Player;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 设置面板：音量（主/音乐/音效）、鼠标灵敏度、全屏。
    /// 数值写入 SaveSystem，退出时持久化。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        static SettingsPanel _instance;

        public static SettingsPanel Ensure()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("SettingsPanelRuntime");
            DontDestroyOnLoad(go);
            return go.AddComponent<SettingsPanel>();
        }

        CanvasGroup _group;
        bool _built;

        Slider _master, _music, _sfx, _sens;
        Text _masterVal, _musicVal, _sfxVal, _sensVal;
        Text _fullscreenLabel;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildUi();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public bool IsOpen { get { return _group != null && _group.alpha > 0.5f; } }

        public void Open()
        {
            if (_group == null) return;
            ApplyFromSave();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Debug.Log("[Settings] 打开设置面板");
        }

        public void Close()
        {
            if (_group == null) return;
            SaveSystem.Save();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Debug.Log("[Settings] 关闭设置面板，已保存");
        }

        void ApplyFromSave()
        {
            var d = SaveSystem.Data;
            if (_master != null) _master.SetValueWithoutNotify(d.masterVolume);
            if (_music != null) _music.SetValueWithoutNotify(d.musicVolume);
            if (_sfx != null) _sfx.SetValueWithoutNotify(d.sfxVolume);
            if (_sens != null) _sens.SetValueWithoutNotify(d.mouseSensitivity);
            RefreshLabels();
            ApplyAudio();
            ApplySensitivity();
            if (_fullscreenLabel != null)
                _fullscreenLabel.text = Screen.fullScreen ? "全屏" : "窗口化";
        }

        void RefreshLabels()
        {
            if (_masterVal != null) _masterVal.text = Mathf.RoundToInt(_master.value * 100f) + "%";
            if (_musicVal != null) _musicVal.text = Mathf.RoundToInt(_music.value * 100f) + "%";
            if (_sfxVal != null) _sfxVal.text = Mathf.RoundToInt(_sfx.value * 100f) + "%";
            if (_sensVal != null) _sensVal.text = _sens.value.ToString("F2") + "x";
        }

        void ApplyAudio()
        {
            var a = AudioDirector.Instance;
            if (a == null) return;
            a.SetBusVolume(AudioBus.Master, _master != null ? _master.value : 0.8f);
            a.SetBusVolume(AudioBus.Music, _music != null ? _music.value : 0.7f);
            a.SetBusVolume(AudioBus.Sfx, _sfx != null ? _sfx.value : 0.9f);
        }

        void ApplySensitivity()
        {
            float mul = _sens != null ? _sens.value : 1f;
            var readers = FindObjectsOfType<InputReader>();
            for (int i = 0; i < readers.Length; i++)
                readers[i].lookSensitivity = 0.12f * mul;
        }

        void OnMasterChanged(float v)
        {
            SaveSystem.Data.masterVolume = v;
            ApplyAudio();
            RefreshLabels();
        }

        void OnMusicChanged(float v)
        {
            SaveSystem.Data.musicVolume = v;
            ApplyAudio();
            RefreshLabels();
        }

        void OnSfxChanged(float v)
        {
            SaveSystem.Data.sfxVolume = v;
            ApplyAudio();
            RefreshLabels();
        }

        void OnSensChanged(float v)
        {
            SaveSystem.Data.mouseSensitivity = v;
            ApplySensitivity();
            RefreshLabels();
        }

        void ToggleFullscreen()
        {
            Screen.fullScreen = !Screen.fullScreen;
            SaveSystem.Data.fullscreen = Screen.fullScreen;
            if (_fullscreenLabel != null)
                _fullscreenLabel.text = Screen.fullScreen ? "全屏" : "窗口化";
            SaveSystem.Save();
        }

        // ---------------- UI 构建 ----------------

        void BuildUi()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("SettingsCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 240;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var dimRt = NewRect("SettingsDim", canvasGo.transform);
            Stretch(dimRt);
            var dim = dimRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.02f, 0.03f, 0.78f);
            dim.raycastTarget = true;

            var cardRt = NewRect("SettingsCard", canvasGo.transform);
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(720f, 560f);
            var card = cardRt.gameObject.AddComponent<Image>();
            card.color = new Color(0.07f, 0.07f, 0.08f, 0.97f);

            Font font = CjkFont.Get();

            var title = NewText("SettingsTitle", cardRt, font, 40, new Vector2(0f, 224f), new Vector2(660f, 54f));
            title.text = "设置";
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.94f, 0.90f, 0.84f, 1f);

            _master = MakeSlider(cardRt, font, "主音量", 190f, 0f, 1f, 0.8f, out _masterVal, OnMasterChanged);
            _music = MakeSlider(cardRt, font, "音乐", 110f, 0f, 1f, 0.7f, out _musicVal, OnMusicChanged);
            _sfx = MakeSlider(cardRt, font, "音效", 30f, 0f, 1f, 0.9f, out _sfxVal, OnSfxChanged);
            _sens = MakeSlider(cardRt, font, "鼠标灵敏度", -50f, 0.3f, 2.5f, 1f, out _sensVal, OnSensChanged);

            // 全屏切换
            var fsBtn = MakeButton(cardRt, font, "全屏", new Vector2(-200f, -150f), new Vector2(220f, 56f),
                new Color(0.18f, 0.18f, 0.20f, 0.96f), out _fullscreenLabel);
            fsBtn.onClick.AddListener(ToggleFullscreen);

            // 返回
            var back = MakeButton(cardRt, font, "返回", new Vector2(200f, -150f), new Vector2(220f, 56f),
                new Color(0.36f, 0.20f, 0.18f, 0.96f), out _);
            back.onClick.AddListener(Close);

            var tip = NewText("SettingsTip", cardRt, font, 20, new Vector2(0f, -222f), new Vector2(660f, 30f));
            tip.text = "设置会自动保存，下次启动继续生效";
            tip.color = new Color(0.66f, 0.63f, 0.60f, 0.9f);
        }

        Slider MakeSlider(Transform parent, Font font, string label, float y, float min, float max, float value,
            out Text valueText, UnityEngine.Events.UnityAction<float> onChange)
        {
            var rowRt = NewRect("Row_" + label, parent);
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(640f, 60f);
            rowRt.anchoredPosition = new Vector2(0f, y);

            var nameText = NewText("Label_" + label, rowRt, font, 24, new Vector2(-230f, 0f), new Vector2(180f, 34f));
            nameText.text = label;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.color = new Color(0.88f, 0.85f, 0.80f, 1f);

            // 滑条背景
            var sliderRt = NewRect("Slider_" + label, rowRt);
            sliderRt.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRt.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRt.pivot = new Vector2(0.5f, 0.5f);
            sliderRt.sizeDelta = new Vector2(340f, 10f);
            sliderRt.anchoredPosition = new Vector2(10f, 0f);
            var sliderBg = sliderRt.gameObject.AddComponent<Image>();
            sliderBg.color = new Color(0.22f, 0.21f, 0.20f, 1f);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;

            var fillArea = NewRect("FillArea", sliderRt);
            fillArea.anchorMin = new Vector2(0f, 0f);
            fillArea.anchorMax = new Vector2(1f, 1f);
            fillArea.offsetMin = new Vector2(0f, 0f);
            fillArea.offsetMax = new Vector2(0f, 0f);
            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(0.72f, 0.46f, 0.26f, 1f);

            var handleArea = NewRect("HandleArea", sliderRt);
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(6f, -8f);
            handleArea.offsetMax = new Vector2(-6f, 8f);
            var handle = NewRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(18f, 26f);
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.95f, 0.90f, 0.84f, 1f);

            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;

            valueText = NewText("Value_" + label, rowRt, font, 22, new Vector2(268f, 0f), new Vector2(90f, 32f));
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.color = new Color(0.86f, 0.82f, 0.76f, 1f);

            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        Button MakeButton(Transform parent, Font font, string label, Vector2 pos, Vector2 size, Color bg, out Text labelText)
        {
            var rt = NewRect("Btn_" + label, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var img = rt.gameObject.AddComponent<Image>();
            img.color = bg;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
            colors.pressedColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);
            btn.colors = colors;

            var labelRt = NewRect("Label", rt);
            Stretch(labelRt);
            labelText = labelRt.gameObject.AddComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 24;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.text = label;
            labelText.color = new Color(0.97f, 0.95f, 0.92f, 1f);
            labelText.raycastTarget = false;
            return btn;
        }

        Text NewText(string name, Transform parent, Font font, int size, Vector2 pos, Vector2 sizeDelta)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            var txt = rt.gameObject.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
