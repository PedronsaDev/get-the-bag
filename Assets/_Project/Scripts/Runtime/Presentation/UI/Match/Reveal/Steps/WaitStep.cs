using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
[AddComponentMenu("Reveal/Wait")]
public class WaitStep : RevealStep
{
    [SerializeField, Min(0f)] private float _seconds = 0.4f;

    public override UniTask Play(RevealContext ctx, CancellationToken ct) => WaitAsync(_seconds, ctx, ct);
}
}
