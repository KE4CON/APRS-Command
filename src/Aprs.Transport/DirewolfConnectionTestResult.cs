namespace Aprs.Transport;

public sealed record DirewolfConnectionTestResult(
    bool IsSuccess,
    string Host,
    int Port,
    DateTimeOffset TestedAtUtc,
    string? FailureReason)
{
    public static DirewolfConnectionTestResult Succeeded(string host, int port, DateTimeOffset testedAtUtc)
    {
        return new DirewolfConnectionTestResult(true, host, port, testedAtUtc, null);
    }

    public static DirewolfConnectionTestResult Failed(string host, int port, DateTimeOffset testedAtUtc, string failureReason)
    {
        return new DirewolfConnectionTestResult(false, host, port, testedAtUtc, failureReason);
    }
}
