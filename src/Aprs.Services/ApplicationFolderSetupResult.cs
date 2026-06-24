namespace Aprs.Services;

public sealed record ApplicationFolderSetupResult(
    ApplicationFolderLayout Layout,
    FirstRunSetupConfiguration Configuration,
    IReadOnlyList<string> CreatedFolders,
    IReadOnlyList<string> CreatedConfigurationFiles,
    IReadOnlyList<string> Warnings)
{
    public bool Success => Warnings.Count == 0;
}
