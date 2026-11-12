using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.Outline
{
    internal static class OutlineMeshCache
    {
        private readonly struct Key : IEquatable<Key>
        {
            public readonly Mesh Source;
            public readonly int Slots;
            public readonly bool Skinned;
            public readonly Vector3Int ScaleKey;
            public Key(Mesh source, int slots, bool skinned, Vector3Int scaleKey) { Source = source; Slots = slots; Skinned = skinned; ScaleKey = scaleKey; }
            public bool Equals(Key other) => Source == other.Source && Slots == other.Slots && Skinned == other.Skinned && ScaleKey == other.ScaleKey;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Source, Slots, Skinned, ScaleKey);
        }

        private sealed class Entry { public Mesh Mesh; public int Users; }
        private static readonly Dictionary<Key, Entry> Entries = new Dictionary<Key, Entry>();

        // With domain reload disabled, static state survives into the next Play Mode
        // session while the meshes it points to were destroyed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayModeEnter() => Entries.Clear();

        public static Mesh Acquire(Mesh source, int slots, bool skinned, Vector3Int scaleKey)
        {
            var key = new Key(source, slots, skinned, scaleKey);
            if (!Entries.TryGetValue(key, out var entry))
            {
                entry = new Entry { Mesh = Create(key) };
                Entries.Add(key, entry); // Register only after successful preparation.
            }
            else if (!entry.Mesh)
            {
                entry.Mesh = Create(key); // Destroyed by something outside the cache; rebuild it.
            }
            entry.Users++;
            return entry.Mesh;
        }

        public static void Release(Mesh source, int slots, bool skinned, Vector3Int scaleKey)
        {
            var key = new Key(source, slots, skinned, scaleKey);
            if (!Entries.TryGetValue(key, out var entry) || --entry.Users > 0) return;
            Entries.Remove(key);
            OutlineMeshPreparation.Destroy(entry.Mesh);
        }

        private static Mesh Create(Key key)
        {
            return OutlineMeshPreparation.Create(key.Source, key.Slots, key.Skinned, OutlineScaleKey.ToRatio(key.ScaleKey));
        }
    }
}
