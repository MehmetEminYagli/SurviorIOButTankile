using UnityEngine;

public interface IMoveable
{
    void Move(Vector3 direction, float speed);
    void Rotate(Vector3 direction);
}

public interface IGroundChecker
{
    bool IsGrounded { get; }
    void CheckGround();
}

public interface IInputHandler
{
    Vector3 GetMovementInput();
    bool HasInput { get; }
}

public interface IMaterialScroller
{
    void ScrollMaterial(Vector3 moveDirection);
}