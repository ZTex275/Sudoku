namespace Sudoku.Core.Models;

public sealed class BoardDefinition
{
    public required PuzzleKind Kind { get; init; }
    public required string Title { get; init; }
    public required int Size { get; init; }
    public int Stride { get; init; }
    public int BoxWidth { get; init; }
    public int BoxHeight { get; init; }
    public required IReadOnlyList<int[]> Groups { get; init; }
    public required int[] RegionOfCell { get; init; }
    public IReadOnlyList<Cage> Cages { get; init; } = Array.Empty<Cage>();
    public int[] CageId { get; init; } = Array.Empty<int>();
    public bool[] DiagonalCells { get; init; } = Array.Empty<bool>();
    public bool[] StarCells { get; init; } = Array.Empty<bool>();
    public int Width => Stride > 0 ? Stride : Size;
    public int Height => Size;
    public int CellCount => Width * Height;

    public int Index(int row, int col) => row * Width + col;

    public (int Row, int Col) Coords(int cell) => (cell / Width, cell % Width);

    public bool ArePeers(int a, int b)
    {
        if (a == b)
            return false;
        foreach (var group in Groups)
        {
            var hasA = false;
            var hasB = false;
            foreach (var cell in group)
            {
                if (cell == a) hasA = true;
                if (cell == b) hasB = true;
            }

            if (hasA && hasB)
                return true;
        }

        return false;
    }

    public bool SameBox(int a, int b)
    {
        if (BoxWidth <= 0 || BoxHeight <= 0)
            return RegionOfCell[a] == RegionOfCell[b];
        var (r1, c1) = Coords(a);
        var (r2, c2) = Coords(b);
        return r1 / BoxHeight == r2 / BoxHeight && c1 / BoxWidth == c2 / BoxWidth;
    }
}
