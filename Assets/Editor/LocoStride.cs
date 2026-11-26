using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>Measure stride length / natural speed of locomotion clips retargeted onto the player model.</summary>
    public static class LocoStride
    {
        const string Model = "Assets/Suriyun/Kazuko/FBX/Kazuko.fbx";
        static readonly string[] Clips =
        {
            "Assets/Suriyun/Animations/Anim@Walk.fbx",
            "Assets/Suriyun/Animations/Anim@Run.fbx",
            "Assets/Space/Animation/Animations/Walking.fbx",
            "Assets/Space/Animation/Animations/RunForward.fbx",
            "Assets/Space/Animation/Animations/Idle.fbx",
            "Assets/Space/Animation/Animations/JumpOver.fbx",
            "Assets/Space/Animation/Animations/Scientist@StaticJump.fbx",
            "Assets/Space/Animation/Animations/pickRiflePistol.FBX",
            "Assets/Kevin Iglesias/Basic Motions/Animations/Movement/BasicMotions@Walk01.fbx",
            "Assets/Kevin Iglesias/Basic Motions/Animations/Movement/BasicMotions@Run01.fbx",
        };

        [MenuItem("Medusa/Loco Stride")]
        public static void Run()
        {
            var sb = new StringBuilder();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            foreach (var cp in Clips)
            {
                var objs = AssetDatabase.LoadAllAssetsAtPath(cp);
                if (objs == null || objs.Length == 0) { sb.AppendLine("MISSING " + cp); continue; }
                foreach (var clip in objs.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                {
                    var go = (GameObject)UnityEngine.Object.Instantiate(model);
                    try
                    {
                        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                        if (anim.avatar == null) { sb.AppendLine("no avatar " + clip.name); continue; }
                        int n = 24;
                        float minFoot = 999f, maxSep = 0f, sumHipY = 0f;
                        float prevZ = 0f;
                        float maxTravel = 0f;
                        AnimationMode.StartAnimationMode();
                        for (int i = 0; i < n; i++)
                        {
                            float t = clip.length * i / (float)n;
                            AnimationMode.BeginSampling();
                            AnimationMode.SampleAnimationClip(go, clip, t);
                            AnimationMode.EndSampling();
                            var lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                            var rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                            var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                            if (lf == null || rf == null || hips == null) break;
                            minFoot = Mathf.Min(minFoot, Mathf.Min(lf.position.y, rf.position.y));
                            sumHipY += hips.position.y;
                            float sep = Mathf.Abs(lf.position.z - rf.position.z);
                            if (sep > maxSep) maxSep = sep;
                            float mid = (lf.position.z + rf.position.z) * 0.5f;
                            if (i > 0) maxTravel += Mathf.Abs(mid - prevZ);
                            prevZ = mid;
                        }
                        AnimationMode.StopAnimationMode();
                        float natural = 2f * maxSep / Mathf.Max(0.1f, clip.length);
                        sb.AppendLine(string.Format("{0,-28} len={1:F2} minFootY={2:F3} maxFootSep={3:F3} naturalSpeed≈{4:F2} m/s  avgHipY={5:F3}",
                            clip.name, clip.length, minFoot, maxSep, natural, sumHipY / n));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            }
            System.IO.File.WriteAllText(@"E:/SoftWare/DeepSeek-Harness/workspace/default-workspace/_anim2/stride.txt", sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[LocoStride]\n" + sb);
        }
    }
}
