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
            public readonly bool Prepared;
            public Key(Mesh source, int slots, bool skinned, bool prepared) { Source = source; Slots = slots; Skinned = skinned; Prepared = prepared; }
            public bool Equals(Key other) => Source == other.Source && Slots == other.Slots && Skinned == other.Skinned && Prepared == other.Prepared;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(Source, Slots, Skinned, Prepared);
        }

        private sealed class Entry { public Mesh Mesh; public int Users; }
        private static readonly Dictionary<Key, Entry> Entries = new Dictionary<Key, Entry>();

        public static Mesh Acquire(Mesh source, int slots, bool skinned, bool prepared)
        {
            var key = new Key(source, slots, skinned, prepared);
            if (!Entries.TryGetValue(key, out var entry))
            {
                var mesh = OutlineMeshPreparation.Create(source, slots, skinned, prepared);
                entry = new Entry { Mesh = mesh };
                Entries.Add(key, entry); // Register only after successful preparation.
            }
            entry.Users++;
            return entry.Mesh;
        }

        public static void Release(Mesh source, int slots, bool skinned, bool prepared)
        {
            var key = new Key(source, slots, skinned, prepared);
            if (!Entries.TryGetValue(key, out var entry) || --entry.Users > 0) return;
            Entries.Remove(key);
            OutlineMeshPreparation.Destroy(entry.Mesh);
        }
    }
}
