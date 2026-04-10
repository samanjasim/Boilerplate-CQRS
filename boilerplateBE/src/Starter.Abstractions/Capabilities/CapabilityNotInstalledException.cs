namespace Starter.Abstractions.Capabilities;

/// <summary>
/// Thrown by Null Object capability implementations when a write operation is
/// attempted but the providing module is not installed in the current build.
///
/// The global exception middleware maps this to <c>501 Not Implemented</c>
/// with a message that names the missing module so operators can take action.
///
/// Read operations on Null Objects should return safe defaults (empty lists,
/// "free tier" values, etc.) rather than throw — only writes throw.
/// </summary>
public sealed class CapabilityNotInstalledException : Exception
{
    public string CapabilityName { get; }
    public string? ModuleName { get; }

    public CapabilityNotInstalledException(string capabilityName, string? moduleName = null)
        : base(BuildMessage(capabilityName, moduleName))
    {
        CapabilityName = capabilityName;
        ModuleName = moduleName;
    }

    private static string BuildMessage(string capabilityName, string? moduleName) =>
        moduleName is null
            ? $"The capability '{capabilityName}' is not installed in this build."
            : $"The capability '{capabilityName}' is not installed in this build. " +
              $"Install the {moduleName} module to enable it.";
}
