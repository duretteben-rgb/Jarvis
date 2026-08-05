# JARVIS OS — Architecture

This document describes the architecture of JARVIS OS: a modular, extensible personal AI
assistant platform written in C# / .NET 8. It covers the base skeleton plus the professional
plugin system (permissions, versioning, dynamic loading, events) and the JARVIS memory system
(SQLite, local embeddings, vector search, preferences).

## Module layout

```
Jarvis/
├── Jarvis.SDK      # Public contracts: events, plugin interfaces, host API. No logic.
├── Jarvis.Core     # Engine: EventBus, ServiceManager, Configuration, Logging, DI, PluginManager.
├── Jarvis.Runtime  # Headless host: Startup, ProcessManager, BackgroundServices.
├── Jarvis.Memory   # SQLite-backed memory: embeddings, vector search, preferences.
├── Jarvis.Plugins  # Extension system. Each capability ships as a plugin.
│   ├── Jarvis.Plugins.Example
│   ├── Jarvis.Plugins.Minecraft
│   ├── Jarvis.Plugins.Desktop
│   ├── Jarvis.Plugins.Automation
│   └── Jarvis.Plugins.AI
└── Jarvis.UI       # Desktop interface (Avalonia).
```

## Dependencies

Only upward references are allowed. This keeps every module replaceable and testable.

```
Jarvis.UI ─────────────┐
Jarvis.Runtime ────────┤
Jarvis.Memory ─────────┼──> Jarvis.SDK        (contracts only, no logic)
Jarvis.Plugins.* ──────┤        ▲
Jarvis.Core ───────────┘      (no other deps)

Jarvis.Core <── Jarvis.Runtime, Jarvis.UI
Jarvis.Memory <── Jarvis.Runtime, Jarvis.UI
```

- `Jarvis.SDK` depends on nothing but `Microsoft.Extensions.Logging.Abstractions`.
- `Jarvis.Core` depends on `Jarvis.SDK` + Microsoft.Extensions (Hosting, Logging, DI,
  Configuration).
- `Jarvis.Memory` depends on `Jarvis.SDK` + Microsoft.Data.Sqlite + Microsoft.Extensions
  (Options, DI abstractions, Logging).
- `Jarvis.Runtime` and `Jarvis.UI` depend on `Jarvis.Core`, `Jarvis.Memory` + `Jarvis.SDK`.
- Plugins depend only on `Jarvis.SDK` and are therefore fully decoupled from Core, Memory
  and each other. New capabilities are added as plugins without touching Core.

## Module responsibilities

### Jarvis.SDK

The contract layer. It defines what a plugin is, what events look like and how modules
communicate. Because plugins only see the SDK, the SDK is the stable public surface.

| Folder | Content |
| --- | --- |
| `Events` | `IEvent`, `JarvisEvent`, `IEventBus`, priorities, system events |
| `Plugins` | `IJarvisPlugin`, `JarvisPluginBase`, `PluginManifest`, `PluginCommand`, `PluginContext` |
| `Permissions` | `IPermissionService`, well-known `PermissionIds` |
| `Memory` | `IMemoryService`, `MemoryEntry`, `MemorySearchResult`, `MemoryKind` |
| `Services` | `IJarvisService` lifecycle contract |
| `Configuration` | `IJarvisConfiguration` read-only view |
| `Host` | `IJarvisHost` public API (config, event bus, permissions, commands, plugins) |

### Jarvis.Core

The brain. Core orchestrates and never contains feature-specific logic.

| Folder | Responsibility |
| --- | --- |
| `EventBus` | In-process async pub/sub used for all inter-module communication |
| `ServiceManager` | Lifecycle of internal `IJarvisService` instances |
| `Configuration` | Typed access to the configuration store (JSON, env vars, CLI) |
| `Logging` | Central console logging configuration |
| `DependencyInjection` | `AddJarvisCore()` registration and wiring |
| `Hosting` | `JarvisHostFactory`, `HeartbeatService`, `PluginHostedService`, `PluginWatchService` |
| `Plugins` | `PluginManager`, `PluginLoader`, version/dependency validation, isolated load contexts |
| `Permissions` | `PermissionService`, policy-driven grants (all-or-nothing per plugin) |
| `Host` | `JarvisHost`, the concrete `IJarvisHost` |

### Jarvis.Runtime

The always-on headless host. It bootstraps the core, wires runtime services and can manage
external processes.

| Folder | Responsibility |
| --- | --- |
| `Startup` | `StartupRunner`: builds the host, wires runtime services, waits for shutdown |
| `ProcessManager` | Start / stop / monitor child processes with output capture |
| `BackgroundServices` | Base class for long-running runtime services |

### Jarvis.Memory

The self-contained memory system of JARVIS OS. It is registered by the runtime (or UI) via
`AddJarvisMemory()` and exposed to plugins through `IMemoryService` in the SDK.

| Folder | Responsibility |
| --- | --- |
| `Database` | `MemoryDatabase` (serialized SQLite connection + schema), `DatabaseSchema` |
| `Repository` | `MemoryRepository`: CRUD for entries and user preferences |
| `Embedding` | `HashEmbeddingService`: deterministic local embeddings (no external model) |
| `VectorStore` | `VectorSearch`: cosine-similarity ranking of stored entries |
| `Configuration` | `MemoryOptions`, `EmbeddingOptions` (bound from `Memory` / `Embeddings` sections) |
| `DependencyInjection` | `AddJarvisMemory()` registration and wiring |

