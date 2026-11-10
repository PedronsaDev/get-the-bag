using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Project.UI.Match.Reveal
{
[Flags]
public enum RevealOutcome
{
    Extracted = 1 << 0,
    LeftBehind = 1 << 1,
    NobodyExtracted = 1 << 2,
    Wiped = 1 << 3,
    TimedOut = 1 << 4,
    NoData = 1 << 5,

    Lost = LeftBehind | NobodyExtracted | Wiped | TimedOut
}

public readonly struct RevealItem
{
    public readonly string Name;
    public readonly int Quantity;
    public readonly int Value;

    public RevealItem(string name, int quantity, int value)
    {
        Name = name;
        Quantity = quantity;
        Value = value;
    }
}

/// <summary>
/// Everything a reveal step may read: the outcome, the numbers behind {tokens}, the haul and the skip flag.
/// Steps never talk to networking or RaidManager directly.
/// </summary>
public class RevealContext
{
    private static readonly Regex TokenPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

    private readonly Dictionary<string, int> _numbers = new();
    private readonly Dictionary<string, string> _texts = new();

    public RevealOutcome Outcome { get; }
    public bool Extracted => Outcome == RevealOutcome.Extracted;
    public IReadOnlyList<RevealItem> Items { get; }
    public Func<float> ReturnSeconds { get; }
    public bool SkipRequested { get; private set; }

    public RevealContext(RevealOutcome outcome, IReadOnlyList<RevealItem> items, Func<float> returnSeconds)
    {
        Outcome = outcome;
        Items = items ?? Array.Empty<RevealItem>();
        ReturnSeconds = returnSeconds;
    }

    public void RequestSkip() => SkipRequested = true;

    public RevealContext SetNumber(string token, int value)
    {
        _numbers[token] = value;
        return this;
    }

    public RevealContext SetText(string token, string value)
    {
        _texts[token] = value;
        return this;
    }

    public int GetNumber(string token) =>
        !string.IsNullOrEmpty(token) && _numbers.TryGetValue(token, out int value) ? value : 0;

    /// <summary>
    /// Replaces {token} with its value. Numbers get thousands separators. Extra tokens (for example
    /// {value} inside a count-up) can be passed in; unknown tokens are left as they are.
    /// </summary>
    public string Format(string template, IReadOnlyDictionary<string, string> extra = null)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return TokenPattern.Replace(template, match =>
        {
            string key = match.Groups[1].Value;
            if (extra != null && extra.TryGetValue(key, out string e)) return e;
            if (_texts.TryGetValue(key, out string t)) return t;
            if (_numbers.TryGetValue(key, out int n)) return n.ToString("N0");
            return match.Value;
        });
    }
}
}
