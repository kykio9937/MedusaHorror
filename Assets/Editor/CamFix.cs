using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>
    /// 修第三人称相机被角色自身挡住的问题：玩家打 Player tag，
    /// CinemachineCollider 忽略该 tag。距离/俯仰/CameraTarget 高度一律保持原样。
    /// </summary>
    public static class CamFix
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string PrefabPath = "Assets/Prefabs/Kazuko_W1_Player.prefab";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        public static void Run()
        {
            var log = new StringBuilder();

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            root.tag = "Player";
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            log.AppendLine("[PREFAB] tag=Player");

            foreach (var sp in Scenes)
            {
                if (!File.Exists(sp)) continue;
                var sc = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                int tagged = 0, colliders = 0;

                foreach (var r in sc.GetRootGameObjects())
                {
                    foreach (var tpc in r.GetComponentsInChildren<MedusaHorror.Player.ThirdPersonController>(true))
                    {
                        tpc.gameObject.tag = "Player";
                        tagged++;
                    }

                    foreach (var mb in r.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        if (mb == null || mb.GetType().Name != "CinemachineCollider") continue;
                        var so = new SerializedObject(mb);
                        var ignore = so.FindProperty("m_IgnoreTag");
                        if (ignore != null) ignore.stringValue = "Player";
                        so.ApplyModifiedPropertiesWithoutUndo();
                        colliders++;
                    }
                }

                EditorSceneManager.MarkSceneDirty(sc);
                EditorSceneManager.SaveScene(sc);
                log.AppendLine("[SCENE] " + sc.name + " tagged=" + tagged + " colliders=" + colliders);
            }

            File.WriteAllText(Path.Combine(OutDir, "camfix.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[CamFix]\n" + log.ToString());
        }
    }
}
