# PROJECT KNOWLEDGE BASE

**Generated:** 2025-05-09
**Commit:** cc52f88
**Branch:** main

## OVERVIEW
Downroot — Godot 4.6 + C# (.NET 8) 2D farming/survival game with dimensional shard mechanics. Content-pack driven architecture with procedural world generation.

## STRUCTURE
```
.
├── src/                          # Pure C# game libraries
│   ├── Downroot.Core/            # Save system, definitions, IDs, input, registries
│   ├── Downroot.Content/         # Content pack loading, registries
│   ├── Downroot.World/           # World generation passes & models
│   ├── Downroot.Gameplay/        # Game runtime, systems, persistence, bootstrap
│   └── Downroot.UI/              # ViewData DTOs, presentation builders
├── game/Downroot.Game/           # Godot project (scenes, runtime controllers/views)
├── packs/basegame/               # Default content pack (assets, defs, scenes)
└── docs/                         # Architecture docs, pack conventions
```

## WHERE TO LOOK
| Task | Location | Notes |
|------|----------|-------|
| Entry point | `game/Downroot.Game/Runtime/AppRoot.cs` | Main scene controller, page navigation |
| Game bootstrap | `src/Downroot.Gameplay/Bootstrap/GameBootstrapper.cs` | DI setup, world creation, save loading |
| World generation | `src/Downroot.World/Generation/Passes/` | Per-pass terrain/feature generation |
| Portal generation | `src/Downroot.World/Generation/Passes/PortalSitePass.cs` | Probabilistic portal spawn, min spacing |
| Pocket world management | `src/Downroot.Gameplay/Runtime/GameRuntime.cs` | PocketWorlds dict, lazy creation |
| Portal travel | `src/Downroot.Gameplay/Runtime/Systems/PortalTravelSystem.cs` | World switching, lazy pocket world init |
| Game simulation | `src/Downroot.Gameplay/Runtime/GameSimulation.cs` | Main tick loop |
| Day-night cycle | `src/Downroot.Gameplay/Runtime/GameSimulation.cs` → `UpdateWorldTime()` | `DayLengthSeconds` controls cycle duration |
| Lighting | `src/Downroot.Gameplay/Runtime/Systems/LightingFieldSystem.cs` | Skylight + emitter field |
| Content definitions | `src/Downroot.Core/Definitions/` | Abstract `ContentDef` record hierarchy |
| Save/load | `src/Downroot.Core/Save/` + `game/.../Infrastructure/` | JSON file store, repositories |
| UI presentation | `src/Downroot.UI/Presentation/` | ViewData records, `GamePresentationBuilder` |
| Godot rendering | `game/Downroot.Game/Runtime/WorldRenderer.cs` | Camera2D, terrain/entity sprites, chunk visuals |
| Content packs | `packs/basegame/` + `docs/PACKS.md` | Asset/ddef/scene conventions |
| Basegame assets | `packs/basegame/assets/README.md` | Asset layout, naming rules, atlas notes |

## CODE MAP

| Symbol | Type | Location | Role |
|--------|------|----------|------|
| AppRoot | class | game/Runtime/AppRoot.cs | Main scene, page host, session lifecycle |
| GameBootstrapper | class | src/Gameplay/Bootstrap/ | DI composition, world init, save restore |
| GameRuntime | class | src/Gameplay/Runtime/ | Central runtime state holder (PocketWorlds dict) |
| GameSimulation | class | src/Gameplay/Runtime/ | Tick loop, system orchestration |
| WorldGenerator | class | src/World/Generation/ | Chunk generation coordinator |
| PortalSitePass | class | src/World/Generation/Passes/ | Probabilistic portal spawn (SpawnChance, MinChunkSpacing) |
| PortalTravelSystem | class | src/Gameplay/Runtime/Systems/ | Portal travel, lazy pocket world creation |
| WorldRuntimeFacade | class | src/Gameplay/Runtime/ | Dynamic portal links, world query facade |
| WorldState | class | src/Gameplay/Runtime/ | Active world tracking (PocketWorlds dict, ActivePocketWorldId) |
| ContentRegistrySet | class | src/Content/Registries/ | Aggregated content registries |
| ContentDef | record | src/Core/Definitions/ | Base definition type |
| SaveGameData | class | src/Core/Save/ | Root save DTO |
| WorldStreamingSystem | class | src/Gameplay/Runtime/Systems/ | Chunk load/unload around player |
| GamePresentationBuilder | class | src/UI/Presentation/ | Binds runtime → ViewData |

## CONVENTIONS
- **C# 12 primary constructors** used extensively (parameters auto-become fields)
- **`sealed` by default** — classes are sealed unless inheritance is required
- **Records for defs/DTOs** — `ContentDef` and derivatives are `abstract record`
- **Nullable reference types enabled** — `?` annotations required
- **Manual DI** — no container; `AppRoot` and `GameBootstrapper` wire dependencies by hand
- **Godot/C# split** — pure logic in `src/`, Godot nodes and views in `game/`
- **Namespaces match folders** — `Downroot.Gameplay.Runtime.Systems` etc.

