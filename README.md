# JARVIS OS

A modular, extensible personal AI assistant platform inspired by JARVIS. Written in
C# on .NET 8.

> Status: **professional plugin system + memory system**. The repository contains the modular
> foundation (Core, Runtime, SDK, plugin system with permissions/versioning/events, dynamic
> loading) and the JARVIS memory system (SQLite, local embeddings, vector search, preferences),
> plus four feature plugins built entirely on the SDK.

## Structure

```
Jarvis/
├── Jarvis.SDK      # Public contracts: events, plugin interfaces, host API, memory contracts
├── Jarvis.Core     # Engine: EventBus, ServiceManager, Configuration, Logging, DI, PluginManager
├── Jarvis.Runtime  # Headless host: Startup, ProcessManager, BackgroundServices
├── Jarvis.Memory   # SQLite-backed memory: embeddings, vector search, preferences
├── Jarvis.Plugins  # Plugin system (Example, Minecraft, Desktop, Automation, AI)
└── Jarvis.UI       # Desktop interface (Avalonia)
```

## Prerequisites

- .NET 8 SDK
- A graphical session for the UI (the runtime is headless)

## Quick start

```bash
# Build everything (the runtime also deploys all plugins)
dotnet build Jarvis.sln

# Run the headless runtime
dotnet run --project Jarvis.Runtime

# Run the desktop UI
dotnet run --project Jarvis.UI
```

## What the platform already does

- Boots a full host: configuration, logging, dependency injection, event bus.
- Loads plugins from the `plugins` directory into isolated load contexts, with semantic
  version checks, dependency resolution and policy-driven permissions (all-or-nothing).
- Hot-loads plugins added or removed under `plugins/` while the runtime runs.
- Routes commands from the host to plugins (`IJarvisHost.ExecuteCommandAsync`) and raises
  `PluginCommandEvent` for every execution.
- Persists facts, knowledge and user preferences in SQLite with deterministic local
  embeddings and cosine-similarity semantic search (`IMemoryService`).
- Publishes heartbeat events on the EventBus; plugins and the UI react to them.
- Shuts down gracefully (Ctrl+C stops the runtime, stops plugins, unloads assemblies).

## Feature plugins (each added without touching Core)

| Plugin | Commands | Permissions |
| --- | --- | --- |
| Minecraft | `minecraft.launch`, `minecraft.stop`, `minecraft.status` | processes, files |
| Desktop | `desktop.notify`, `desktop.screenshot` | ui, system |
| Automation | `automation.list`, `automation.add`, `automation.run` | automation |
| AI | `ai.remember`, `ai.search`, `ai.set-preference`, `ai.get-preference` | ai, memory |

See `docs/ARCHITECTURE.md` for the full design.
