using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using AnimCtrl = UnityEditor.Animations.AnimatorController;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Builds the Kazuko player animation set (locomotion + sword combo + gun) and applies it to prefab and scenes.</summary>
    public static class PlayerAnimBuilder
    {
        public const string CtrlPath = "Assets/Animations/PlayerCombat.controller";
        public const string MaskPath = "Assets/Animations/UpperBody.mask";
        public const string KazukoFbx = "Assets/Suriyun/Kazuko/FBX/Kazuko.fbx";
        public const string KazukoPrefab = "Assets/Suriyun/Kazuko/Prefab/Kazuko.prefab";
        public const string PlayerPrefab = "Assets/Prefabs/Kazuko_W1_Player.prefab";
        public const string SwordPrefab = "Assets/Art/Weapons/Claymore/Kazuko_claymore.prefab";

        // Suriyun (Kazuko) native clips: authored on this exact rig, so no retarget distortion
        // (the Kevin Iglesias set leans the torso ~15 deg and bends the elbows on this avatar).
        const string ClipIdle = "Assets/Suriyun/Animations/Anim@Idle_A.fbx";
        const string ClipWalk = "Assets/Suriyun/Animations/Anim@Walk.fbx";
        const string ClipRun = "Assets/Suriyun/Animations/Anim@Run.fbx";
        const string ClipJump = "Assets/Suriyun/Animations/Anim@Jump.fbx";
        static readonly string[] ClipAtk =
        {
            "Assets/Suriyun/Animations/Anim@Atk1.fbx",
            "Assets/Suriyun/Animations/Anim@Atk2.fbx",
            "Assets/Suriyun/Animations/Anim@Atk3.fbx",
        };
        const string ClipGun = "Assets/Space/Animation/Animations/pickRiflePistol.FBX";

        static readonly string[] Scenes =
        {
            "Assets/Scenes/W0_Base.unity",
            "Assets/Scenes/W2_Wasteland.unity",
            "Assets/Scenes/W3_Subway.unity",
            "Assets/Scenes/W4_NeonStreet.unity",
            "Assets/Scenes/W5_AlienGate.unity",
        };

        [MenuItem("Medusa/1 Build Player Anim Assets")]
        public static void BuildAssets()
        {
            FixClipLoops();
            var mask = BuildMask();
            var ctrl = BuildController(mask);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerAnimBuilder] assets built: " + (ctrl != null));
        }

        [MenuItem("Medusa/2 Apply Player Anim To Prefab And Scenes")]
        public static void ApplyAll()
        {
            var avatar = LoadAvatar();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CtrlPath);
            if (avatar == null || ctrl == null) { Debug.LogError("[PlayerAnimBuilder] missing avatar or controller"); return; }

            // prefab
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
            try
            {
                PatchPlayer(root, avatar, ctrl);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            // scenes
            foreach (var s in Scenes)
            {
                if (!File.Exists(s)) continue;
                var scene = EditorSceneManager.OpenScene(s, OpenSceneMode.Single);
                var tpc = Object.FindObjectOfType<MedusaHorror.Player.ThirdPersonController>();
                if (tpc == null) { Debug.LogWarning("[PlayerAnimBuilder] no player in " + s); continue; }
                PatchPlayer(tpc.gameObject, avatar, ctrl);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[PlayerAnimBuilder] patched " + s);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PlayerAnimBuilder] apply done");
        }

        // ---------------- internals ----------------

        static Avatar LoadAvatar()
        {
            var imp = AssetImporter.GetAtPath(KazukoFbx) as ModelImporter;
            if (imp != null && imp.sourceAvatar != null) return imp.sourceAvatar;
            return AssetDatabase.LoadAllAssetsAtPath(KazukoFbx).OfType<Avatar>().FirstOrDefault();
        }

        static AnimationClip Clip(string path, string prefer = null)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            if (clips.Length == 0) return null;
            if (!string.IsNullOrEmpty(prefer))
            {
                var hit = clips.FirstOrDefault(c => c.name == prefer);
                if (hit != null) return hit;
            }
            return clips[0];
        }

        static void FixClipLoops()
        {
            var paths = new List<string>(ClipAtk) { ClipJump };
            foreach (var p in paths)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                var settings = imp.clipAnimations;
                if (settings == null || settings.Length == 0) settings = imp.defaultClipAnimations;
                foreach (var c in settings)
                {
                    c.loopTime = false;
                    c.loopPose = false;
                }
                imp.clipAnimations = settings;
                imp.SaveAndReimport();
            }
        }

        static AvatarMask BuildMask()
        {
            AssetDatabase.DeleteAsset(MaskPath);
            var mask = new AvatarMask();
            mask.name = "UpperBody";
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            AssetDatabase.CreateAsset(mask, MaskPath);
            return AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        }

        static AnimCtrl BuildController(AvatarMask mask)
        {
            AssetDatabase.DeleteAsset(CtrlPath);
            var ctrl = AnimCtrl.CreateAnimatorControllerAtPath(CtrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("grounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("ComboStep", AnimatorControllerParameterType.Int);
            ctrl.AddParameter("GunAim", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

            // ---- base layer: locomotion ----
            var baseSM = ctrl.layers[0].stateMachine;
            baseSM.name = "Base";

            var idle = Clip(ClipIdle); var walk = Clip(ClipWalk); var run = Clip(ClipRun); var jump = Clip(ClipJump);
            var blend = new BlendTree();
            blend.name = "LocomotionBlend";
            blend.blendType = BlendTreeType.Simple1D;
            blend.blendParameter = "Speed";
            blend.useAutomaticThresholds = false;
            AssetDatabase.AddObjectToAsset(blend, ctrl);
            if (idle != null) blend.AddChild(idle, 0f);
            if (walk != null)
            {
                blend.AddChild(walk, 1.25f);
                var children = blend.children;
                children[children.Length - 1].timeScale = 0.6f;
                blend.children = children;
            }
            if (run != null) blend.AddChild(run, 2.6f);
            EditorUtility.SetDirty(blend);

            var loco = baseSM.AddState("Locomotion", new Vector3(320, 0, 0));
            loco.motion = blend;
            loco.iKOnFeet = false;

            var jumpState = baseSM.AddState("Jump", new Vector3(320, 140, 0));
            if (jump != null) jumpState.motion = jump;

            var toJump = loco.AddTransition(jumpState);
            toJump.hasExitTime = false; toJump.duration = 0.08f; toJump.hasFixedDuration = true;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");

            var fromJump = jumpState.AddTransition(loco);
            fromJump.hasExitTime = false; fromJump.duration = 0.14f; fromJump.hasFixedDuration = true;
            fromJump.AddCondition(AnimatorConditionMode.If, 0f, "grounded");

            baseSM.defaultState = loco;

            // ---- combat layer: sword / gun, upper body only ----
            var layer = new AnimatorControllerLayer();
            layer.name = "Combat";
            layer.defaultWeight = 1f;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layer.avatarMask = mask;
            layer.stateMachine = new AnimatorStateMachine();
            layer.stateMachine.name = "Combat";
            AssetDatabase.AddObjectToAsset(layer.stateMachine, ctrl);
            var layers = new List<AnimatorControllerLayer>(ctrl.layers);
            layers.Add(layer);
            // IK Pass so PlayerIKRig.OnAnimatorIK runs (feet on slopes, hands on the weapon)
            for (int li = 0; li < layers.Count; li++) layers[li].iKPass = true;
            ctrl.layers = layers.ToArray();
            var sm = ctrl.layers[1].stateMachine;

            var empty = sm.AddState("Empty", new Vector3(200, 0, 0));
            empty.writeDefaultValues = false;

            for (int i = 0; i < ClipAtk.Length; i++)
            {
                var clip = Clip(ClipAtk[i]);
                if (clip == null) continue;
                var st = sm.AddState("Atk" + (i + 1), new Vector3(430, -90 + i * 90, 0));
                st.motion = clip;
                st.speed = 1f;

                var any = sm.AddAnyStateTransition(st);
                any.hasExitTime = false; any.duration = 0.06f; any.hasFixedDuration = true;
                any.canTransitionToSelf = false;
                any.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                any.AddCondition(AnimatorConditionMode.Equals, i + 1, "ComboStep");

                var back = st.AddTransition(empty);
                back.hasExitTime = true; back.exitTime = 0.80f; back.duration = 0.10f; back.hasFixedDuration = true;
            }

            var gunIdle = Clip(ClipGun, "pistolIdle");
            var gunShot = Clip(ClipGun, "pistolShot");
            var aim = sm.AddState("GunAim", new Vector3(430, 320, 0));
            if (gunIdle != null) aim.motion = gunIdle;
            var shot = sm.AddState("GunShot", new Vector3(620, 320, 0));
            if (gunShot != null) shot.motion = gunShot;

            var toAim = empty.AddTransition(aim);
            toAim.hasExitTime = false; toAim.duration = 0.16f; toAim.hasFixedDuration = true;
            toAim.AddCondition(AnimatorConditionMode.If, 0f, "GunAim");

            var fromAim = aim.AddTransition(empty);
            fromAim.hasExitTime = false; fromAim.duration = 0.16f; fromAim.hasFixedDuration = true;
            fromAim.AddCondition(AnimatorConditionMode.IfNot, 0f, "GunAim");

            var toShot = sm.AddAnyStateTransition(shot);
            toShot.hasExitTime = false; toShot.duration = 0.04f; toShot.hasFixedDuration = true;
            toShot.canTransitionToSelf = false;
            toShot.AddCondition(AnimatorConditionMode.If, 0f, "Fire");

            var shotBack = shot.AddTransition(aim);
            shotBack.hasExitTime = true; shotBack.exitTime = 0.55f; shotBack.duration = 0.08f; shotBack.hasFixedDuration = true;

            sm.defaultState = empty;
            return ctrl;
        }

        static void PatchPlayer(GameObject player, Avatar avatar, RuntimeAnimatorController ctrl)
        {
            // 1. drop old model children (anything that is not the camera target)
            for (int i = player.transform.childCount - 1; i >= 0; i--)
            {
                var c = player.transform.GetChild(i);
                if (c.name == "CameraTarget") continue;
                if (c.name == "Model_Kazuko" || c.name == "Kazuko" || c.name.StartsWith("Model_") || c.name.Contains("Aphrodite"))
                    Object.DestroyImmediate(c.gameObject);
            }
            var old = player.transform.Find("SwordRig");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // 2. add Kazuko model (unpacked so it is a plain hierarchy)
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(KazukoPrefab);
            if (src == null) { Debug.LogError("[PlayerAnimBuilder] Kazuko prefab missing"); return; }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            // NOTE: keep the prefab root name - the humanoid Avatar resolves bones by path from this node.
            model.transform.SetParent(player.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 1.12f;
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
            // the pack ships a Rigidbody + CapsuleCollider on the model; they fight the CharacterController
            // and make the camera boom collide into the character
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
            foreach (var col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            // 3. animator lives on the model root (canonical Unity setup: Animator on the Avatar root)
            var oldRootAnim = player.GetComponent<Animator>();
            if (oldRootAnim != null) Object.DestroyImmediate(oldRootAnim);
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.runtimeAnimatorController = ctrl;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 4. drop the old pose hack
            var fix = player.GetComponent<MedusaHorror.Player.AphroditePoseFixer>();
            if (fix != null) Object.DestroyImmediate(fix);

            // 5b. anim rig
            var rig = player.GetComponent<MedusaHorror.Player.PlayerAnimRig>();
            if (rig == null) rig = player.AddComponent<MedusaHorror.Player.PlayerAnimRig>();
            rig.animator = animator;
            rig.swordPrefab = null;   // Kazuko.prefab already ships her claymore bound to ClaymoreA
            rig.disableLegacyPoseScripts = true;
            rig.autoAlignSword = true;
            rig.swordAimEuler = new Vector3(-38f, 0f, 0f);
            rig.swordLocalScale = Vector3.one;
            rig.modelRootYOffset = 0.16f;

            // IK rig must sit on the same GameObject as the Animator (OnAnimatorIK is dispatched there)
            var strayIk = player.GetComponent<MedusaHorror.Player.PlayerIKRig>();
            if (strayIk != null) Object.DestroyImmediate(strayIk);
            var ik = animator.gameObject.GetComponent<MedusaHorror.Player.PlayerIKRig>();
            if (ik == null) ik = animator.gameObject.AddComponent<MedusaHorror.Player.PlayerIKRig>();
            ik.animator = animator;
            ik.animRig = rig;

            // 6. locomotion tuning to match the new clips (measured natural speed 1.44 / 2.71 m/s)
            var tpc = player.GetComponent<MedusaHorror.Player.ThirdPersonController>();
            if (tpc != null)
            {
                tpc.walkSpeed = 1.25f;
                tpc.runSpeed = 2.6f;
                tpc.animWalkSpeed = 1.01f;
                tpc.animRunSpeed = 2.12f;
                tpc.speedParam = "Speed";
                tpc.groundedParam = "grounded";
                tpc.jumpParam = "Jump";
            }

            // 7. camera target height for the shorter model
            var ct = player.transform.Find("CameraTarget");
            if (ct != null) ct.localPosition = new Vector3(0f, 1.28f, 0.05f);
        }
    }
}












