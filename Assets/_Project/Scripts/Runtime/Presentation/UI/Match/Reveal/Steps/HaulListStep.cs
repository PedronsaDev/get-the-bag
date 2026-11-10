using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace Project.UI.Match.Reveal
{
[AddComponentMenu("Reveal/Haul List")]
public class HaulListStep : RevealStep
{
    [SerializeField] private TMP_Text _target;

    [SerializeField, TextArea(1, 3)] private string _title = "<size=70%>HAUL</size>\n";

    [Tooltip("One line per item when you extracted. Tokens: {name}, {qty}, {value}.")]
    [SerializeField, TextArea(1, 3)] private string _itemLine = "<+move>{name}  x{qty}  ${value}</+move><!wait=0.12>\n";

    [Tooltip("One line per item when the haul was lost. Same tokens.")]
    [SerializeField, TextArea(1, 3)] private string _lostItemLine = "<+fade><s>{name}  x{qty}</s></+fade><!wait=0.08>\n";

    [Tooltip("Shown instead of items when the bag was empty.")]
    [SerializeField, TextArea(1, 3)] private string _emptyLine = "<+fade><i>Empty bag.</i></+fade>";

    [SerializeField] private bool _instant;

    private readonly Dictionary<string, string> _extra = new();

    public override UniTask Play(RevealContext ctx, CancellationToken ct) =>
        RevealText.TypeAsync(_target, Build(ctx), _instant, ctx, ct);

    public override void ResetVisuals() => RevealText.Clear(_target);

    private string Build(RevealContext ctx)
    {
        var sb = new StringBuilder(ctx.Format(_title));

        if (ctx.Items.Count == 0)
            return sb.Append(ctx.Format(_emptyLine)).ToString();

        string line = ctx.Extracted ? _itemLine : _lostItemLine;
        foreach (var item in ctx.Items)
        {
            _extra["name"] = item.Name;
            _extra["qty"] = item.Quantity.ToString();
            _extra["value"] = item.Value.ToString("N0");
            sb.Append(ctx.Format(line, _extra));
        }

        return sb.ToString();
    }
}
}
