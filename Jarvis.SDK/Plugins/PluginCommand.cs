namespace Jarvis.SDK.Plugins;

/// <summary>
/// A command exposed by a plugin. Commands make plugins actionable: the host (or another
/// plugin) can invoke them through <see cref="IJarvisPlugin.ExecuteCommandAsync(string, IReadOnlyDictionary{string, object?}?, CancellationToken)"/>.
/// </summary>
/// <param name="Name">Unique name of the command within the plugin (e.g. <c>minecraft.launch</c>).</param>
/// <param name="Description">Short description shown in command discovery UIs.</param>
public sealed record PluginCommand(string Name, string Description);
