using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "PlayerMaterials", menuName = "Game/Player Materials")]
public class PlayerMaterialsData : ScriptableObject
{
    [System.Serializable]
    public class MaterialData
    {
        public string materialName;
        public Material material;
        public Color previewColor;
    }

    public List<MaterialData> availableMaterials = new List<MaterialData>();
} 