# Basegame Mod Asset Library Layout

## Principles
- Use gameplay-oriented folders first, then asset subtype folders.
- Keep original large source sheets in `raw/` as immutable backups.
- Use `snake_case` for all exported asset names.
- Keep processed game-ready assets separate from raw source material.

## Top Level
- `raw/`: untouched source files such as spritesheets, references, and concept exports.
- `world/`: terrain and map-placed natural environment assets.
- `structures/`: walls, doors, roofs, windows, and building shell parts.
- `production/`: workbenches, storage, power, and functional colony devices.
- `furniture/`: comfort, decor, tables, and non-industrial placeables.
- `items/`: inventory objects and world pickups.
- `characters/`: humans, animals, enemies, corpses, and layered overlays.
- `effects/`: combat, weather, fire, smoke, and ambient VFX.
- `ui/`: HUD, icons, cursors, markers, and portraits.
- `tilesets/`: Godot-ready atlases, tile metadata, and generated tileset resources.
- `docs/`: naming rules and asset pipeline notes.

## Current Natural Assets
- `nature/trees_32x32/` and `nature/forest_16x16/` are legacy processed folders from the initial sheet split.
- Recommended long-term destination:
  - trees -> `world/nature/trees/`
  - plants and flowers -> `world/nature/plants/` or `world/nature/flowers/`
  - rocks and ores -> `world/nature/rocks/` and `world/nature/ores/`

## Rock Assets (`world/nature/rocks/`)
- `stone.png` — 96×32 atlas (12×4 grid of 8×8 cells), contains 3 stone variants for `ResourceNodeDef`
  - Variant 1 (small): columns 1–2 (~8×8)
  - Variant 2 (medium): columns 4–7 (~16×16) — extracted as `rock_outcrop.png`
  - Variant 3 (large): columns 8–11 (~24×24)
- `rock_outcrop.png` — 32×32, extracted from `stone.png` variant 2, used by `rock_outcrop` resource node

## Raw Backups
- Store original sheets in `raw/spritesheets/`.
- Do not edit files in `raw/`; export derived assets elsewhere.
