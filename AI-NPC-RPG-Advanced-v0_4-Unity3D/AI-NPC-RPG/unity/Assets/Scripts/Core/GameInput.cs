using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// One input facade that works with the legacy Input Manager, the new Input System,
/// or both enabled. Set Blocked = true while a text box / menu owns the keyboard.
/// </summary>
public static class GameInput
{
    public static bool Blocked;

#if ENABLE_INPUT_SYSTEM
    static bool Held(UnityEngine.InputSystem.Controls.KeyControl k) { return k != null && k.isPressed; }
    static bool Down(UnityEngine.InputSystem.Controls.KeyControl k) { return k != null && k.wasPressedThisFrame; }

    public static Vector2 Move
    {
        get
        {
            var k = Keyboard.current;
            if (Blocked || k == null) return Vector2.zero;
            float x = (Held(k.dKey) || Held(k.rightArrowKey) ? 1f : 0f) - (Held(k.aKey) || Held(k.leftArrowKey) ? 1f : 0f);
            float y = (Held(k.wKey) || Held(k.upArrowKey) ? 1f : 0f) - (Held(k.sKey) || Held(k.downArrowKey) ? 1f : 0f);
            Vector2 v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
    public static bool Sprint { get { var k = Keyboard.current; return !Blocked && k != null && Held(k.leftShiftKey); } }
    public static bool JumpDown { get { var k = Keyboard.current; return !Blocked && k != null && Down(k.spaceKey); } }
    public static bool InteractDown { get { var k = Keyboard.current; return !Blocked && k != null && Down(k.eKey); } }
    public static bool HealDown { get { var k = Keyboard.current; return !Blocked && k != null && Down(k.hKey); } }
    public static bool AttackDown
    {
        get
        {
            var k = Keyboard.current; var m = Mouse.current;
            if (Blocked) return false;
            return (k != null && Down(k.fKey)) || (m != null && m.leftButton.wasPressedThisFrame);
        }
    }
    public static bool EscapeDown { get { var k = Keyboard.current; return k != null && Down(k.escapeKey); } }
    public static bool OrbitHeld { get { var m = Mouse.current; return !Blocked && m != null && m.rightButton.isPressed; } }
    public static Vector2 LookDelta { get { var m = Mouse.current; return Blocked || m == null ? Vector2.zero : m.delta.ReadValue() * 0.12f; } }
    public static float Scroll { get { var m = Mouse.current; return Blocked || m == null ? 0f : m.scroll.ReadValue().y * 0.01f; } }
#else
    public static Vector2 Move
    {
        get
        {
            if (Blocked) return Vector2.zero;
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            Vector2 v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
    public static bool Sprint { get { return !Blocked && Input.GetKey(KeyCode.LeftShift); } }
    public static bool JumpDown { get { return !Blocked && Input.GetKeyDown(KeyCode.Space); } }
    public static bool InteractDown { get { return !Blocked && Input.GetKeyDown(KeyCode.E); } }
    public static bool HealDown { get { return !Blocked && Input.GetKeyDown(KeyCode.H); } }
    public static bool AttackDown { get { return !Blocked && (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(0)); } }
    public static bool EscapeDown { get { return Input.GetKeyDown(KeyCode.Escape); } }
    public static bool OrbitHeld { get { return !Blocked && Input.GetMouseButton(1); } }
    public static Vector2 LookDelta { get { return Blocked ? Vector2.zero : new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 3f; } }
    public static float Scroll { get { return Blocked ? 0f : Input.mouseScrollDelta.y; } }
#endif
}
