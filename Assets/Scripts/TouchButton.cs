using UnityEngine;
using UnityEngine.EventSystems;

public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum ButtonType { Left, Right, Jump, Swing }
    public ButtonType type = ButtonType.Swing;

    private bool pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        switch (type)
        {
            case ButtonType.Left: MobileInput.TouchLeft = true; break;
            case ButtonType.Right: MobileInput.TouchRight = true; break;
            case ButtonType.Jump: MobileInput.TouchJump = true; break;
            case ButtonType.Swing:
                MobileInput.TouchSwing = true;
                MobileInput.TouchSwingDown = true;
                break;
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        switch (type)
        {
            case ButtonType.Left: MobileInput.TouchLeft = false; break;
            case ButtonType.Right: MobileInput.TouchRight = false; break;
            case ButtonType.Jump: MobileInput.TouchJump = false; break;
            case ButtonType.Swing: MobileInput.TouchSwing = false; break;
        }
    }

    void OnDisable()
    {
        if (pressed)
        {
            pressed = false;
            switch (type)
            {
                case ButtonType.Left: MobileInput.TouchLeft = false; break;
                case ButtonType.Right: MobileInput.TouchRight = false; break;
                case ButtonType.Jump: MobileInput.TouchJump = false; break;
                case ButtonType.Swing: MobileInput.TouchSwing = false; break;
            }
        }
    }
}
