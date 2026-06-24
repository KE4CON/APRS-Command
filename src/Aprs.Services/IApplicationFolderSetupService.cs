namespace Aprs.Services;

public interface IApplicationFolderSetupService
{
    ApplicationFolderSetupResult PrepareFolders(
        FirstRunSetupConfiguration configuration,
        bool createDefaultConfigurationFiles = true);

    IReadOnlyList<FirstRunDefaultConfigurationFile> GetDefaultConfigurationFiles(
        FirstRunSetupConfiguration configuration);
}
