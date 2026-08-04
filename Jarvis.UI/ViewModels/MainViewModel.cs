using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Jarvis.SDK.Events;
using Jarvis.SDK.Events.SystemEvents;
using Jarvis.SDK.Host;

namespace Jarvis.UI.ViewModels;

/// <summary>
/// Main window view model. Exposes the JARVIS host status and reacts to system events
/// published on the event bus (demonstrating inter-module communication in the UI).
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IDisposable? _heartbeatSubscription;
    private int _heartbeatCount;
    private string _status;

    /// <summary>
    /// Standby instance used at design-time or when no host service provider is available.
    /// </summary>
    public MainViewModel()
    {
        Host = null;
        InstanceName = "JARVIS OS";
        ApplicationVersion = "design-time";
        _status = "Standby";
    }

    public MainViewModel(IJarvisHost host, IEventBus eventBus)
    {
        Host = host;
        InstanceName = host.InstanceName;
        ApplicationVersion = host.ApplicationVersion;
        _status = "Ready";

        foreach (SDK.Plugins.IJarvisPlugin plugin in host.Plugins)
        {
            Plugins.Add($"{plugin.Manifest.Name}  v{plugin.Manifest.Version}");
        }

        // Live indicator: the UI reacts to heartbeat events published by the runtime host.
        _heartbeatSubscription = eventBus.Subscribe<HeartbeatEvent>(async (_, _) =>
        {
            int count = Interlocked.Increment(ref _heartbeatCount);
            await Dispatcher.UIThread.InvokeAsync(() => HeartbeatCount = count);
        });
    }

    /// <summary>The running JARVIS host, or null when in standby mode.</summary>
    public IJarvisHost? Host { get; }

    /// <summary>Configured instance name.</summary>
    public string InstanceName { get; }

    /// <summary>Platform version.</summary>
    public string ApplicationVersion { get; }

    /// <summary>Names of the plugins currently loaded.</summary>
    public ObservableCollection<string> Plugins { get; } = new();

    /// <summary>Number of heartbeat ticks observed on the event bus.</summary>
    public int HeartbeatCount
    {
        get => _heartbeatCount;
        private set
        {
            if (_heartbeatCount != value)
            {
                _heartbeatCount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Overall system status.</summary>
    public string Status
    {
        get => _status;
        private set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Display text for the plugins status card.</summary>
    public string PluginCountText => $"{Plugins.Count} plugin(s) active";

    /// <summary>Display text for the event bus status card.</summary>
    public string EventBusStatus => Host is null ? "Not connected" : "Connected";

    /// <summary>Live system feed line combining the heartbeat count and overall status.</summary>
    public string HeartbeatSummary => $"Heartbeat count: {HeartbeatCount}   |   Status: {Status}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        _heartbeatSubscription?.Dispose();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
