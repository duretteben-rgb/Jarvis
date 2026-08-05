namespace Jarvis.Core.Configuration;

/// <summary>
/// Permission policy bound from the <c>Permissions</c> configuration section. The policy decides
/// which permissions declared by plugin manifests are actually granted.
/// </summary>
public sealed class PermissionOptions
{
    /// <summary>
    /// When true, every permission declared by a plugin is granted. When false, only the
    /// permissions listed in <see cref="Allowed"/> are granted and plugins requesting anything
    /// else are rejected.
    /// </summary>
    public bool AllowAll { get; set; } = true;

    /// <summary>Explicitly allowed permissions, used when <see cref="AllowAll"/> is false.</summary>
    public List<string> Allowed { get; set; } = new();
}
