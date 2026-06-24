namespace Aprs.Services;

public sealed record ExtensionPermissionSet(IReadOnlySet<ExtensionPermission> Permissions)
{
    public static ExtensionPermissionSet Default { get; } = new(new HashSet<ExtensionPermission> { ExtensionPermission.ReadOnly });

    public bool HasPermission(ExtensionPermission permission)
    {
        return Permissions.Contains(ExtensionPermission.Admin) || Permissions.Contains(permission);
    }

    public bool HasTransmitPermission =>
        HasPermission(ExtensionPermission.TransmitAprsIs)
        || HasPermission(ExtensionPermission.TransmitRf)
        || HasPermission(ExtensionPermission.Admin);
}
