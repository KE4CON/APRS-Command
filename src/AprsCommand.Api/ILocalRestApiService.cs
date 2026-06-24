namespace AprsCommand.Api;

public interface ILocalRestApiService
{
    LocalRestApiHostStatus Status { get; }

    IReadOnlyList<LocalRestApiEndpoint> Endpoints { get; }

    Task<LocalRestApiHostStatus> StartAsync(CancellationToken cancellationToken = default);

    Task<LocalRestApiHostStatus> StopAsync(CancellationToken cancellationToken = default);

    Task<LocalRestApiResponse> HandleAsync(LocalRestApiRequest request, CancellationToken cancellationToken = default);
}
