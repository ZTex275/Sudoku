using Sudoku.Core.Models;

namespace Sudoku.Core.Services;

public sealed class SudokuGenerator
{
    private readonly SudokuSolver _solver = new();

    public GeneratedPuzzle Generate(PuzzleKind kind, Difficulty difficulty, int? seed = null)
    {
        var rng = seed is null ? new Random() : new Random(seed.Value);
        return kind switch
        {
            PuzzleKind.Classic9 => GenerateRegular(BoardFactory.Classic(9), difficulty, rng, usePattern: true),
            PuzzleKind.Classic16 => GenerateRegular(BoardFactory.Classic(16), difficulty, rng, usePattern: true),
            PuzzleKind.Diagonal => GenerateConstrained9(PuzzleKind.Diagonal, difficulty, rng),
            PuzzleKind.Jigsaw => GenerateJigsaw(difficulty, rng),
            PuzzleKind.Star => GenerateAstra(difficulty, rng),
            PuzzleKind.Hoshi => GenerateHoshi(difficulty, rng),
            PuzzleKind.Killer => GenerateKiller(difficulty, rng),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private GeneratedPuzzle GenerateRegular(BoardDefinition board, Difficulty difficulty, Random rng, bool usePattern)
    {
        var solution = usePattern
            ? PatternSolution(board.Size, board.BoxWidth, rng)
            : FillEmpty(board, rng) ?? throw new InvalidOperationException("Не удалось заполнить поле.");

        if (!_solver.IsValid(solution, board, allowEmpty: false))
            throw new InvalidOperationException("Сгенерированное решение не прошло проверку.");

        var givens = DigHoles(solution, board, difficulty, rng);
        return new GeneratedPuzzle
        {
            Board = board,
            Difficulty = difficulty,
            Givens = givens,
            Solution = solution
        };
    }

    private GeneratedPuzzle GenerateConstrained9(PuzzleKind kind, Difficulty difficulty, Random rng)
    {
        BoardDefinition? board = kind == PuzzleKind.Diagonal ? BoardFactory.Diagonal9() : null;
        if (board is null)
            throw new ArgumentOutOfRangeException(nameof(kind));

        int[]? solution = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            solution = FillEmpty(board, rng);
            if (solution is not null)
                break;
        }

        if (solution is null)
            throw new InvalidOperationException("Не удалось построить диагональное судоку.");

        var givens = DigHoles(solution, board, difficulty, rng);
        return new GeneratedPuzzle
        {
            Board = board,
            Difficulty = difficulty,
            Givens = givens,
            Solution = solution
        };
    }

    private GeneratedPuzzle GenerateAstra(Difficulty difficulty, Random rng)
    {
        var board = BoardFactory.Astra();
        var solution = AstraLayout.PatternSolution(rng);
        if (!_solver.IsValid(solution, board, allowEmpty: false))
            throw new InvalidOperationException("Не удалось построить судоку-астру.");

        var givens = DigHoles(solution, board, difficulty, rng);
        return new GeneratedPuzzle
        {
            Board = board,
            Difficulty = difficulty,
            Givens = givens,
            Solution = solution
        };
    }

    private GeneratedPuzzle GenerateHoshi(Difficulty difficulty, Random rng)
    {
        var board = BoardFactory.Hoshi();
        int[]? solution = null;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            solution = FillEmpty(board, rng);
            if (solution is not null)
                break;
        }

        if (solution is null)
            throw new InvalidOperationException("Не удалось построить судоку-звезду.");

        var givens = DigHoles(solution, board, difficulty, rng);
        return new GeneratedPuzzle
        {
            Board = board,
            Difficulty = difficulty,
            Givens = givens,
            Solution = solution
        };
    }

    private GeneratedPuzzle GenerateJigsaw(Difficulty difficulty, Random rng)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var regions = RegionBuilder.Jigsaw(rng);
            var board = BoardFactory.Jigsaw(regions);

            var filled = new int[board.CellCount];
            if (!_solver.Solve(filled, board, rng, nodeLimit: 400_000)
                || !_solver.IsValid(filled, board, allowEmpty: false))
                continue;

