using UnityEngine;

public class PlayerMovement : MonoBehaviour, IMoveable
{
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public void Move(Vector3 direction, float speed)
    {
        _rb.MovePosition(_rb.position + direction * speed * Time.fixedDeltaTime);
    }

    public void Rotate(Vector3 direction)
    {
        if (direction != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
            _rb.rotation = Quaternion.Lerp(_rb.rotation, toRotation, Time.fixedDeltaTime * 10f);
        }
    }
}