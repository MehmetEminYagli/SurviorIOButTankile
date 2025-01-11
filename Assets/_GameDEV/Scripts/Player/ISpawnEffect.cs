using UnityEngine;

public interface ISpawnEffect
{
    void PlayEffect(Vector3 position, Color playerColor);
    void StopEffect();
} 