using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{

    public float moveSpeed = 5f; // Hareket hýzý
    public Joystick joystick;   // Joystick referansý
    public Transform cameraTransform; // Kameranýn Transform'u
    private Rigidbody rb;       // Karakterin Rigidbody'si

    void Start()
    {
        rb = GetComponentInChildren<Rigidbody>(); // Rigidbody bileþenini al
    }

    void FixedUpdate()
    {
        // Joystick deðerlerini al
        float horizontal = joystick.Horizontal;
        float vertical = joystick.Vertical;

        // Joystick yönünü hesapla
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            // Kameraya göre hareket yönünü hesapla
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            // Y ekseni düzlemini sýfýrla (sadece yatay hareket)
            cameraForward.y = 0f;
            cameraRight.y = 0f;

            // Kamera yönlerine göre joystick yönünü hesapla
            Vector3 moveDirection = (cameraForward.normalized * inputDirection.z +
                                     cameraRight.normalized * inputDirection.x).normalized;

            // Hareketi uygula
            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);

            // Karakterin yüz yönünü ayarla
            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            rb.rotation = Quaternion.Lerp(rb.rotation, toRotation, Time.fixedDeltaTime * 10f);
        }
    }
}
