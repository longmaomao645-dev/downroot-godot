# Downroot.Core

Core primitives: identifiers, definitions, save DTOs, input map, world constants.

## STRUCTURE
```
Downroot.Core/
├── Definitions/          # ContentDef record hierarchy (ItemDef, TerrainDef, etc.)
├── Save/                 # JSON-serializable save DTOs
├── World/                # World space kinds, chunk coords, surface regions
├── Ids/                  # ContentId, EntityId value objects
├── Input/                # GameInputMapInstaller
├── Registries/           # Base registry interfaces
├── Content/              # Content pack metadata types
└── Diagnostics/          # RuntimeProfiler
```

## WHERE TO LOOK
| Task | Location |
|------|----------|
| Content definition base | `Definitions/ContentDef.cs` |
| Save root DTO | `Save/SaveGameData.cs` |
| World space enum | `World/WorldSpaceKind.cs` |
| World gen pass definition | `World/WorldGenPassDef.cs` | SpawnChance, RequiredChunkCoord, MinChunkSpacing for portal generation |
| Portal world link def | `World/PortalWorldLinkDef.cs` |
| ID value objects | `Ids/ContentId.cs`, `Ids/EntityId.cs` |
| Profiling | `Diagnostics/RuntimeProfiler.cs` |
| Bootstrap config | `Gameplay/GameBootstrapConfig.cs` | Day length, player stats, spawn points |

## CONVENTIONS
- **Records for defs** — `ContentDef` is `abstract record`; all defs derive from it
- **Mutable save DTOs** — save classes use init-capable properties for JSON deserialization
- **Value objects** — `ContentId` wraps string, `EntityId` wraps string

## ANTI-PATTERNS
- Do not add Godot dependencies in this project (keep it pure C#)
