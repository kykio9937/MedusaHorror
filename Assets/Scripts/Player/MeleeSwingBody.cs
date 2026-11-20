using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// 剑攻击的上身程序化动作：在 Animator 评估之后（LateUpdate）以「叠加旋转」方式驱动
    /// 脊柱/胸腔/右肩/右臂/右腕，配合上身扭转与重心前移，与 MeleeWeapon 的剑体摆动共用同一相位。
    /// 不改任何战斗数值：只在表现层叠加旋转与微小平移，退出时完全交还 Animator 姿态。
    /// </summary>
    [DisallowMultipleComponent]
    public class MeleeSwingBody : MonoBehaviour
    {
        [Header("幅度（度 / 米）")]
        public float chestYaw = 34f;
        public float chestPitch = 9f;
        public float spineShare = 0.55f;
        public float armYaw = 62f;
        public float armRaise = 58f;
        public float elbowBend = 52f;
        public float wristSnap = 40f;
        public float leftArmSway = 16f;
        public float hipsShift = 0.05f;

        [Header("命中顿挫")]
        public float hitChestPitch = 6f;
        public float hitArmRecoil = 10f;
        public float hitWristRecoil = 12f;

        // ---------------- 诊断只读状态 ----------------
        public bool Active { get; private set; }
        public float Phase { get; private set; }
        public int SwingCount { get; private set; }
        public float LastDuration { get; private set; }
        public float PeakChestYaw { get; private set; }
        public float PeakArmYaw { get; private set; }
        public float PeakWrist { get; private set; }
        public float MinHandRadius { get; private set; }
        public float MinHandForward { get; private set; }
        public float MaxHandForward { get; private set; }
        public float HandHeightDelta { get; private set; }
        public float HitPulsePeak { get; private set; }
        public bool HitPulseApplied { get; private set; }
        public bool BonesBound { get; private set; }
        public string BoundSummary = "";
        public string Trace = "";
        public float HandTravel { get; private set; }
        public float ChestYawMeasured { get; private set; }
        public float BladeYAtPeak { get; private set; }

        Animator _animator;
        MeleeWeapon _melee;
        Transform _root;
        Transform _hips, _spine, _chest, _rightShoulder, _rightUpperArm, _rightLowerArm, _rightHand;
        Transform _leftUpperArm, _leftLowerArm;

        bool _hit;
        float _hitPulse;
        bool _appliedThisFrame;
        readonly List<string> _trace = new List<string>();

        void Awake()
        {
            _root = transform;
            _melee = GetComponent<MeleeWeapon>();
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null) _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Bind();
        }

        void Start()
        {
            if (!BonesBound) Bind();
        }

        /// <summary>按 Humanoid 骨骼映射取骨骼，避免依赖具体命名。</summary>
        public void Bind()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_animator == null || _animator.avatar == null || !_animator.avatar.isHuman)
            {
                BoundSummary = "NO_HUMANOID_AVATAR";
                BonesBound = false;
                return;
            }

            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _rightShoulder = _animator.GetBoneTransform(HumanBodyBones.RightShoulder);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);

            BonesBound = _spine != null && _chest != null && _rightUpperArm != null && _rightLowerArm != null;
            BoundSummary = "hips=" + N(_hips) + " spine=" + N(_spine) + " chest=" + N(_chest)
                + " shoulder=" + N(_rightShoulder) + " upperArm=" + N(_rightUpperArm)
                + " lowerArm=" + N(_rightLowerArm) + " hand=" + N(_rightHand)
                + " leftArm=" + N(_leftUpperArm);
        }

        static string N(Transform t) { return t == null ? "-" : t.name; }

        /// <summary>MeleeWeapon.Swing() 调用：开始一次上身动作。</summary>
        public void BeginSwing(float duration, bool hit)
        {
            if (!BonesBound) Bind();
            Active = true;
            Phase = 0f;
            _hit = hit;
            _hitPulse = 0f;
            HitPulsePeak = 0f;
            HitPulseApplied = false;
            LastDuration = Mathf.Max(0.05f, duration);
            SwingCount++;
            PeakChestYaw = 0f;
            PeakArmYaw = 0f;
            PeakWrist = 0f;
            MinHandRadius = 999f;
            MinHandForward = 999f;
            MaxHandForward = -999f;
            HandHeightDelta = 0f;
            _trace.Clear();
            _baseHandHeight = _rightHand != null ? _rightHand.position.y : transform.position.y;
            _startChestFwd = _chest != null ? _chest.forward : transform.forward;
            _startHandLocal = _rightHand != null ? _root.InverseTransformPoint(_rightHand.position) : Vector3.zero;
            HandTravel = 0f;
            ChestYawMeasured = 0f;
            BladeYAtPeak = 0f;
        }

        /// <summary>MeleeWeapon 每帧把剑体摆动相位原样传进来，保证两者严格同步。</summary>
        public void SetSwingPhase(float u)
        {
            if (!Active) return;
            Phase = Mathf.Clamp01(u);
            if (u >= 1f) Active = false;
        }

        /// <summary>诊断用：把上身定格在指定相位（不随挥砍结束复位），便于截图核对穿模与体态。</summary>
        public void FreezePose(float u)
        {
            if (!BonesBound) Bind();
            _frozen = true;
            Active = true;
            Phase = Mathf.Clamp01(u);
        }

        /// <summary>诊断用：解除定格并复位上身。</summary>
        public void ReleasePose()
        {
            _frozen = false;
            Cancel();
        }

        public void Cancel()
        {
            Active = false;
            Phase = 0f;
        }

        bool _frozen;
        float _baseHandHeight;
        Vector3 _startChestFwd;
        Vector3 _startHandLocal;

        // 角色自带的 IKSystem/MotionSystem 也在 LateUpdate 里写骨骼，会覆盖本组件的叠加旋转，
        // 因此改到「渲染前」施加（此时所有 LateUpdate 都已跑完），并保证每帧只叠加一次。
        int _appliedFrame = -1;

        void OnEnable() { Camera.onPreCull += HandlePreCull; }
        void OnDisable() { Camera.onPreCull -= HandlePreCull; }

        void HandlePreCull(Camera cam)
        {
            if (!Active || !BonesBound) return;
            if (Time.frameCount == _appliedFrame) return;
            _appliedFrame = Time.frameCount;
            Apply(Phase);
        }

        static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void Apply(float u)
        {
            // 三段式（与 MeleeWeapon.UpdateSwingAnimation 严格同相位）：
            //   0.00~0.30 蓄力：剑举过右肩、胸腔向右后拧、屈肘
            //   0.30~0.58 斩击：斜劈至左前下方，胸腔反向拧、伸肘、手腕甩
            //   0.58~1.00 收招：回到持剑待机位
            float q;   // -1 = 蓄力到位，+1 = 斩击到位，0 = 中立
            if (u < 0.30f)
            {
                q = -Ease(u / 0.30f);
            }
            else if (u < 0.58f)
            {
                q = Mathf.Lerp(-1f, 1f, Ease((u - 0.30f) / 0.28f));
            }
            else
            {
                q = Mathf.Lerp(1f, 0f, Ease((u - 0.58f) / 0.42f));
            }
            float negQ = Mathf.Max(0f, -q);   // 蓄力分量 0~1
            float posQ = Mathf.Max(0f, q);    // 斩击分量 0~1

            // 命中顿挫：命中瞬间其后的短促回弹（约 0.36~0.58 相位内的一记钟形脉冲）
            _hitPulse = 0f;
            if (_hit && u > 0.36f && u < 0.58f)
            {
                float t = (u - 0.36f) / 0.22f;
                _hitPulse = Mathf.Sin(t * Mathf.PI);
                HitPulseApplied = true;
                if (_hitPulse > HitPulsePeak) HitPulsePeak = _hitPulse;
            }

            Vector3 worldUp = Vector3.up;
            Vector3 axRight = _root.right;
            Vector3 axFwd = _root.forward;

            float chestYawDeg = chestYaw * q;
            float chestPitchDeg = chestPitch * (0.85f * negQ + 0.65f * posQ) - hitChestPitch * _hitPulse;
            // 实测映射（角色本地 x=右 y=上 z=前）：
            //   绕前向轴正转 = 手臂向上外抬；绕右向轴正转 = 手臂向后，负转 = 向前；绕上轴正转 = 手臂向身体左侧收
            float armYawDeg = armYaw * (-0.30f * negQ + 0.55f * posQ);
            float armLiftDeg = armRaise * negQ - armRaise * 0.85f * posQ;
            float armPitchDeg = armRaise * 0.35f * negQ - armRaise * 0.45f * posQ;
            float elbowDeg = elbowBend * (negQ + 0.10f * posQ);
            float wristDeg = wristSnap * (-0.85f * negQ + posQ) - hitWristRecoil * _hitPulse;
            float leftArmDeg = leftArmSway * (-0.8f * negQ + posQ);
            float hipsShiftM = hipsShift * (0.5f * negQ + posQ);

            RotateAdd(_spine, worldUp, chestYawDeg * spineShare);
            RotateAdd(_spine, axRight, chestPitchDeg * spineShare);

            RotateAdd(_chest, worldUp, chestYawDeg * (1f - spineShare));
            RotateAdd(_chest, axRight, chestPitchDeg * (1f - spineShare));

            RotateAdd(_rightUpperArm, worldUp, armYawDeg);
            RotateAdd(_rightUpperArm, axFwd, armLiftDeg);
            RotateAdd(_rightUpperArm, axRight, armPitchDeg);
            RotateAdd(_rightLowerArm, axRight, -elbowDeg);
            RotateAdd(_rightHand, axRight, wristDeg);
            RotateAdd(_rightHand, worldUp, wristDeg * 0.35f);

            RotateAdd(_leftUpperArm, worldUp, leftArmDeg * 0.6f);
            RotateAdd(_leftLowerArm, axRight, elbowDeg * 0.35f);

            if (_hips != null)
            {
                // 重心前移：在 Animator 写好的髋部位置上叠加沿角色朝向的平移（幅度小，避免拉扯腿部与地面穿插）
                Vector3 worldShift = axFwd * hipsShiftM;
                _hips.position = _hips.position + worldShift;
            }

            // ---- 实测：胸腔朝向角变化（Animator + 叠加后的真实结果） ----
            if (_chest != null)
            {
                Vector3 a = new Vector3(_startChestFwd.x, 0f, _startChestFwd.z);
                Vector3 b2 = new Vector3(_chest.forward.x, 0f, _chest.forward.z);
                if (a.sqrMagnitude > 0.0001f && b2.sqrMagnitude > 0.0001f)
                {
                    float meas = Vector3.Angle(a, b2);
                    if (meas > ChestYawMeasured) ChestYawMeasured = meas;
                }
            }
            if (_rightHand != null)
            {
                float travel = Vector3.Distance(_root.InverseTransformPoint(_rightHand.position), _startHandLocal);
                if (travel > HandTravel) HandTravel = travel;
            }
            float bladeY = Mathf.Abs(CurrentBladeY());
            if (bladeY > BladeYAtPeak) BladeYAtPeak = bladeY;

            // ---- 遥测：手臂是否真的动了、手/剑是否穿身或跑到身后 ----
            float absChest = Mathf.Abs(chestYawDeg);
            float absArm = Mathf.Abs(armYawDeg);
            float absWrist = Mathf.Abs(wristDeg);
            if (absChest > PeakChestYaw) PeakChestYaw = absChest;
            if (absArm > PeakArmYaw) PeakArmYaw = absArm;
            if (absWrist > PeakWrist) PeakWrist = absWrist;

            if (_rightHand != null)
            {
                Vector3 local = _root.InverseTransformPoint(_rightHand.position);
                float radius = new Vector2(local.x, local.z).magnitude;
                if (radius < MinHandRadius) MinHandRadius = radius;
                if (local.z < MinHandForward) MinHandForward = local.z;
                if (local.z > MaxHandForward) MaxHandForward = local.z;
                HandHeightDelta = _rightHand.position.y - _baseHandHeight;
            }

            if (_trace.Count < 14 && (u < 0.02f || u > 0.98f || _trace.Count == 0 || u - _lastTraceU >= 0.09f))
            {
                _lastTraceU = u;
                _trace.Add("u=" + u.ToString("0.00") + " chest=" + chestYawDeg.ToString("0.0")
                    + " arm=" + armYawDeg.ToString("0.0") + " rise=" + armLiftDeg.ToString("0.0")
                    + " wrist=" + wristDeg.ToString("0.0") + " hip=" + hipsShiftM.ToString("0.000")
                    + (_hitPulse > 0f ? " punch=" + _hitPulse.ToString("0.00") : "")
                    + " | measChestYaw=" + ChestYawMeasured.ToString("0.0")
                    + " handTravel=" + HandTravel.ToString("0.000")
                    + " bladeY=" + CurrentBladeY().ToString("0.0"));
                var sbb = new StringBuilder();
                for (int i = 0; i < _trace.Count; i++)
                {
                    if (i > 0) sbb.Append(" ; ");
                    sbb.Append(_trace[i]);
                }
                Trace = sbb.ToString();
            }
        }

        float _lastTraceU = -1f;

        float CurrentBladeY()
        {
            if (_melee == null || _melee.bladeRoot == null) return 0f;
            float y = _melee.bladeRoot.localRotation.eulerAngles.y;
            if (y > 180f) y -= 360f;
            return y;
        }

        static void RotateAdd(Transform bone, Vector3 axis, float deg)
        {
            if (bone == null || Mathf.Abs(deg) < 0.0001f) return;
            bone.rotation = Quaternion.AngleAxis(deg, axis) * bone.rotation;
        }
    }
}
