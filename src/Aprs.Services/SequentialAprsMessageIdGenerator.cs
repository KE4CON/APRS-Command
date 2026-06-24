namespace Aprs.Services;

public sealed class SequentialAprsMessageIdGenerator : IAprsMessageIdGenerator
{
    private int nextValue;

    public SequentialAprsMessageIdGenerator(int seed = 1)
    {
        nextValue = Math.Clamp(seed, 0, 99);
    }

    public string NextId()
    {
        var value = nextValue;
        nextValue = (nextValue + 1) % 100;
        return value.ToString("00");
    }
}
