using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// 玩家外观配色：V 键在三套配色间切换，只染衣服/护甲，不动皮肤和武器。
    /// 玩家的骨骼动画和武器绑定保持不变，避免换模型带来的动作错位。
    /// </summary>
    public class PlayerSkin : MonoBehaviour
    {
        public static readonly string[] Names = { "默认", "暗夜", "赤红" };

        static readonly Color[] Tints =
        {
            Color.white,
            new Color(0.45f, 0.52f, 0.85f),
            new Color(0.85f, 0.32f, 0.30f),
        };

        const string PrefKey = "medusa_player_skin";

        int _index;
        Renderer[] _renders;
        Color[] _origColors;
        bool _captured;

        public int Index { get { return _index; } }
        public string SkinName { get { return Names[Mathf.Clamp(_index, 0, Names.Length - 1)]; } }

        void Start()
        {
            _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Names.Length - 1);
            Capture();
            Apply();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.V))
            {
                _index = (_index + 1) % Names.Length;
                PlayerPrefs.SetInt(PrefKey, _index);
                PlayerPrefs.Save();
                Apply();
                Gameplay.LootToast.Show("外观：" + SkinName);
            }
        }

        void Capture()
        {
            if (_captured) return;
            _renders = GetComponentsInChildren<Renderer>(true);
            _origColors = new Color[_renders.Length];
            for (int i = 0; i < _renders.Length; i++)
            {
                var r = _renders[i];
                if (r == null) continue;
                var m = r.sharedMaterial;
                _origColors[i] = (m != null && m.HasProperty("_Color")) ? m.color : Color.white;
            }
            _captured = true;
        }

        public void Apply()
        {
            Capture();
            var tint = Tints[Mathf.Clamp(_index, 0, Tints.Length - 1)];
            for (int i = 0; i < _renders.Length; i++)
            {
                var r = _renders[i];
                if (r == null) continue;
                var mats = r.materials;   // 实例化，避免污染共享材质
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null || !mat.HasProperty("_Color")) continue;
                    string lower = mat.name.ToLowerInvariant();
                    if (lower.Contains("skin") || lower.Contains("hair") || lower.Contains("weapon")) continue;
                    if (lower.Contains("blade") || lower.Contains("sword")) continue;
                    mat.color = Color.Lerp(_origColors[i], tint, 0.5f);
                    changed = true;
                }
                if (changed) r.materials = mats;
            }
        }
    }
}
