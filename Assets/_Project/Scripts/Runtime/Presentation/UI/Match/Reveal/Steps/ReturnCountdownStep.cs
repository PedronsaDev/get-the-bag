using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
/// <summary>Starts a countdown line that keeps updating in the background. The sequence does not wait for it.</summary>
[AddComponentMenu("Reveal/Return Countdown")]
public class ReturnCountdownStep : RevealStep
{
    [SerializeField] private TMP_Text _target;

    [Tooltip("{seconds} is the time left. Hidden when there is no countdown.")]
    [SerializeField] private string _text = "<size=60%>Back to the hideout in {seconds}</size>";

    private readonly Dictionary<string, string> _extra = new();

    public override UniTask Play(RevealContext ctx, CancellationToken ct)
    {
        RunAsync(ctx, ct).Forget();
        return UniTask.CompletedTask;
    }

    public override void ResetVisuals() => RevealText.Clear(_target);

    private async UniTaskVoid RunAsync(RevealContext ctx, CancellationToken ct)
    {
        int shown = -1;
        while (_target)
        {
            int seconds = Mathf.CeilToInt(ctx.ReturnSeconds?.Invoke() ?? 0f);
            if (seconds != shown)
            {
                shown = seconds;
                _extra["seconds"] = seconds.ToString();
                RevealText.SetPlain(_target, seconds > 0 ? ctx.Format(_text, _extra) : string.Empty);
            }

            if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow())
                return;
        }
    }
}
}