Search pipeline: content -> embedding -> stored in SQLite; on search, the query is embedded
and ranked against stored vectors with cosine similarity. Because embeddings are deterministic
hashes, results are stable across restarts and no external service is required.

### Jarvis.Plugins

Extension catalog. Every plugin is a class library referencing `Jarvis.SDK` and implementing
`IJarvisPlugin` (usually by deriving from `JarvisPluginBase`). Plugins are deployed in their
own sub-directory and loaded at startup or dynamically while the host runs.

Built-in plugins (each is a feature added without touching Core):

- `Jarvis.Plugins.Minecraft` — launch/stop/status of a Minecraft server (`processes`).
- `Jarvis.Plugins.Desktop` — notifications, screenshots, desktop actions (`ui`, `system`).
- `Jarvis.Plugins.Automation` — register, list and run automations (`automation`).
- `Jarvis.Plugins.AI` — semantic memory commands backed by `IMemoryService` (`ai`, `memory`).
- `Jarvis.Plugins.Example` — reference plugin demonstrating the SDK and the EventBus.

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
Discover ──> Validate (version + dependencies + permissions) ──> Load ──> InitializeAsync(context)
    ──> StartAsync() ──> Running
                        │
shutdown: StopAsync() ──> DisposeAsync() ──> Unload context
```

- Discovery: `PluginLoader` scans `plugins/` for `<folder>/<folder>.dll` assemblies and picks
  types implementing `IJarvisPlugin`.
- Version management: manifests carry a semantic version and a minimum core version. The
  `PluginVersionValidator` checks the running platform against `MinimumCoreVersion`
  (SemVer 2.0.0 precedence) and rejects incompatible plugins.
- Dependency resolution: `PluginDependencyResolver` orders plugin loading by declared
  dependencies and rejects missing or circular dependencies.
- Permissions: each manifest declares the permissions it needs. The host applies the policy in
  the `Permissions` configuration section (`AllowAll`, or an explicit allow-list). Grants are
  all-or-nothing: if any requested permission is not granted, the plugin is rejected so it can
  never run partially authorized. `IPermissionService` on the host lets plugins check their own
  grants at runtime.
- Isolation: each plugin is loaded into its own `AssemblyLoadContext`, so plugins can be
  unloaded/updated without restarting the host. SDK and framework assemblies are shared to
  guarantee type identity.
- Dynamic loading: `PluginWatchService` (a `FileSystemWatcher` with debounce) reacts to new or
  removed plugin folders in `plugins/` while the host runs.
- Events: `PluginLifecycleEvent` is published on every transition and `PluginCommandEvent` is
  published after each command execution, so any module can observe plugin state.

## Commands

Plugins expose an immutable list of `PluginCommand` (name + description) and handle invocations
through `ExecuteCommandAsync`. The host routes commands to the owning plugin via
`IJarvisHost.ExecuteCommandAsync(pluginId, command, parameters)`, which the UI or other modules
can call. Every execution publishes a `PluginCommandEvent`. Unknown commands throw a
`PluginException` with the plugin id.

## Adding a plugin

1. Create a class library under `Jarvis.Plugins/` and add it to `Jarvis.sln`.
2. Reference `Jarvis.SDK` only.
3. Derive from `JarvisPluginBase` and override `OnInitializeAsync` / `OnStartAsync` /
   `OnStopAsync` and `ExecuteCommandAsync` as needed.
4. Set `Manifest` (id, name, version, minimum core version, permissions, dependencies).
5. Expose an immutable `Commands` list.
6. Build. The runtime's `DeployPlugins` target automatically deploys every plugin project
   under `Jarvis.Plugins/` into `plugins/<AssemblyName>/`, where the loader finds it.

## Memory usage

Modules and plugins read/write memory through `IMemoryService` (SDK):

```csharp
IMemoryService memory = host.Services.GetService<IMemoryService>()!;
await memory.StoreAsync(new MemoryEntry { Kind = MemoryKind.LongTerm, Content = "..." });
IReadOnlyList<MemorySearchResult> hits = await memory.SearchAsync("query", kind: MemoryKind.LongTerm);
await memory.SetPreferenceAsync("theme", "dark");
string? theme = await memory.GetPreferenceAsync("theme");
```

Entries are persisted in a SQLite database (`data/jarvis-memory.db` by default), so facts,
knowledge and preferences survive host restarts.

## Build & run

```bash
# Build everything. The runtime deploys all plugins into plugins/.
dotnet build Jarvis.sln

# Run the headless runtime (heartbeats every 5 s, plugins loaded from plugins/)
dotnet run --project Jarvis.Runtime

# Override the heartbeat interval from the command line
dotnet run --project Jarvis.Runtime -- --Jarvis:HeartbeatIntervalSeconds=1

# Restrict plugin permissions to an explicit allow-list
dotnet run --project Jarvis.Runtime -- --Permissions:AllowAll=false --Permissions:Allowed:0=ai

# Run the desktop UI (needs a graphical session)
dotnet run --project Jarvis.UI
```

## Roadmap (future, per PROJECT_SPEC.md)

Each item slots into an existing module without touching Core:

- **AI orchestration**: local models (Ollama) and cloud connectors as Core services or an AI
  plugin; richer embeddings backed by a real model in place of the hash provider.
- **Plugins**: System, Browser, Developer, Gaming, Media, Smart Home.
- **UI**: conversation hub, monitor, settings screens wired to `IMemoryService` and commands.
- **Tests**: `Jarvis.Tests` covering EventBus, PluginManager, permissions and memory.
