# Downroot.World

Procedural world generation: chunk models and generation passes.

## STRUCTURE
```
Downroot.World/
├── Generation/           # WorldGenerator, pass factory, autotile resolvers
│   └── Passes/           # Individual IWorldGenPass implementations
└── Models/               # ChunkData, GeneratedChunk, SurfaceCell, WorldModel
```

## WHERE TO LOOK
| Task | Location |
|------|----------|
| Chunk generator | `Generation/WorldGenerator.cs` |
| Pass implementations | `Generation/Passes/` (Grass, River, Rock, Ore, Portal, etc.) |
| Portal site generation | `Generation/Passes/PortalSitePass.cs` | Probabilistic, deterministic, min-spacing |
| Chunk models | `Models/GeneratedChunk.cs`, `Models/ChunkData.cs` |
| World model | `Models/WorldModel.cs` |
| Pass factory | `Generation/WorldGenPassFactory.cs` |

## CONVENTIONS
- **Pass-based generation** — each terrain/feature is a separate `IWorldGenPass`
- **Immutable models** — `GeneratedChunk`, `WorldModel` use primary constructors (C# 12)
- **ContentId injection** — passes receive `ContentId` for the terrain/feature they place
- **Autotile resolution** — `RaisedFeatureAutotileResolver` computes tile variants post-generation
- **Probabilistic passes** — `PortalSitePass` uses `SpawnChance` + `MinChunkSpacing` for Poisson-like portal distribution; `RequiredChunkCoord` constrains specific portals to exact chunks

## PORTAL GENERATION

`PortalSitePass` uses deterministic probability-based sampling:
- `SpawnChance` (e.g., 0.20) — per-chunk probability threshold
- `MinChunkSpacing` (e.g., 3) — guarantees no two portals within N chunks
- `RequiredChunkCoord` — forces portal spawn in a specific chunk (used for pocket world return portals)
- Algorithm: each chunk computes `GetStableUnitValue`; only the chunk with the lowest value in its neighborhood spawns a portal

## ANTI-PATTERNS
- Passes should not depend on Godot types (use pure math/struct models)
