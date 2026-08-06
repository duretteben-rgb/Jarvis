# JARVIS OS

A modular, extensible personal AI assistant platform inspired by JARVIS. Written in
C# on .NET 8.

> Status: **plugin system + memory + local AI engine + HUB shell + PC control**. The repository
> contains the modular foundation (Core, Runtime, SDK, plugin system with
> permissions/versioning/events, dynamic loading), the JARVIS memory system (SQLite, local
> embeddings, vector search, preferences), a local-first AI engine (Ollama + OpenAI-compatible
> providers, model routing, conversation context), a REST/SSE API host, an Electron HUB with a
> Windows 11 Fluent/Mica UI, and a PC-control plugin (processes, files, hardware, apps).

## Structure

```
Jarvis/
├── Jarvis.SDK      # Public contracts: events, plugin interfaces, host API, AI + memory contracts
├── Jarvis.Core     # Engine: EventBus, ServiceManager, Configuration, Logging, DI, PluginManager
├── Jarvis.Runtime  # Headless host: Startup, ProcessManager, BackgroundServices
├── Jarvis.Memory   # SQLite-backed memory: embeddings, vector search, preferences
├── Jarvis.AI       # AI engine: Ollama + OpenAI-compatible providers, model router, context
├── Jarvis.API      # REST + SSE host: memory, plugins, AI (also serves the HUB renderer)
├── Jarvis.Hub      # Electron shell (Windows 11 Fluent/Mica) wrapping the API renderer
├── Jarvis.Plugins  # Plugin system (Example, Minecraft, Desktop, System, Automation, AI)
└── Jarvis.UI       # Desktop interface (Avalonia)
```

## Prerequisites

- .NET 8 SDK
- Node.js 18+ (HUB only)
- A graphical session for the UI (the runtime and API are headless)
- Optional: Ollama for fully local models, or a free Groq API key for cloud models

## Quick start

```bash
# Build everything (runtime and API also deploy all plugins)
dotnet build Jarvis.sln

# Run the headless runtime
dotnet run --project Jarvis.Runtime

# Run the web/API host (serves the HUB UI at http://localhost:5178)
dotnet run --project Jarvis.API

# Desktop HUB
cd Jarvis.Hub && npm install && npm start
```

The AI engine works offline with a local Ollama server
(`--AI:Ollama:Enabled=true --AI:Ollama:BaseUrl=http://localhost:11434`). For cloud models set
`JARVIS_OPENAI_API_KEY` and `--AI:OpenAI:Enabled=true --AI:OpenAI:BaseUrl=https://api.groq.com/openai/v1`
(Groq offers free tiers); any OpenAI-compatible endpoint works.

## What the platform already does

- Boots a full host: configuration, logging, dependency injection, event bus.
- Loads plugins from the `plugins` directory into isolated load contexts, with semantic
  version checks, dependency resolution and policy-driven permissions (all-or-nothing).
- Hot-loads plugins added or removed under `plugins/` while the host runs.
- Routes commands from the host to plugins (`IJarvisHost.ExecuteCommandAsync`) and raises
  `PluginCommandEvent` for every execution.
- Persists facts, knowledge and user preferences in SQLite with deterministic local
  embeddings and cosine-similarity semantic search (`IMemoryService`).
- Chats through `IAIService`: model routing by task kind (simple/complex/reasoning/coding/
  summarization), local-first preference, health-based fallback and session context.
- Streams chat completions to the HUB over Server-Sent Events.
- Publishes heartbeat events on the EventBus; plugins and the UI react to them.
- Shuts down gracefully (Ctrl+C stops the runtime, stops plugins, unloads assemblies).

## Feature plugins (each added without touching Core)

| Plugin | Commands | Permissions |
| --- | --- | --- |
| Minecraft | `minecraft.launch`, `minecraft.stop`, `minecraft.status` | processes, files |
| Desktop | `desktop.notify`, `desktop.screenshot` | ui, system |
| System | `system.process.list`/`info`/`start`/`kill`, `system.file.list`/`read`/`write`/`copy`/`move`/`search`, `system.app.launch`/`stop`/`running`, `system.hardware.metrics` | processes, files, system |
| Automation | `automation.list`, `automation.add`, `automation.run` | automation |
| AI | `ai.remember`, `ai.search`, `ai.set-preference`, `ai.get-preference` | ai, memory |

See `docs/ARCHITECTURE.md` for the full design.
