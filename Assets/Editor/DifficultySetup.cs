using System.Text;
using MedusaHorror.Gameplay;
using MedusaHorror.Player;
using MedusaHorror.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 难度配置资产（DifficultyProfile）的落地工具：
    /// 1) EnsureAsset：把单一数据源资产创建/补齐到 Assets/Config/DefaultDifficulty.asset；
    /// 2) WireScene：把场景里 MedusaBoss / MedusaGaze / PlayerPetrify 的 profile 引用指向该资产并保存场景；
    /// 3) Run：调试通道入口（DIFFCFG）。
    /// </summary>
    public static class DifficultySetup
    {
        const string ConfigDir = "Assets/Config";

        public static DifficultyProfile EnsureAsset(out string log)
        {
            var sb = new StringBuilder();
            var asset = AssetDatabase.LoadAssetAtPath<DifficultyProfile>(DifficultyProfile.ResourcePath);
            if (asset == null)
            {
                if (!AssetDatabase.IsValidFolder(ConfigDir)) AssetDatabase.CreateFolder("Assets", "Config");
                asset = ScriptableObject.CreateInstance<DifficultyProfile>();
                AssetDatabase.CreateAsset(asset, DifficultyProfile.ResourcePath);
                AssetDatabase.SaveAssets();
                sb.AppendLine("[DiffCfg] 已新建配置资产 " + DifficultyProfile.ResourcePath);
            }
            else sb.AppendLine("[DiffCfg] 复用既有配置资产 " + DifficultyProfile.ResourcePath);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            DifficultyProfile.SetActive(asset);
            log = sb.ToString();
            return asset;
        }

        /// <summary>把配置资产接到场景内所有相关组件，并保存场景。</summary>
        public static string WireScene()
        {
            var sb = new StringBuilder();
            var asset = EnsureAsset(out string log);
            sb.Append(log);

            int bosses = 0, gazes = 0, pets = 0;
            var bossList = Object.FindObjectsOfType<MedusaBoss>(true);
            for (int i = 0; i < bossList.Length; i++)
            {
                var b = bossList[i];
                b.profile = asset;
                asset.ApplyToBoss(b);
                EditorUtility.SetDirty(b);
                bosses++;
                var g = b.GetComponent<MedusaGaze>();
                if (g == null) g = b.GetComponentInChildren<MedusaGaze>(true);
                if (g != null)
                {
                    g.profile = asset;
                    asset.ApplyToGaze(g);
                    EditorUtility.SetDirty(g);
                    gazes++;
                }
            }

            var petList = Object.FindObjectsOfType<PlayerPetrify>(true);
            for (int i = 0; i < petList.Length; i++)
            {
                petList[i].profile = asset;
                asset.ApplyToPetrify(petList[i]);
                EditorUtility.SetDirty(petList[i]);
                pets++;
            }

            if (!EditorApplication.isPlaying)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sb.AppendLine("[DiffCfg] 场景已保存 " + scene.path);
            }

            sb.AppendLine("[DiffCfg] 绑定 boss=" + bosses + " gaze=" + gazes + " petrify=" + pets);
            sb.AppendLine("[DiffCfg] " + asset.Describe());
            return sb.ToString();
        }

        public static string Run(string[] parts)
        {
            if (parts != null && parts.Length > 1 && parts[1] == "ASSET")
            {
                var a = EnsureAsset(out string log);
                return log + a.Describe();
            }
            return WireScene();
        }
    }

    /// <summary>
    /// P0 遗留问题的一体化自检入口（调试通道 P0DIAG）：
    /// 一次性回读九项遗留问题涉及的关键运行期状态，便于逐项实测取证。
    /// </summary>
    public static class P0Diag
    {
        public static string Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[P0] scene=" + EditorSceneManager.GetActiveScene().name +
                          " playing=" + EditorApplication.isPlaying);

            // 1) 胜利面板三按钮 + 继续前进
            var flow = Object.FindObjectOfType<Core.GameFlow>();
            sb.AppendLine("[P0] flow " + (flow != null ? flow.PanelDiag() : "null"));

            // 2) 准星可见度
            var pw = Object.FindObjectOfType<PlayerWeapon>();
            sb.AppendLine("[P0] crosshair " + (pw != null ? pw.CrosshairDiag() : "null"));

            // 3) 射线命中
            if (pw != null)
                sb.AppendLine("[P0] lastHit name=" + pw.LastHitName + " dist=" + pw.LastHitDistance.ToString("F2"));

            // 4) BGM 双轨互斥
            var audio = Core.AudioDirector.Instance;
            sb.AppendLine("[P0] bgm " + (audio != null ? audio.BgmPlayState : "null"));

            // 6) 难度单一数据源
            var asset = AssetDatabase.LoadAssetAtPath<DifficultyProfile>(DifficultyProfile.ResourcePath);
            var active = DifficultyProfile.Active;
            sb.AppendLine("[P0] diffAsset=" + (asset != null ? "found" : "missing") +
                          " active=" + (active != null ? active.name : "null"));
            if (active != null) sb.AppendLine("[P0] " + active.Describe());
            if (asset != null)
            {
                var b = Object.FindObjectOfType<MedusaBoss>();
                var g = Object.FindObjectOfType<MedusaGaze>();
                var p = Object.FindObjectOfType<PlayerPetrify>();
                sb.AppendLine("[P0] profileRef boss=" + (b != null && b.profile == asset) +
                              " gaze=" + (g != null && g.profile == asset) +
                              " petrify=" + (p != null && p.profile == asset));
                if (b != null) sb.AppendLine("[P0] bossLive hp=" + b.maxHealth + " hunt=" + b.huntSpeed +
                                             " rage=" + b.rageSpeed + " stop=" + b.stopDistance);
                if (p != null) sb.AppendLine("[P0] petrifyLive gaze=" + p.gazeRate + " recover=" + p.recoverRate +
                                             " struggle=" + p.struggleRequired + " autoRel=" + p.autoRelease);
                if (g != null) sb.AppendLine("[P0] gazeLive close=" + g.closeWeightFactor +
                                             " center=" + g.centerWeight + " edge=" + g.edgeWeight);
            }

            // 7) SwordRig 幂等
            int rigs = 0;
            var players = Object.FindObjectsOfType<Player.WeaponLoadout>(true);
            for (int i = 0; i < players.Length; i++)
            {
                var trs = players[i].GetComponentsInChildren<Transform>(true);
                for (int k = 0; k < trs.Length; k++)
                    if (trs[k] != null && trs[k].name == "SwordRig") rigs++;
            }
            sb.AppendLine("[P0] swordRig=" + rigs);

            // 8) 交互提示 vs 武器 HUD 叠压
            var it = Object.FindObjectOfType<PlayerInteractor>();
            var hud = Object.FindObjectOfType<WeaponHud>();
            if (it != null && hud != null)
            {
                var r1 = it.HintScreenRect();
                var r2 = hud.HudScreenRect();
                bool overlap = r1.xMin < r2.xMax && r2.xMin < r1.xMax && r1.yMin < r2.yMax && r2.yMin < r1.yMax;
                sb.AppendLine("[P0] hintRect=" + Fmt(r1) + " hudRect=" + Fmt(r2) + " overlap=" + overlap);
            }
            else sb.AppendLine("[P0] uiProbe unavailable interactor=" + (it != null) + " hud=" + (hud != null));

            return sb.ToString();
        }

        static string Fmt(Rect r)
        {
            return "(" + r.xMin.ToString("F0") + "," + r.yMin.ToString("F0") + ")-(" +
                   r.xMax.ToString("F0") + "," + r.yMax.ToString("F0") + ")";
        }
    }
}
