using Sudoku.Core.Models;
using Sudoku.Core.Services;

namespace Sudoku.Core;

public sealed class SudokuGame
{
    private readonly SudokuSolver _solver = new();
    private readonly Stack<Move> _undo = new();
    private DateTime _startedUtc;
    private TimeSpan _frozenElapsed;

    public SudokuGame(GeneratedPuzzle puzzle)
    {
        Board = puzzle.Board;
        Difficulty = puzzle.Difficulty;
        Givens = (int[])puzzle.Givens.Clone();
        Solution = (int[])puzzle.Solution.Clone();
        Current = (int[])puzzle.Givens.Clone();
        Notes = new ulong[Board.CellCount];
        Selected = FirstEmpty();
        _startedUtc = DateTime.UtcNow;
    }

    public SavedGame ToSaved(PuzzleKind selectedKind, Difficulty selectedDifficulty) => new()
    {
        Version = SavedGame.CurrentVersion,
        Kind = selectedKind,
        Difficulty = selectedDifficulty,
        PuzzleKind = Board.Kind,
        PuzzleDifficulty = Difficulty,
        RegionOfCell = Board.RegionOfCell.ToArray(),
        Cages = Board.Cages.Select(c => new SavedCage { Cells = c.Cells.ToArray(), Sum = c.Sum }).ToArray(),
        Givens = (int[])Givens.Clone(),
        Solution = (int[])Solution.Clone(),
        Current = (int[])Current.Clone(),
        Notes = (ulong[])Notes.Clone(),
        Selected = Selected,
        PencilMode = PencilMode,
        Completed = Completed,
        ElapsedSeconds = Elapsed.TotalSeconds
    };

    public static SudokuGame? TryRestore(SavedGame save)
    {
        if (save.Givens is null || save.Solution is null || save.Current is null)
            return null;

        BoardDefinition board;
        try
        {
            board = BoardFactory.Restore(save.PuzzleKind, save.RegionOfCell, save.Cages);
        }
        catch
        {
            return null;
        }

        var n = board.CellCount;
        if (save.Givens.Length != n || save.Solution.Length != n || save.Current.Length != n)
            return null;
        if (save.Notes is not null && save.Notes.Length != n)
            return null;

        var puzzle = new GeneratedPuzzle
        {
            Board = board,
            Difficulty = save.PuzzleDifficulty,
            Givens = save.Givens,
            Solution = save.Solution
        };
        var game = new SudokuGame(puzzle);
        for (var i = 0; i < n; i++)
        {
            if (game.Givens[i] != 0)
            {
                game.Current[i] = game.Givens[i];
                game.Notes[i] = 0;
                continue;
            }

            var value = save.Current[i];
            game.Current[i] = value >= 0 && value <= board.Size ? value : 0;
            game.Notes[i] = save.Notes is null || game.Current[i] != 0 ? 0UL : save.Notes[i];
        }

        game.Selected = save.Selected >= 0 && save.Selected < n ? save.Selected : game.FirstEmpty();
        game.PencilMode = save.PencilMode;
        var elapsed = TimeSpan.FromSeconds(Math.Max(0, save.ElapsedSeconds));
        if (save.Completed && game.Current.All(v => v != 0) && game._solver.IsValid(game.Current, board, allowEmpty: false))
        {
            game.Completed = true;
            game._frozenElapsed = elapsed;
        }
        else
        {
            game._startedUtc = DateTime.UtcNow - elapsed;
        }

        return game;
    }

    public BoardDefinition Board { get; }
    public Difficulty Difficulty { get; }
    public int[] Givens { get; }
    public int[] Solution { get; }
    public int[] Current { get; }
    public ulong[] Notes { get; }
    public int Selected { get; set; } = -1;
    public bool PencilMode { get; set; }
    public bool Completed { get; private set; }
    public bool ShowMistakes { get; private set; }
    public bool CanUndo => _undo.Count > 0;

    public TimeSpan Elapsed => Completed ? _frozenElapsed : DateTime.UtcNow - _startedUtc;

    public bool IsGiven(int cell) => Givens[cell] != 0;

    public int Remaining => Current.Count(v => v == 0);

