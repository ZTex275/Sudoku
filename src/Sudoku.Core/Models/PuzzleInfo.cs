namespace Sudoku.Core.Models;

public sealed record PuzzleInfo(
    PuzzleKind Kind,
    string Title,
    string Subtitle,
    string Badge);
