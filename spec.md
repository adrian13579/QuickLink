# ResourceFinder — Product Specification

## Overview

ResourceFinder is a Windows 11 native launcher/search overlay for internal company resources (URLs, dashboards, services, portals). Inspired by macOS Spotlight, it surfaces the right link instantly via fuzzy search and manages URL history so teams always know which endpoint is current.

---

## Problem Statement

Internal URLs change frequently. Teams lose time hunting for the current address of identity servers, dashboards, CI systems, and internal tools. There is no single source of truth, and no way to know whether a link someone shared is still valid or has been superseded.

---

## Goals

- Sub-100ms UI response for all local operations
- Windows 11 Fluent Design aesthetic (Mica background, rounded corners, depth)
- Global hotkey invocation — appears and disappears like Spotlight
- Fuzzy search across resource names, tags, and descriptions
- Full URL history per resource with a single "current" designation
- Deprecated flag with visual treatment
- JSON-first storage with a clean repository abstraction ready for a future central database

---

## Non-Goals (v1)

- Multi-user sync / central database (planned for v2)
- Authentication / authorization
- Browser extension or deep OS integration
- macOS or Linux support

---

## Technology Stack

| Layer | Choice | Reason |
|---|---|---|
| UI Framework | **WinUI 3** (Windows App SDK 1.5+) | True Windows 11 native; Mica, Fluent controls, layered windows |
| Language | **C# 12 / .NET 9** | Performance, ecosystem, first-class WinUI support |
| MVVM | **CommunityToolkit.Mvvm** | Source-generated commands/observables, zero boilerplate |
| Fuzzy Search | **FuzzySharp** (or hand-rolled Bitap) | Fast in-process ranking with no network calls |
| Storage (v1) | **System.Text.Json** → local JSON file | Zero dependencies, trivially replaceable |
| Storage (v2) | Repository interface swap to SQL/REST | Repository pattern isolates the concern |
| DI | **Microsoft.Extensions.DependencyInjection** | Aligns with .NET conventions |
| Global Hotkey | **WinAPI PInvoke** (`RegisterHotKey`) | Only reliable cross-app hotkey method on Windows |

### Why WinUI 3 over WPF

WPF is mature but does not get new Fluent controls, Mica material, or rounded-corner chrome automatically. WinUI 3 ships with the Windows App SDK and renders exactly like Microsoft's own Settings and Explorer panels on Windows 11. The tradeoff is a slightly larger deployment (MSIX or self-contained exe), which is acceptable for an internal tool.

---

## Application Architecture

```
ResourceFinder/
├── App.xaml / App.xaml.cs          # Startup, DI container, tray icon
├── Views/
│   ├── SearchWindow.xaml           # Main overlay (frameless, centered)
│   └── ManageWindow.xaml           # Full management CRUD UI
├── ViewModels/
│   ├── SearchViewModel.cs
│   └── ManageViewModel.cs
├── Models/
│   ├── Resource.cs                 # A named entity (e.g. "Identity Server")
│   ├── ResourceUrl.cs              # One URL entry, with version metadata
│   └── SearchResult.cs             # Ranked result returned by search
├── Services/
│   ├── IResourceRepository.cs      # Abstraction over storage
│   ├── JsonResourceRepository.cs   # v1 implementation
│   ├── SearchService.cs            # Fuzzy ranking logic
│   └── HotkeyService.cs            # RegisterHotKey wrapper
└── resources.json                  # Default storage location
```

---

## Data Model

### Resource

