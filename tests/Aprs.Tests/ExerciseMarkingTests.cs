using Aprs.Services;
using Xunit;

namespace Aprs.Tests;

public sealed class ExerciseMarkingTests
{
    [Fact]
    public void Inactive_LeavesEverythingUnchanged()
    {
        var marking = new ExerciseMarking();

        Assert.False(marking.Active);
        Assert.Equal(string.Empty, marking.MessagePrefix);
        Assert.Equal(0, marking.ReservedMessageLength);
        Assert.Equal("meet at the EOC", marking.MarkBody("meet at the EOC"));
        Assert.Equal("at staging", marking.MarkComment("  at staging  ")); // trims only
        Assert.Equal(string.Empty, marking.MarkComment(""));
    }

    [Fact]
    public void Active_DefaultRepeatIsTwo()
    {
        var marking = new ExerciseMarking();
        marking.Set(active: true, repeat: marking.Repeat);

        Assert.True(marking.Active);
        Assert.Equal(2, marking.Repeat);
        Assert.Equal("EXERCISE EXERCISE ", marking.MessagePrefix);
        Assert.Equal("EXERCISE EXERCISE ".Length, marking.ReservedMessageLength);
    }

    [Fact]
    public void MarkBody_PrependsPrefix_WhenActive()
    {
        var marking = new ExerciseMarking();
        marking.Set(active: true, repeat: 2);

        Assert.Equal("EXERCISE EXERCISE meet at the EOC", marking.MarkBody("meet at the EOC"));
    }

    [Fact]
    public void MarkBody_DoesNotDoubleTag_WhenBodyAlreadyMarked()
    {
        var marking = new ExerciseMarking();
        marking.Set(active: true, repeat: 3);

        // e.g. the operator used the built-in EXERCISE template already carrying the tag.
        var alreadyTagged = "EXERCISE EXERCISE EXERCISE net control check";
        Assert.Equal(alreadyTagged, marking.MarkBody(alreadyTagged));
    }

    [Theory]
    [InlineData("", "EXERCISE")]
    [InlineData("Water point", "Water point EXERCISE")]
    [InlineData("Shelter EXERCISE", "Shelter EXERCISE")]   // already tagged -> unchanged
    public void MarkComment_AppendsTagOnce(string input, string expected)
    {
        var marking = new ExerciseMarking();
        marking.Set(active: true, repeat: 2);

        Assert.Equal(expected, marking.MarkComment(input));
    }

    [Theory]
    [InlineData(0, 1)]   // clamp below
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(9, 3)]   // clamp above
    public void Set_ClampsRepeatToOneThroughThree(int requested, int expected)
    {
        var marking = new ExerciseMarking();
        marking.Set(active: true, repeat: requested);

        Assert.Equal(expected, marking.Repeat);
    }

    [Fact]
    public void Changed_FiresOnlyOnActualChange()
    {
        var marking = new ExerciseMarking();
        var count = 0;
        marking.Changed += (_, _) => count++;

        marking.Set(active: true, repeat: 2);
        marking.Set(active: true, repeat: 2);   // no-op, should not fire
        marking.Set(active: false, repeat: 2);

        Assert.Equal(2, count);
    }
}
