using System.Collections.Generic;
using UnityEngine;

namespace Project.AI.Perception
{
public class AICorpse : MonoBehaviour
{
    private static readonly List<AICorpse> _all = new(16);

    public static IReadOnlyList<AICorpse> All => _all;

    public bool Discovered { get; private set; }
    public Vector3 Position => transform.position + Vector3.up;

    private void OnEnable()
    {
        Discovered = false;
        _all.Add(this);
    }

    private void OnDisable() => _all.Remove(this);

    public bool TryDiscover()
    {
        if (Discovered)
            return false;
        Discovered = true;
        return true;
    }
}
}
