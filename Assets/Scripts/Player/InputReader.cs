using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MedusaHorror.Player
{
    /// <summary>
    /// W1 输入采集层：直接读取 Input System 的 Keyboard / Mouse，不依赖 Input Actions 资产。
    /// 其它脚本（角色控制器、相机控制器）只读取本类暴露的只读属性，不直接触达 Input System，
    /// 后续需要替换为 Input Actions 或新增手柄支持时，只需改这一个文件。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public class InputReader : MonoBehaviour
    {
        [Header("视角灵敏度（鼠标像素 -> 角度）")]
        [Range(0.02f, 0.50f)]
        public float lookSensitivity = 0.12f;

        [Header("光标")]
        [Tooltip("启用时锁死并隐藏光标；游戏中按 Esc 释放，点击左键重新锁定")]
        public bool lockCursorOnStart = true;

        /// <summary>
        /// 自动化验证用的输入注入（仅编辑器诊断命令使用，运行时保持 false / zero）。
        /// 开启后忽略真实键鼠，直接使用 debugMove / debugFire 等值。
        /// </summary>
        public static bool debugOverrideEnabled;
        public static Vector2 debugMove;
        public static bool debugFire;
        public static bool debugFireHeld;
        public static bool debugRun;
        public static bool debugAim;
        public bool AimHeld { get; private set; }
        public static bool debugReload;
        /// <summary>一次性换弹脉冲（编辑器诊断命令用，读取后自动清零）。</summary>
        public static bool debugReloadPulse;
        public static bool debugInteractPulse;

        /// <summary>移动输入：x = A/D 左右，y = W/S 前后，范围 -1..1，已归一化</summary>
        public Vector2 Move { get; private set; }

        /// <summary>每帧视角增量，单位「度」，已乘 lookSensitivity；y 向上为正</summary>
        public Vector2 Look { get; private set; }

        /// <summary>Shift 是否按住（跑步）</summary>
        public bool RunHeld { get; private set; }

        /// <summary>本帧是否按下跳跃（空格）</summary>
        public bool JumpPressed { get; private set; }

        /// <summary>本帧是否按下交互键（E）</summary>
        public bool InteractPressed { get; private set; }

        /// <summary>本帧是否按下开火键（鼠标左键，仅在光标锁定时有效）</summary>
        public bool FirePressed { get; private set; }
        public bool FireHeld { get; private set; }

        /// <summary>本帧是否按下换弹键（R）。</summary>
        public bool ReloadPressed { get; private set; }

        /// <summary>本帧是否按下切剑键（数字 1）。</summary>
        public bool Weapon1Pressed { get; private set; }

        /// <summary>本帧是否按下切枪键（数字 2）。</summary>
        public bool Weapon2Pressed { get; private set; }
        /// <summary>数字键 3：脉冲步枪（等级 5 解锁）。</summary>
        public bool Weapon3Pressed { get; private set; }

        /// <summary>本帧是否按下武器切换键（Q）。</summary>
        public bool SwitchWeaponPressed { get; private set; }

        /// <summary>本帧是否按下主动技能 1（F）。</summary>
        public bool Skill1Pressed { get; private set; }

        /// <summary>本帧是否按下主动技能 2（V）。</summary>
        public bool Skill2Pressed { get; private set; }

        /// <summary>一次性技能脉冲：-1 无效，0 技能1，1 技能2（编辑器诊断命令用，读取后清零）。</summary>
        public static int debugSkillPulse = -1;

        /// <summary>一次性切武器脉冲：-1 无效，0 切剑，1 切枪（编辑器诊断命令用，读取后清零）。</summary>
        public static int debugSwitchPulse = -1;

        /// <summary>UI 面板打开时置 true：本帧所有游戏输入清零，避免面板后面角色继续跑动。</summary>
        public static bool blocked;

        /// <summary>键盘与鼠标设备是否就绪</summary>
        public bool DevicesReady { get; private set; }

        bool _warnedOnce;

        void OnEnable()
        {
            if (lockCursorOnStart) LockCursor(true);
        }

        void OnDisable()
        {
            LockCursor(false);
        }

        void Update()
        {
            Vector2 move = Vector2.zero;
            Vector2 look = Vector2.zero;
            bool run = false;
            bool jump = false;
            bool interact = false;
            bool fire = false;
            bool fireHeld = false;
            bool reload = false;
            bool w1 = false;
            bool w2 = false;
            bool w3 = false;
            bool swap = false;
            bool sk1 = false;
            bool sk2 = false;
            bool ready = false;
            bool aim = false;

            if (debugOverrideEnabled)
            {
                move = debugMove;
                run = debugRun;
                aim = debugAim;
                fire = debugFire;
                fireHeld = debugFireHeld;
                reload = debugReload || debugReloadPulse;
                interact = debugInteractPulse;
                if (debugSwitchPulse == 0) { w1 = true; swap = true; }
                else if (debugSwitchPulse == 1) { w2 = true; swap = true; }
                if (debugSkillPulse == 0) { sk1 = true; debugSkillPulse = -1; }
                else if (debugSkillPulse == 1) { sk2 = true; debugSkillPulse = -1; }
                ready = true;
            }
            else
            {
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            ready = kb != null && ms != null;

            if (kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                run = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
                interact = kb.eKey.wasPressedThisFrame;
                reload = kb.rKey.wasPressedThisFrame;
                w1 = kb.digit1Key.wasPressedThisFrame;
                w2 = kb.digit2Key.wasPressedThisFrame;
                w3 = kb.digit3Key.wasPressedThisFrame;
                swap = kb.qKey.wasPressedThisFrame;
                sk1 = kb.fKey.wasPressedThisFrame;
                sk2 = kb.vKey.wasPressedThisFrame;

                if (kb.escapeKey.wasPressedThisFrame) LockCursor(false);
            }

            if (ms != null)
            {
                fireHeld = ms.leftButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
                aim = ms.rightButton.isPressed && Cursor.lockState == CursorLockMode.Locked;
                Vector2 delta = ms.delta.ReadValue();
                look = new Vector2(delta.x, delta.y) * lookSensitivity;

                if (ms.leftButton.wasPressedThisFrame)
                {
                    // 光标锁定时左键 = 开火；未锁定则用于重新捕获鼠标
                    if (Cursor.lockState == CursorLockMode.Locked) fire = true;
                    else LockCursor(true);
                }
            }
#endif
            }

            if (blocked)
            {
                move = Vector2.zero;
                look = Vector2.zero;
                run = jump = interact = fire = reload = w1 = w2 = w3 = swap = false;
                sk1 = sk2 = false;
                aim = false;
                fireHeld = false;
            }

            if (move.sqrMagnitude > 1f) move.Normalize();

            Move = move;
            Look = look;
            RunHeld = run;
            AimHeld = aim;
            JumpPressed = jump;
            InteractPressed = interact;
            FirePressed = fire;
            FireHeld = fireHeld;
            ReloadPressed = reload;
            Weapon1Pressed = w1;
            Weapon2Pressed = w2;
            Weapon3Pressed = w3;
            SwitchWeaponPressed = swap;
            Skill1Pressed = sk1;
            Skill2Pressed = sk2;
            debugSwitchPulse = -1;
            debugReloadPulse = false;
            debugInteractPulse = false;
            DevicesReady = ready;

            if (!ready && !_warnedOnce)
            {
                _warnedOnce = true;
                Debug.LogWarning("[InputReader] 未检测到可用的键盘/鼠标设备。" +
                                 "请确认 Project Settings > Player > Active Input Handling 已设为 Input System Package。");
            }
        }

        /// <summary>锁定 / 释放光标</summary>
        public void LockCursor(bool locked)
        {
            if (locked && blocked) locked = false;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
