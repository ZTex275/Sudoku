namespace Sudoku.Core.Services;

public static class RegionBuilder
{
    private static readonly (int Dr, int Dc)[] Ortho = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    private static readonly HashSet<string> TriangleShapes = AllTriangleShapes().ToHashSet();

    // Ряды пирамид 1-3-5 и 2-3-4, состыкованные рядом.
    private static readonly int[][] AstraTemplates = ParseTemplates(
    [
        "001111122000111222000012222334444455333444555333345555667777788666777888666678888",
        "000012222000111222001111122333345555333444555334444455666678888666777888667777788"
    ]);

    public static int[] Jigsaw(Random rng, int exchanges = 200)
    {
        var map = BoardFactory.BoxRegions(9, 3, 3);
        Morph(map, 9, rng, exchanges);
        if (IsStandardBoxes(map, 9, 3))
            Morph(map, 9, rng, exchanges * 2);
        return map;
    }

    public static int[] AstraTriangles(Random rng)
    {
        var src = AstraTemplates[rng.Next(AstraTemplates.Length)];
        var idMap = Enumerable.Range(0, 9).OrderBy(_ => rng.Next()).ToArray();
        return Transform(src, 9, rng.Next(8), idMap);
    }

    public static bool IsStandardBoxes(int[] map, int size, int box)
    {
        var expected = BoardFactory.BoxRegions(size, box, box);
        return map.Length == expected.Length && map.SequenceEqual(expected);
    }

    public static bool IsTriangleRegion(int[] map, int region, int size)
    {
        var cells = new List<(int R, int C)>(size);
        for (var i = 0; i < map.Length; i++)
        {
            if (map[i] != region)
                continue;
            cells.Add((i / size, i % size));
        }

        if (cells.Count != size)
            return false;

        var minR = cells.Min(c => c.R);
        var minC = cells.Min(c => c.C);
        return TriangleShapes.Contains(ShapeKey(cells.Select(c => (R: c.R - minR, C: c.C - minC))));
    }

    public static bool HasUniqueDigits(int[] grid, IEnumerable<int> cells, int size)
    {
        var seen = 0;
        var count = 0;
        foreach (var cell in cells)
        {
            var value = grid[cell];
            if (value < 1 || value > size)
                return false;
            var bit = 1 << (value - 1);
            if ((seen & bit) != 0)
                return false;
            seen |= bit;
            count++;
        }

        return count == size && seen == (1 << size) - 1;
    }

    private static void Morph(int[] map, int size, Random rng, int exchanges)
    {
        var n = size * size;
        var done = 0;
        for (var attempt = 0; attempt < exchanges * 120 && done < exchanges; attempt++)
        {
            var cell = rng.Next(n);
            var (dr, dc) = Ortho[rng.Next(Ortho.Length)];
            var row = cell / size;
            var col = cell % size;
            var nr = row + dr;
            var nc = col + dc;
            if ((uint)nr >= (uint)size || (uint)nc >= (uint)size)
                continue;

            var other = nr * size + nc;
            var a = map[cell];
            var b = map[other];
            if (a == b)
                continue;

            map[cell] = b;
            if (!IsConnected(map, a, size))
            {
                map[cell] = a;
                continue;
            }

            var moved = false;
            foreach (var donor in Donors(map, b, a, cell, size, rng))
            {
                map[donor] = a;
                if (IsConnected(map, a, size) && IsConnected(map, b, size))
                {
                    done++;
                    moved = true;
                    break;
                }

                map[donor] = b;
            }

            if (!moved)
                map[cell] = a;
        }
    }

    private static IEnumerable<int> Donors(int[] map, int fromRegion, int toRegion, int skip, int size, Random rng)
    {
        var n = size * size;
        var found = new List<int>(8);
        for (var i = 0; i < n; i++)
        {
            if (i == skip || map[i] != fromRegion)
                continue;
            if (TouchesRegion(map, i, toRegion, size))
                found.Add(i);
        }

        for (var i = found.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (found[i], found[j]) = (found[j], found[i]);
        }

        var take = Math.Min(8, found.Count);
        for (var i = 0; i < take; i++)
            yield return found[i];
    }

