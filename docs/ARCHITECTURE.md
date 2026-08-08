# JARVIS OS — Architecture

This document describes the architecture of JARVIS OS: a modular, extensible personal AI
assistant platform written in C# / .NET 8. It covers the base skeleton, the professional
plugin system (permissions, versioning, dynamic loading, events), the JARVIS memory system
(SQLite, local embeddings, vector search, preferences), the local AI engine (multi-provider
abstraction, model routing, context management) and the web/desktop shell (JARVIS API + HUB).

## Module layout

```
Jarvis/
├── Jarvis.SDK      # Public contracts: events, plugin interfaces, host API, AI contracts. No logic.
├── Jarvis.Core     # Engine: EventBus, ServiceManager, Configuration, Logging, DI, PluginManager.
├── Jarvis.Runtime  # Headless host: Startup, ProcessManager, BackgroundServices.
├── Jarvis.Memory   # SQLite-backed memory: embeddings, vector search, preferences.
├── Jarvis.AI       # AI engine: providers (Ollama/OpenAI-compatible), model router, context.
├── Jarvis.API      # REST + SSE host exposing memory, plugins and AI to any client.
├── Jarvis.Hub      # Electron shell (Windows 11 Fluent/Mica) wrapping the API renderer.
├── Jarvis.Plugins  # Extension system. Each capability ships as a plugin.
│   ├── Jarvis.Plugins.Example
│   ├── Jarvis.Plugins.Minecraft
│   ├── Jarvis.Plugins.Desktop
│   ├── Jarvis.Plugins.System
│   ├── Jarvis.Plugins.Automation
│   └── Jarvis.Plugins.AI
└── Jarvis.UI       # Desktop interface (Avalonia).
```

## Dependencies

Only upward references are allowed. This keeps every module replaceable and testable.

```
Jarvis.Hub (Electron) ──┐
Jarvis.API ─────────────┤
Jarvis.AI ──────────────┤
Jarvis.UI ──────────────┤
Jarvis.Runtime ─────────┤
Jarvis.Memory ──────────┼──> Jarvis.SDK        (contracts only, no logic)
Jarvis.Plugins.* ───────┤        ▲
Jarvis.Core ────────────┘      (no other deps)

Jarvis.Core <── Jarvis.Runtime, Jarvis.UI, Jarvis.API
Jarvis.Memory <── Jarvis.Runtime, Jarvis.UI, Jarvis.API
Jarvis.AI <── Jarvis.API
```

- `Jarvis.SDK` depends on nothing but `Microsoft.Extensions.Logging.Abstractions`.
- `Jarvis.Core` depends on `Jarvis.SDK` + Microsoft.Extensions (Hosting, Logging, DI,
  Configuration).
- `Jarvis.Memory` depends on `Jarvis.SDK` + Microsoft.Data.Sqlite + Microsoft.Extensions
  (Options, DI abstractions, Logging).
- `Jarvis.AI` depends on `Jarvis.SDK` + Microsoft.Extensions (Options, DI, Logging) +
  `System.Net.Http` for provider calls. No model code ships in the engine; models live in
  providers (Ollama locally, OpenAI-compatible cloud endpoints).
- `Jarvis.API` depends on `Jarvis.AI`, `Jarvis.Core`, `Jarvis.Memory` + `Jarvis.SDK` and hosts
  the ASP.NET Core web server.
- `Jarvis.Hub` is a Node/Electron project; it talks to `Jarvis.API` over HTTP/SSE and shares
  the same renderer that the API serves as static files.
- `Jarvis.Runtime` and `Jarvis.UI` depend on `Jarvis.Core`, `Jarvis.Memory` + `Jarvis.SDK`.
- Plugins depend only on `Jarvis.SDK` and are therefore fully decoupled from Core, Memory,
  AI and each other. New capabilities are added as plugins without touching Core.

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
| `AI` | `IAIService`, `ChatMessage`/`ChatRequest`/`ChatResponse`, `ChatChunk`, `AIProviderInfo`, `TaskKind` |

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

### Jarvis.AI

The local-first AI engine. It is registered via `AddJarvisAI()` and exposed to the rest of the
system through `IAIService` in the SDK. It contains no model code of its own: everything is
delegated to pluggable providers.

| Folder | Responsibility |
| --- | --- |
| `AIProvider` | `IAIProvider` contract + `OllamaProvider` (local, offline) + `OpenAIProvider` (any OpenAI-compatible endpoint: Groq, Together, OpenAI...) |
| `ModelRouter` | `ModelRouter`: health cache, failure cooldowns, task-kind tag matching, local-first ordering |
| `ContextManager` | `ConversationContextManager`: in-memory sessions, token-budget trimming, LRU eviction |
| `Configuration` | `AIOptions`: providers, model definitions, routing + context settings |
| `DependencyInjection` | `AddJarvisAI()` registration and wiring |
| `JarvisAIService` | `IAIService` implementation; fallback across candidates, session context recording |

Routing: a `ChatRequest` optionally names a model id (from the `AI:Models` list) or a task
kind (simple/complex/reasoning/coding/summarization). The router prefilters providers by cached
health, orders local-first when the request's `PreferLocal` is set (so SDK callers can force
cloud routing per request), and matches model definitions by task kind tags. If a provider
fails, the service falls back to the next healthy candidate, so the assistant keeps working
offline even when the cloud is unreachable. Messages support multimodal input: `ChatMessage.UserWithImage`
carries a `ChatImage` (mime type + base64), which `OpenAIProvider` serializes as an OpenAI
`image_url` content part and `OllamaProvider` as an `images` array, enabling vision
(`vision.analyze`) through the same routing pipeline.

