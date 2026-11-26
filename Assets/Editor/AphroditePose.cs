using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MedusaHorror.EditorTools
{
    /// <summary>诊断 Aphrodite 的骨骼层级、披风网格与行走动画曲线，为姿态修正提供依据。</summary>
    public static class AphroditePose
    {
        const string OutDir = @"C:/Users/50108/AppData/Roaming/Tencent/Marvis/User/oAN1i2aPqJR3Bfho54JNHN93bnAI/workspace/conv_ce6ca2f121154e3dbdf04023f121a7a2/temp";
        const string PrefabPath = "Assets/AscendantOne/Aphrodite/Aphrodite_Player.prefab";
        const string FbxPath = "Assets/AscendantOne/Aphrodite/Aphrodite.fbx";

        public static void Diag()
        {
            var log = new StringBuilder();

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null) { log.AppendLine("PREFAB NULL"); Finish(log); return; }

            log.AppendLine("=== 层级（含骨骼）===");
            DumpTree(root.transform, 0, log, 12);

            log.AppendLine("\n=== SkinnedMeshRenderer ===");
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = smr.sharedMesh;
                log.AppendLine(string.Format("[SMR] {0} mesh={1} verts={2} bones={3} bounds={4}",
                    TPath(smr.transform), m != null ? m.name : "null", m != null ? m.vertexCount : 0,
                    smr.bones != null ? smr.bones.Length : 0,
                    m != null ? m.bounds.ToString("F3") : "-"));
                if (m != null && (m.name.ToLower().Contains("cape") || TPath(smr.transform).ToLower().Contains("cape")))
                {
                    var names = new List<string>();
                    if (smr.bones != null) foreach (var b in smr.bones) names.Add(b != null ? b.name : "null");
                    log.AppendLine("   CAPE BONES: " + string.Join(", ", names.ToArray()));
                    if (smr.rootBone != null) log.AppendLine("   CAPE ROOT: " + smr.rootBone.name);
                    // 顶点范围
                    var vs = m.vertices;
                    var min = new Vector3(9999, 9999, 9999); var max = new Vector3(-9999, -9999, -9999);
                    foreach (var v in vs) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }
                    log.AppendLine("   LOCAL MIN=" + min.ToString("F3") + " MAX=" + max.ToString("F3"));
                }
            }

            log.AppendLine("\n=== Animator ===");
            var anim = root.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                log.AppendLine("avatar=" + (anim.avatar != null ? anim.avatar.name + " valid=" + anim.avatar.isValid + " human=" + anim.avatar.isHuman : "null"));
                log.AppendLine("controller=" + (anim.runtimeAnimatorController != null ? anim.runtimeAnimatorController.name : "null"));
                if (anim.avatar != null && anim.avatar.isHuman)
                {
                    var hd = anim.avatar.humanDescription;
                    log.AppendLine("armStretch=" + hd.armStretch + " legStretch=" + hd.legStretch + " upperArmTwist=" + hd.upperArmTwist);
                    var n = hd.human != null ? hd.human.Length : 0;
                    log.AppendLine("human bones=" + n);
                }
                // 关键骨骼位置
                foreach (HumanBodyBones b in new[] { HumanBodyBones.Hips, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.Head })
                {
                    var t = anim.GetBoneTransform(b);
                    if (t == null) { log.AppendLine(b + " = NULL"); continue; }
                    log.AppendLine(string.Format("{0} localPos={1} localRotEuler={2}", b, t.localPosition.ToString("F4"), t.localRotation.eulerAngles.ToString("F1")));
                }
            }

            PrefabUtility.UnloadPrefabContents(root);
            Finish(log);
        }

        static void DumpTree(Transform t, int depth, StringBuilder log, int maxDepth)
        {
            if (depth > maxDepth) return;
            log.AppendLine(new string(' ', depth * 2) + t.name + "  pos=" + t.localPosition.ToString("F3"));
            foreach (Transform c in t) DumpTree(c, depth + 1, log, maxDepth);
        }

        static string TPath(Transform t)
        {
            var s = t.name;
            var p = t.parent;
            while (p != null) { s = p.name + "/" + s; p = p.parent; }
            return s;
        }

        static void Finish(StringBuilder log)
        {
            File.WriteAllText(Path.Combine(OutDir, "aphrodite_pose.txt"), log.ToString(), new UTF8Encoding(false));
            Debug.Log("[AphroditePose]\n" + log.ToString());
        }
    }
}
