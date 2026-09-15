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
            PuzzleKind.Jigsaw => GenerateJigsawOrStar(PuzzleKind.Jigsaw, difficulty, rng),
            PuzzleKind.Star => GenerateJigsawOrStar(PuzzleKind.Star, difficulty, rng),
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

    private GeneratedPuzzle GenerateJigsawOrStar(PuzzleKind kind, Difficulty difficulty, Random rng)
    {
        for (var attempt = 0; attempt < 120; attempt++)
        {
            var regions = kind == PuzzleKind.Star
                ? RegionBuilder.AstraTriangles(rng)
                : RegionBuilder.Jigsaw(rng);
            var board = kind == PuzzleKind.Star
                ? BoardFactory.Star(regions)
                : BoardFactory.Jigsaw(regions);

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

        throw new InvalidOperationException(
            kind == PuzzleKind.Star
                ? "Не удалось построить судоку-астру. Попробуйте ещё раз."
                : "Не удалось построить фигурное судоку. Попробуйте ещё раз.");
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

        var extra = board.Kind is PuzzleKind.Jigsaw or PuzzleKind.Star or PuzzleKind.Diagonal ? 2 : 0;
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
