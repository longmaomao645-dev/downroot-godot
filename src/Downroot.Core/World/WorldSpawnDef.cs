using Downroot.Core.Ids;

namespace Downroot.Core.World;

public sealed record WorldSpawnDef(ContentId ContentId, WorldTileCoord Tile, int PixelOffsetX = 0, int PixelOffsetY = 0);