### Jarvis.API

The web/API host. It boots the same `JarvisHostFactory.Configure` bootstrap as the runtime and
exposes everything over REST + Server-Sent Events:

- `GET /api/health`, `GET /api/status` — liveness and system overview (plugins, memory, AI).
- `GET /api/plugins`, `POST /api/plugins/{id}/commands/{command}` — plugin inspection and
  command execution (parameters are normalized from JSON to primitives before dispatch).
  Command responses carry both a readable `result` text (enumerables are joined line by line)
  and, for structured plugin results, a `data` field that richer clients can render.
- `GET/POST /api/memory`, `/api/memory/recent`, `/api/memory/search` — memory CRUD + vector search.
- `GET/POST/DELETE /api/memory/preferences[/{key}]` — preference store, including list-all.
- `GET /api/ai/providers`, `GET /api/ai/models` — provider health and model definitions.
- `POST /api/ai/chat` — non-streaming chat (JSON).
- `POST /api/ai/chat/stream` — streaming chat (SSE, `data:` frames + `[DONE]`).
- `POST /api/ai/sessions/{id}/clear` — clear a conversation session.

It also serves the Hub renderer (`Jarvis.Hub/renderer/**`) as static files, so the same UI
works in a browser and inside Electron. CORS is open so any client can connect.

### Jarvis.Hub

The premium desktop shell: an Electron app with a Windows 11 Fluent / Mica-inspired renderer
(frameless window, custom titlebar, acrylic backdrop, light/dark themes). Views: Dashboard,
Assistant (streaming chat with a caret indicator), Memory (entries + preferences), Plugins
(command chips), System (PC control: hardware meters, searchable processes with kill actions,
a file browser with inline previews, and an application launcher), Studio (scaffold, generate,
build, test and run developer projects against `jarvis.developer`), Senses (speak with browser
speech fallback, transcribe, vision analysis with image preview, and screen capture against
`jarvis.senses`) and Settings. It talks to
`Jarvis.API` via `window.jarvis` (contextBridge) and can spawn the API host itself if it is not
already running.

### Jarvis.Plugins

Extension catalog. Every plugin is a class library referencing `Jarvis.SDK` and implementing
`IJarvisPlugin` (usually by deriving from `JarvisPluginBase`). Plugins are deployed in their
own sub-directory and loaded at startup or dynamically while the host runs.

Built-in plugins (each is a feature added without touching Core):

- `Jarvis.Plugins.Minecraft` — launch/stop/status of a Minecraft server (`processes`).
- `Jarvis.Plugins.Desktop` — notifications, screenshots, desktop actions (`ui`, `system`).
- `Jarvis.Plugins.System` — process/file/hardware/application control: list and stop processes,
  list/read/write/copy/move/search files, launch/stop apps and report CPU/RAM/disk/uptime
  metrics (`processes`, `files`, `system`).
- `Jarvis.Plugins.Developer` — JARVIS STUDIO developer agent: scaffold dotnet/node/python
  projects, read/write project files, generate code through `IAIService` (cloud-routed,
  `TaskKind.Coding`, code fences stripped) and build/test/run projects with a timeout and
  whole-tree kill (`processes`, `files`, `ai`). Projects live under `Jarvis:Studio:Root`
  (default: `<AppContext.BaseDirectory>/projects`).
- `Jarvis.Plugins.Senses` — voice & vision: `voice.speak` (local TTS via espeak-ng, with a
  structured fallback so the HUB can use browser speech), `voice.transcribe` (whisper CLI when
  present), `vision.analyze` (image path, data URI or URL encoded as a `ChatImage` and sent to
  a multimodal model) and `vision.screen` (screen capture that degrades on headless hosts)
  (`ai`, `files`, `network`).
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
# Build everything. The runtime and API deploy all plugins into plugins/.
dotnet build Jarvis.sln

# Run the headless runtime (heartbeats every 5 s, plugins loaded from plugins/)
dotnet run --project Jarvis.Runtime

# Override the heartbeat interval from the command line
dotnet run --project Jarvis.Runtime -- --Jarvis:HeartbeatIntervalSeconds=1

# Restrict plugin permissions to an explicit allow-list
dotnet run --project Jarvis.Runtime -- --Permissions:AllowAll=false --Permissions:Allowed:0=ai

# Run the desktop UI (needs a graphical session)
dotnet run --project Jarvis.UI

# Run the web/API host (http://localhost:5178, serves the Hub renderer too)
dotnet run --project Jarvis.API

# Point the AI engine at a local Ollama server (models auto-registered)
dotnet run --project Jarvis.API -- --AI:Ollama:Enabled=true --AI:Ollama:BaseUrl=http://localhost:11434

# Enable an OpenAI-compatible cloud provider (Groq/Together/OpenAI...)
JARVIS_OPENAI_API_KEY=... dotnet run --project Jarvis.API -- --AI:OpenAI:Enabled=true \
    --AI:OpenAI:BaseUrl=https://api.groq.com/openai/v1

# Desktop Hub (needs npm install once)
cd Jarvis.Hub && npm install && npm start
```

## Roadmap (future, per PROJECT_SPEC.md)

Each item slots into an existing module without touching Core:

- **AI orchestration**: richer embeddings backed by a real model in place of the hash provider;
  tool/function calling and more task-kind tags.
- **Plugins**: System, Browser, Developer, Gaming, Media, Smart Home.
- **UI**: conversation hub, monitor, settings screens wired to `IMemoryService` and commands.
- **Tests**: `Jarvis.Tests` covering EventBus, PluginManager, permissions, memory and AI routing.
