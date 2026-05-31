# Offline Store

MiniLauncher can read a local folder as an offline store. This is useful for shared internal tools, portable app folders, or a local drive synced by another tool.

## Store Layout

Use a root `config.json`:

```json
{
  "apps": [
    {
      "name": "Example Tool",
      "path": "Tools/ExampleTool.exe",
      "icon": "Icons/example.png",
      "category": "Tools",
      "keywords": "example utility"
    }
  ]
}
```

Paths can be absolute or relative to the folder containing `config.json`.

## Per-App Config

Each app folder can also contain its own `config.json`:

```json
{
  "name": "Internal Dashboard",
  "path": "Dashboard.lnk",
  "icon": "dashboard.png",
  "category": "Office",
  "keywords": "reports metrics"
}
```

## Quick Pinning

You can drag an app, shortcut, script, or folder onto the launcher or Offline Store page. MiniLauncher will pin it automatically.
