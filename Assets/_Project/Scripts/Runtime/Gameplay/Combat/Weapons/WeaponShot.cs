using System;
using PurrNet.Packing;
using UnityEngine;

namespace Project.Combat.Weapons
{
    [Serializable]
    public struct WeaponShot : IPackedAuto
    {
        public string WeaponId;
        public WeaponSlot Slot;
        public uint Sequence;
        public Vector3 Origin;
        public Vector3 EndPoint;
        public Vector3 HitNormal;
        public bool Hit;
    }
}
