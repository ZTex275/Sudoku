namespace Sudoku.Core.Models;

public sealed class SavedCage
{
    public int[] Cells { get; set; } = [];
    public int Sum { get; set; }
}

public sealed class SavedGame
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;
    public PuzzleKind Kind { get; set; } = PuzzleKind.Classic9;
    public Difficulty Difficulty { get; set; } = Difficulty.Easy;
    public PuzzleKind PuzzleKind { get; set; } = PuzzleKind.Classic9;
    public Difficulty PuzzleDifficulty { get; set; } = Difficulty.Easy;
    public int[]? RegionOfCell { get; set; }
    public SavedCage[]? Cages { get; set; }
    public int[]? Givens { get; set; }
    public int[]? Solution { get; set; }
    public int[]? Current { get; set; }
    public ulong[]? Notes { get; set; }
    public int Selected { get; set; } = -1;
    public bool PencilMode { get; set; }
    public bool Completed { get; set; }
    public double ElapsedSeconds { get; set; }
}
