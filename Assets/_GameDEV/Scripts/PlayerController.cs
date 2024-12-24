using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public Joystick joystick;
    public Transform cameraTransform;
    private Rigidbody rb;

    public Material material1;
    public Material material2; 
    public float offsetSpeed = -0.01f; 

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        float horizontal = joystick.Horizontal;
        float vertical = joystick.Vertical;

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            Vector3 moveDirection = (cameraForward.normalized * inputDirection.z + cameraRight.normalized * inputDirection.x).normalized;

            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);

            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            rb.rotation = Quaternion.Lerp(rb.rotation, toRotation, Time.fixedDeltaTime * 10f);

            ScrollTexture(moveDirection);
        }
    }

    private void ScrollTexture(Vector3 moveDirection)
    {
        if (material1 != null)
        {
            Vector2 offsetY = material1.mainTextureOffset;
            offsetY.y += Time.time * offsetSpeed * moveDirection.magnitude;
            material1.mainTextureOffset = offsetY;
        }

        if (material2 != null)
        {
            Vector2 offsetY = material2.mainTextureOffset;
            offsetY.y += Time.time * offsetSpeed * moveDirection.magnitude;
            material2.mainTextureOffset = offsetY;
        }
    }
}
