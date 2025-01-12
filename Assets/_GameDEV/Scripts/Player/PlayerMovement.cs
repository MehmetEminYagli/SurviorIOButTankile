using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour, IMoveable
{
    private Rigidbody rb;
    private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>();
    private NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>();

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
        if (rb == null || !IsOwner) return;
        
        Vector3 newPosition = rb.position + direction * speed * Time.fixedDeltaTime;
        rb.MovePosition(newPosition);
        
        if (IsServer)
        {
            networkPosition.Value = newPosition;
        }
    }

    public void Rotate(Vector3 direction)
    {
        if (rb == null || !IsOwner) return;
        
        if (direction != Vector3.zero)
        {
            Quaternion newRotation = Quaternion.LookRotation(direction, Vector3.up);
            rb.rotation = Quaternion.Lerp(rb.rotation, newRotation, Time.fixedDeltaTime * 10f);
            
            if (IsServer)
            {
                networkRotation.Value = newRotation;
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsOwner)
        {
            // Client tarafında pozisyon ve rotasyonu güncelle
            rb.MovePosition(Vector3.Lerp(rb.position, networkPosition.Value, Time.fixedDeltaTime * 10f));
            rb.MoveRotation(Quaternion.Lerp(rb.rotation, networkRotation.Value, Time.fixedDeltaTime * 10f));
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (!IsOwner)
        {
            // İlk spawn olduğunda pozisyon ve rotasyonu senkronize et
            rb.position = networkPosition.Value;
            rb.rotation = networkRotation.Value;
        }
    }
}