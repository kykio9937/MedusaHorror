using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityAnimatorController = UnityEditor.Animations.AnimatorController;
using UnityEngine;
using MedusaHorror.Player;

namespace MedusaHorror.EditorTools
{
    /// <summary>自制关键帧源：蓄力、出刀、随动、收招。生成普通.anim，可在Animation窗口继续调。</summary>
    [InitializeOnLoad] public static class FirstPersonAnimationAuthoring
    {
        const string Folder = "Assets/Resources/FirstPersonMotion";
        static FirstPersonAnimationAuthoring() { EditorApplication.update += PollRequest; }
        static void PollRequest()
        {
            const string request = "Temp/FirstPersonAnimationCommand.txt";
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim(); File.Delete(request);
            try
            {
                if (command == "build" && !EditorApplication.isPlaying) Build();
                else if (command == "verify" && !EditorApplication.isPlaying) FirstPersonAnimationValidation.Start();
                else if (command == "hold-verify" && !EditorApplication.isPlaying) HeldComboValidation.Start();
            }
            catch (Exception e) { Debug.LogException(e); }
        }
        struct PoseKey
        {
            public float t; public Vector3 p, e;
            public PoseKey(float time, Vector3 position, Vector3 euler) { t = time; p = position; e = euler; }
        }
        static PoseKey K(float t, float x, float y, float z, float pitch, float yaw, float roll)
            => new PoseKey(t, new Vector3(x, y, z), new Vector3(pitch, yaw, roll));
        static readonly PoseKey Rest = K(0, .26f, -.25f, .43f, -16, -8, -22);

        [MenuItem("Medusa/动画/生成自制第一人称关键帧")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Make("SwordIdle", true, Rest,
                K(1.2f,.263f,-.246f,.433f,-15,-7,-21),
                K(2.4f,.26f,-.25f,.43f,-16,-8,-22));
            Make("SwordWalk", true, Rest,
                K(.3f,.269f,-.262f,.426f,-14,-6,-19),
                K(.6f,.26f,-.25f,.43f,-16,-8,-22),
                K(.9f,.251f,-.262f,.434f,-17,-10,-24),
                K(1.2f,.26f,-.25f,.43f,-16,-8,-22));
            Make("SwordRun", true,
                K(0,.18f,-.34f,.36f,20,5,-42),
                K(.2f,.20f,-.365f,.34f,25,8,-37),
                K(.4f,.18f,-.34f,.36f,20,5,-42),
                K(.6f,.16f,-.365f,.38f,16,2,-47),
                K(.8f,.18f,-.34f,.36f,20,5,-42));
            // 三段不同轨迹；0.42相位是命中时刻，末帧回到相同待机，避免接招跳变。
            Make("SlashRight", false, Rest,
                K(.20f,.31f,-.17f,.37f,-26,30,-52),
                K(.32f,.32f,-.15f,.40f,-23,22,-44),
                K(.42f,.07f,-.22f,.52f,12,-24,32),
                K(.59f,-.13f,-.31f,.39f,30,-40,65),
                K(.78f,.13f,-.30f,.38f,0,-20,0),
                K(1,.26f,-.25f,.43f,-16,-8,-22));
            Make("SlashLeft", false, Rest,
                K(.20f,-.16f,-.18f,.34f,-16,-34,65),
                K(.30f,-.13f,-.16f,.40f,-18,-25,55),
                K(.42f,.13f,-.23f,.53f,14,20,-32),
                K(.59f,.32f,-.32f,.38f,30,36,-57),
                K(.80f,.30f,-.29f,.37f,0,5,-36),
                K(1,.26f,-.25f,.43f,-16,-8,-22));
            Make("SlashDown", false, Rest,
                K(.23f,.14f,.01f,.39f,-54,4,-7),
                K(.32f,.12f,-.02f,.43f,-40,3,-6),
                K(.42f,.10f,-.23f,.52f,26,0,-5),
                K(.61f,.10f,-.43f,.37f,66,-4,-4),
                K(.82f,.21f,-.34f,.37f,8,-6,-16),
                K(1,.26f,-.25f,.43f,-16,-8,-22));
            Make("SwordStow", false, Rest,
                K(.18f,.20f,-.22f,.39f,-8,-20,-12),
                K(.46f,.05f,-.36f,.30f,35,-32,28),
                K(.80f,-.10f,-.68f,.18f,68,-42,50),
                K(1,-.12f,-.82f,.12f,75,-45,55));
            Make("SwordDraw", false,
                K(0,-.12f,-.82f,.12f,75,-45,55),
                K(.28f,-.07f,-.58f,.23f,45,-30,35),
                K(.63f,.24f,-.20f,.39f,-25,-6,-28),
                K(.83f,.27f,-.24f,.44f,-18,-7,-24),
                K(1,.26f,-.25f,.43f,-16,-8,-22));
            Make("GunHold", true,
                K(0,.17f,-.31f,.25f,0,0,0),
                K(1.2f,.17f,-.307f,.252f,0,0,0),
                K(2.4f,.17f,-.31f,.25f,0,0,0));
            Make("GunStow", false,
                K(0,.17f,-.31f,.25f,0,0,0),
                K(.23f,.20f,-.35f,.24f,12,4,-5),
                K(.60f,.27f,-.65f,.13f,48,10,-13),
                K(1,.28f,-.96f,.07f,68,12,-17));
            Make("GunDraw", false,
                K(0,.28f,-.96f,.07f,68,12,-17),
                K(.30f,.25f,-.69f,.13f,45,9,-12),
                K(.68f,.17f,-.28f,.26f,-5,-1,1),
                K(.85f,.17f,-.32f,.25f,2,0,0),
                K(1,.17f,-.31f,.25f,0,0,0));
            SplitRecovery("SlashRight", "RecoverRight", .59f, 0);
            SplitRecovery("SlashLeft", "RecoverLeft", .59f, 1);
            SplitRecovery("SlashDown", "RecoverDown", .61f, 2);
            BuildPreview(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[动画制作] 14个关键帧片段已生成，三段后摇0.3/0.7/1.2秒。");
        }
        static void SplitRecovery(string attackName, string recoveryName, float split, int step)
        {
            var attack=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/"+attackName+".anim");
            string path=Folder+"/"+recoveryName+".anim";
            var recovery=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(recovery==null){recovery=new AnimationClip();AssetDatabase.CreateAsset(recovery,path);}
            recovery.ClearCurves();recovery.name=recoveryName;recovery.frameRate=60;
            foreach(var binding in AnimationUtility.GetCurveBindings(attack))
            {
                var original=AnimationUtility.GetEditorCurve(attack,binding);
                var strike=new AnimationCurve();var settle=new AnimationCurve();
                for(int i=0;i<=60;i++)
                {
                    float p=i/60f;
                    strike.AddKey(p,original.Evaluate(p*split));
                    settle.AddKey(p*MeleeWeapon.RecoveryForStep(step),original.Evaluate(Mathf.Lerp(split,1,p)));
                }
                Smooth(strike);Smooth(settle);
                AnimationUtility.SetEditorCurve(attack,binding,strike);
                AnimationUtility.SetEditorCurve(recovery,binding,settle);
            }
            EditorUtility.SetDirty(attack);EditorUtility.SetDirty(recovery);
        }
        static void Make(string name, bool loop, params PoseKey[] poses)
        {
            string path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves(); clip.name = name; clip.frameRate = 60;
            foreach (bool left in new[] { false, true })
            {
                string node = left ? "LeftGrip" : "RightGrip";
                for (int axis = 0; axis < 3; axis++)
                {
                    var position = new AnimationCurve(); var rotation = new AnimationCurve();
                    foreach (var key in poses)
                    {
                        Vector3 p = key.p;
                        if (left) p += Quaternion.Euler(key.e) * new Vector3(-.05f, -.085f, 0);
                        position.AddKey(key.t, p[axis]); rotation.AddKey(key.t, key.e[axis]);
                    }
                    Smooth(position); Smooth(rotation);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(node, typeof(Transform), "m_LocalPosition." + "xyz"[axis]), position);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(node, typeof(Transform), "localEulerAnglesRaw." + "xyz"[axis]), rotation);
                }
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings); EditorUtility.SetDirty(clip);
        }
        static void Smooth(AnimationCurve curve)
        {
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }
        static void BuildPreview()
        {
            string path = Folder + "/FirstPersonPreview.controller";
            var controller = AssetDatabase.LoadAssetAtPath<UnityAnimatorController>(path);
            if (controller == null) controller = UnityAnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            foreach (var child in sm.states) sm.RemoveState(child.state);
            foreach (var name in FirstPersonMotion.ClipNames)
            {
                var state = sm.AddState(name); state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim");
                if (name == "SwordIdle") sm.defaultState = state;
            }
            var root = new GameObject("FirstPersonKeyframePreview");
            try
            {
                root.AddComponent<Animator>().runtimeAnimatorController = controller;
                foreach (var name in new[] { "RightGrip", "LeftGrip" })
                {
                    var t = new GameObject(name).transform; t.SetParent(root.transform, false);
                    t.localPosition = Rest.p + (name == "LeftGrip" ? Quaternion.Euler(Rest.e) * new Vector3(-.05f,-.085f,0) : Vector3.zero);
                    t.localRotation = Quaternion.Euler(Rest.e);
                }
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "/FirstPersonKeyframePreview.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
