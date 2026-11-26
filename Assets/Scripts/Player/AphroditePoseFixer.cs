using System.Globalization;
using System.IO;
using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// Aphrodite 姿态修正：把上臂收向腿侧、大腿收拢、脚尖内收。
    /// 只绕角色前后轴旋转，保留动画本身的前后摆臂/迈步。
    /// 调试时读项目根目录 posefix.json 实时改参数。
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public class AphroditePoseFixer : MonoBehaviour
    {
        public float armIn = 12f;
        public float legIn = 6f;
        public float toeIn = 0f;
        public float neckScale = 1f;    // 颈部骨骼偏移倍数，>1 拉长脖子
        public float legThick = 1f;     // 大腿横向加粗倍数
        public float calfThick = 1f;    // 小腿横向加粗倍数
        public float capeFactor = 0f;   // >0 表示披风骨骼已按此系数拉伸过
        public bool liveReload = true;

        Animator anim;
        Transform lUpArm, rUpArm, lThigh, rThigh, lFoot, rFoot;
        Transform neck1, head, lCalf, rCalf;
        Vector3 neck1Base, headBase;
        Vector3 lThighScale, rThighScale, lCalfScale, rCalfScale;
        float timer;

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (anim == null) anim = GetComponentInChildren<Animator>();
            Cache();
            Reload();
        }

        void Cache()
        {
            if (anim == null) return;
            lUpArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rUpArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lThigh = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rThigh = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            lFoot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            rFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            lCalf = anim.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            rCalf = anim.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            head = anim.GetBoneTransform(HumanBodyBones.Head);
            neck1 = FindDeep(anim.transform, "Bip001-Neck1");
            if (neck1 != null) neck1Base = neck1.localPosition;
            if (head != null) headBase = head.localPosition;
            if (lThigh != null) lThighScale = lThigh.localScale;
            if (rThigh != null) rThighScale = rThigh.localScale;
            if (lCalf != null) lCalfScale = lCalf.localScale;
            if (rCalf != null) rCalfScale = rCalf.localScale;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
            return null;
        }

        void LateUpdate()
        {
            if (anim == null) { Cache(); if (anim == null) return; }
            if (lUpArm == null) Cache();

            if (liveReload)
            {
                timer += Time.unscaledDeltaTime;
                if (timer > 0.4f) { timer = 0f; Reload(); }
            }

            ApplyPose(anim, transform, armIn, legIn, toeIn);
            ApplyBody(neckScale, legThick, calfThick);
        }

        /// <summary>脖子拉长 + 大腿/小腿横向加粗（只改垂直骨长的两个轴，不改变腿长）。</summary>
        void ApplyBody(float nk, float thick, float cthick)
        {
            if (neck1 != null && Mathf.Abs(nk - 1f) > 0.001f)
                neck1.localPosition = neck1Base * nk;
            if (head != null && Mathf.Abs(nk - 1f) > 0.001f)
                head.localPosition = headBase * nk;

            if (Mathf.Abs(thick - 1f) > 0.001f)
            {
                if (lThigh != null) lThigh.localScale = new Vector3(lThighScale.x, lThighScale.y * thick, lThighScale.z * thick);
                if (rThigh != null) rThigh.localScale = new Vector3(rThighScale.x, rThighScale.y * thick, rThighScale.z * thick);
            }
            if (Mathf.Abs(cthick - 1f) > 0.001f)
            {
                if (lCalf != null) lCalf.localScale = new Vector3(lCalfScale.x, lCalfScale.y * cthick, lCalfScale.z * cthick);
                if (rCalf != null) rCalf.localScale = new Vector3(rCalfScale.x, rCalfScale.y * cthick, rCalfScale.z * cthick);
            }
        }

        /// <summary>对当前姿态叠加内收修正（世界空间绕角色前后轴旋转）。</summary>
        public static void ApplyPose(Animator anim, Transform root, float armIn, float legIn, float toeIn)
        {
            if (anim == null || root == null) return;
            var fwd = root.forward;
            var up = root.up;

            Rot(anim.GetBoneTransform(HumanBodyBones.LeftUpperArm), fwd, armIn);
            Rot(anim.GetBoneTransform(HumanBodyBones.RightUpperArm), fwd, -armIn);
            Rot(anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg), fwd, legIn);
            Rot(anim.GetBoneTransform(HumanBodyBones.RightUpperLeg), fwd, -legIn);
            if (Mathf.Abs(toeIn) > 0.001f)
            {
                Rot(anim.GetBoneTransform(HumanBodyBones.LeftFoot), up, toeIn);
                Rot(anim.GetBoneTransform(HumanBodyBones.RightFoot), up, -toeIn);
            }
        }

        static void Rot(Transform bone, Vector3 axis, float deg)
        {
            if (bone == null || Mathf.Abs(deg) < 0.001f) return;
            bone.rotation = Quaternion.AngleAxis(deg, axis) * bone.rotation;
        }

        void Reload()
        {
            var p = Path.GetFullPath(Path.Combine(Application.dataPath, "../posefix.json"));
            if (!File.Exists(p)) return;
            try
            {
                foreach (var raw in File.ReadAllLines(p))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("//")) continue;
                    int c = line.IndexOf(':');
                    if (c <= 0) continue;
                    var key = line.Substring(0, c).Trim().Trim('"');
                    var val = line.Substring(c + 1).Trim().Trim(',', '"', ' ');
                    float v;
                    if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                    if (key == "armIn") armIn = v;
                    else if (key == "legIn") legIn = v;
                    else if (key == "toeIn") toeIn = v;
                    else if (key == "neckScale") neckScale = v;
                    else if (key == "legThick") legThick = v;
                    else if (key == "calfThick") calfThick = v;
                }
            }
            catch { }
        }
    }
}
