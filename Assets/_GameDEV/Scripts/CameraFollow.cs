using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Takip edilecek hedef
    public Vector3 offset;   // Kameran�n hedefe g�re konumu
    public float smoothSpeed = 0.125f; // Yumu�ak takip h�z�

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;

            // �ste�e ba�l�: Kameray� hedefe bakacak �ekilde d�nd�r
            transform.LookAt(target);
        }
    }
}
