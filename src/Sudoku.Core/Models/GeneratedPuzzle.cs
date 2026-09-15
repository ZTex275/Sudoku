namespace Sudoku.Core.Models;

public sealed class GeneratedPuzzle
{
    public required BoardDefinition Board { get; init; }
    public required Difficulty Difficulty { get; init; }
    public required int[] Givens { get; init; }
    public required int[] Solution { get; init; }
    public int ClueCount => Givens.Count(v => v != 0);
}
