namespace Downroot.World.Generation;

public static class CoverDualGridResolver
{
    public const int Empty = 0;
    public const int OuterTopLeft = 1;
    public const int OuterTopRight = 2;
    public const int OuterBottomLeft = 3;
    public const int OuterBottomRight = 4;
    public const int EdgeTop = 5;
    public const int EdgeRight = 6;
    public const int EdgeBottom = 7;
    public const int EdgeLeft = 8;
    public const int InnerTopLeft = 9;
    public const int InnerTopRight = 10;
    public const int InnerBottomLeft = 11;
    public const int InnerBottomRight = 12;
    public const int DiagonalA = 13;
    public const int DiagonalB = 14;
    public const int Full = 15;

    public static int ResolveVariantIndex(bool topLeft, bool topRight, bool bottomLeft, bool bottomRight)
    {
        var mask = (topLeft ? 8 : 0)
            | (topRight ? 4 : 0)
            | (bottomLeft ? 2 : 0)
            | (bottomRight ? 1 : 0);

        return mask switch
        {
            0b0000 => Empty,
            0b1000 => OuterTopLeft,
            0b0100 => OuterTopRight,
            0b0010 => OuterBottomLeft,
            0b0001 => OuterBottomRight,
            0b1100 => EdgeTop,
            0b0101 => EdgeRight,
            0b0011 => EdgeBottom,
            0b1010 => EdgeLeft,
            0b1110 => InnerTopLeft,
            0b1101 => InnerTopRight,
            0b1011 => InnerBottomLeft,
            0b0111 => InnerBottomRight,
            0b1001 => DiagonalA,
            0b0110 => DiagonalB,
            0b1111 => Full,
            _ => throw new InvalidOperationException($"Unsupported dual-grid mask '{mask}'.")
        };
    }
}
