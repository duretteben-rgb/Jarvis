# JARVIS OS

A modular, extensible personal AI assistant platform inspired by JARVIS. Written in
C# on .NET 8.

> Status: **base skeleton**. This repository currently contains the modular foundation:
> Core, Runtime, plugin system and internal communication. Advanced features (memory, AI
> orchestration, automation, ...) are built on top of it.

## Structure

```
Jarvis/
├── Jarvis.SDK      # Public contracts: events, plugin interfaces, host API
├── Jarvis.Core     # Engine: EventBus, ServiceManager, Configuration, Logging, DI, PluginManager
├── Jarvis.Runtime  # Headless host: Startup, ProcessManager, BackgroundServices
├── Jarvis.Plugins  # Plugin system (Jarvis.Plugins.Example demonstrates the SDK)
└── Jarvis.UI       # Desktop interface (Avalonia)
```

## Prerequisites

- .NET 8 SDK
- A graphical session for the UI (the runtime is headless)

## Quick start

```bash
# Build everything (the runtime also deploys the example plugin)
dotnet build Jarvis.sln

# Run the headless runtime
dotnet run --project Jarvis.Runtime

# Run the desktop UI
dotnet run --project Jarvis.UI
```

## What the skeleton already does

- Boots a full host: configuration, logging, dependency injection.
- Discovers and loads plugins from the `plugins` directory with isolated load contexts.
- Publishes heartbeat events on the EventBus; the example plugin and the UI react to them.
- Shuts down gracefully (Ctrl+C stops the runtime, stops plugins, unloads assemblies).

See `docs/ARCHITECTURE.md` for the full design.
