using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class SpawnEffectManager : NetworkBehaviour
{
    public static SpawnEffectManager Instance { get; private set; }
    
    [System.Serializable]
    public class EffectEntry
    {
        public string effectName;
        public GameObject effectPrefab;
    }
    
    [SerializeField] private List<EffectEntry> availableEffects;
    [SerializeField] private string defaultEffectName;
    
    private Dictionary<string, GameObject> effectPrefabs = new Dictionary<string, GameObject>();
    private Dictionary<ulong, ISpawnEffect> activeEffects = new Dictionary<ulong, ISpawnEffect>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeEffectPrefabs();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeEffectPrefabs()
    {
        effectPrefabs.Clear();
        foreach (var entry in availableEffects)
        {
            if (entry.effectPrefab != null && !string.IsNullOrEmpty(entry.effectName))
            {
                effectPrefabs[entry.effectName] = entry.effectPrefab;
            }
        }
    }

    private void SetParticleSystemColor(ParticleSystem ps, Color color)
    {
        if (ps == null)
        {
            Debug.LogWarning("SetParticleSystemColor: Particle System is null!");
            return;
        }

        Debug.Log($"Setting particle color to: R:{color.r}, G:{color.g}, B:{color.b}, A:{color.a}");

        // Ana renk ayarı
        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(color);
        Debug.Log("Main particle color set");

        // Particle system'in renderer'ını al
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Debug.Log("Found ParticleSystemRenderer");
            // Materyal renk özelliklerini güncelle
            if (renderer.sharedMaterial != null)
            {
                Debug.Log($"Material name: {renderer.sharedMaterial.name}");
                Material materialCopy = new Material(renderer.sharedMaterial);
                if (materialCopy.HasProperty("_TintColor"))
                {
                    materialCopy.SetColor("_TintColor", color);
                    Debug.Log("Set _TintColor property");
                }
                if (materialCopy.HasProperty("_Color"))
                {
                    materialCopy.SetColor("_Color", color);
                    Debug.Log("Set _Color property");
                }
                if (materialCopy.HasProperty("_EmissionColor"))
                {
                    materialCopy.SetColor("_EmissionColor", color * 2f);
                    Debug.Log("Set _EmissionColor property");
                }
                renderer.material = materialCopy;
            }
            else
            {
                Debug.LogWarning("Particle system renderer material is null!");
            }
        }
        else
        {
            Debug.LogWarning("No ParticleSystemRenderer found!");
        }

        // Renk modüllerini güncelle
        if (ps.colorOverLifetime.enabled)
        {
            Debug.Log("Updating ColorOverLifetime module");
            var col = ps.colorOverLifetime;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            col.color = gradient;
        }

        // Trail renderer varsa onu da güncelle
        var trail = ps.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            Debug.Log("Updating TrailRenderer");
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            
            if (trail.sharedMaterial != null)
            {
                Debug.Log($"Trail material name: {trail.sharedMaterial.name}");
                Material trailMaterialCopy = new Material(trail.sharedMaterial);
                if (trailMaterialCopy.HasProperty("_TintColor"))
                {
                    trailMaterialCopy.SetColor("_TintColor", color);
                    Debug.Log("Set trail _TintColor property");
                }
                if (trailMaterialCopy.HasProperty("_Color"))
                {
                    trailMaterialCopy.SetColor("_Color", color);
                    Debug.Log("Set trail _Color property");
                }
                trail.material = trailMaterialCopy;
            }
        }

        // Alt particle sistemlerini de güncelle
        var childPS = ps.GetComponentsInChildren<ParticleSystem>();
        Debug.Log($"Found {childPS.Length} child particle systems");
        foreach (var childSystem in childPS)
        {
            if (childSystem != ps)
            {
                Debug.Log($"Updating child particle system: {childSystem.name}");
                var childMain = childSystem.main;
                childMain.startColor = new ParticleSystem.MinMaxGradient(color);
                
                // Alt sistemin renderer'ını da güncelle
                var childRenderer = childSystem.GetComponent<ParticleSystemRenderer>();
                if (childRenderer != null && childRenderer.sharedMaterial != null)
                {
                    Debug.Log($"Child material name: {childRenderer.sharedMaterial.name}");
                    Material childMaterialCopy = new Material(childRenderer.sharedMaterial);
                    if (childMaterialCopy.HasProperty("_TintColor"))
                    {
                        childMaterialCopy.SetColor("_TintColor", color);
                        Debug.Log("Set child _TintColor property");
                    }
                    if (childMaterialCopy.HasProperty("_Color"))
                    {
                        childMaterialCopy.SetColor("_Color", color);
                        Debug.Log("Set child _Color property");
                    }
                    if (childMaterialCopy.HasProperty("_EmissionColor"))
                    {
                        childMaterialCopy.SetColor("_EmissionColor", color * 2f);
                        Debug.Log("Set child _EmissionColor property");
                    }
                    childRenderer.material = childMaterialCopy;
                }
            }
        }
    }
    
    public void PlaySpawnEffect(ulong clientId, Vector3 position, Color playerColor, string effectName = "")
    {
        Debug.Log($"Playing spawn effect for client {clientId} with color: R:{playerColor.r}, G:{playerColor.g}, B:{playerColor.b}, A:{playerColor.a}");
        
        if (string.IsNullOrEmpty(effectName))
        {
            effectName = defaultEffectName;
            Debug.Log($"Using default effect: {defaultEffectName}");
        }
        
        if (!effectPrefabs.ContainsKey(effectName))
        {
            Debug.LogWarning($"Effect {effectName} not found!");
            return;
        }
        
        if (activeEffects.ContainsKey(clientId))
        {
            Debug.Log($"Stopping existing effect for client {clientId}");
            activeEffects[clientId].StopEffect();
        }
        
        Vector3 spawnPosition = new Vector3(position.x, 0.01f, position.z);
        GameObject effectInstance = Instantiate(effectPrefabs[effectName], spawnPosition, Quaternion.identity);
        Debug.Log($"Instantiated effect: {effectInstance.name} at position: {spawnPosition}");
        
        // Get the BaseSpawnEffect component to get the rotation
        BaseSpawnEffect baseEffect = effectInstance.GetComponent<BaseSpawnEffect>();
        if (baseEffect != null)
        {
            // Apply the rotation from the prefab
            effectInstance.transform.rotation = Quaternion.Euler(baseEffect.GetSpawnRotation());
        }
        
        // Particle System rengini ayarla
        var particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in particleSystems)
        {
            SetParticleSystemColor(ps, playerColor);
            ps.Clear();
            ps.Play();
        }
        
        NetworkObject networkObject = effectInstance.GetComponent<NetworkObject>();
        
        if (networkObject != null)
        {
            if (IsServer)
            {
                networkObject.Spawn();
            }
            
            ISpawnEffect spawnEffect = effectInstance.GetComponent<ISpawnEffect>();
            
            if (spawnEffect != null)
            {
                activeEffects[clientId] = spawnEffect;
                spawnEffect.PlayEffect(position, playerColor);
                
                if (IsServer)
                {
                    // Renk değişimini tüm clientlara bildir
                    UpdateEffectColorClientRpc(networkObject.NetworkObjectId, playerColor);
                }
            }
            else
            {
                Debug.LogError("Spawn effect prefab does not implement ISpawnEffect!");
                if (IsServer)
                {
                    networkObject.Despawn();
                }
                Destroy(effectInstance);
            }
        }
    }
    
    [ClientRpc]
    private void UpdateEffectColorClientRpc(ulong networkObjectId, Color color)
    {
        if (IsServer) return;
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject networkObject))
        {
            if (networkObject != null)
            {
                var particleSystems = networkObject.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particleSystems)
                {
                    SetParticleSystemColor(ps, color);
                    ps.Clear();
                    ps.Play();
                }
            }
        }
        else
        {
            Debug.LogWarning($"NetworkObject with ID {networkObjectId} not found in SpawnedObjects dictionary");
        }
    }
    
    public void StopEffect(ulong clientId)
    {
        if (activeEffects.TryGetValue(clientId, out ISpawnEffect effect))
        {
            effect.StopEffect();
            activeEffects.Remove(clientId);
        }
    }
    
    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
} 