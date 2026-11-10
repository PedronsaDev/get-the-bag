using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
[AddComponentMenu("Reveal/Punch Scale")]
public class PunchStep : RevealStep
{
    [SerializeField] private Transform _target;
    [SerializeField] private float _strength = 0.2f;
    [SerializeField, Min(0.01f)] private float _duration = 0.35f;

    [Tooltip("Let the next step start right away instead of waiting for the punch to finish.")]
    [SerializeField] private bool _dontWait = true;

    private Vector3? _baseScale;

    public override async UniTask Play(RevealContext ctx, CancellationToken ct)
    {
        if (!_target || ctx.SkipRequested) return;

        _baseScale ??= _target.localScale;
        var tween = Tween.PunchScale(_target, Vector3.one * _strength, _duration, useUnscaledTime: true);
        if (_dontWait) return;

        using (ct.Register(() => tween.Stop()))
            await tween;
    }

    public override void ResetVisuals()
    {
        if (!_target || !_baseScale.HasValue) return;
        Tween.StopAll(_target);
        _target.localScale = _baseScale.Value;
    }
}
}
