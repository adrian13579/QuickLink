# QuickLink

A lightweight Windows 11 URL launcher that lives in your system tray. Press a global hotkey, search for a resource by name or tag, and copy its URL to the clipboard in one keystroke — no browser required.

![QuickLink search window](docs/screenshot.png)

## Features

- **Instant search** — match resources by name or tag as you type, with matching text highlighted in results
- **One-key copy** — press `Enter` to copy the current URL to the clipboard
- **Open in browser** — press `Ctrl+Enter` to launch the URL directly
- **Pin resources** — pin frequently used resources with `Ctrl+P`; pinned items appear when the search box is empty
- **Quick edit** — press `Ctrl+E` to jump straight to the edit page for the selected result
- **Tags & deprecation** — organise resources with tags and mark stale entries as deprecated
- **Global hotkey** — summon the window from anywhere with a configurable shortcut (default `Ctrl+Shift+Space`)
- **System tray** — hides instead of closing; accessible from the tray at all times
- **Always on top** — optionally keep the window above all other windows (configurable in Settings)
- **Acrylic backdrop** — native Windows 11 Mica/Acrylic look, no title bar chrome

## Keyboard shortcuts

| Key | Action |
|-----|--------|
| `↑` / `↓` | Navigate results |
| `Enter` | Copy URL to clipboard |
| `Ctrl+Enter` | Open URL in browser |
| `Ctrl+P` | Pin / unpin selected result |
| `Ctrl+E` | Edit selected result |
| `Esc` | Hide window |
| `Ctrl+Shift+Space` | Show window (global, from tray) |

## Prerequisites

- Windows 10 (1809+) or Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

All other dependencies (Windows App SDK, CommunityToolkit.Mvvm, etc.) are NuGet packages restored automatically on first build.

## Running from source

```powershell
dotnet run --project src\QuickLink.csproj
```

This registers a debug package identity via the Windows App SDK tooling and launches the app. No separate install step is needed.

## Building

```powershell
dotnet build src\QuickLink.csproj
```

Optional flags:

```powershell
dotnet build src\QuickLink.csproj -p:Platform=x64      # x64 (default), x86, ARM64
dotnet build src\QuickLink.csproj -c Debug             # Release (default) or Debug
```

## Data

Resources are stored in `sample-data\resources.json` next to the executable. Each entry has a name, description, tags, one or more URLs, and an optional "current" URL pointer.
