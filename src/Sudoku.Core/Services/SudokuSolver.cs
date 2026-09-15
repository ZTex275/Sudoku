using Sudoku.Core.Models;

namespace Sudoku.Core.Services;

public sealed class SudokuSolver
{
    public bool Solve(int[] grid, BoardDefinition board, Random? rng = null, int nodeLimit = 2_000_000)
    {
        var work = (int[])grid.Clone();
        var state = SolverState.Create(board, work);
        if (state is null)
            return false;

        var nodes = 0;
        if (!Search(state, rng, ref nodes, nodeLimit, stopAt: 1))
            return false;

        Array.Copy(state.Grid, grid, grid.Length);
        return true;
    }

    public int CountSolutions(int[] grid, BoardDefinition board, int limit = 2, int nodeLimit = 400_000)
    {
        var state = SolverState.Create(board, (int[])grid.Clone());
        if (state is null)
            return 0;

        var nodes = 0;
        Search(state, rng: null, ref nodes, nodeLimit, stopAt: limit);
        if (nodes >= nodeLimit && state.Solutions < limit)
            return -1;
        return state.Solutions;
    }

    public bool IsValid(int[] grid, BoardDefinition board, bool allowEmpty)
    {
        foreach (var group in board.Groups)
        {
            var seen = 0;
            foreach (var cell in group)
            {
                var value = grid[cell];
                if (value == 0)
                {
                    if (!allowEmpty)
                        return false;
                    continue;
                }

                if (value < 1 || value > board.Size)
                    return false;

                var bit = 1 << (value - 1);
                if ((seen & bit) != 0)
                    return false;
                seen |= bit;
            }
        }

        return true;
    }

    public IReadOnlyList<int> ConflictingCells(int[] grid, BoardDefinition board)
    {
        var bad = new bool[board.CellCount];
        foreach (var group in board.Groups)
        {
            var counts = new int[board.Size + 1];
            foreach (var cell in group)
            {
                var value = grid[cell];
                if (value > 0)
                    counts[value]++;
            }

            foreach (var cell in group)
            {
                var value = grid[cell];
                if (value > 0 && counts[value] > 1)
                    bad[cell] = true;
            }
        }

        return Enumerable.Range(0, board.CellCount).Where(i => bad[i]).ToList();
    }

    public int CandidatesMask(int[] grid, BoardDefinition board, int cell)
    {
        var state = SolverState.Create(board, grid);
        return state is null ? 0 : state.Candidates(cell);
    }

    private static bool Search(SolverState state, Random? rng, ref int nodes, int nodeLimit, int stopAt)
    {
        if (nodes++ >= nodeLimit)
            return state.Solutions >= stopAt;

        var cell = state.PickMrv();
        if (cell < 0)
        {
            state.Solutions++;
            return state.Solutions >= stopAt;
        }

        var mask = state.Candidates(cell);
        if (mask == 0)
            return false;

        Span<int> values = stackalloc int[16];
        var n = 0;
        for (var bit = mask; bit != 0; bit &= bit - 1)
            values[n++] = BitOperationsTrailingZero(bit) + 1;

        if (rng is not null)
            Shuffle(values, n, rng);

        for (var i = 0; i < n; i++)
        {
            var value = values[i];
            if (!state.TryPlace(cell, value))
                continue;

            if (Search(state, rng, ref nodes, nodeLimit, stopAt))
            {
                if (stopAt == 1)
                    return true;
            }

            state.Undo(cell, value);
            if (state.Solutions >= stopAt)
                return true;
            if (nodes >= nodeLimit)
                return state.Solutions >= stopAt;
        }

        return state.Solutions >= stopAt;
    }

    private static int BitOperationsTrailingZero(int mask) =>
        System.Numerics.BitOperations.TrailingZeroCount((uint)mask);

    private static void Shuffle(Span<int> values, int n, Random rng)
    {
        for (var i = n - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private sealed class SolverState
    {
        public required int Size { get; init; }
        public required int[] Grid { get; init; }
        public required int[] GroupUsed { get; init; }
        public required int[][] CellGroups { get; init; }
        public int Solutions { get; set; }

        public static SolverState? Create(BoardDefinition board, int[] grid)
        {
            var groupUsed = new int[board.Groups.Count];
            var cellGroups = new int[board.CellCount][];
            var tmp = Enumerable.Range(0, board.CellCount).Select(_ => new List<int>()).ToArray();

            for (var g = 0; g < board.Groups.Count; g++)
            {
                foreach (var cell in board.Groups[g])
                    tmp[cell].Add(g);
            }

            for (var i = 0; i < board.CellCount; i++)
                cellGroups[i] = tmp[i].ToArray();

            var state = new SolverState
            {
                Size = board.Size,
                Grid = grid,
                GroupUsed = groupUsed,
                CellGroups = cellGroups
            };

            for (var i = 0; i < grid.Length; i++)
            {
                var value = grid[i];
                if (value == 0)
                    continue;
                if (value < 1 || value > board.Size)
                    return null;
                if (!state.TryPlace(i, value, writingGrid: false))
                    return null;
            }

            return state;
        }

        public int Candidates(int cell)
        {
            if (Grid[cell] != 0)
                return 0;

            var all = (1 << Size) - 1;
            var mask = all;
            foreach (var g in CellGroups[cell])
                mask &= ~GroupUsed[g];
            return mask;
        }

        public int PickMrv()
        {
            var bestCell = -1;
            var bestCount = 64;
            for (var i = 0; i < Grid.Length; i++)
            {
                if (Grid[i] != 0)
                    continue;
                var count = System.Numerics.BitOperations.PopCount((uint)Candidates(i));
                if (count == 0)
                    return i;
                if (count < bestCount)
                {
                    bestCount = count;
                    bestCell = i;
                    if (count == 1)
                        break;
                }
            }
            return bestCell;
        }

        public bool TryPlace(int cell, int value, bool writingGrid = true)
        {
            var bit = 1 << (value - 1);
            foreach (var g in CellGroups[cell])
            {
                if ((GroupUsed[g] & bit) != 0)
                    return false;
            }

            foreach (var g in CellGroups[cell])
                GroupUsed[g] |= bit;

            if (writingGrid)
                Grid[cell] = value;
            return true;
        }

        public void Undo(int cell, int value)
        {
            var bit = 1 << (value - 1);
            foreach (var g in CellGroups[cell])
                GroupUsed[g] &= ~bit;
            Grid[cell] = 0;
        }
    }
}
