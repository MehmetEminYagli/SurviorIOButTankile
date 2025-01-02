using UnityEngine;

public interface IShooter
{
    void Shoot(Vector3 direction, Vector3 spawnPosition);
    bool CanShoot { get; }
}