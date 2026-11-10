using System;
using UnityEngine;

namespace Project.Combat.Weapons
{
public static class GunfireNoise
{
    public static event Action<Vector3, GameObject> OnPlayerShot;

    public static void Raise(Vector3 origin, GameObject shooter)
    {
        OnPlayerShot?.Invoke(origin, shooter);
    }
}
}
