namespace Sudoku.Core.Models;

public sealed class Cage
{
    public required int Id { get; init; }
    public required int[] Cells { get; init; }
    public required int Sum { get; init; }

    public int Anchor => Cells.Length == 0 ? 0 : Cells.Min();
}
