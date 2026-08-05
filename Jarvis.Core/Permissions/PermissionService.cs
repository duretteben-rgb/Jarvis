using System.Collections.Concurrent;
using Jarvis.SDK.Permissions;
using Microsoft.Extensions.Logging;

namespace Jarvis.Core.Permissions;

/// <summary>
/// Default in-memory implementation of <see cref="IPermissionService"/>. Permissions are
/// granted by the plugin manager when a plugin loads and revoked when it unloads.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly ConcurrentDictionary<string, HashSet<string>> _grants = new();
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(ILogger<PermissionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsGranted(string pluginId, string permission)
        => _grants.TryGetValue(pluginId, out HashSet<string>? permissions)
           && permissions.Contains(permission);

    /// <inheritdoc />
    public void Grant(string pluginId, string permission)
    {
        HashSet<string> permissions = _grants.GetOrAdd(pluginId, static _ => new HashSet<string>(StringComparer.Ordinal));
        lock (permissions)
        {
            if (permissions.Add(permission))
            {
                _logger.LogTrace("Granted permission '{Permission}' to '{Plugin}'.", permission, pluginId);
            }
        }
    }

    /// <inheritdoc />
    public void Revoke(string pluginId, string permission)
    {
        if (_grants.TryGetValue(pluginId, out HashSet<string>? permissions))
        {
            lock (permissions)
            {
                permissions.Remove(permission);
            }
        }
    }

    /// <inheritdoc />
    public void RevokeAll(string pluginId)
    {
        if (_grants.TryRemove(pluginId, out _))
        {
            _logger.LogTrace("Revoked all permissions from '{Plugin}'.", pluginId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetGrants(string pluginId)
        => _grants.TryGetValue(pluginId, out HashSet<string>? permissions)
            ? permissions.ToArray()
            : Array.Empty<string>();
}
