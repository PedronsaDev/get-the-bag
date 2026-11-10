using System.Threading;
using Cysharp.Threading.Tasks;
using TMPEffects.Components;
using TMPro;

namespace Project.UI.Match.Reveal
{
/// <summary>
/// The two ways a step puts text on screen. Any text change restarts a TMPWriter,
/// so typed text goes through the writer and changing text (counters, timers) turns it off.
/// </summary>
public static class RevealText
{
    /// <summary>Types <paramref name="content"/> with the target's TMPWriter. Without a writer the text just appears.</summary>
    public static async UniTask TypeAsync(TMP_Text text, string content, bool instant, RevealContext ctx, CancellationToken ct)
    {
        if (!text) return;

        text.TryGetComponent(out TMPWriter writer);
        if (writer)
        {
            writer.WriteOnStart = false;
            writer.WriteOnNewText = false;
            writer.UseScaledTime = false;
            if (!writer.enabled) writer.enabled = true;
        }

        text.text = content;
        text.ForceMeshUpdate();

        if (!writer || !writer.isActiveAndEnabled) return;

        writer.RestartWriter();
        while (writer.IsWriting)
        {
            if ((instant || ctx.SkipRequested) && writer.MaySkip)
                writer.SkipWriter(skipShowAnimation: false);
            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }
    }

    /// <summary>Sets text immediately, with the writer off. Use for text that changes every frame.</summary>
    public static void SetPlain(TMP_Text text, string content)
    {
        if (!text) return;

        if (text.TryGetComponent(out TMPWriter writer) && writer.enabled)
            writer.enabled = false;

        text.text = content;
    }

    public static void Clear(TMP_Text text)
    {
        if (text) text.text = string.Empty;
    }
}
}
