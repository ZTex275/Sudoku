using System.Text.Json;
using Sudoku.Core;
using Sudoku.Core.Models;
using Sudoku.Core.Services;

namespace Sudoku.Tests;

public class SavedGameTests
{
    private readonly SudokuGenerator _generator = new();

    [Fact]
    public void Roundtrip_KeepsPuzzleKindDifficultyAndEnteredValues()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Medium, seed: 9);
        var game = new SudokuGame(puzzle);
        var cell = Enumerable.Range(0, game.Board.CellCount).First(i => game.Givens[i] == 0);
        game.Selected = cell;
        game.Place(3);

        var noteCell = Enumerable.Range(0, game.Board.CellCount).First(i => game.Givens[i] == 0 && game.Current[i] == 0);
        game.Selected = noteCell;
        game.PencilMode = true;
        game.Place(4);

        var json = JsonSerializer.Serialize(game.ToSaved(PuzzleKind.Diagonal, Difficulty.Hard));
        var save = JsonSerializer.Deserialize<SavedGame>(json)!;
        var restored = SudokuGame.TryRestore(save);

        Assert.NotNull(restored);
        Assert.Equal(PuzzleKind.Diagonal, save.Kind);
        Assert.Equal(Difficulty.Hard, save.Difficulty);
        Assert.Equal(PuzzleKind.Classic9, restored.Board.Kind);
        Assert.Equal(Difficulty.Medium, restored.Difficulty);
        Assert.Equal(game.Givens, restored.Givens);
        Assert.Equal(game.Solution, restored.Solution);
        Assert.Equal(game.Current, restored.Current);
        Assert.Equal(game.Notes, restored.Notes);
        Assert.Equal(noteCell, restored.Selected);
        Assert.True(restored.PencilMode);
        Assert.Equal(3, restored.Current[cell]);
        Assert.NotEqual(0UL, restored.Notes[noteCell] & (1UL << 4));
    }

    [Theory]
    [InlineData(PuzzleKind.Jigsaw)]
    [InlineData(PuzzleKind.Killer)]
    [InlineData(PuzzleKind.Hoshi)]
    [InlineData(PuzzleKind.Star)]
    public void Roundtrip_SpecialBoardsKeepLayoutAndFills(PuzzleKind kind)
    {
        var puzzle = _generator.Generate(kind, Difficulty.Easy, seed: 15);
        var game = new SudokuGame(puzzle);
        var cell = Enumerable.Range(0, game.Board.CellCount).First(i => game.Givens[i] == 0);
        game.Selected = cell;
        game.Place(game.Solution[cell]);

        var restored = SudokuGame.TryRestore(game.ToSaved(kind, Difficulty.Easy));
        Assert.NotNull(restored);
        Assert.Equal(kind, restored.Board.Kind);
        Assert.Equal(game.Board.CellCount, restored.Board.CellCount);
        Assert.Equal(game.Board.RegionOfCell, restored.Board.RegionOfCell);
        Assert.Equal(game.Board.Cages.Count, restored.Board.Cages.Count);
        Assert.Equal(game.Current, restored.Current);
        Assert.Equal(game.Solution[cell], restored.Current[cell]);
    }

    [Fact]
    public void HasUserDigits_TrueOnlyForEnteredValues()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Easy, seed: 3);
        var game = new SudokuGame(puzzle);
        Assert.False(game.HasUserDigits);

        var cell = Enumerable.Range(0, game.Board.CellCount).First(i => game.Givens[i] == 0);
        game.Selected = cell;
        game.PencilMode = true;
        game.Place(1);
        Assert.False(game.HasUserDigits);

        game.PencilMode = false;
        game.Place(1);
        Assert.True(game.HasUserDigits);
    }

    [Fact]
    public void TryRestore_BrokenPayload_ReturnsNull()
    {
        Assert.Null(SudokuGame.TryRestore(new SavedGame()));
        Assert.Null(SudokuGame.TryRestore(new SavedGame
        {
            Givens = new int[2],
            Solution = new int[2],
            Current = new int[2]
        }));
    }
}
