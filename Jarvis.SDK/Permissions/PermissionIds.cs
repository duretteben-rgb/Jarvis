namespace Jarvis.SDK.Permissions;

/// <summary>
/// Well-known permission identifiers that plugins can declare in their manifest. Hosts apply a
/// permission policy (see <c>Permissions</c> configuration section) before granting them.
/// </summary>
public static class PermissionIds
{
    /// <summary>Access to core orchestration services.</summary>
    public const string Core = "core";

    /// <summary>Read/write access to the user file system.</summary>
    public const string Files = "files";

    /// <summary>Ability to start and stop processes.</summary>
    public const string Processes = "processes";

    /// <summary>Access to the network.</summary>
    public const string Network = "network";

    /// <summary>Ability to modify system settings.</summary>
    public const string System = "system";

    /// <summary>Read/write access to the JARVIS memory system.</summary>
    public const string Memory = "memory";

    /// <summary>Access to AI models and inference.</summary>
    public const string AI = "ai";

    /// <summary>Ability to define and run automations.</summary>
    public const string Automation = "automation";

    /// <summary>Ability to control the user interface.</summary>
    public const string UserInterface = "ui";

    /// <summary>Read/write access to the JARVIS configuration store.</summary>
    public const string Config = "config";
}
