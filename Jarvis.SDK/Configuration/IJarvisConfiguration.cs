namespace Jarvis.SDK.Configuration;

/// <summary>
/// Read-only view over the JARVIS configuration store. Plugins and modules use it instead of
/// reaching into the host configuration stack directly.
/// </summary>
public interface IJarvisConfiguration
{
    /// <summary>Reads a single value using a flat key (e.g. <c>Jarvis:InstanceName</c>).</summary>
    string? GetValue(string key);

    /// <summary>Binds the configuration section with the given name into a typed options object.</summary>
    T? GetSection<T>(string sectionName) where T : class, new();

    /// <summary>Reads every resolved configuration entry as a flat key/value dictionary.</summary>
    IReadOnlyDictionary<string, string> GetAll();
}
