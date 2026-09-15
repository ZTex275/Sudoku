using Sudoku.Core.Models;

namespace Sudoku.Core.Services;

public static class BoardFactory
{
    public static BoardDefinition Classic(int size)
    {
        if (size is not (9 or 16))
            throw new ArgumentOutOfRangeException(nameof(size));

        var box = size == 9 ? 3 : 4;
        var groups = StandardGroups(size, box, box);
        return new BoardDefinition
        {
            Kind = size == 9 ? PuzzleKind.Classic9 : PuzzleKind.Classic16,
            Title = size == 9 ? "Классика 9×9" : "Большое 16×16",
            Size = size,
            BoxWidth = box,
            BoxHeight = box,
            Groups = groups,
            RegionOfCell = BoxRegions(size, box, box)
        };
    }

    public static BoardDefinition Diagonal9()
    {
        var groups = StandardGroups(9, 3, 3);
        groups.Add(MainDiagonal(9));
        groups.Add(AntiDiagonal(9));
        var diagonal = new bool[81];
        foreach (var cell in MainDiagonal(9).Concat(AntiDiagonal(9)))
            diagonal[cell] = true;

        return new BoardDefinition
        {
            Kind = PuzzleKind.Diagonal,
            Title = "Диагональное",
            Size = 9,
            BoxWidth = 3,
            BoxHeight = 3,
            Groups = groups,
            RegionOfCell = BoxRegions(9, 3, 3),
            DiagonalCells = diagonal
        };
    }

    public static BoardDefinition Jigsaw(int[] regionOfCell)
    {
        var size = 9;
        EnsureRegions(regionOfCell, size);
        var groups = RowColGroups(size);
        groups.AddRange(GroupsFromRegions(regionOfCell, size));
        return new BoardDefinition
        {
            Kind = PuzzleKind.Jigsaw,
            Title = "Фигурное",
            Size = size,
            Groups = groups,
            RegionOfCell = regionOfCell
        };
    }

    public static BoardDefinition Astra()
    {
        var regions = new int[AstraLayout.Cells];
        for (var i = 0; i < regions.Length; i++)
            regions[i] = AstraLayout.Coords(i).Sector;

        return new BoardDefinition
        {
            Kind = PuzzleKind.Star,
            Title = "Астра",
            Size = AstraLayout.Rings,
            Stride = AstraLayout.Sectors,
            Groups = AstraLayout.AllGroups(),
            RegionOfCell = regions
        };
    }

    public static List<int[]> StandardGroups(int size, int boxWidth, int boxHeight)
    {
        var groups = RowColGroups(size);
        for (var br = 0; br < size / boxHeight; br++)
        {
            for (var bc = 0; bc < size / boxWidth; bc++)
            {
                var box = new int[size];
                var n = 0;
                for (var r = 0; r < boxHeight; r++)
                {
                    for (var c = 0; c < boxWidth; c++)
                        box[n++] = (br * boxHeight + r) * size + bc * boxWidth + c;
                }
                groups.Add(box);
            }
        }
        return groups;
    }

    public static List<int[]> RowColGroups(int size)
    {
        var groups = new List<int[]>(size * 3);
        for (var r = 0; r < size; r++)
        {
            var row = new int[size];
            var col = new int[size];
            for (var i = 0; i < size; i++)
            {
                row[i] = r * size + i;
                col[i] = i * size + r;
            }
            groups.Add(row);
            groups.Add(col);
        }
        return groups;
    }

    public static int[] MainDiagonal(int size)
    {
        var d = new int[size];
        for (var i = 0; i < size; i++)
            d[i] = i * size + i;
        return d;
    }

    public static int[] AntiDiagonal(int size)
    {
        var d = new int[size];
        for (var i = 0; i < size; i++)
            d[i] = i * size + (size - 1 - i);
        return d;
    }

    public static int[] BoxRegions(int size, int boxWidth, int boxHeight)
    {
        var map = new int[size * size];
        var boxesPerRow = size / boxWidth;
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
                map[r * size + c] = r / boxHeight * boxesPerRow + c / boxWidth;
        }
        return map;
    }

    public static List<int[]> GroupsFromRegions(int[] regionOfCell, int size)
    {
        var buckets = Enumerable.Range(0, size).Select(_ => new List<int>(size)).ToArray();
        for (var i = 0; i < regionOfCell.Length; i++)
            buckets[regionOfCell[i]].Add(i);

        return buckets.Select(b => b.ToArray()).ToList();
    }

    private static void EnsureRegions(int[] regionOfCell, int size)
    {
        if (regionOfCell.Length != size * size)
            throw new ArgumentException("Карта регионов не совпадает с размером поля.");

        var counts = new int[size];
        foreach (var id in regionOfCell)
        {
            if (id < 0 || id >= size)
                throw new ArgumentException("Некорректный id региона.");
            counts[id]++;
        }

        if (counts.Any(c => c != size))
            throw new ArgumentException("Каждый регион должен содержать ровно Size клеток.");
    }
}
