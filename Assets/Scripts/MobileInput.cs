using UnityEngine;

public static class MobileInput
{
    // 触屏按钮状态（由 TouchButton 设置）
    public static bool TouchLeft;
    public static bool TouchRight;
    public static bool TouchJump;
    public static bool TouchSwing;
    public static bool TouchSwingDown;

    // 统一接口：键盘 + 触屏
    public static bool MoveLeft()
    {
        return Input.GetKey(KeyCode.A) || TouchLeft;
    }

    public static bool MoveRight()
    {
        return Input.GetKey(KeyCode.D) || TouchRight;
    }

    public static bool JumpDown()
    {
        return Input.GetKey(KeyCode.W) || TouchJump;
    }

    public static bool Swing()
    {
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.Space) || TouchSwing;
    }

    public static bool SwingDown()
    {
        bool keyDown = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.Space);
        bool touch = TouchSwingDown;
        TouchSwingDown = false;
        return keyDown || touch;
    }
}
