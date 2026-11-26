using UnityEngine;

namespace MedusaHorror.Player
{
    [DisallowMultipleComponent]
    public class PlayerIKRig : MonoBehaviour
    {
        [Header("Refs")]
        public Animator animator;
        public PlayerAnimRig animRig;

        [Header("Foot IK")]
        public bool footIK = true;
        [Range(0f, 1f)] public float footWeight = 0.9f;
        public LayerMask groundMask = ~0;
        public float footRayUp = 0.55f;
        public float footRayDown = 1.1f;
        public float footOffset = 0.02f;

        [Header("Hand IK")]
        public bool handIK = true;
        public float leftHandForward = 0.17f;
        public float leftHandDrop = 0.02f;

        [Header("Look At")]
        public bool lookAt = true;
        [Range(0f, 1f)] public float lookBodyWeight = 0.12f;
        [Range(0f, 1f)] public float lookHeadWeight = 0.55f;
        [Range(0f, 1f)] public float lookEyesWeight = 0.4f;

        const float MaxFootAdjustment = 0.26f;
        const float MaxToeAngle = 16f;
        readonly RaycastHit[] _hits = new RaycastHit[16];

        CharacterController _controller;
        ThirdPersonController _movement;
        Transform _player;
        Transform _weaponRig;
        WeaponLoadout _loadout;
        PlayerWeapon _heldWeapon;
        Quaternion _rightPalmLocal, _leftPalmLocal;
        Transform _leftToes, _rightToes, _leftFoot, _rightFoot;
        float _groundBlend, _handBlend;
        FootContact _left, _right;

        struct FootContact
        {
            public float Weight;
            public float Height;
            public Vector3 Normal;
        }

        public int IkCalls;
        public int FootHits;
        public bool HasWeaponRig => _weaponRig != null;

