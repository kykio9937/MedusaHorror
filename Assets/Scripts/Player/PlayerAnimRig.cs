using UnityEngine;

namespace MedusaHorror.Player
{
    /// <summary>
    /// Drives the player Animator combat states (sword combo / gun aim+fire) and keeps the
    /// claymore parented to the right hand. Locomotion params (Speed / grounded / Jump) stay
    /// owned by ThirdPersonController.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerAnimRig : MonoBehaviour
    {
        [Header("Refs")]
        public Animator animator;
        public GameObject swordPrefab;

        [Header("Sword socket")]
        public Vector3 swordLocalPos = Vector3.zero;
        [Tooltip("Pitch applied after the blade is aimed along the character forward axis")]
        public Vector3 swordAimEuler = new Vector3(-38f, 0f, 0f);
        public Vector3 swordLocalScale = Vector3.one;
        [Tooltip("Aim the blade along character forward instead of the hand bone axis")]
        public bool autoAlignSword = true;

        [Header("Options")]
        public bool disableLegacyPoseScripts = true;
        [Tooltip("Lift applied to the model root so the authored feet line up with the capsule bottom")]
        public float modelRootYOffset = 0.16f;

        int _attackId, _stepId, _aimId, _fireId, _emptyState;
        MeleeWeapon _melee;
        PlayerWeapon _gun;
        WeaponLoadout _loadout;
        int _lastSwing;
        int _lastShots;
        PlayerWeapon _observedGun;
        bool _aim;

        void Awake()
        {
            // Runtime NPC clones inherit the whole player prefab; only the real player (which owns a
            // ThirdPersonController) may drive weapons, HUD and combat animation.
            if (GetComponent<ThirdPersonController>() == null)
            {
                foreach (var mb in GetComponents<MonoBehaviour>())
                    if (mb is MeleeWeapon || mb is PlayerWeapon || mb is WeaponLoadout || mb is MedusaHorror.UI.WeaponHud)
                        Destroy(mb);
                enabled = false;
                return;
            }
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            _attackId = Animator.StringToHash("Attack");
            _stepId = Animator.StringToHash("ComboStep");
            _aimId = Animator.StringToHash("GunAim");
            _fireId = Animator.StringToHash("Fire");
            _emptyState = Animator.StringToHash("Empty");
        }

        void Start()
        {
            if (disableLegacyPoseScripts) DisableLegacy();
            AttachSword();
        }

        void DisableLegacy()
        {
            var body = GetComponent<MeleeSwingBody>();
            if (body != null) body.enabled = false;
            var fix = GetComponent<AphroditePoseFixer>();
            if (fix != null) fix.enabled = false;
        }

        void AttachSword()
        {
            if (swordPrefab == null || animator == null || !animator.isHuman) return;
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;
            if (hand.Find("SwordRig") != null) return;
            var go = Instantiate(swordPrefab, hand);
            go.name = "SwordRig";
            go.transform.localScale = swordLocalScale;
            if (autoAlignSword)
            {
                go.transform.position = hand.position + hand.rotation * swordLocalPos;
                go.transform.rotation = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(swordAimEuler);
            }
            else
            {
                go.transform.localPosition = swordLocalPos;
                go.transform.localRotation = Quaternion.Euler(swordAimEuler);
            }
        }

        /// <summary>Humanoid clips bake a root Y offset (Kazuko's clips sit ~1.72 m high).
        /// We never use root motion, so pin the model root every frame after animation evaluation.</summary>
        void LateUpdate()
        {
            if (animator == null) return;
            var root = animator.transform;
            if (root == null || root.parent == null) return;
            root.localPosition = new Vector3(0f, modelRootYOffset, 0f);
            root.localRotation = Quaternion.identity;
        }

        void Update()
        {
            if (animator == null) return;
            Resolve();
            PollMelee();
            PollGun();
            PollFallbackInput();
            PollWeaponFallback();
            EnforceNoLegacyBody();
            TuneCombatLayer();
        }

        float _attackRequestedAt = float.NegativeInfinity;
        void TuneCombatLayer()
        {
            // The combat layer only contributes while a sword swing or the gun pose is active.
            // Keeping it at weight 1 with an empty state made the upper body drift off the
            // locomotion pose (hunched shoulders), so it is driven to 0 the rest of the time.
            if (animator.layerCount < 2) return;
            bool active = _aim || Time.time - _attackRequestedAt < 0.12f ||
                          animator.IsInTransition(1) || animator.GetCurrentAnimatorStateInfo(1).shortNameHash != _emptyState;
            float w = active ? 1f : 0f;
            float cur = animator.GetLayerWeight(1);
            float next = Mathf.MoveTowards(cur, w, Time.deltaTime * 8f);
            if (Mathf.Abs(next - cur) > 0.001f) animator.SetLayerWeight(1, next);
        }

        float _legacyTimer;
        void EnforceNoLegacyBody()
        {
            if (!disableLegacyPoseScripts) return;
            _legacyTimer += Time.unscaledDeltaTime;
            if (_legacyTimer < 1f) return;
            _legacyTimer = 0f;
            var body = GetComponent<MeleeSwingBody>();
            if (body != null && body.enabled) body.enabled = false;
        }

        Transform _swordVisual;
        public void SetSwordVisible(bool visible) { ApplySwordVisibility(!visible); }
        void ApplySwordVisibility(bool gunMode)
        {
            if (_swordVisual == null)
            {
                var all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name == "Kazuko_claymore" && all[i].childCount == 0)
                    {
                        _swordVisual = all[i];
                        break;
                    }
                }
                if (_swordVisual == null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (all[i].name == "Kazuko_claymore") { _swordVisual = all[i]; break; }
                    }
                }
            }
            if (_swordVisual != null) _swordVisual.gameObject.SetActive(!gunMode);
        }

        void Resolve()
        {
            if (_melee == null) _melee = GetComponent<MeleeWeapon>();
            if (_gun == null) _gun = GetComponent<PlayerWeapon>();
            if (_loadout == null) _loadout = GetComponent<WeaponLoadout>();
        }

        InputReader _input;
        float _nextFallbackSwing;
        float _lastFallbackTime = -10f;
        const float ComboWindow = 0.9f;
        int _fallbackStep = -1;

        /// <summary>Fallback: if no MeleeWeapon exists on the player, drive the sword combo straight from input
        /// so the attack animations still play (damage is handled elsewhere / not at all).</summary>
        void PollFallbackInput()
        {
            if (_melee != null) return;
            if (_input == null) _input = GetComponent<InputReader>();
            if (_input == null) return;
            if (!_input.FirePressed) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (Time.time < _nextFallbackSwing) return;
            float since = Time.time - _lastFallbackTime;
            _fallbackStep = since <= ComboWindow ? (_fallbackStep + 1) % 3 : 0;
            _lastFallbackTime = Time.time;
            _nextFallbackSwing = Time.time + 0.62f;
            PlaySword(_fallbackStep);
        }

        void PollWeaponFallback()
        {
            if (_loadout != null) return;
            if (_input == null) _input = GetComponent<InputReader>();
            if (_input == null) return;
            if (!_input.Weapon2Pressed) return;   // Q 是 GameFlow 的退出键，这里只用数字键 2
            _aim = !_aim;
            animator.SetBool(_aimId, _aim);
            ApplySwordVisibility(_aim);
        }

        void PollMelee()
        {
            if (_melee == null) return;
            if (_melee.SwingCount == _lastSwing) return;
            _lastSwing = _melee.SwingCount;
            PlaySword(_melee.LastComboStep);
        }

        void PollGun()
        {
            var activeGun = _loadout != null && _loadout.Current == WeaponKind.Rifle ? _loadout.rifle : _gun;
            if (activeGun != _observedGun)
            {
                _observedGun = activeGun;
                _lastShots = activeGun != null ? activeGun.ShotsFired : 0;
            }
            if (activeGun != null && activeGun.ShotsFired != _lastShots)
            {
                _lastShots = activeGun.ShotsFired;
                animator.SetTrigger(_fireId);
            }
            bool want = _loadout != null && _loadout.Current != WeaponKind.Sword;
            if (want != _aim)
            {
                _aim = want;
                animator.SetBool(_aimId, want);
                ApplySwordVisibility(want);
            }
        }

        public bool IsAiming { get { return _aim; } }

        /// <summary>step01: 0..2 current combo step.</summary>
        public void PlaySword(int step01)
        {
            if (_loadout != null && _loadout.IsSwitching) return;
            if (animator == null) return;
            animator.SetInteger(_stepId, Mathf.Clamp(step01, 0, 2) + 1);
            animator.SetTrigger(_attackId);
            _attackRequestedAt = Time.time;
        }
    }
}










