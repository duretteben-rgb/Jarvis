namespace Jarvis.SDK.Permissions;

/// <summary>
/// Service that tracks which permissions are granted to which plugin. Permissions are declared
/// in a plugin's manifest and are granted by the host when the plugin loads, subject to the
/// host permission policy.
/// </summary>
public interface IPermissionService
{
    /// <summary>Returns true if <paramref name="permission"/> is granted to <paramref name="pluginId"/>.</summary>
    bool IsGranted(string pluginId, string permission);

    /// <summary>Grants a permission to a plugin.</summary>
    void Grant(string pluginId, string permission);

    /// <summary>Revokes a permission from a plugin.</summary>
    void Revoke(string pluginId, string permission);

    /// <summary>Revokes every permission granted to a plugin.</summary>
    void RevokeAll(string pluginId);

    /// <summary>Snapshot of the permissions currently granted to a plugin.</summary>
    IReadOnlyList<string> GetGrants(string pluginId);
}
