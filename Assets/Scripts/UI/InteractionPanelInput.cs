using MedusaHorror.Player;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MedusaHorror.UI
{
    // Stores the input state owned by a local shop or encounter panel.
    public sealed class InteractionPanelInput
    {
        static int _closedFrame = -1;
        public static bool ClosingThisFrame => _closedFrame == Time.frameCount;
        public static void MarkClosed() { _closedFrame = Time.frameCount; }
        bool _open, _blocked, _visible;
        CursorLockMode _cursor;

        public void Open()
        {
            if (_open) return;
            _open = true;
            _blocked = InputReader.blocked;
            _cursor = Cursor.lockState;
            _visible = Cursor.visible;
            InputReader.blocked = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static bool GameplayBlocked => InputReader.blocked;

        public void Close()
        {
            if (!_open) return;
            _open = false;
            MarkClosed();
            InputReader.blocked = _blocked;
            Cursor.lockState = _cursor;
            Cursor.visible = _visible;
        }

        public static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (key == KeyCode.Escape) return keyboard.escapeKey.wasPressedThisFrame;
            if (key == KeyCode.R) return keyboard.rKey.wasPressedThisFrame;
            if (key == KeyCode.B) return keyboard.bKey.wasPressedThisFrame;
            if (key == KeyCode.M) return keyboard.mKey.wasPressedThisFrame;
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
                return keyboard[(Key)((int)Key.Digit1 + (int)key - (int)KeyCode.Alpha1)].wasPressedThisFrame;
            return false;
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}