    public bool HasUserDigits =>
        Enumerable.Range(0, Board.CellCount).Any(i => Givens[i] == 0 && Current[i] != 0);

    public void Place(int value)
    {
        if (Completed || Selected < 0 || IsGiven(Selected))
            return;

        if (value < 1 || value > Board.Size)
            return;

        PushUndo(Selected);
        ShowMistakes = false;
        if (PencilMode)
        {
            Current[Selected] = 0;
            Notes[Selected] ^= 1UL << value;
            return;
        }

        Current[Selected] = Current[Selected] == value ? 0 : value;
        Notes[Selected] = 0;
        CheckCompletion();
    }

    public void ClearSelected()
    {
        if (Completed || Selected < 0 || IsGiven(Selected))
            return;
        PushUndo(Selected);
        Current[Selected] = 0;
        Notes[Selected] = 0;
        ShowMistakes = false;
    }

    public void Undo()
    {
        if (_undo.Count == 0)
            return;

        var move = _undo.Pop();
        Current[move.Cell] = move.Value;
        Notes[move.Cell] = move.Notes;
        Selected = move.Cell;
        ShowMistakes = false;
        Completed = false;
    }

    public void MoveSelection(int dRow, int dCol)
    {
        if (Selected < 0)
        {
            Selected = 0;
            return;
        }

        if (Board.Kind == PuzzleKind.Hoshi)
        {
            MoveHoshi(dRow, dCol);
            return;
        }

        var (row, col) = Board.Coords(Selected);
        row = Math.Clamp(row + dRow, 0, Board.Height - 1);
        if (Board.Kind == PuzzleKind.Star)
            col = (col + dCol + Board.Width) % Board.Width;
        else
            col = Math.Clamp(col + dCol, 0, Board.Width - 1);
        Selected = Board.Index(row, col);
    }

    private void MoveHoshi(int dRow, int dCol)
    {
        var (cx, cy) = HoshiLayout.Center(Selected);
        var best = -1;
        var bestScore = 0.12;
        foreach (var nb in HoshiLayout.Neighbors(Selected))
        {
            var (nx, ny) = HoshiLayout.Center(nb);
            var dx = nx - cx;
            var dy = -(ny - cy);
            var len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9)
                continue;
            var dot = (dx * dCol + dy * dRow) / len;
            if (dot > bestScore)
            {
                bestScore = dot;
                best = nb;
            }
        }

        if (best >= 0)
            Selected = best;
    }

    public bool Hint()
    {
        if (Completed)
            return false;

        var empties = Enumerable.Range(0, Board.CellCount)
            .Where(i => Current[i] == 0)
            .ToList();
        if (empties.Count == 0)
            return false;

        var cell = empties[Random.Shared.Next(empties.Count)];
        Selected = cell;
        PencilMode = false;
        PushUndo(cell);
        Current[cell] = Solution[cell];
        Notes[cell] = 0;
        CheckCompletion();
        return true;
    }

    public IReadOnlyList<int> Conflicts() => _solver.ConflictingCells(Current, Board);

    public IReadOnlyList<int> CheckAgainstSolution()
    {
        ShowMistakes = true;
        return Enumerable.Range(0, Board.CellCount)
            .Where(i => Current[i] != 0 && Current[i] != Solution[i])
            .ToList();
    }

    public bool IsPeer(int cell)
    {
        if (Selected < 0 || cell == Selected)
            return false;
        return Board.ArePeers(Selected, cell);
    }

    public bool SameValueAsSelected(int cell) =>
        Selected >= 0 && Current[cell] != 0 && Current[cell] == Current[Selected];

    private void PushUndo(int cell) =>
        _undo.Push(new Move(cell, Current[cell], Notes[cell]));

    private readonly record struct Move(int Cell, int Value, ulong Notes);

    private void CheckCompletion()
    {
        if (Current.Any(v => v == 0))
            return;
        if (!_solver.IsValid(Current, Board, allowEmpty: false))
            return;
        Completed = true;
        _frozenElapsed = DateTime.UtcNow - _startedUtc;
    }

    private int FirstEmpty()
    {
        for (var i = 0; i < Givens.Length; i++)
        {
            if (Givens[i] == 0)
                return i;
        }
        return 0;
    }
}
