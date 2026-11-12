using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
/// <summary>
/// One beat of a reveal. Put step components on the children of a <see cref="RevealSequence"/>;
/// they play top to bottom. To add a new effect, derive from this and implement Play.
/// </summary>
public abstract class RevealStep : MonoBehaviour
{
    [Tooltip("Outcomes this step plays for.")]
    [SerializeField] private RevealOutcome _when = (RevealOutcome)~0;

    [Tooltip("Extra filter: stealth only, loud only, or only when civilians were killed.")]
    [SerializeField] private RevealCondition _condition = RevealCondition.Always;

    [Tooltip("Start at the same time as the step above instead of waiting for it.")]
    [SerializeField] private bool _withPrevious;

    public bool WithPrevious => _withPrevious;

    public bool PlaysFor(RevealContext ctx) => (_when & ctx.Outcome) != 0 && ctx.Matches(_condition);

    /// <summary>Plays the step. The sequence waits for the returned task.</summary>
    public abstract UniTask Play(RevealContext ctx, CancellationToken ct);

    /// <summary>Called before every run so nothing from the last run stays on screen.</summary>
    public virtual void ResetVisuals() { }

    /// <summary>Waits unscaled time; ends early on skip.</summary>
    protected static async UniTask WaitAsync(float seconds, RevealContext ctx, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < seconds && !ctx.SkipRequested)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            elapsed += Time.unscaledDeltaTime;
        }
    }
}
}