Represents a named internal service or tool.

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Identity Server",
  "description": "Central OAuth2/OIDC authority",
  "tags": ["auth", "oauth", "sso"],
  "isDeprecated": false,
  "currentUrlId": "url-id-2",
  "urls": [ ... ]
}
```

### ResourceUrl

One URL entry within a resource. A resource has many URLs over time.

```json
{
  "id": "url-id-2",
  "url": "https://auth.internal.example.com",
  "label": "Migrated to new cluster",
  "addedAt": "2025-11-01T09:00:00Z",
  "addedBy": "adrianportales",
  "isCurrent": true,
  "isDeprecated": false
}
```

Key invariant: exactly one `ResourceUrl.isCurrent == true` per resource at any time (enforced in the service layer, not just the UI).

---

## Search Behavior

1. User types into the search box.
2. `SearchService` runs a fuzzy match against `Resource.name`, `Resource.tags`, `Resource.description`, and the current URL string.
3. Results are ranked by score (Bitap / Levenshtein ratio) then sorted: non-deprecated first, current URL present first.
4. Deprecated resources appear at the bottom with a strikethrough / muted style — still discoverable but visually deprioritized.
5. Each result row shows: **name**, current URL (truncated), tags, deprecated badge if applicable.
6. Pressing Enter or clicking a result copies the current URL to clipboard and closes the overlay (configurable: could open in browser instead).

---

## UI — Search Overlay (SearchWindow)

```
┌─────────────────────────────────────────────────┐  ← Mica background, 600×500, centered
│  🔍  Search resources...                        │  ← AutoSuggestBox, full-width
├─────────────────────────────────────────────────┤
│  Identity Server                                │
│  https://auth.internal.example.com   auth sso  │
│                                                 │
│  CI Dashboard                                   │
│  https://ci.internal.example.com     devops     │
│                                                 │
│  ⚠ Old Portal                    [deprecated]  │
│  https://portal-old.example.com                 │
├─────────────────────────────────────────────────┤
│  [Manage Resources]               Ctrl+M        │
└─────────────────────────────────────────────────┘
```

- No titlebar chrome — custom frameless window with `ExtendsContentIntoTitleBar = true`
- Acrylic / Mica L2 background so it blends with whatever is behind it
- Dismissed by pressing Escape or clicking outside (loses focus)
- `ListView` with virtualization for large resource lists
- Keyboard-navigable: arrow keys move selection, Enter activates

---

## UI — Manage Window (ManageWindow)

Full-screen (or large) management interface:

```
┌─ Manage Resources ──────────────────────────────────────────────────────┐
│  [+ New Resource]   [Search: ____________]                              │
├──────────────────────────────────────────────────────────────────────────┤
│  NAME              CURRENT URL                    TAGS      STATUS      │
│  ─────────────────────────────────────────────────────────────────────  │
│  Identity Server   https://auth.internal/...      auth sso  ● Active    │
│  CI Dashboard      https://ci.internal/...        devops    ● Active    │
│  Old Portal        https://portal-old/...         legacy    ⚠ Deprecated│
├──────────────────────────────────────────────────────────────────────────┤
│  [Selected: Identity Server]                                            │
│    Name:        Identity Server                                         │
│    Description: Central OAuth2/OIDC authority                           │
│    Tags:        auth, oauth, sso                                        │
│    Status:      ○ Active  ● Deprecated                                  │
│                                                                         │
│    URL History                           [+ Add URL]                   │
│    ───────────────────────────────────────────────────────              │
│    ● https://auth.internal.example.com   Current  2025-11-01           │
│      https://auth-v1.internal.com        Superseded  2024-03-15         │
│      https://sso.corp.example.com        Superseded  2023-01-10         │
│                                                                         │
│    [Save]  [Delete Resource]                                            │
└──────────────────────────────────────────────────────────────────────────┘
```

- Master-detail layout (NavigationView + content pane)
- URL history shown as a timeline list; clicking any row allows copying that historical URL
- "Set as Current" button on any historical URL promotes it and demotes the old current
- "Mark URL Deprecated" hides it from search but preserves it in history

---

## Global Hotkey

Default: `Ctrl + Space` (configurable via settings)

Implementation:
- `RegisterHotKey(HWND_MESSAGE, id, MOD_CONTROL, VK_SPACE)` on app start
- A hidden message-only window receives `WM_HOTKEY`
- On receipt: if overlay is hidden → show and activate; if visible → hide

Configurable alternative: `Ctrl+Shift+F` for environments where `Ctrl+Space` conflicts with IDE shortcuts.

---

## Storage — v1 (JSON)

```
%LOCALAPPDATA%\ResourceFinder\resources.json
```

- Read entirely into memory on startup (acceptable for <10,000 resources)
- Written atomically: write to `.tmp` file then `File.Replace` to avoid corruption
- `IResourceRepository` interface means replacing this with HTTP/SQL in v2 is a one-line DI change

### IResourceRepository interface

```csharp
public interface IResourceRepository
{
    Task<IReadOnlyList<Resource>> GetAllAsync();
    Task<Resource?> GetByIdAsync(Guid id);
    Task SaveAsync(Resource resource);       // add or update
    Task DeleteAsync(Guid id);
}
```

---

## Storage — v2 Roadmap (Centralized)

When the team grows, point `IResourceRepository` at one of:

| Option | Tradeoff |
|---|---|
| REST API + PostgreSQL | Full flexibility, requires backend service |
| Azure Table Storage | No-infra, cheap, limited query |
| SharePoint List via Graph API | No new infra if org is on M365 |
| SQLite with OneDrive sync | Zero-server, acceptable for small teams |

The JSON model maps directly to a relational schema (`resources` + `resource_urls` tables) with no structural changes to the C# models.

---

## Settings

Stored at `%LOCALAPPDATA%\ResourceFinder\settings.json`:

```json
{
  "hotkey": "Ctrl+Space",
  "defaultAction": "CopyToClipboard",  // or "OpenInBrowser"
  "showInTaskbar": false,
  "dataFilePath": "%LOCALAPPDATA%\\ResourceFinder\\resources.json"
}
```

---

## Deployment

- **Self-contained single-file exe** (no MSIX required for internal distribution)
- Startup: added to `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` optionally
- System tray icon (Windows.UI.SystemTray or NotifyIcon via WPF interop)
- No installer required for v1 — xcopy deployable

---

## Project Structure (Solution)

```
ResourceFinder.sln
├── ResourceFinder/              # WinUI 3 app (Windows App SDK)
│   ├── ResourceFinder.csproj
│   └── ...
├── ResourceFinder.Core/         # Models, services, repository interfaces
│   ├── ResourceFinder.Core.csproj
│   └── ...
└── ResourceFinder.Tests/        # xUnit tests for search logic and repository
    └── ResourceFinder.Tests.csproj
