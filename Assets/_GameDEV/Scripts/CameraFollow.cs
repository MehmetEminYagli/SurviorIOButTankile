using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Takip edilecek hedef
    public Vector3 offset;   // Kameranýn hedefe göre konumu
    public float smoothSpeed = 0.125f; // Yumuþak takip hýzý

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;

            // Ýsteðe baðlý: Kamerayý hedefe bakacak þekilde döndür
            transform.LookAt(target);
        }
    }
}
