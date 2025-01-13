using UnityEngine;
using Unity.Netcode;
using System.Collections;
using DG.Tweening;

public abstract class BaseSpawnEffect : NetworkBehaviour, ISpawnEffect
{
    [Header("Effect Settings")]
    [SerializeField] protected new ParticleSystem particleSystem;
    [SerializeField] protected float effectDuration = 2f;
    [SerializeField] protected float scaleAnimationDuration = 0.5f;
    [SerializeField] protected float maxScale = 1.5f;

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
        
        // Set initial scale
        transform.localScale = Vector3.one;
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
        
        // Kill any existing tweens
        transform.DOKill();
        
        position.y = 0.01f;
        transform.position = position;
        transform.localScale = Vector3.one;

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
        yield return new WaitForSeconds(effectDuration - scaleAnimationDuration);
        
        if (IsServer)
        {
            PlayDestroyAnimationClientRpc();
        }
        
        yield return new WaitForSeconds(scaleAnimationDuration);
        
        if (IsServer)
        {
            shouldDestroy.Value = true;
            Destroy(gameObject);
        }
    }

    [ClientRpc]
    private void PlayDestroyAnimationClientRpc()
    {
        // Kill any existing tweens
        transform.DOKill();
        
        // Önce büyüt, tamamlanınca küçült
        transform.DOScale(Vector3.one * maxScale, scaleAnimationDuration * 0.5f)
            .SetEase(Ease.InOutQuad)
            .OnComplete(() => {
                transform.DOScale(Vector3.zero, scaleAnimationDuration * 0.5f)
                    .SetEase(Ease.InOutQuad);
            });
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
    
    public override void OnDestroy()
    {
        Debug.Log($"[{(IsServer ? "Server" : "Client")}] OnDestroy called for effect {gameObject.name}");
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
        }
        transform.DOKill();
    }
    
    protected virtual void OnValidate()
    {
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
    }
} 