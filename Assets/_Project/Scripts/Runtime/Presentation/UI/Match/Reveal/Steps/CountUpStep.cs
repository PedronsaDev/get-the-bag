using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
[AddComponentMenu("Reveal/Count Up")]
public class CountUpStep : RevealStep
{
    [SerializeField] private TMP_Text _target;

    [Tooltip("Token to count to: take, banked or bankedBefore.")]
    [SerializeField] private string _toToken = "take";
    [Tooltip("Token to count from. Empty = 0.")]
    [SerializeField] private string _fromToken = "";

    [Tooltip("Shown while counting. {value} is the current number.")]
    [SerializeField] private string _countingText = "TAKE  ${value}";
    [Tooltip("Shown when done. Looping TMPEffects tags (<wave>, <jump>) work here; show tags (<+grow>) do not.")]
    [SerializeField] private string _finalText = "TAKE  <jump>${value}</jump>";

    [SerializeField, Min(0f)] private float _duration = 1.2f;

    private readonly Dictionary<string, string> _extra = new();

    public override async UniTask Play(RevealContext ctx, CancellationToken ct)
    {
        int from = ctx.GetNumber(_fromToken);
        int to = ctx.GetNumber(_toToken);

        float elapsed = 0f;
        int shown = int.MinValue;
        while (elapsed < _duration && from != to && !ctx.SkipRequested)
        {
            float t = 1f - Mathf.Pow(1f - elapsed / _duration, 3f);
            int value = Mathf.RoundToInt(Mathf.Lerp(from, to, t));
            if (value != shown)
            {
                shown = value;
                RevealText.SetPlain(_target, Format(ctx, _countingText, value));
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
            elapsed += Time.unscaledDeltaTime;
        }

        RevealText.SetPlain(_target, Format(ctx, _finalText, to));
    }

    public override void ResetVisuals() => RevealText.Clear(_target);

    private string Format(RevealContext ctx, string template, int value)
    {
        _extra["value"] = value.ToString("N0");
        return ctx.Format(template, _extra);
    }
}
}
