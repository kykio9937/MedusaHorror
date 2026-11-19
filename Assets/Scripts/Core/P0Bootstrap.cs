using UnityEngine;
using UnityEngine.SceneManagement;
using MedusaHorror.UI;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;

namespace MedusaHorror.Core
{
    /// <summary>
    /// P0 阶段运行时自举入口：不依赖场景序列化改动，场景加载后按需装配各子系统。
    /// </summary>
    public static class P0Bootstrap
    {
        public const string BaseScene = "W0_Base";
        public const string HospitalScene = "W1_Hospital";
        public const string WastelandScene = "W2_Wasteland";
        public const string SubwayScene = "W3_Subway";
        public const string NeonStreetScene = "W4_NeonStreet";
        public const string AlienGateScene = "W5_AlienGate";
        public const string MenuScene = "MainMenu";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Apply(scene);
        }

        static void Apply(Scene scene)
        {
            // P0-1 UI 中文字体
            int fixedText = CjkFont.Apply();
            if (fixedText > 0)
                Debug.Log("[P0] 中文字体：已把 " + fixedText + " 个 UI.Text 切换到随包中文字体");

            // P0-3 音频框架：任何场景都保证有 AudioMixer 总线 + 合成音效库 + BGM
            var audio = AudioDirector.Ensure();
            if (audio != null)
            {
                audio.StartMusic();
                int adopted = 0;
                if (scene.name == HospitalScene) audio.AdoptSceneAmbience();
                adopted = audio.AdoptedAmbienceCount;
                Debug.Log("[P0] 音频：mixer=" + (audio.MixerLoaded ? "loaded" : "missing") +
                          " bgm=playing ambienceAdopted=" + adopted);
            }

            // P0-2 失败 / 重开闭环：战斗关卡都需要局内流程中枢
            if (scene.name == BaseScene || scene.name == HospitalScene || scene.name == WastelandScene
                || scene.name == SubwayScene || scene.name == NeonStreetScene || scene.name == AlienGateScene)
                GameFlow.Ensure();

            // 每次进入关卡都从「未拿钥匙、Boss 未死」开始，避免基地往返后门禁残留
            MedusaHorror.Gameplay.ExitPortal.ResetStaticState();

            // 反射探针：整搬进来的场景带实时探针，每帧渲染 6 面非常吃性能。
            // 统一改为按需刷新，并在加载后只渲染一次。
            var probes = Object.FindObjectsOfType<ReflectionProbe>(true);
            for (int i = 0; i < probes.Length; i++)
            {
                var p = probes[i];
                if (p == null || p.mode != UnityEngine.Rendering.ReflectionProbeMode.Realtime) continue;
                p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                p.RenderProbe();
            }
        }
    }
}
