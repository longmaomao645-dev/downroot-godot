using Downroot.Core.World;

namespace Downroot.Game.Runtime;

public enum DualGridRenderRole : byte
{
    BaseVisual = 0,
    OverlayVisual = 1
}

public sealed record DualGridLayerDef(
    TerrainVisualKind VisualKind,
    DualGridRenderRole RenderRole,
    int RenderOrder,
    string TextureId,
    string TexturePath);
