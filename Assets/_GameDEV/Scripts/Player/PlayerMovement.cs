using UnityEngine;

public class PlayerMovement : MonoBehaviour, IMoveable
{
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Rigidbody component is missing!");
            enabled = false;
        }
    }

    public void Move(Vector3 direction, float speed)
    {
        if (rb == null) return;
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
    }

    public void Rotate(Vector3 direction)
    {
        if (rb == null) return;
        if (direction != Vector3.zero)
        {
            Quaternion toRotation = Quaternion.LookRotation(direction, Vector3.up);
            rb.rotation = Quaternion.Lerp(rb.rotation, toRotation, Time.fixedDeltaTime * 10f);
        }
    }
}