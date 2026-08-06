using System;
using System.Linq;

namespace Aprs.Services;

/// <summary>
/// Exercise Traffic Marking. When active, stamps "EXERCISE" onto every outbound APRS transmission so
/// drill traffic can never be mistaken for real traffic.
///
/// This is the OPPOSITE of the transmit-inhibit "exercise mode" (<see cref="ITransmitSafetyAuthority"/>'s
/// global inhibit): inhibit means <em>send nothing</em>; marking means <em>send, clearly labeled</em>.
/// A single shared instance is consulted by every per-type outbound formatter (messages, objects/items,
/// position beacons, status, weather), because the raw wire string is assembled per type — there is no
/// single choke point that still knows the semantic fields.
/// </summary>
public sealed class ExerciseMarking
{
    /// <summary>The exercise tag word. Universally recognized EmComm convention.</summary>
    public const string Tag = "EXERCISE";

    /// <summary>Whether marking is currently on.</summary>
    public bool Active { get; private set; }

    /// <summary>How many times the tag is repeated as a message/status prefix (1–3). Default 2
    /// ("EXERCISE EXERCISE ") balances a clear label against the 67-character message budget; 3 is the
    /// classic "EXERCISE EXERCISE EXERCISE" opener but leaves little room. Comment fields always get a
    /// single tag regardless of this count.</summary>
    public int Repeat { get; private set; } = 2;

    /// <summary>Raised whenever <see cref="Active"/> or <see cref="Repeat"/> changes (drives the UI indicator).</summary>
    public event EventHandler? Changed;

    /// <summary>Turn marking on/off and set the repeat count. Clamped to 1–3.</summary>
    public void Set(bool active, int repeat)
    {
        repeat = Math.Clamp(repeat, 1, 3);
        if (Active == active && Repeat == repeat)
        {
            return;
        }

        Active = active;
        Repeat = repeat;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The message/status prefix, e.g. "EXERCISE " or "EXERCISE EXERCISE EXERCISE " (trailing
    /// space included), or an empty string when marking is off.</summary>
    public string MessagePrefix => Active ? string.Concat(Enumerable.Repeat(Tag + " ", Repeat)) : string.Empty;

    /// <summary>Number of characters <see cref="MessagePrefix"/> consumes — reserve this in the 67-char
    /// message-body limit so a marked message never overflows on the air.</summary>
    public int ReservedMessageLength => MessagePrefix.Length;

    /// <summary>Prefix a message or status body with the exercise tag. No-op when inactive, or when the
    /// body is already tagged (prevents doubling when a template already carries the prefix).</summary>
    public string MarkBody(string? body)
    {
        var text = body ?? string.Empty;
        if (!Active)
        {
            return text;
        }

        return text.TrimStart().StartsWith(Tag, StringComparison.OrdinalIgnoreCase)
            ? text
            : MessagePrefix + text;
    }

    /// <summary>Append the exercise tag to a comment field (object/item, position beacon, weather). No-op
    /// when inactive or when the comment already contains the tag.</summary>
    public string MarkComment(string? comment)
    {
        var text = (comment ?? string.Empty).Trim();
        if (!Active)
        {
            return text;
        }

        if (text.Contains(Tag, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        return text.Length == 0 ? Tag : $"{text} {Tag}";
    }
}
