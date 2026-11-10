using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
[AddComponentMenu("Reveal/Type Text")]
public class TypeTextStep : RevealStep
{
    [SerializeField] private TMP_Text _target;

    [Tooltip("TMPEffects tags work here. Tokens: {take}, {banked}, {bankedBefore}.")]
    [SerializeField, TextArea(2, 6)] private string _text = "<+fade>TEXT</+fade>";

    [Tooltip("Show everything at once (show animations still play).")]
    [SerializeField] private bool _instant;

    public override UniTask Play(RevealContext ctx, CancellationToken ct) =>
        RevealText.TypeAsync(_target, ctx.Format(_text), _instant, ctx, ct);

    public override void ResetVisuals() => RevealText.Clear(_target);
}
}
