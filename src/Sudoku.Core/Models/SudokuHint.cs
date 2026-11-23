namespace Sudoku.Core.Models;

public sealed record SudokuHint(int Cell, int Value, string Technique, string Explanation);
