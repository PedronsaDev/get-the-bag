using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.Collections
{
    /// <summary>
    /// Generic dictionary that serializes via Unity's ISerializationCallbackReceiver.
    /// Allows dictionaries to be saved by JsonUtility and inspected in the Unity Inspector.
    /// </summary>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new();
        [SerializeField] private List<TValue> values = new();

        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();

            if (keys.Count != values.Count)
            {
                Debug.LogError($"[SerializableDictionary] Deserialization mismatch: keys count ({keys.Count}) does not match values count ({values.Count}).");
                return;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                this[keys[i]] = values[i];
            }
        }
    }
}