            var givens = DigHoles(filled, board, difficulty, rng);
            return new GeneratedPuzzle
            {
                Board = board,
                Difficulty = difficulty,
                Givens = givens,
                Solution = filled
            };
        }

        throw new InvalidOperationException("Не удалось построить фигурное судоку. Попробуйте ещё раз.");
    }

    private GeneratedPuzzle GenerateKiller(Difficulty difficulty, Random rng)
    {
        var maxSize = difficulty switch
        {
            Difficulty.Easy => 3,
            Difficulty.Medium => 4,
            Difficulty.Hard => 5,
            _ => 5
        };

        var solution = PatternSolution(9, 3, rng);
        const int size = 9;
        const int n = 81;
        var groups = Enumerable.Range(0, n).Select(i => new List<int> { i }).ToList();
        var idOf = Enumerable.Range(0, n).ToArray();
        var edges = KillerEdges(size, rng);
        var empty = new int[n];
        var uniqueLimit = 80_000;
        var started = System.Diagnostics.Stopwatch.StartNew();
        var budgetMs = 3500;

        List<Cage> Snapshot()
        {
            var cages = new List<Cage>();
            foreach (var cells in groups)
            {
                if (cells.Count == 0)
                    continue;
                var arr = cells.ToArray();
                var sum = 0;
                foreach (var cell in arr)
                    sum += solution[cell];
                cages.Add(new Cage { Id = cages.Count, Cells = arr, Sum = sum });
            }

            return cages;
        }

        bool Unique(int nodeLimit)
        {
            var board = BoardFactory.Killer(Snapshot());
            return _solver.CountSolutions(empty, board, limit: 2, nodeLimit: nodeLimit) == 1;
        }

        bool TryMerge(int a, int b, int cap, bool mustStayUnique)
        {
            var ia = idOf[a];
            var ib = idOf[b];
            if (ia == ib)
                return false;

            var left = groups[ia];
            var right = groups[ib];
            if (left.Count + right.Count > cap)
                return false;
            if (KillerDigitsOverlap(left, right, solution))
                return false;

            left.AddRange(right);
            var moved = right.ToList();
            right.Clear();
            foreach (var cell in moved)
                idOf[cell] = ia;

            if (mustStayUnique && !Unique(uniqueLimit))
            {
                foreach (var cell in moved)
                {
                    left.Remove(cell);
                    idOf[cell] = ib;
                }
                right.AddRange(moved);
                return false;
            }

            return true;
        }

        foreach (var (a, b) in edges)
            TryMerge(a, b, cap: 2, mustStayUnique: false);

        if (!Unique(uniqueLimit))
        {
            foreach (var (a, b) in edges)
            {
                var ia = idOf[a];
                if (groups[ia].Count < 2)
                    continue;
                SplitOff(groups, idOf, a);
                if (Unique(uniqueLimit))
                    break;
            }
        }

        if (maxSize > 2)
        {
            var extra = difficulty switch
            {
                Difficulty.Easy => 36,
                Difficulty.Medium => 22,
                Difficulty.Hard => 14,
                _ => 10
            };
            foreach (var (a, b) in edges)
            {
                if (extra <= 0 || started.ElapsedMilliseconds > budgetMs)
                    break;
                if (TryMerge(a, b, maxSize, mustStayUnique: true))
                    extra--;
            }
        }

        var final = Snapshot();
        var resultBoard = BoardFactory.Killer(final);
        if (!_solver.IsValid(solution, resultBoard, allowEmpty: false)
            || _solver.CountSolutions(empty, resultBoard, limit: 2, nodeLimit: 200_000) != 1)
            throw new InvalidOperationException("Не удалось построить киллер-судоку. Попробуйте ещё раз.");

        return new GeneratedPuzzle
        {
            Board = resultBoard,
            Difficulty = difficulty,
            Givens = empty,
            Solution = solution
        };
    }

    private static void SplitOff(List<List<int>> groups, int[] idOf, int cell)
    {
        var id = idOf[cell];
        var host = groups[id];
        if (host.Count < 2)
            return;
        host.Remove(cell);
        var fresh = groups.Count;
        groups.Add([cell]);
        idOf[cell] = fresh;
    }

    private static List<(int A, int B)> KillerEdges(int size, Random rng)
    {
        var edges = new List<(int, int)>(size * size * 2);
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                var i = r * size + c;
                if (c + 1 < size)
                    edges.Add((i, i + 1));
                if (r + 1 < size)
                    edges.Add((i, i + size));
            }
        }

        for (var i = edges.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (edges[i], edges[j]) = (edges[j], edges[i]);
        }

        return edges;
    }

    private static bool KillerDigitsOverlap(List<int> left, List<int> right, int[] solution)
    {
        var seen = 0;
        foreach (var cell in left)
            seen |= 1 << (solution[cell] - 1);
        foreach (var cell in right)
        {
            var bit = 1 << (solution[cell] - 1);
            if ((seen & bit) != 0)
                return true;
        }

        return false;
    }

    private int[]? FillEmpty(BoardDefinition board, Random rng)
    {
        var grid = new int[board.CellCount];
        return _solver.Solve(grid, board, rng, nodeLimit: 1_500_000) ? grid : null;
    }

    private int[] DigHoles(int[] solution, BoardDefinition board, Difficulty difficulty, Random rng)
    {
        var puzzle = (int[])solution.Clone();
        var order = Enumerable.Range(0, board.CellCount).OrderBy(_ => rng.Next()).ToArray();
        var clues = board.CellCount;
        var target = TargetClues(board, difficulty);
        var uniqueLimit = board.Size >= 16 ? 50_000 : 250_000;
        var budgetMs = board.Size >= 16 ? 4000 : 8000;
        var started = System.Diagnostics.Stopwatch.StartNew();

        foreach (var cell in order)
        {
            if (clues <= target || started.ElapsedMilliseconds > budgetMs)
                break;

            var backup = puzzle[cell];
            puzzle[cell] = 0;
            var count = _solver.CountSolutions(puzzle, board, limit: 2, nodeLimit: uniqueLimit);
            if (count != 1)
                puzzle[cell] = backup;
            else
                clues--;
        }

        return puzzle;
    }

    private static int TargetClues(BoardDefinition board, Difficulty difficulty)
    {
        if (board.Size == 16)
        {
            return difficulty switch
            {
                Difficulty.Easy => 150,
                Difficulty.Medium => 128,
                Difficulty.Hard => 110,
                _ => 96
            };
        }

        if (board.Kind == PuzzleKind.Star)
        {
            return difficulty switch
            {
                Difficulty.Easy => 50,
                Difficulty.Medium => 40,
                Difficulty.Hard => 32,
                _ => 24
            };
        }

        if (board.Kind == PuzzleKind.Hoshi)
        {
            return difficulty switch
            {
                Difficulty.Easy => 28,
                Difficulty.Medium => 22,
                Difficulty.Hard => 16,
                _ => 12
            };
        }

        var extra = board.Kind is PuzzleKind.Jigsaw or PuzzleKind.Diagonal ? 2 : 0;
        return extra + difficulty switch
        {
            Difficulty.Easy => 40,
            Difficulty.Medium => 32,
            Difficulty.Hard => 28,
            _ => 24
        };
    }

    public static int[] PatternSolution(int size, int box, Random rng)
    {
        var grid = new int[size * size];
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
                grid[r * size + c] = (r * box + r / box + c) % size + 1;
        }

        ShuffleRowsInBands(grid, size, box, rng);
        Transpose(grid, size);
        ShuffleRowsInBands(grid, size, box, rng);
        Transpose(grid, size);
        ShuffleBands(grid, size, box, rng);
        Transpose(grid, size);
        ShuffleBands(grid, size, box, rng);
        Transpose(grid, size);
        PermuteDigits(grid, size, rng);
        return grid;
    }

    private static void ShuffleRowsInBands(int[] grid, int size, int box, Random rng)
    {
        for (var band = 0; band < box; band++)
        {
            var rows = Enumerable.Range(0, box).OrderBy(_ => rng.Next()).ToArray();
            var copy = new int[box][];
            for (var i = 0; i < box; i++)
                copy[i] = Row(grid, size, band * box + i);

            for (var i = 0; i < box; i++)
                SetRow(grid, size, band * box + i, copy[rows[i]]);
        }
    }

    private static void ShuffleBands(int[] grid, int size, int box, Random rng)
    {
        var order = Enumerable.Range(0, box).OrderBy(_ => rng.Next()).ToArray();
        var copies = new int[box][][];
        for (var b = 0; b < box; b++)
        {
            copies[b] = new int[box][];
            for (var i = 0; i < box; i++)
                copies[b][i] = Row(grid, size, b * box + i);
        }

        for (var b = 0; b < box; b++)
        {
            for (var i = 0; i < box; i++)
                SetRow(grid, size, b * box + i, copies[order[b]][i]);
        }
    }

    private static void PermuteDigits(int[] grid, int size, Random rng)
    {
        var map = Enumerable.Range(1, size).OrderBy(_ => rng.Next()).ToArray();
        for (var i = 0; i < grid.Length; i++)
            grid[i] = map[grid[i] - 1];
    }

    private static int[] Row(int[] grid, int size, int row)
    {
        var data = new int[size];
        Array.Copy(grid, row * size, data, 0, size);
        return data;
    }

    private static void SetRow(int[] grid, int size, int row, int[] data) =>
        Array.Copy(data, 0, grid, row * size, size);

    private static void Transpose(int[] grid, int size)
    {
        for (var r = 0; r < size; r++)
        {
            for (var c = r + 1; c < size; c++)
            {
                var a = r * size + c;
                var b = c * size + r;
                (grid[a], grid[b]) = (grid[b], grid[a]);
            }
        }
    }
}
