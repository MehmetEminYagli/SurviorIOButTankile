using UnityEngine;

public interface IShooter
{
    void Shoot(Vector3 direction);
    bool CanShoot { get; }
}