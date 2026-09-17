using Sudoku.Core.Models;

namespace Sudoku.Core.Services;

public static class CageBuilder
{
    private static readonly (int Dr, int Dc)[] Ortho = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    public static List<Cage> Partition(int[] solution, int size, Random rng, int maxSize, bool preferSingles)
    {
        var n = size * size;
        if (solution.Length != n)
            throw new ArgumentException("Решение не совпадает с размером поля.");

        var used = new bool[n];
        var order = Enumerable.Range(0, n).OrderBy(_ => rng.Next()).ToArray();
        var cages = new List<Cage>(n / 2);

        foreach (var start in order)
        {
            if (used[start])
                continue;

            var target = NextSize(rng, maxSize, preferSingles);
            var cells = Grow(start, target, used, solution, size, rng);
            cages.Add(Make(cages.Count, cells, solution));
        }

        if (!preferSingles)
            AbsorbSingles(cages, solution, size, maxSize, rng);

        return Reindex(cages);
    }

    public static List<Cage> SplitRandom(IReadOnlyList<Cage> cages, int[] solution, int size, Random rng)
    {
        var list = cages.ToList();
        var idx = list
            .Select((cage, i) => (cage, i))
            .Where(x => x.cage.Cells.Length >= 2)
            .OrderByDescending(x => x.cage.Cells.Length)
            .ThenBy(_ => rng.Next())
            .Select(x => x.i)
            .DefaultIfEmpty(-1)
            .First();
        if (idx < 0)
            return Reindex(list);

        var cage = list[idx];
        var pick = cage.Cells[rng.Next(cage.Cells.Length)];
        var rest = cage.Cells.Where(c => c != pick).ToArray();
        list.RemoveAt(idx);
        list.Add(Make(0, [pick], solution));
        foreach (var component in Components(rest, size))
            list.Add(Make(0, component, solution));

        return Reindex(list);
    }

    public static bool CoversBoard(IReadOnlyList<Cage> cages, int cellCount)
    {
        if (cages.Count == 0)
            return false;

        var seen = new bool[cellCount];
        foreach (var cage in cages)
        {
            var digits = 0;
            foreach (var cell in cage.Cells)
            {
                if ((uint)cell >= (uint)cellCount || seen[cell])
                    return false;
                seen[cell] = true;
                digits++;
            }

            if (digits == 0)
                return false;
        }

        return seen.All(v => v);
    }

    private static void AbsorbSingles(List<Cage> cages, int[] solution, int size, int maxSize, Random rng)
    {
        var map = CellMap(cages, size * size);
        var singles = cages.Where(c => c.Cells.Length == 1).OrderBy(_ => rng.Next()).ToList();
        foreach (var single in singles)
        {
            if (!cages.Contains(single) || single.Cells.Length != 1)
                continue;

            var cell = single.Cells[0];
            var neighbors = AdjacentCages(cell, map, cages, size, rng);
            foreach (var other in neighbors)
            {
                if (other.Cells.Length >= maxSize)
                    continue;
                if (other.Cells.Any(c => solution[c] == solution[cell]))
                    continue;

                var merged = other.Cells.Concat(single.Cells).ToArray();
                cages.Remove(single);
                cages.Remove(other);
                cages.Add(Make(0, merged, solution));
                map = CellMap(cages, size * size);
                break;
            }
        }
    }

    private static List<int> Grow(int start, int target, bool[] used, int[] solution, int size, Random rng)
    {
        var cells = new List<int>(target) { start };
        var digits = 1 << (solution[start] - 1);
        used[start] = true;

        while (cells.Count < target)
        {
            var candidates = new List<int>(8);
            foreach (var cell in cells)
            {
                foreach (var next in Neighbors(cell, size))
                {
                    if (used[next])
                        continue;
                    var bit = 1 << (solution[next] - 1);
                    if ((digits & bit) != 0)
                        continue;
                    if (!candidates.Contains(next))
                        candidates.Add(next);
                }
            }

            if (candidates.Count == 0)
                break;

            var pick = candidates[rng.Next(candidates.Count)];
            cells.Add(pick);
            used[pick] = true;
            digits |= 1 << (solution[pick] - 1);
        }

        return cells;
    }

    private static int NextSize(Random rng, int maxSize, bool preferSingles)
    {
        var roll = rng.Next(100);
        if (preferSingles)
        {
            if (maxSize <= 3)
                return roll < 18 ? 1 : roll < 68 ? 2 : 3;
            if (roll < 10) return 1;
            if (roll < 45) return 2;
            if (roll < 78) return 3;
            return Math.Min(4, maxSize);
        }

        if (maxSize <= 4)
            return roll < 28 ? 2 : roll < 68 ? 3 : Math.Min(4, maxSize);

        if (roll < 22) return 3;
        if (roll < 62) return 4;
        if (roll < 90) return Math.Min(5, maxSize);
        return Math.Min(6, maxSize);
    }

    private static List<int[]> Components(int[] cells, int size)
    {
        var set = cells.ToHashSet();
        var seen = new HashSet<int>();
        var list = new List<int[]>();
        foreach (var start in cells)
        {
            if (!seen.Add(start))
                continue;

            var stack = new Stack<int>();
            var part = new List<int>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                part.Add(cur);
                foreach (var next in Neighbors(cur, size))
                {
                    if (!set.Contains(next) || !seen.Add(next))
                        continue;
                    stack.Push(next);
                }
            }

            list.Add(part.ToArray());
        }

        return list;
    }

    private static IEnumerable<int> Neighbors(int cell, int size)
    {
        var row = cell / size;
        var col = cell % size;
        foreach (var (dr, dc) in Ortho)
        {
            var nr = row + dr;
            var nc = col + dc;
            if ((uint)nr >= (uint)size || (uint)nc >= (uint)size)
                continue;
            yield return nr * size + nc;
        }
    }

    private static int[] CellMap(List<Cage> cages, int n)
    {
        var map = new int[n];
        Array.Fill(map, -1);
        for (var i = 0; i < cages.Count; i++)
        {
            foreach (var cell in cages[i].Cells)
                map[cell] = i;
        }

        return map;
    }

    private static IEnumerable<Cage> AdjacentCages(int cell, int[] map, List<Cage> cages, int size, Random rng)
    {
        var ids = new List<int>();
        foreach (var next in Neighbors(cell, size))
        {
            var id = map[next];
            if (id < 0 || id == map[cell] || ids.Contains(id))
                continue;
            ids.Add(id);
        }

        return ids.OrderBy(_ => rng.Next()).Select(id => cages[id]);
    }

    private static Cage Make(int id, IReadOnlyList<int> cells, int[] solution)
    {
        var arr = cells.ToArray();
        var sum = 0;
        foreach (var cell in arr)
            sum += solution[cell];
        return new Cage { Id = id, Cells = arr, Sum = sum };
    }

    private static List<Cage> Reindex(List<Cage> cages)
    {
        for (var i = 0; i < cages.Count; i++)
        {
            var cage = cages[i];
            cages[i] = new Cage { Id = i, Cells = cage.Cells, Sum = cage.Sum };
        }

        return cages;
    }
}
