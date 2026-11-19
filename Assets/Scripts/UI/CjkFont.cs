using UnityEngine;
using UnityEngine.UI;

namespace MedusaHorror.UI
{
    /// <summary>
    /// 中文字体提供器（P0-1）。
    /// 工程内所有 legacy UI.Text 都经由此处取字体：
    /// 优先 Assets/Resources/Fonts 下的随包中文字体（dynamic 模式，可显示任意汉字），
    /// 找不到时回退 Unity 内置 LegacyRuntime/Arial 并在控制台告警。
    /// </summary>
    public static class CjkFont
    {
        public const string ResourcePath = "Fonts/simhei";

        static Font _font;
        static bool _searched;

        public static Font Get()
        {
            if (_searched) return _font;
            _searched = true;

            _font = Resources.Load<Font>(ResourcePath);
            if (_font == null)
            {
                var all = Resources.LoadAll<Font>("Fonts");
                if (all != null && all.Length > 0) _font = all[0];
            }
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                Debug.LogWarning("[CjkFont] 未找到 Assets/Resources/Fonts 下的中文字体，已回退内置字体，中文可能显示为方块");
            }
            return _font;
        }

        /// <summary>判断某个字体能否承担中文（粗略：非内置 Arial / LegacyRuntime 即视为自定义字体）。</summary>
        public static bool IsCjk(Font f)
        {
            if (f == null) return false;
            string n = f.name;
            if (string.IsNullOrEmpty(n)) return false;
            return !(n.IndexOf("Arial", System.StringComparison.OrdinalIgnoreCase) >= 0
                     || n.IndexOf("LegacyRuntime", System.StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>把场景中所有 legacy Text 切换到中文字体，返回被修复的组件数。</summary>
        public static int Apply()
        {
            var target = Get();
            if (!IsCjk(target)) return 0;

            int fixedCount = 0;
            var texts = Object.FindObjectsOfType<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null) continue;
                if (IsCjk(t.font)) continue;
                t.font = target;
                fixedCount++;
            }
            return fixedCount;
        }
    }
}
