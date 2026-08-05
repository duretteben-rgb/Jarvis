using Jarvis.SDK.Configuration;
using Microsoft.Extensions.Configuration;

namespace Jarvis.Core.Configuration;

/// <summary>
/// Default implementation of <see cref="IJarvisConfiguration"/> backed by the Microsoft
/// <see cref="IConfiguration"/> stack (JSON files, environment variables, command line, ...).
/// </summary>
public sealed class JarvisConfiguration : IJarvisConfiguration
{
    private readonly IConfiguration _configuration;
    private readonly Lazy<PluginOptions> _plugins;
    private readonly Lazy<PermissionOptions> _permissions;

    public JarvisConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
        _plugins = new Lazy<PluginOptions>(() =>
            configuration.GetSection("Plugins").Get<PluginOptions>() ?? new PluginOptions());
        _permissions = new Lazy<PermissionOptions>(() =>
            configuration.GetSection("Permissions").Get<PermissionOptions>() ?? new PermissionOptions());
    }

    /// <summary>Typed access to the <c>Plugins</c> section.</summary>
    public PluginOptions Plugins => _plugins.Value;

    /// <summary>Typed access to the <c>Permissions</c> section.</summary>
    public PermissionOptions Permissions => _permissions.Value;

    /// <summary>Typed access to the <c>Jarvis</c> section.</summary>
    public JarvisOptions Jarvis => GetSection<JarvisOptions>("Jarvis") ?? new JarvisOptions();

    /// <inheritdoc />
    public string? GetValue(string key) => _configuration[key];

    /// <inheritdoc />
    public T? GetSection<T>(string sectionName) where T : class, new()
        => _configuration.GetSection(sectionName).Get<T>();

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetAll()
        => _configuration.AsEnumerable().ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value ?? string.Empty);
}
