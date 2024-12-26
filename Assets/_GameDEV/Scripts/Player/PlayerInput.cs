using UnityEngine;

public class JoystickInput : IInputHandler
{
    private readonly Joystick _joystick;

    public JoystickInput(Joystick joystick)
    {
        _joystick = joystick;
    }

    public Vector3 GetMovementInput()
    {
        return new Vector3(_joystick.Horizontal, 0f, _joystick.Vertical).normalized;
    }

    public bool HasInput => Mathf.Abs(_joystick.Horizontal) > 0.1f || Mathf.Abs(_joystick.Vertical) > 0.1f;
}

public class KeyboardInput : IInputHandler
{
    public Vector3 GetMovementInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        return new Vector3(horizontal, 0f, vertical).normalized;
    }

    public bool HasInput => Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0;
}