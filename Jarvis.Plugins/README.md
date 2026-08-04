# Jarvis.Plugins

This directory contains every plugin that extends JARVIS OS.

## Structure

```
Jarvis.Plugins/
└── Jarvis.Plugins.Example/     # reference plugin demonstrating the SDK contracts
```

## How a plugin works

A plugin is a class library that references `Jarvis.SDK` and implements `IJarvisPlugin`
(usually by deriving from `JarvisPluginBase`). The core's `PluginManager` discovers plugins
at startup by scanning the `plugins` directory configured in `appsettings.json`.

Each plugin must live in its own sub-directory named after its assembly:

```
plugins/
└── Jarvis.Plugins.Example/
    ├── Jarvis.Plugins.Example.dll
    └── ... dependencies ...
```

## Adding a new plugin

1. Create a new class library under `Jarvis.Plugins/`.
2. Add a project reference to `Jarvis.SDK`.
3. Implement `IJarvisPlugin` (or derive from `JarvisPluginBase`).
4. Deploy its build output to the runtime's `plugins` directory.

Future plugins planned per `PROJECT_SPEC.md`: System, Browser, Developer, Gaming, Media,
Smart Home, AI and user plugins.
