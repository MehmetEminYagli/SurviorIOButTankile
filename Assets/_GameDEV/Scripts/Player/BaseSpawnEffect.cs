using UnityEngine;
using Unity.Netcode;
using System.Collections;

public abstract class BaseSpawnEffect : NetworkBehaviour, ISpawnEffect
{
    [Header("Effect Settings")]
    [SerializeField] protected ParticleSystem particleSystem;
    [SerializeField] protected float effectDuration = 2f;

    [Header("Rotation Settings")]
    [Tooltip("The rotation of the effect when spawned (in degrees)")]
    [SerializeField] protected Vector3 spawnRotation = Vector3.zero;
    
    // Rotasyonu dışarıdan okumak için public getter
    public Vector3 GetSpawnRotation() => spawnRotation;
    
    protected Color currentColor;
    private Coroutine destroyCoroutine;
    private NetworkVariable<bool> shouldDestroy = new NetworkVariable<bool>(false);
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        shouldDestroy.OnValueChanged += OnShouldDestroyChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        shouldDestroy.OnValueChanged -= OnShouldDestroyChanged;
    }
    
    private void OnShouldDestroyChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            Debug.Log($"[{(IsServer ? "Server" : "Client")}] Effect {gameObject.name} received destroy signal");
            StopEffect();
            if (!IsServer)
            {
                StartCoroutine(DestroyAfterFrame());
            }
        }
    }

    private IEnumerator DestroyAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] Destroying effect {gameObject.name}");
        Destroy(gameObject);
    }
    
    public virtual void PlayEffect(Vector3 position, Color playerColor)
    {
        if (!IsSpawned) return;
        
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] Playing effect {gameObject.name} at position {position}");
        
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
        }
        
        position.y = 0.01f;
        transform.position = position;
        transform.rotation = Quaternion.Euler(spawnRotation);
        currentColor = playerColor;
        
        if (IsOwner || IsLocalPlayer)
        {
            ApplyEffectVisuals(playerColor);
        }
        
        if (IsServer)
        {
            PlayEffectClientRpc(position, playerColor);
            Debug.Log($"[Server] Starting destroy timer for effect {gameObject.name}");
            destroyCoroutine = StartCoroutine(InitiateDestroySequence());
        }
    }
    
    private void ApplyEffectVisuals(Color color)
    {
        if (particleSystem != null)
        {
            var main = particleSystem.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            
            var trail = particleSystem.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
            
            var childSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var childSystem in childSystems)
            {
                if (childSystem != particleSystem)
                {
                    var childMain = childSystem.main;
                    childMain.startColor = new ParticleSystem.MinMaxGradient(color);
                    
                    var childTrail = childSystem.GetComponent<TrailRenderer>();
                    if (childTrail != null)
                    {
                        childTrail.startColor = color;
                        childTrail.endColor = new Color(color.r, color.g, color.b, 0f);
                    }
                }
            }
            
            particleSystem.Clear();
            particleSystem.Play();
        }
    }
    
    private IEnumerator InitiateDestroySequence()
    {
        yield return new WaitForSeconds(effectDuration);
        
        Debug.Log($"[Server] Initiating destroy sequence for effect {gameObject.name}");
        shouldDestroy.Value = true;
        
        yield return new WaitForEndOfFrame();
        
        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            Debug.Log($"[Server] Despawning network object for effect {gameObject.name}");
            NetworkObject.Despawn(true);
        }
        
        yield return new WaitForEndOfFrame();
        Destroy(gameObject);
    }
    
    [ClientRpc]
    protected virtual void PlayEffectClientRpc(Vector3 position, Color color)
    {
        if (!IsServer && IsLocalPlayer)
        {
            Debug.Log($"[Client] Playing effect {gameObject.name} at position {position}");
            position.y = 0.01f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(spawnRotation);
            currentColor = color;
            
            ApplyEffectVisuals(color);
        }
    }
    
    public virtual void StopEffect()
    {
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] Stopping particle systems for effect {gameObject.name}");
        if (particleSystem != null)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            particleSystem.Clear();
            
            var childSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var childSystem in childSystems)
            {
                if (childSystem != particleSystem)
                {
                    childSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    childSystem.Clear();
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] OnDestroy called for effect {gameObject.name}");
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
        }
    }
    
    protected virtual void OnValidate()
    {
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
    }
} 