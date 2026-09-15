namespace Sudoku.Core.Models;

public sealed class BoardDefinition
{
    public required PuzzleKind Kind { get; init; }
    public required string Title { get; init; }
    public required int Size { get; init; }
    public int BoxWidth { get; init; }
    public int BoxHeight { get; init; }
    public required IReadOnlyList<int[]> Groups { get; init; }
    public required int[] RegionOfCell { get; init; }
    public bool[] DiagonalCells { get; init; } = Array.Empty<bool>();
    public bool[] StarCells { get; init; } = Array.Empty<bool>();
    public int CellCount => Size * Size;

    public int Index(int row, int col) => row * Size + col;

    public (int Row, int Col) Coords(int cell) => (cell / Size, cell % Size);

    public bool SameBox(int a, int b)
    {
        if (BoxWidth <= 0 || BoxHeight <= 0)
            return RegionOfCell[a] == RegionOfCell[b];
        var (r1, c1) = Coords(a);
        var (r2, c2) = Coords(b);
        return r1 / BoxHeight == r2 / BoxHeight && c1 / BoxWidth == c2 / BoxWidth;
    }
}
