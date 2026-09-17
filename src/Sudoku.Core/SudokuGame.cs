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

        var (row, col) = Board.Coords(Selected);
        row = Math.Clamp(row + dRow, 0, Board.Height - 1);
        if (Board.Kind == PuzzleKind.Star)
            col = (col + dCol + Board.Width) % Board.Width;
        else
            col = Math.Clamp(col + dCol, 0, Board.Width - 1);
        Selected = Board.Index(row, col);
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
