using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{

    public float moveSpeed = 5f; // Hareket h�z�
    public Joystick joystick;   // Joystick referans�
    public Transform cameraTransform; // Kameran�n Transform'u
    private Rigidbody rb;       // Karakterin Rigidbody'si

    void Start()
    {
        rb = GetComponentInChildren<Rigidbody>(); // Rigidbody bile�enini al
    }

    void FixedUpdate()
    {
        // Joystick de�erlerini al
        float horizontal = joystick.Horizontal;
        float vertical = joystick.Vertical;

        // Joystick y�n�n� hesapla
        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            // Kameraya g�re hareket y�n�n� hesapla
            Vector3 cameraForward = cameraTransform.forward;
            Vector3 cameraRight = cameraTransform.right;

            // Y ekseni d�zlemini s�f�rla (sadece yatay hareket)
            cameraForward.y = 0f;
            cameraRight.y = 0f;

            // Kamera y�nlerine g�re joystick y�n�n� hesapla
            Vector3 moveDirection = (cameraForward.normalized * inputDirection.z +
                                     cameraRight.normalized * inputDirection.x).normalized;

            // Hareketi uygula
            rb.MovePosition(rb.position + moveDirection * moveSpeed * Time.fixedDeltaTime);

            // Karakterin y�z y�n�n� ayarla
            Quaternion toRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            rb.rotation = Quaternion.Lerp(rb.rotation, toRotation, Time.fixedDeltaTime * 10f);
        }
    }
}
