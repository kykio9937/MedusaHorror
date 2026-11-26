using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    public static class AnimScout
    {
        const string Root = "E:/UnityProjects/MedusaHorror";
        static readonly StringBuilder sb = new StringBuilder();

        [MenuItem("Medusa/AnimScout")]
        public static void Run()
        {
            sb.Length = 0;
            Line("AnimScout2 " + DateTime.Now.ToString("s"));
            Probe("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", "Assets/Suriyun/Animations/Anim@Walk.fbx");
            Probe("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", "Assets/Kevin Iglesias/Basic Motions/Animations/Movement/BasicMotions@Walk01.fbx");
            Probe("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", "Assets/Space/Animation/Animations/Walking.fbx");
            Probe("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Suriyun/Animations/Anim@Walk.fbx");
            Probe("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Suriyun/Animations/Anim@Run.fbx");
            Probe("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Suriyun/Animations/Anim@Atk1.fbx");
            Probe("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Space/Animation/Animations/Walking.fbx");
            Probe("Assets/Suriyun/Kazuko/FBX/Kazuko.fbx", "Assets/Space/Animation/Animations/pickRiflePistol.FBX");
            Probe("Assets/Space/models/spaceMan.FBX", "Assets/Space/Animation/Animations/Walking.fbx");
            Probe("Assets/Space/models/spaceMan.FBX", "Assets/Space/Animation/Animations/pickRiflePistol.FBX");
            Probe("Assets/Space/models/spaceMan.FBX", "Assets/Suriyun/Animations/Anim@Atk1.fbx");
            Probe("Assets/AscendantOne/Aphrodite/Aphrodite.fbx", "Assets/Suriyun/Animations/Anim@Atk1.fbx");

            Directory.CreateDirectory(Root + "/Library");
            File.WriteAllText(Root + "/Library/animscout2.txt", sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[AnimScout2] done chars=" + sb.Length);
        }

        static void Line(string s) { sb.AppendLine(s); }

        static void Probe(string modelPath, string clipPath)
        {
            Line("");
            Line("=== PROBE model=" + modelPath + " clip=" + clipPath);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) { Line("  model missing"); return; }
            var clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) { Line("  clip missing"); return; }
            var inst = (GameObject)UnityEngine.Object.Instantiate(model);
            try
            {
                var animator = inst.GetComponent<Animator>();
                if (animator == null) animator = inst.AddComponent<Animator>();
                if (animator.avatar == null) { Line("  no avatar"); return; }
                Line("  clip=" + clip.name + " len=" + clip.length.ToString("F3") + " human=" + animator.isHuman);
                float minFootY = 999f, maxSpread = 0f, minSpread = 999f, sumHipY = 0f;
                int n = 8;
                AnimationMode.StartAnimationMode();
                for (int i = 0; i < n; i++)
                {
                    float t = clip.length * i / (float)n;
                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(inst, clip, t);
                    AnimationMode.EndSampling();
                    var lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                    var head = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (lf == null || rf == null || hips == null) { Line("  missing bones"); break; }
                    float fy = Mathf.Min(lf.position.y, rf.position.y);
                    if (fy < minFootY) minFootY = fy;
                    float spread = Vector3.Distance(new Vector3(lf.position.x, 0f, lf.position.z), new Vector3(rf.position.x, 0f, rf.position.z));
                    if (spread > maxSpread) maxSpread = spread;
                    if (spread < minSpread) minSpread = spread;
                    sumHipY += hips.position.y;
                    if (i == 0 || i == n / 2)
                        Line(string.Format("  t={0:F2} hipY={1:F3} headY={2:F3} footY L={3:F3} R={4:F3} spread={5:F3} handY L={6:F3} R={7:F3}",
                            t, hips.position.y, head ? head.position.y : 0f, lf.position.y, rf.position.y, spread,
                            animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y,
                            animator.GetBoneTransform(HumanBodyBones.RightHand).position.y));
                }
                AnimationMode.StopAnimationMode();
                Line(string.Format("  SUMMARY minFootY={0:F3} spread[min={1:F3} max={2:F3}] avgHipY={3:F3}", minFootY, minSpread, maxSpread, sumHipY / n));
            }
            catch (Exception e) { Line("  EXC " + e.Message); }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
        }
    }
}
