using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
/// <summary>
/// Plays the <see cref="RevealStep"/>s on its children in hierarchy order.
/// Steps marked "With Previous" run together with the step above them.
/// Disabled or inactive steps, and steps whose outcome filter does not match, are skipped.
/// </summary>
public class RevealSequence : MonoBehaviour
{
    private readonly List<RevealStep> _steps = new();
    private readonly List<UniTask> _group = new();

    public async UniTask Play(RevealContext ctx, CancellationToken ct)
    {
        CollectSteps(ctx);

        int i = 0;
        while (i < _steps.Count)
        {
            _group.Clear();
            _group.Add(_steps[i].Play(ctx, ct));
            i++;

            while (i < _steps.Count && _steps[i].WithPrevious)
            {
                _group.Add(_steps[i].Play(ctx, ct));
                i++;
            }

            if (_group.Count == 1) await _group[0];
            else await UniTask.WhenAll(_group.ToArray());
        }
    }

    public void ResetVisuals()
    {
        foreach (var step in GetComponentsInChildren<RevealStep>(true))
            step.ResetVisuals();
    }

    private void CollectSteps(RevealContext ctx)
    {
        _steps.Clear();
        foreach (Transform child in transform)
        {
            if (!child.gameObject.activeSelf) continue;

            foreach (var step in child.GetComponents<RevealStep>())
            {
                if (step.enabled && step.PlaysFor(ctx))
                    _steps.Add(step);
            }
        }
    }
}
}