        void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animRig == null) animRig = GetComponentInParent<PlayerAnimRig>();
            _movement = GetComponentInParent<ThirdPersonController>();
            if (_movement == null) { enabled = false; return; }
            _player = _movement.transform;
            _controller = _movement.GetComponent<CharacterController>();
            _rightPalmLocal = PalmFrame(HumanBodyBones.RightHand, HumanBodyBones.RightMiddleProximal,
                HumanBodyBones.RightIndexProximal, HumanBodyBones.RightLittleProximal);
            _leftPalmLocal = PalmFrame(HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleProximal,
                HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftLittleProximal);
        }

        Quaternion PalmFrame(HumanBodyBones handBone, HumanBodyBones middleBone,
            HumanBodyBones indexBone, HumanBodyBones littleBone)
        {
            if (animator == null || !animator.isHuman) return Quaternion.identity;
            var hand = animator.GetBoneTransform(handBone);
            var middle = animator.GetBoneTransform(middleBone);
            var index = animator.GetBoneTransform(indexBone);
            var little = animator.GetBoneTransform(littleBone);
            if (hand == null || middle == null || index == null || little == null) return Quaternion.identity;
            Vector3 fingers = hand.InverseTransformDirection(middle.position - hand.position);
            Vector3 across = hand.InverseTransformDirection(index.position - little.position);
            return Quaternion.LookRotation(fingers, Vector3.Cross(fingers, across));
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || !animator.isHuman) return;
            IkCalls++;

            // The combat mask excludes both legs. Resolve feet on the base layer so
            // locomotion also works when the combat layer is faded all the way out.
            if (layerIndex == 0)
            {
                bool planted = footIK && _controller != null && _controller.enabled &&
                               _controller.isGrounded && !_movement.suspended;
                _groundBlend = planted ? Mathf.MoveTowards(_groundBlend, 1f, Time.deltaTime * 14f) : 0f;
                if (_leftFoot == null)
                {
                    _leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    _rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    _leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                    _rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);
                }
                ApplyFoot(AvatarIKGoal.LeftFoot, _leftFoot, _leftToes, animator.leftFeetBottomHeight, ref _left);
                ApplyFoot(AvatarIKGoal.RightFoot, _rightFoot, _rightToes, animator.rightFeetBottomHeight, ref _right);
                if (lookAt) ApplyLook();
            }

            if (layerIndex == animator.layerCount - 1 && handIK)
            {
                ApplyHands();
                ApplySwordStow();
            }
        }

        void ApplySwordStow()
        {
            if (_loadout == null || !_loadout.IsSwitching || _loadout.Current != WeaponKind.Sword) return;
            float amount = _loadout.StowAmount;
            // Reach across the shoulder to put away or retrieve the blade. The sword
            // follows its hand socket throughout the motion.
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, amount);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, amount);
            animator.SetIKPosition(AvatarIKGoal.RightHand, _player.TransformPoint(new Vector3(0.17f, 1.68f, -0.12f)));
            animator.SetIKRotation(AvatarIKGoal.RightHand, _player.rotation *
                Quaternion.Euler(-55f, 15f, -30f) * Quaternion.Inverse(_rightPalmLocal));
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, amount);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, _player.TransformPoint(new Vector3(0.53f, 1.48f, 0.06f)));
        }

        void ApplyFoot(AvatarIKGoal goal, Transform foot, Transform toes, float soleHeight, ref FootContact contact)
        {
            Vector3 position = animator.GetIKPosition(goal);
            Quaternion rotation = animator.GetIKRotation(goal);
            float delta = Time.deltaTime * 18f;
            float weight = 0f;

            if (_groundBlend > 0 && TryGround(position, out RaycastHit ground))
            {
                FootHits++;
                float targetHeight = ground.point.y + soleHeight + footOffset;
                float clearance = position.y - targetHeight;
                // Keep the swing foot off the floor and let the clip carry the knee and stride.
                weight = 1f - Mathf.InverseLerp(0.075f, 0.23f, clearance);
                if (contact.Weight < 0.01f)
                {
                    contact.Height = targetHeight;
                    contact.Normal = ground.normal;
                }
                contact.Height = Mathf.Lerp(contact.Height, targetHeight, delta);
                contact.Normal = Vector3.Slerp(contact.Normal, ground.normal, Mathf.Clamp01(delta));
            }

            contact.Weight = _groundBlend > 0 ? Mathf.MoveTowards(contact.Weight, weight, delta) : 0f;
            animator.SetIKPositionWeight(goal, contact.Weight * footWeight * _groundBlend);
            animator.SetIKRotationWeight(goal, contact.Weight * _groundBlend);
            if (contact.Weight <= 0) return;

            position.y += Mathf.Clamp(contact.Height - position.y, -MaxFootAdjustment, MaxFootAdjustment);
            animator.SetIKPosition(goal, position);

            Vector3 animatedHeading = foot != null && toes != null ? toes.position - foot.position : rotation * Vector3.forward;
            animatedHeading.y = 0;
            float yaw = animatedHeading.sqrMagnitude > 0.001f
                ? Vector3.SignedAngle(_player.forward, animatedHeading, Vector3.up) : 0f;
            Vector3 heading = Quaternion.AngleAxis(Mathf.Clamp(yaw, -MaxToeAngle, MaxToeAngle), Vector3.up) * _player.forward;
            Vector3 forward = Vector3.ProjectOnPlane(heading, contact.Normal);
            animator.SetIKRotation(goal, Quaternion.LookRotation(forward, contact.Normal));
        }

        bool TryGround(Vector3 position, out RaycastHit ground)
        {
            float rootFloor = _controller.bounds.min.y;
            Vector3 origin = new Vector3(position.x, rootFloor + footRayUp, position.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, footRayUp + footRayDown,
                groundMask, QueryTriggerInteraction.Ignore);
            float minimumUp = Mathf.Cos(_controller.slopeLimit * Mathf.Deg2Rad);
            ground = default;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance >= closest || hit.normal.y < minimumUp ||
                    Mathf.Abs(hit.point.y - rootFloor) > _controller.stepOffset + 0.06f ||
                    hit.transform.IsChildOf(_player) || hit.collider.attachedRigidbody != null ||
                    hit.collider.GetComponentInParent<CharacterController>() != null)
                    continue;
                closest = hit.distance;
                ground = hit;
            }
            return ground.collider != null;
        }

        void ApplyHands()
        {
            if (_loadout == null) _loadout = _player.GetComponent<WeaponLoadout>();
            if (_loadout != null)
            {
                _heldWeapon = _loadout.Current == WeaponKind.Rifle ? _loadout.rifle : _loadout.gun;
                _weaponRig = _loadout.Current != WeaponKind.Sword && _heldWeapon != null ? _heldWeapon.GripTarget : null;
            }
            else if (_weaponRig == null) _weaponRig = _player.Find("WeaponRig");
            bool aiming = _weaponRig != null && _weaponRig.gameObject.activeInHierarchy &&
                          animRig != null && animRig.IsAiming;
            _handBlend = Mathf.MoveTowards(_handBlend, aiming ? 1f : 0f, Time.deltaTime * 12f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _handBlend);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _handBlend);
            bool hasGripRotations = _heldWeapon != null && _heldWeapon.LeftGrip != null;
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, hasGripRotations ? _handBlend : 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, hasGripRotations ? _handBlend : 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, _handBlend * 0.65f);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, _handBlend * 0.65f);
            if (_weaponRig == null || _handBlend <= 0f) return;

            var rightGrip = _heldWeapon != null ? _heldWeapon.RightGrip : _weaponRig;
            var leftGrip = _heldWeapon != null ? _heldWeapon.LeftGrip : null;
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightGrip.position);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip != null ? leftGrip.position :
                _weaponRig.position + _weaponRig.forward * leftHandForward - _weaponRig.up * leftHandDrop);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightGrip.rotation * Quaternion.Inverse(_rightPalmLocal));
            if (leftGrip != null)
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation * Quaternion.Inverse(_leftPalmLocal));
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, _player.TransformPoint(new Vector3(0.5f, 1.12f, 0.08f)));
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, _player.TransformPoint(new Vector3(-0.4f, 1.12f, 0.2f)));
        }

        void ApplyLook()
        {
            Camera camera = Camera.main;
            Vector3 target = camera != null
                ? camera.transform.position + camera.transform.forward * 12f
                : _player.position + _player.forward * 12f + Vector3.up * 1.4f;
            // Restrict the procedural gaze so a steep camera angle does not twist the torso.
            Vector3 direction = (target - animator.bodyPosition).normalized;
            direction = Vector3.RotateTowards(_player.forward, direction, 40f * Mathf.Deg2Rad, 0f);
            animator.SetLookAtWeight(1f, lookBodyWeight, lookHeadWeight, lookEyesWeight, 0.5f);
            animator.SetLookAtPosition(animator.bodyPosition + direction * 12f);
        }
    }
}