## ANTI-PATTERNS (THIS PROJECT)
- No test project or CI pipeline
- No generic `as any` / `@ts-ignore` equivalents (C# nullable strict)
- `RuntimeProfiler.Measure` calls should stay lightweight; avoid nesting deeply

## UNIQUE STYLES
- **Content pack system** — runtime scans `packs/*`, each pack registers via `IContentPack`
- **World space kinds** — Overworld + DimShardPocket with probabilistic portal generation
- **Entity projection** — `GameRuntime` maintains a flattened entity view for rendering
- **Chunk-based streaming** — radius-based load/unload with `WorldStreamingSystem`

## COMMANDS
```bash
# Build solution
dotnet build Downroot.sln

# Run Godot project
godot --path game/Downroot.Game
```

## TIME SYSTEM

The game uses **dual time tracking** with no time-scale multiplier:

| Counter | Purpose | Unit |
|---------|---------|------|
| `WorldState.TotalElapsedSeconds` | Survival mechanics (hunger drain, poison, hit flash, fuel) | Real seconds |
| `WorldState.TimeOfDaySeconds` | Day-night cycle, lighting, skylight | Real seconds (displayed as "game minutes") |

- **Day length** is configured per-content-pack via `BootstrapConfig.DayLengthSeconds`
- Base game sets this to `1440` → one full day-night cycle = **24 real minutes**
- Both counters increment by raw `deltaSeconds`; pause is handled via Godot's `ProcessMode.Pausable`
- If you need faster/slower days, change `DayLengthSeconds` and re-tune survival intervals (hunger drain uses `TotalElapsedSeconds % 3f`)

## NOTES
- Uses **Jolt Physics** for 3D (configured in `project.godot`)
- Rendering: D3D12 on Windows, pixel-snap enabled for 2D
- Window size: 1600×900
- Content root relative path: `../../` (project looks up two dirs from `game/Downroot.Game/`)
- `packs/basegame/assets/_inbox/` is temporary — confirmed assets should migrate to permanent dirs

## ASSET CHANGES
- **`packs/basegame/assets/world/nature/rocks/rock_outcrop.png`** — Extracted from `stone.png` (variant 2, medium-sized rock). See `packs/basegame/assets/README.md` for atlas layout details.

## COLLISION SYSTEM

### Collision Center Alignment (2025-05-12)
**Problem**: Resource nodes and placeables with large sprites (e.g., 32×32 `rock_outcrop`) had their collision centered on the sprite's top-left corner (`entity.Position`), causing a perceptible offset between the visual sprite and the collision area.

**Solution**: `LoadedWorldState` now computes a `GetCollisionCenter()` based on the entity's sprite dimensions:
- `ResourceNode`: `Position + (SpriteWidth/2, SpriteHeight/2)`
- `Placeable`: `Position + (SpriteWidth/2, SpriteHeight/2)`

**Files changed**:
- `src/Downroot.Gameplay/Runtime/LoadedWorldState.cs`
  - `GetCollisionCenter()` — new method
  - `GetResourceCollisionCenter()` — new method
  - `GetPlaceableCollisionCenter()` — new method
  - `UpdateBlockerIndex()` — uses collision center for tile indexing
  - `IsBlocked()` — uses collision center for distance checks

## PORTAL SYSTEM

### Probabilistic Portal Generation
Portals spawn on Overworld chunks via `PortalSitePass` with deterministic, seed-based probability:
- **SpawnChance**: 20% per chunk (`PortalModContentPack.cs`)
- **MinChunkSpacing**: 3 — Poisson-like grid sampling ensures no two portals within 3 chunks
- **Deterministic**: `GetStableUnitValue` guarantees same seed always produces same portal layout
- No portal positions are stored in saves — they are regenerated identically on reload

### Multi-Pocket-World Architecture
Each portal leads to its own independent `DimShardPocket` world:
- **World ID format**: `dimshard:{overworldSeed}:{chunkX},{chunkY}` — encodes everything needed to reconstruct
- **Pocket world size**: 3×3 chunks (from -1,-1 to 1,1), same as original
- **Return portal**: always in chunk (0,0) of the pocket world (`RequiredChunkCoord`)
- **Content**: dimfrag terrain + frostcore ore + rocks + return portal

### Lazy Creation & Persistence
- Pocket worlds are created **lazily** on first portal entry (not at boot)
- Both `GameRuntime` and `WorldState` hold `PocketWorlds` dictionaries (kept in sync)
- `ActivePocketWorldId` on `WorldState` tracks which pocket world is currently active
- Save/load serializes all pocket worlds — modifications (placed items, destroyed resources) persist

### Camera
- `WorldRenderer` creates a `Camera2D` as child of player body with configurable `Zoom` parameter
- Godot's built-in frustum culling skips off-screen sprites automatically
- Chunk loading radius (`OverworldLoadRadius = 1`) comfortably covers the viewport at default zoom