    private static bool TouchesRegion(int[] map, int cell, int region, int size)
    {
        var row = cell / size;
        var col = cell % size;
        foreach (var (dr, dc) in Ortho)
        {
            var nr = row + dr;
            var nc = col + dc;
            if ((uint)nr >= (uint)size || (uint)nc >= (uint)size)
                continue;
            if (map[nr * size + nc] == region)
                return true;
        }

        return false;
    }

    private static bool IsConnected(int[] map, int region, int size)
    {
        var first = -1;
        var total = 0;
        for (var i = 0; i < map.Length; i++)
        {
            if (map[i] != region)
                continue;
            total++;
            if (first < 0)
                first = i;
        }

        if (total == 0 || first < 0)
            return false;

        Span<bool> seen = stackalloc bool[map.Length];
        Span<int> stack = stackalloc int[map.Length];
        var depth = 0;
        var visited = 0;
        stack[depth++] = first;
        seen[first] = true;

        while (depth > 0)
        {
            var cur = stack[--depth];
            visited++;
            var row = cur / size;
            var col = cur % size;
            foreach (var (dr, dc) in Ortho)
            {
                var nr = row + dr;
                var nc = col + dc;
                if ((uint)nr >= (uint)size || (uint)nc >= (uint)size)
                    continue;
                var next = nr * size + nc;
                if (seen[next] || map[next] != region)
                    continue;
                seen[next] = true;
                stack[depth++] = next;
            }
        }

        return visited == total;
    }

    private static int[] Transform(int[] map, int size, int variant, int[] idMap)
    {
        var n = size * size;
        var tmp = new int[n];
        var flip = (variant & 4) != 0;
        var rots = variant & 3;
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                var rr = r;
                var cc = flip ? size - 1 - c : c;
                for (var k = 0; k < rots; k++)
                    (rr, cc) = (cc, size - 1 - rr);
                tmp[rr * size + cc] = idMap[map[r * size + c]];
            }
        }

        return tmp;
    }

    private static int[][] ParseTemplates(string[] raw)
    {
        var list = new int[raw.Length][];
        for (var t = 0; t < raw.Length; t++)
        {
            var s = raw[t];
            if (s.Length != 81)
                throw new InvalidOperationException("Шаблон астры должен быть 9×9.");
            var map = new int[81];
            var counts = new int[9];
            for (var i = 0; i < 81; i++)
            {
                var id = s[i] - '0';
                if ((uint)id >= 9)
                    throw new InvalidOperationException("Некорректный шаблон астры.");
                map[i] = id;
                counts[id]++;
            }

            if (counts.Any(c => c != 9))
                throw new InvalidOperationException("В шаблоне астры каждая фигура должна быть из 9 клеток.");
            list[t] = map;
        }

        return list;
    }

    private static IEnumerable<string> AllTriangleShapes()
    {
        int[][] bases =
        [
            // 1-3-5
            [0, 1, 2, 3, 4, 17, 18, 19, 34],
            // 2-3-4
            [0, 1, 16, 17, 18, 32, 33, 34, 35]
        ];

        foreach (var cells in bases)
        {
            foreach (var shape in Orients(cells))
                yield return ShapeKey(shape);
        }
    }

    private static IEnumerable<(int R, int C)[]> Orients(int[] packed)
    {
        var seen = new HashSet<string>();
        var cur = packed.Select(p => (R: p / 16, C: p % 16)).ToArray();
        for (var flip = 0; flip < 2; flip++)
        {
            for (var rot = 0; rot < 4; rot++)
            {
                var norm = Normalize(cur);
                var key = string.Join(";", norm.Select(c => $"{c.R},{c.C}"));
                if (seen.Add(key))
                    yield return norm;
                cur = cur.Select(c => (c.C, -c.R)).ToArray();
            }

            cur = cur.Select(c => (c.R, -c.C)).ToArray();
        }
    }

    private static (int R, int C)[] Normalize((int R, int C)[] cells)
    {
        var minR = cells.Min(c => c.R);
        var minC = cells.Min(c => c.C);
        return cells.Select(c => (R: c.R - minR, C: c.C - minC)).OrderBy(c => c.R).ThenBy(c => c.C).ToArray();
    }

    private static string ShapeKey(IEnumerable<(int R, int C)> cells) =>
        string.Join(";", cells.OrderBy(c => c.R).ThenBy(c => c.C).Select(c => $"{c.R},{c.C}"));
}
