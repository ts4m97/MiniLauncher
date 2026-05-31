# MiniLauncher

MiniLauncher is a compact Windows app launcher built with WPF and .NET 9. It is designed for offline/local use: pinned apps, settings, store metadata, and usage history are saved on the machine without any cloud dependency.

## Features

- Global hotkey: `Alt + Space`
- Search pinned apps, folders, shortcuts, and custom keywords
- Pin apps/folders by button or drag-and-drop
- Drag apps, shortcuts, scripts, or folders onto Offline Store to pin them
- Grid and list views
- Favorite pins and recent/usage-based sorting
- Rename pins, edit keywords, change icons, move pins up/down
- Offline Store from local `config.json` files
- System tray menu
- Start with Windows
- Always on top, hide on lost focus, open near cursor
- Export/import config
- Portable single-file publish

## Requirements

- Windows 10 or Windows 11
- .NET SDK 9.0 to build from source

The published self-contained `win-x64` build does not require a separate .NET runtime install.

## Build

```powershell
cd MiniLauncher
dotnet build
```

## Publish Portable Build

```powershell
cd MiniLauncher
dotnet publish -c Release -r win-x64 --self-contained true
```

The published app is created at:

```text
MiniLauncher/bin/Release/net9.0-windows/win-x64/publish/MiniLauncher.exe
```

## Local Config

MiniLauncher saves its user config here:

```text
%AppData%\MiniLauncher\config.json
```

## Offline Store Format

Set a local Store Path in Settings. The store folder can contain a root `config.json`:

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

Each app folder can also contain its own `config.json` using the same fields.

## License

MIT
