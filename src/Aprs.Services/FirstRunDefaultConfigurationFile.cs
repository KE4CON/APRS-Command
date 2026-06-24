namespace Aprs.Services;

public sealed record FirstRunDefaultConfigurationFile(
    string RelativePath,
    string Description,
    string Content);
