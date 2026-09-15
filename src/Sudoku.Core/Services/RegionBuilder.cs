namespace Sudoku.Core.Services;

public static class RegionBuilder
{
    private static readonly (int Dr, int Dc)[] Ortho = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    public static int[] JigsawFromBoxes(int[] solution, int size, int box, Random rng, int swaps, int? frozenRegion = null)
    {
        var map = BoardFactory.BoxRegions(size, box, box);
        var n = size * size;
        var done = 0;
        for (var attempt = 0; attempt < swaps * 80 && done < swaps; attempt++)
        {
            var cell = rng.Next(n);
            var row = cell / size;
            var col = cell % size;
            var (dr, dc) = Ortho[rng.Next(Ortho.Length)];
            var nr = row + dr;
            var nc = col + dc;
            if ((uint)nr >= (uint)size || (uint)nc >= (uint)size)
                continue;

            var other = nr * size + nc;
            var a = map[cell];
            var b = map[other];
            if (a == b)
                continue;
            if (frozenRegion is { } frozen && (a == frozen || b == frozen))
                continue;
            if (solution[cell] != solution[other])
                continue;

            map[cell] = b;
            map[other] = a;
            if (!IsConnected(map, a, size) || !IsConnected(map, b, size))
            {
                map[cell] = a;
                map[other] = b;
                continue;
            }

            done++;
        }

        return map;
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
}
