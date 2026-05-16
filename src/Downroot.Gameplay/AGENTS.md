# Downroot.Gameplay

Game runtime: state machines, systems, persistence, bootstrap.

## STRUCTURE
```
Downroot.Gameplay/
├── Bootstrap/            # GameBootstrapper, GameStartOptions
├── Runtime/              # GameRuntime, GameSimulation, player/world state
│   └── Systems/          # Movement, Crafting, Placement, PortalTravel, WorldStreaming, etc.
└── Persistence/          # Save/load adapters
```

## WHERE TO LOOK
| Task | Location |
|------|----------|
| Main game loop | `Runtime/GameSimulation.cs` |
| Runtime state holder | `Runtime/GameRuntime.cs` | PocketWorlds dict, lazy pocket world creation |
| Portal travel | `Runtime/Systems/PortalTravelSystem.cs` | World switching, pocket world creation on first entry |
| World streaming | `Runtime/Systems/WorldStreamingSystem.cs` |
| Bootstrap / DI | `Bootstrap/GameBootstrapper.cs` | No longer pre-creates DimShardPocket |
| Save loading | `Persistence/GameSaveLoader.cs` | Restores all pocket worlds from save |
| Snapshot builder | `Persistence/GameSaveSnapshotBuilder.cs` | Exports all pocket worlds |
| World state | `Runtime/WorldState.cs` | PocketWorlds dict + ActivePocketWorldId |
| World facade | `Runtime/WorldRuntimeFacade.cs` | Dynamic portal links, multi-world query |
| World time / day-night cycle | `Runtime/GameSimulation.cs` → `UpdateWorldTime()` |
| Lighting / skylight | `Runtime/Systems/LightingFieldSystem.cs` |
| Collision / blocker index | `Runtime/LoadedWorldState.cs` | `GetCollisionCenter()`, `UpdateBlockerIndex()`, `IsBlocked()` |

## CONVENTIONS
- **System pattern** — each system is a `sealed class` with primary constructor taking `GameRuntime`
- **Tick methods** — systems expose `Update()` or `Update(delta)` called from `GameSimulation`
- **Manual DI** — `GameBootstrapper` wires all systems by hand; no container
- **State mutation** — `GameRuntime` holds mutable state; systems mutate it

## ANTI-PATTERNS
- Do not let systems hold references to Godot nodes (use `WorldRuntimeFacade` abstractions)
- Keep `RuntimeProfiler.Measure` calls shallow

## PORTAL SYSTEM

### Multi-Pocket-World Architecture
Each portal on the overworld leads to its own independent `DimShardPocket` world:
- `GameRuntime.PocketWorlds` — `Dictionary<string, LoadedWorldState>` mapping worldId → world
- `GameRuntime.PocketGenerators` — `Dictionary<string, WorldGenerator>` for per-world generation
- `WorldState.PocketWorlds` — synced copy for entity projection and querying
- `WorldState.ActivePocketWorldId` — tracks which pocket world is currently active

### Lazy Creation
- Pocket worlds are NOT pre-created at bootstrap
- `PortalTravelSystem.CreatePocketWorld()` creates them on first portal entry
- World ID format: `dimshard:{overworldSeed}:{portalChunk.X},{portalChunk.Y}`
- Seed derived via `GameBootstrapper.CreatePocketWorldSeed(overworldSeed, portalChunk)`

### Portal Travel
- `PortalTravelSystem.StartPortalTravel()` resolves the target pocket world (creating if needed)
- Sets `WorldState.ActivePocketWorldId` when entering a pocket world, clears it when returning to overworld
- `WorldRuntimeFacade.GetPortalLink()` dynamically creates `PortalWorldLinkDef` (no longer registered in content packs)
- `IsPortalEntity()` matches entities by portal definition ID (no longer checks fixed portal links)

### Save/Load
- `GameSaveSnapshotBuilder.BuildWorlds()` iterates `runtime.PocketWorlds.Values` for all pocket worlds
- `GameSaveLoader.Load()` recreates pocket worlds from their stable world ID, restores saved chunk data
- Pocket world worldId encodes the portal chunk, allowing reconstruction from save data

## TIME SYSTEM

### Dual Time Tracking
`GameSimulation.Tick()` maintains two separate time counters:

| Counter | Purpose | Driven By | Affected By Pause |
|---------|---------|-----------|-------------------|
| `TotalElapsedSeconds` | Survival mechanics (hunger, poison, fuel) | Raw `deltaSeconds` | Yes (via Godot pause) |
| `TimeOfDaySeconds` | Day-night cycle, lighting | Raw `deltaSeconds` | Yes (via Godot pause) |

Both counters use **real elapsed time** (`deltaSeconds`). There is no time-scale multiplier. The perceived "game minutes" are a display convention — `TimeOfDaySeconds` increments by 1 per real second, but the UI may choose to display this as "1 game minute".

### Day Length Configuration
- Configured in `BootstrapConfig.DayLengthSeconds` (set by content packs)
- Base game: `1440` → one full day-night cycle takes **24 real minutes**
- Lighting transitions: 40% day → 10% dusk → 40% night → 10% dawn (see `LightingFieldSystem.ResolveOutdoorSkylightLevel()`)

### Why No TimeScale?
Early versions considered a `TimeScale` multiplier to decouple day-night speed from survival tick rate. This was rejected because:
- Hunger/poison drains and fuel consumption are balanced around real-time intervals
- A multiplier would require rebalancing all survival mechanics or create confusing divergence between "day length" and "survival time"
- If you need to change day-night speed, adjust `DayLengthSeconds` directly and re-tune survival intervals accordingly

## COLLISION SYSTEM

### Collision Center Alignment
`LoadedWorldState` uses sprite-centered collision for `ResourceNode` and `Placeable` entities:
- Collision center = `entity.Position + (SpriteWidth/2, SpriteHeight/2)`
- This aligns the circular collision radius with the visual center of the sprite
- Without this offset, large sprites (e.g., 32×32) would have their collision anchored at the top-left corner
- See `GetCollisionCenter()`, `GetResourceCollisionCenter()`, `GetPlaceableCollisionCenter()` in `LoadedWorldState.cs`
