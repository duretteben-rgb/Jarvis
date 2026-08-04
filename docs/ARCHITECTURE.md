# JARVIS OS — Architecture

This document describes the base skeleton of JARVIS OS: a modular, extensible personal AI
assistant platform written in C# / .NET 8.

## Module layout

```
Jarvis/
├── Jarvis.SDK      # Public contracts: events, plugin interfaces, host API. No logic.
├── Jarvis.Core     # Engine: EventBus, ServiceManager, Configuration, Logging, DI, PluginManager.
├── Jarvis.Runtime  # Headless host: Startup, ProcessManager, BackgroundServices.
├── Jarvis.Plugins  # Extension system. Each capability ships as a plugin.
│   └── Jarvis.Plugins.Example
└── Jarvis.UI       # Desktop interface (Avalonia).
```

## Dependencies

Only upward references are allowed. This keeps every module replaceable and testable.

```
Jarvis.UI ─────────────┐
Jarvis.Runtime ────────┤
Jarvis.Plugins.* ──────┼──> Jarvis.SDK        (contracts only, no logic)
Jarvis.Core ───────────┘        ▲
                              (no other deps)

Jarvis.Core <── Jarvis.Runtime, Jarvis.UI, Jarvis.Plugins
```

- `Jarvis.SDK` depends on nothing but `Microsoft.Extensions.Logging.Abstractions`.
- `Jarvis.Core` depends on `Jarvis.SDK` + Microsoft.Extensions (Hosting, Logging, DI,
  Configuration).
- `Jarvis.Runtime` and `Jarvis.UI` depend on `Jarvis.Core` + `Jarvis.SDK`.
- Plugins depend only on `Jarvis.SDK`.

## Module responsibilities

### Jarvis.SDK

The contract layer. It defines what a plugin is, what events look like and how modules
communicate. Because plugins only see the SDK, the SDK is the stable public surface.

| Folder | Content |
| --- | --- |
| `Events` | `IEvent`, `JarvisEvent`, `IEventBus`, priorities, system events |
| `Plugins` | `IJarvisPlugin`, `JarvisPluginBase`, `PluginManifest`, `PluginContext` |
| `Services` | `IJarvisService` lifecycle contract |
| `Configuration` | `IJarvisConfiguration` read-only view |
| `Host` | `IJarvisHost` public API (config, event bus, service locator, plugins) |

### Jarvis.Core

The brain. Core orchestrates and never contains feature-specific logic.

| Folder | Responsibility |
| --- | --- |
| `EventBus` | In-process async pub/sub used for all inter-module communication |
| `ServiceManager` | Lifecycle of internal `IJarvisService` instances |
| `Configuration` | Typed access to the configuration store (JSON, env vars, CLI) |
| `Logging` | Central console logging configuration |
| `DependencyInjection` | `AddJarvisCore()` registration and wiring |
| `Hosting` | `JarvisHostFactory`, `HeartbeatService`, `PluginHostedService` |
| `Plugins` | `PluginManager`, `PluginLoader`, isolated load contexts |
| `Host` | `JarvisHost`, the concrete `IJarvisHost` |

### Jarvis.Runtime

The always-on headless host. It bootstraps the core, wires runtime services and can manage
external processes.

| Folder | Responsibility |
| --- | --- |
| `Startup` | `StartupRunner`: builds the host, starts services, waits for shutdown |
| `ProcessManager` | Start / stop / monitor child processes with output capture |
| `BackgroundServices` | Base class for long-running runtime services |

### Jarvis.Plugins

Extension catalog. Every plugin is a class library referencing `Jarvis.SDK` and implementing
`IJarvisPlugin`. Plugins are deployed in their own sub-directory and loaded at startup.

### Jarvis.UI

Desktop shell built with Avalonia (cross-platform, C#, Windows 11 Fluent-inspired). It boots
the same `JarvisHostFactory` as the runtime and renders the host status on the event bus.

## Internal communication

All modules communicate through the `IEventBus` in `Jarvis.SDK`. Publish/subscribe keeps
modules decoupled: a producer does not know its consumers and vice versa.

Example flow (the skeleton demo):

```
Jarvis.Core.Hosting.HeartbeatService
        │  publishes HeartbeatEvent
        ▼
IEventBus (Jarvis.Core.EventBus)
        │  delivers to all subscribers, ordered by priority
        ▼
Jarvis.Plugins.Example   (subscribes in OnStartAsync)
Jarvis.UI.MainViewModel  (subscribes to render a live counter)
```

Design rules of the bus:

- Subscribers run in `EventPriority` order (Critical first).
- A failing subscriber never blocks the others (isolation).
- Publishing is async; subscriptions are removed by disposing the returned `IDisposable`.
- Events are plain SDK types so producers and consumers never share assemblies beyond the SDK.

## Plugin lifecycle

```
Discover ──> Load ──> InitializeAsync(context) ──> StartAsync() ──> Running
                                                            │
        shutdown: StopAsync() ──> DisposeAsync() ──> Unload context
```

- Discovery: `PluginLoader` scans `plugins/` for `<folder>/<folder>.dll` assemblies and picks
  types implementing `IJarvisPlugin`.
- Isolation: each plugin is loaded into its own `AssemblyLoadContext`, so plugins can be
  unloaded/updated without restarting the host. SDK and framework assemblies are shared to
  guarantee type identity.
- Events: `PluginLifecycleEvent` is published on every transition so any module can observe
  plugin state.

## Adding a plugin

1. Create a class library under `Jarvis.Plugins/`.
2. Reference `Jarvis.SDK`.
3. Derive from `JarvisPluginBase` and override `OnInitializeAsync` / `OnStartAsync` /
   `OnStopAsync`.
4. Set `Manifest` (id, name, version, description).
5. Deploy the build output to the runtime's `plugins/<AssemblyName>/` directory.

## Build & run

```bash
# Build everything (the runtime also deploys the example plugin)
dotnet build Jarvis.sln

# Run the headless runtime (heartbeats every 5 s)
dotnet run --project Jarvis.Runtime

# Override the heartbeat interval from the command line
dotnet run --project Jarvis.Runtime -- --Jarvis:HeartbeatIntervalSeconds=1

# Run the desktop UI (needs a graphical session)
dotnet run --project Jarvis.UI
```

## Roadmap (future, per PROJECT_SPEC.md)

The skeleton is intentionally feature-free. Natural next steps, each fitting into an existing
module:

- **Jarvis.Memory**: SQLite + embeddings + semantic search (new Core service, exposed via SDK).
- **AI orchestration**: local models (Ollama) and cloud connectors as Core services.
- **Plugins**: System, Browser, Developer, Gaming, Media, Smart Home, AI.
- **Permission system**: declared in `PluginManifest.Permissions`, enforced by the core.
- **UI**: conversation hub, monitor, settings screens.
- **Tests**: `Jarvis.Tests` covering EventBus, PluginManager and Configuration.