```

Separating Core keeps the business logic testable without a UI host.

---

## Key Risks and Mitigations

| Risk | Mitigation |
|---|---|
| WinUI 3 packaged app restrictions (file access, hotkeys) | Use unpackaged deployment model for v1; avoids sandboxing |
| JSON file corruption on crash | Atomic write via temp file + `File.Replace` |
| Hotkey conflict with other apps | Make hotkey configurable; detect registration failure and notify user |
| Large resource list slowing search | Cap fuzzy search to top 50 results; `ListView` virtualization handles rendering |
| Migration to central DB | Repository pattern; add `HttpResourceRepository` without touching UI |

---

## Suggested Build Order

1. `ResourceFinder.Core` — models + `JsonResourceRepository` + `SearchService` (testable, no UI)
2. Tests for search ranking and JSON round-trip
3. `SearchWindow` — frameless overlay, fuzzy search, keyboard navigation, clipboard copy
4. Global hotkey + tray icon
5. `ManageWindow` — CRUD, URL history, deprecation
6. Settings UI (hotkey picker, default action)
7. Polish: animations, Mica, accessibility (narrator labels)
8. v2 planning: repository swap, auth, sync

---

## Open Questions

- Should the overlay remember its last search between invocations, or always open blank?
- Should "open in browser" vs "copy to clipboard" be per-resource or global?
- Who can add/edit resources in v1? Anyone on the machine, or RBAC from v2 onward?
- Should the app export/import resources (JSON share between team members before the DB lands)?
