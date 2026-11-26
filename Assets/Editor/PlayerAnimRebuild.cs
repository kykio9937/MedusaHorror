using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>用 Suriyun 女性动作重建玩家动画控制器（idle/walk/run + jump）。</summary>
    public static class PlayerAnimRebuild
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string AnimDir = "Assets/Suriyun/Animations";
        const string KevinDir = "Assets/Kevin Iglesias/Basic Motions/Animations";
        const string CtrlPath = "Assets/AscendantOne/Aphrodite/AphroditeLocomotion.controller";
        const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        public static void Rebuild()
        {
            var log = new StringBuilder();

            var idle = LoadClipAt(KevinDir + "/Idles/BasicMotions@Idle01.fbx", "BasicMotions@Idle01", out string idleName);
            var walk = LoadClipAt(KevinDir + "/Movement/BasicMotions@Walk01.fbx", "BasicMotions@Walk01 - Forwards", out string walkName);
            var run = LoadClipAt(KevinDir + "/Movement/BasicMotions@Run01.fbx", "BasicMotions@Run01 - Forwards", out string runName);
            var jump = LoadClipAt(KevinDir + "/Movement/BasicMotions@Jump01.fbx", "BasicMotions@Jump01", out string jumpName);
            log.AppendLine("[CLIP] idle=" + idleName + " walk=" + walkName + " run=" + runName + " jump=" + jumpName);
            if (idle == null || walk == null || run == null) { log.AppendLine("MISSING CLIP"); Finish(log); return; }

            AssetDatabase.DeleteAsset(CtrlPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
            if (ctrl.layers == null || ctrl.layers.Length == 0) ctrl.AddLayer("Base Layer");
            // 必须显式设为 1，否则新建的 layer 权重为 0，动画完全不生效（角色僵直滑行）
            ctrl.layers[0].defaultWeight = 1f;
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var loco = sm.AddState("Locomotion");
            var tree = new BlendTree
            {
                name = "LocomotionBlend",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, ctrl);
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, 2f);
            tree.AddChild(run, 5.2f);
            loco.motion = tree;
            sm.defaultState = loco;

            if (jump != null)
            {
                var js = sm.AddState("Jump");
                js.motion = jump;
                var t1 = loco.AddTransition(js);
                t1.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
                t1.hasExitTime = false;
                t1.duration = 0.06f;
                var t2 = js.AddTransition(loco);
                t2.hasExitTime = true;
                t2.exitTime = 0.85f;
                t2.duration = 0.12f;
            }

            AssetDatabase.SaveAssets();
            log.AppendLine("[CTRL] " + CtrlPath);

            // 应用到 prefab
            log.AppendLine(ApplyToPrefab());
            // 应用到场景
            foreach (var s in Scenes) log.AppendLine(ApplyToScene(s));

            Finish(log);
        }

        static void Finish(StringBuilder log)
        {
            File.WriteAllText(Path.Combine(OutDir, "anim_rebuild.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[PlayerAnimRebuild]\n" + log.ToString());
        }

        static AnimationClip LoadClipAt(string fbxPath, string preferName, out string clipName)
        {
            clipName = "";
            var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip first = null;
            foreach (var a in all)
            {
                var c = a as AnimationClip;
                if (c == null) continue;
                if (c.name.StartsWith("__preview__")) continue;
                if (first == null) first = c;
                if (c.name == preferName) { clipName = c.name; return c; }
            }
            if (first != null) { clipName = first.name; return first; }
            return null;
        }

        static AnimationClip LoadClip(string file, out string clipName)
        {
            clipName = "";
            string path = AnimDir + "/" + file + ".fbx";
            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            AnimationClip first = null;
            foreach (var a in all)
            {
                var c = a as AnimationClip;
                if (c == null) continue;
                if (c.name.StartsWith("__preview__")) continue;
                if (first == null) first = c;
                // 优先取与文件名后半段同名的 clip
                string tail = file.Contains("@") ? file.Substring(file.IndexOf('@') + 1) : file;
                if (c.name == tail) { clipName = c.name; return c; }
            }
            if (first != null) { clipName = first.name; return first; }
            return null;
        }

        static void AssignController(GameObject root)
        {
            var anim = root.GetComponent<Animator>();
            if (anim == null) anim = root.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlPath);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var tp = root.GetComponent<MedusaHorror.Player.ThirdPersonController>();
            if (tp != null) { tp.speedParam = "Speed"; tp.groundedParam = "grounded"; tp.jumpParam = "Jump"; }
        }

        static string ApplyToPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            if (root == null) return "[PREFAB] load fail";
            AssignController(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            return "[PREFAB] ok";
        }

        static string ApplyToScene(string path)
        {
            if (!File.Exists(path)) return "[SCENE] missing " + path;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var tp = UnityEngine.Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>(true);
            if (tp == null) return "[SCENE] " + sc.name + " no player";
            AssignController(tp.gameObject);
            EditorSceneManager.MarkSceneDirty(sc);
            bool saved = EditorSceneManager.SaveScene(sc);
            return "[SCENE] " + sc.name + " saved=" + saved;
        }
    }
}
