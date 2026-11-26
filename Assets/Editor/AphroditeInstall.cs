using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>把玩家模型换成运星一号 Aphrodite（含动画/碰撞体适配）。</summary>
    public static class AphroditeInstall
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string ModelPrefab = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";
        const string Fbx = "Assets/AscendantOne/Aphrodite/Aphrodite.fbx";
        const string Ctrl = "Assets/Space/Animation/Animations/SpaceManController.controller";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        const float TargetHeight = 1.72f;

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        public static void InstallAll()
        {
            var log = new StringBuilder();
            log.AppendLine(DoPrefab());
            foreach (var s in Scenes) log.AppendLine(DoScene(s));
            File.WriteAllText(Path.Combine(OutDir, "install_report.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditeInstall]\n" + log.ToString());
        }

        static string DoPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            if (root == null) return "[PREFAB] load fail";
            int removed = StripChildren(root.transform);
            BuildModel(root.transform);
            ApplyRoot(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            return "[PREFAB] removed=" + removed + " ok";
        }

        static string DoScene(string path)
        {
            if (!File.Exists(path)) return "[SCENE] missing " + path;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tp == null) return "[SCENE] " + sc.name + " no player";
            var root = tp.gameObject;
            int removed = StripChildren(root.transform);
            BuildModel(root.transform);
            ApplyRoot(root);
            EditorSceneManager.MarkSceneDirty(sc);
            bool saved = EditorSceneManager.SaveScene(sc);
            return "[SCENE] " + sc.name + " removed=" + removed + " saved=" + saved;
        }

        static int StripChildren(Transform root)
        {
            int removed = 0;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (c.name == "CameraTarget") continue;
                UnityEngine.Object.DestroyImmediate(c.gameObject);
                removed++;
            }
            return removed;
        }

        static void BuildModel(Transform root)
        {
            var mp = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(mp, root);
            inst.name = "Model_Aphrodite";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            var rends = inst.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) inst.transform.localScale *= TargetHeight / b.size.y;

                rends = inst.GetComponentsInChildren<Renderer>(true);
                b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                inst.transform.position += new Vector3(0f, root.position.y - b.min.y, 0f);
            }

            var anims = inst.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < anims.Length; i++) UnityEngine.Object.DestroyImmediate(anims[i]);
        }

        static void ApplyRoot(GameObject root)
        {
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Ctrl);
            Avatar av = null;
            var all = AssetDatabase.LoadAllAssetsAtPath(Fbx);
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i] as Avatar;
                if (c != null && c.isHuman) { av = c; break; }
            }
            if (av != null) anim.avatar = av;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var tp = root.GetComponent<MedusaHorror.Player.ThirdPersonController>();
            if (tp != null) { tp.speedParam = "Speed"; tp.groundedParam = "grounded"; tp.jumpParam = "Jump"; }

            var cc = root.GetComponent<CharacterController>();
            if (cc != null) { cc.height = TargetHeight; cc.radius = 0.34f; cc.center = new Vector3(0f, TargetHeight * 0.5f, 0f); }

            var capsule = root.GetComponent<CapsuleCollider>();
            if (capsule != null) { capsule.height = TargetHeight; capsule.radius = 0.34f; capsule.center = new Vector3(0f, TargetHeight * 0.5f, 0f); }

            if (root.GetComponent<MedusaHorror.Gameplay.FootStepReceiver>() == null)
                root.AddComponent<MedusaHorror.Gameplay.FootStepReceiver>();
        }
    }
}
