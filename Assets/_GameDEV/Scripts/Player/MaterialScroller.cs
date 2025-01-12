using UnityEngine;
using Unity.Netcode;

public class MaterialScroller : NetworkBehaviour, IMaterialScroller
{
    [System.Serializable]
    public class MaterialScrollerSettings
    {
        public Material originalMaterial;
        public Renderer targetRenderer;
        public int materialIndex;

        public MaterialScrollerSettings()
        {
            materialIndex = 0;
        }
    }

    [Header("Material Settings")]
    [SerializeField] private float offsetSpeed = -5f;

    [Header("Track Settings")]
    [SerializeField] private MaterialScrollerSettings leftTrack = new MaterialScrollerSettings();
    [SerializeField] private MaterialScrollerSettings rightTrack = new MaterialScrollerSettings();

    // Runtime variables - not serialized
    private NetworkVariable<float> networkOffset = new NetworkVariable<float>(0f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Owner);
    private Material leftInstancedMaterial;
    private Material rightInstancedMaterial;
    private bool initialized;

    private void Awake()
    {
        initialized = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsClient)
        {
            SetupMaterials();
            networkOffset.OnValueChanged += OnOffsetChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        if (IsClient)
        {
            networkOffset.OnValueChanged -= OnOffsetChanged;
            CleanupMaterials();
        }
    }

    private void SetupMaterials()
    {
        if (initialized) return;

        try
        {
            // Sol track için material setup
            if (!SetupSingleMaterial(leftTrack, ref leftInstancedMaterial, "Left"))
            {
                return;
            }

            // Sağ track için material setup
            if (!SetupSingleMaterial(rightTrack, ref rightInstancedMaterial, "Right"))
            {
                CleanupSingleMaterial(ref leftInstancedMaterial);
                return;
            }

            initialized = true;
            Debug.Log($"[MaterialScroller] Both tracks setup completed for {(IsOwner ? "owner" : "client")}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MaterialScroller] Setup error: {e.Message}");
            CleanupMaterials();
        }
    }

    private bool SetupSingleMaterial(MaterialScrollerSettings settings, ref Material instancedMaterial, string trackName)
    {
        if (settings == null)
        {
            Debug.LogError($"[MaterialScroller] {trackName} track settings is null!");
            return false;
        }

        if (settings.targetRenderer == null || settings.originalMaterial == null)
        {
            Debug.LogError($"[MaterialScroller] {trackName} track setup failed - Renderer: {settings.targetRenderer != null}, Material: {settings.originalMaterial != null}");
            return false;
        }

        try
        {
            // Mevcut material'leri al
            Material[] currentMaterials = settings.targetRenderer.sharedMaterials;
            if (currentMaterials == null || currentMaterials.Length == 0)
            {
                Debug.LogError($"[MaterialScroller] {trackName} track has no materials!");
                return false;
            }

            // Material index kontrolü
            if (settings.materialIndex < 0 || settings.materialIndex >= currentMaterials.Length)
            {
                Debug.LogError($"[MaterialScroller] Invalid material index {settings.materialIndex} for {trackName} track. Renderer has {currentMaterials.Length} materials.");
                return false;
            }

            // Yeni material instance oluştur
            instancedMaterial = new Material(settings.originalMaterial);
            Material[] newMaterials = new Material[currentMaterials.Length];
            System.Array.Copy(currentMaterials, newMaterials, currentMaterials.Length);
            newMaterials[settings.materialIndex] = instancedMaterial;

            // Material'leri güvenli bir şekilde ata
            settings.targetRenderer.materials = newMaterials;

            UpdateMaterialOffset(networkOffset.Value, instancedMaterial);
            Debug.Log($"[MaterialScroller] {trackName} track setup completed");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MaterialScroller] {trackName} track setup error: {e.Message}");
            return false;
        }
    }

    private void OnOffsetChanged(float previousValue, float newValue)
    {
        UpdateMaterialOffsets(newValue);
    }

    public void ScrollMaterial(Vector3 moveDirection)
    {
        if (!IsOwner || !IsSpawned || !initialized)
        {
            return;
        }

        float moveMagnitude = moveDirection.magnitude;
        if (moveMagnitude > 0.1f)
        {
            float newOffset = networkOffset.Value + (Time.deltaTime * offsetSpeed * moveMagnitude);
            networkOffset.Value = newOffset;
        }
    }

    private void UpdateMaterialOffsets(float offset)
    {
        if (!initialized) return;

        UpdateMaterialOffset(offset, leftInstancedMaterial);
        UpdateMaterialOffset(offset, rightInstancedMaterial);
    }

    private void UpdateMaterialOffset(float offset, Material material)
    {
        if (material == null) return;

        Vector2 currentOffset = material.mainTextureOffset;
        currentOffset.y = offset;
        material.mainTextureOffset = currentOffset;
    }

    private void CleanupMaterials()
    {
        CleanupSingleMaterial(ref leftInstancedMaterial);
        CleanupSingleMaterial(ref rightInstancedMaterial);
        initialized = false;
    }

    private void CleanupSingleMaterial(ref Material material)
    {
        if (material != null)
        {
            if (Application.isPlaying)
            {
                Destroy(material);
            }
            else
            {
                DestroyImmediate(material);
            }
            material = null;
        }
    }

    public override void OnDestroy()
    {
        CleanupMaterials();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (leftTrack == null) leftTrack = new MaterialScrollerSettings();
        if (rightTrack == null) rightTrack = new MaterialScrollerSettings();

        ValidateMaterialSettings(leftTrack, "Left Track");
        ValidateMaterialSettings(rightTrack, "Right Track");
    }

    private void ValidateMaterialSettings(MaterialScrollerSettings settings, string trackName)
    {
        if (settings.originalMaterial == null)
        {
            Debug.LogWarning($"[MaterialScroller] Please assign a material for {trackName}.");
        }
        if (settings.targetRenderer == null)
        {
            Debug.LogWarning($"[MaterialScroller] Please assign a renderer for {trackName}.");
        }
    }
#endif
}