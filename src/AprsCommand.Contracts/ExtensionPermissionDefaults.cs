namespace AprsCommand.Contracts;

public static class ExtensionPermissionDefaults
{
    public static IReadOnlyList<ExtensionPermission> DefaultPermissions { get; } = [ExtensionPermission.ReadOnly];

    public static bool IncludesTransmitPermission(IEnumerable<ExtensionPermission>? permissions)
    {
        return permissions?.Any(permission =>
            permission is ExtensionPermission.TransmitAprsIs
                or ExtensionPermission.TransmitRf
                or ExtensionPermission.Admin) == true;
    }
}
