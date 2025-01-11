using UnityEngine;
using Unity.Netcode;

public abstract class BaseSpawnEffect : NetworkBehaviour, ISpawnEffect
{
    [SerializeField] protected ParticleSystem particleSystem;
    [SerializeField] protected float effectDuration = 2f;
    
    protected Color currentColor;

    private void Start()
    {
        // Başlangıçta rotasyonu ayarla
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }
    
    public virtual void PlayEffect(Vector3 position, Color playerColor)
    {
        if (!IsSpawned) return;
        
        transform.position = position;
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        currentColor = playerColor;
        
        if (particleSystem != null)
        {
            // Ana particle system'in rengini ayarla
            var main = particleSystem.main;
            main.startColor = new ParticleSystem.MinMaxGradient(playerColor);
            
            // Trail renderer varsa güncelle
            var trail = particleSystem.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startColor = playerColor;
                trail.endColor = new Color(playerColor.r, playerColor.g, playerColor.b, 0f);
            }
            
            // Tüm alt particle systemlerin rengini ayarla
            var childSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var childSystem in childSystems)
            {
                if (childSystem != particleSystem)
                {
                    var childMain = childSystem.main;
                    childMain.startColor = new ParticleSystem.MinMaxGradient(playerColor);
                    
                    // Alt sistemlerin trail renderer'larını da güncelle
                    var childTrail = childSystem.GetComponent<TrailRenderer>();
                    if (childTrail != null)
                    {
                        childTrail.startColor = playerColor;
                        childTrail.endColor = new Color(playerColor.r, playerColor.g, playerColor.b, 0f);
                    }
                }
            }
            
            particleSystem.Clear();
            particleSystem.Play();
        }
        
        PlayEffectClientRpc(position, playerColor);
    }
    
    [ClientRpc]
    protected virtual void PlayEffectClientRpc(Vector3 position, Color color)
    {
        if (IsServer) return;
        
        transform.position = position;
        transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        currentColor = color;
        
        if (particleSystem != null)
        {
            // Ana particle system'in rengini ayarla
            var main = particleSystem.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
            
            // Trail renderer varsa güncelle
            var trail = particleSystem.GetComponent<TrailRenderer>();
            if (trail != null)
            {
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0f);
            }
            
            // Tüm alt particle systemlerin rengini ayarla
            var childSystems = GetComponentsInChildren<ParticleSystem>();
            foreach (var childSystem in childSystems)
            {
                if (childSystem != particleSystem)
                {
                    var childMain = childSystem.main;
                    childMain.startColor = new ParticleSystem.MinMaxGradient(color);
                    
                    // Alt sistemlerin trail renderer'larını da güncelle
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
    
    public virtual void StopEffect()
    {
        if (particleSystem != null)
        {
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            particleSystem.Clear();
            
            // Alt particle systemleri de durdur ve temizle
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
    
    protected virtual void OnValidate()
    {
        if (particleSystem == null)
        {
            particleSystem = GetComponent<ParticleSystem>();
        }
    }

    private void LateUpdate()
    {
        // Her frame'de rotasyonu kontrol et
        if (transform.rotation != Quaternion.Euler(-90f, 0f, 0f))
        {
            transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        }
    }
} 