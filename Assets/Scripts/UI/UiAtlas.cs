using UnityEngine;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 素材包 UI 图集：统一从 Resources/UI 加载并生成 Sprite。
    /// 运行时用 Sprite.Create 生成，不依赖贴图的导入类型。
    /// </summary>
    public static class UiAtlas
    {
        static Sprite _frame;
        static Sprite _icon;
        static Sprite _buttonOn;
        static Sprite _buttonOff;
        static Sprite _arrow;
        static Sprite _indicator;
        static Sprite _emo;

        /// <summary>浅蓝细线面板框（九宫格）。</summary>
        public static Sprite Frame { get { if (_frame == null) _frame = Make("UI/frame", 20, 20, 20, 20); return _frame; } }

        /// <summary>六边形图标格底。</summary>
        public static Sprite Icon { get { if (_icon == null) _icon = Make("UI/icon", 0, 0, 0, 0); return _icon; } }

        /// <summary>按钮·选中态。</summary>
        public static Sprite ButtonOn { get { if (_buttonOn == null) _buttonOn = Make("UI/anim_button1", 34, 34, 34, 34); return _buttonOn; } }

        /// <summary>按钮·常态。</summary>
        public static Sprite ButtonOff { get { if (_buttonOff == null) _buttonOff = Make("UI/anim_button3", 34, 34, 34, 34); return _buttonOff; } }

        /// <summary>箭头按钮（向上，使用时按需旋转）。</summary>
        public static Sprite Arrow { get { if (_arrow == null) _arrow = Make("UI/arrowButton", 0, 0, 0, 0); return _arrow; } }

        /// <summary>四角取景框，用作选中高亮。</summary>
        public static Sprite Indicator { get { if (_indicator == null) _indicator = Make("UI/indicator", 22, 22, 22, 22); return _indicator; } }

        /// <summary>文字条底衬。</summary>
        public static Sprite Emo { get { if (_emo == null) _emo = Make("UI/emo", 18, 18, 18, 18); return _emo; } }

        static Sprite Make(string path, float left, float bottom, float right, float top)
        {
            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;

            var border = new Vector4(left, bottom, right, top);
            var s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            s.name = "Ui_" + System.IO.Path.GetFileName(path);
            return s;
        }
    }
}
