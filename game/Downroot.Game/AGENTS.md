# Downroot.Game (Godot Project)

Godot integration layer: scenes, runtime controllers, rendering, and infrastructure.

## STRUCTURE
```
Downroot.Game/
├── Runtime/              # AppRoot, GameRoot, HUD, world renderer, controllers
│   ├── HudController.cs
│   ├── HudView.cs
│   ├── WorldRenderer.cs
│   └── SessionController.cs
├── Infrastructure/       # Repositories, file store, settings applier
│   ├── SaveGameRepository.cs
│   ├── JsonFileStore.cs
│   └── SettingsApplier.cs
└── scenes/               # Godot .tscn scenes
```

## WHERE TO LOOK
| Task | Location |
|------|----------|
| Main scene / app root | `Runtime/AppRoot.cs` |
| In-game session | `Runtime/SessionController.cs` |
| HUD rendering | `Runtime/HudController.cs`, `Runtime/HudView.cs` |
| World rendering | `Runtime/WorldRenderer.cs` | Camera2D Zoom, terrain/entity sprites, chunk visuals |
| Save repository | `Infrastructure/SaveGameRepository.cs` |
| JSON file store | `Infrastructure/JsonFileStore.cs` |

## CONVENTIONS
- **Godot nodes** — controllers extend `Control` or `Node2D`; views are `Control` subtrees
- **Manual DI** — `AppRoot` constructs repositories and controllers directly
- **Page navigation** — `AppRoot` swaps `Control` pages in/out of a `_pageHost`
- **Pause handling** — `AppRoot` intercepts Escape to show pause menu when session active

## ANTI-PATTERNS
- Do not put game logic here (delegate to `src/Downroot.Gameplay/`)
- Views should bind to `ViewData` records, not directly to `GameRuntime`
