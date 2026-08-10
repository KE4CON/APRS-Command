using System.Text;
using Aprs.Core;
using Aprs.Transport;
using Xunit;

namespace Aprs.Tests;

/// <summary>
/// In-suite, deterministic (seeded) fuzzing that actually runs in CI — the standalone FuzzHarness connects
/// to live APRS-IS and only exercises AprsParser. The contract for every parser/codec is the same: NEVER
/// throw and NEVER hang on malformed/hostile/truncated input — degrade to validation errors / Unknown / an
/// empty frame list. Deep-audit closed the gap that these ran nowhere.
/// </summary>
public sealed class DeterministicFuzzTests
{
    private static readonly DateTimeOffset When = DateTimeOffset.UnixEpoch;

    // Seed fragments drawn from real APRS structure, so mutation explores realistic parser branches.
    private static readonly string[] Seeds =
    {
        "N0CALL>APRS,TCPIP*:=3903.50N/07201.75W-Test",
        "K1ABC>APRS:!/5L!!<*e7>7P[",
        "K1ABC>APRS:@092345z4903.50N/07201.75W_225/000g000t050r000p001h00b10138",
        "K1ABC>APRS:;LEADER   *092345z4903.50N/07201.75W>Object",
        "K1ABC>APRS:)ITEM!4903.50N/07201.75WrItem",
        "K1ABC>APRS::WU2Z     :Testing{003",
        "K1ABC>APRS:T#005,199,000,255,073,123,01101001",
        "GL7RKJ>APRS:`c51l!{>/\"4)}MicE",
        "K1ABC>APRS:_10090556c220s004g005t077r000p000P000h50b09900",
        "K1ABC>APRS:>092345zStatus",
    };

    [Fact]
    public void AprsParser_NeverThrowsOnRandomAndMutatedInput()
    {
        var rng = new Random(1234567); // fixed seed — deterministic, reproducible
        var parser = new AprsParser();
        var deadline = DateTime.UtcNow.AddSeconds(20); // hang guard

        for (var i = 0; i < 200_000; i++)
        {
            var line = i % 3 == 0 ? RandomAscii(rng) : Mutate(Seeds[rng.Next(Seeds.Length)], rng);

            // Must not throw. (Parse returns an Unknown/invalid packet for garbage; it never throws.)
            var packet = parser.Parse(line, When);
            Assert.NotNull(packet);

            if (i % 5000 == 0)
            {
                Assert.True(DateTime.UtcNow < deadline, $"Parser fuzz exceeded time budget at iteration {i} — possible hang.");
            }
        }
    }

    [Fact]
    public void FrameCodecs_NeverThrowOrHangOnRandomBytes()
    {
        var rng = new Random(76543210);
        var agwpe = new AgwpeFrameCodec();
        var deadline = DateTime.UtcNow.AddSeconds(20);

        for (var i = 0; i < 100_000; i++)
        {
            var bytes = RandomBytes(rng, rng.Next(0, 300));

            // KISS (static) and AGWPE (instance) decoders + their frame-boundary scanners must be
            // total functions over arbitrary bytes — no throw, no infinite loop.
            _ = KissFrameCodec.DecodeMany(bytes, When, "fuzz");
            _ = KissFrameCodec.FindLastCompleteFrameEnd(bytes);
            _ = agwpe.DecodeMany(bytes, When, "fuzz");
            _ = agwpe.FindLastCompleteFrameEnd(bytes);

            if (i % 5000 == 0)
            {
                Assert.True(DateTime.UtcNow < deadline, $"Codec fuzz exceeded time budget at iteration {i} — possible hang.");
            }
        }
    }

    private static string RandomAscii(Random rng)
    {
        var len = rng.Next(0, 120);
        var sb = new StringBuilder(len);
        for (var i = 0; i < len; i++)
        {
            sb.Append((char)rng.Next(0, 128)); // includes control chars, digits, letters, punctuation
        }
        return sb.ToString();
    }

    private static string Mutate(string seed, Random rng)
    {
        var chars = seed.ToCharArray();
        var edits = rng.Next(1, 6);
        for (var e = 0; e < edits && chars.Length > 0; e++)
        {
            var op = rng.Next(3);
            var idx = rng.Next(chars.Length);
            switch (op)
            {
                case 0: chars[idx] = (char)rng.Next(0, 128); break;                  // flip a byte
                case 1: chars[idx] = chars[rng.Next(chars.Length)]; break;           // duplicate another
                case 2: chars[idx] = "/\\>:;,!_@=`'{}".Substring(rng.Next(13), 1)[0]; break; // inject a DTI/delimiter
            }
        }
        // Occasionally truncate to exercise short/incomplete inputs.
        var s = new string(chars);
        return rng.Next(4) == 0 && s.Length > 0 ? s[..rng.Next(s.Length)] : s;
    }

    private static byte[] RandomBytes(Random rng, int count)
    {
        var b = new byte[count];
        rng.NextBytes(b);
        // Salt in KISS/AGWPE control bytes so frame scanners hit real boundary logic.
        for (var i = 0; i < b.Length; i++)
        {
            if (rng.Next(6) == 0) b[i] = (byte)(rng.Next(2) == 0 ? 0xC0 /* KISS FEND */ : 0x00);
        }
        return b;
    }
}
