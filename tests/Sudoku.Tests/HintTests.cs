using Sudoku.Core;
using Sudoku.Core.Models;
using Sudoku.Core.Services;

namespace Sudoku.Tests;

public class HintTests
{
    private readonly SudokuGenerator _generator = new();

    [Theory]
    [InlineData(PuzzleKind.Classic9)]
    [InlineData(PuzzleKind.Classic16)]
    [InlineData(PuzzleKind.Diagonal)]
    [InlineData(PuzzleKind.Jigsaw)]
    [InlineData(PuzzleKind.Star)]
    [InlineData(PuzzleKind.Hoshi)]
    [InlineData(PuzzleKind.Killer)]
    public void Hint_FillsCorrectCellAndExplainsIt(PuzzleKind kind)
    {
        var puzzle = _generator.Generate(kind, Difficulty.Easy, seed: 17);
        var game = new SudokuGame(puzzle);

        Assert.True(game.Hint());
        var hint = game.LastHint;
        Assert.NotNull(hint);
        Assert.Equal(game.Solution[hint.Cell], game.Current[hint.Cell]);
        Assert.Equal(hint.Value, game.Current[hint.Cell]);
        Assert.Equal(0, game.Givens[hint.Cell]);
        Assert.False(string.IsNullOrWhiteSpace(hint.Explanation));
        Assert.Contains(hint.Value.ToString(), hint.Explanation);
    }

    [Fact]
    public void Hint_WorksThroughWholePuzzleWithoutRepeatingCells()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Easy, seed: 3);
        var game = new SudokuGame(puzzle);
        var used = new HashSet<int>();

        while (!game.Completed)
        {
            Assert.True(game.Hint());
            var hint = game.LastHint!;
            Assert.True(used.Add(hint.Cell));
            Assert.Equal(game.Solution[hint.Cell], game.Current[hint.Cell]);
            Assert.True(used.Count <= game.Board.CellCount);
        }

        Assert.Equal(0, game.Remaining);
    }


    [Fact]
    public void Hint_RecognisesNakedSingle()
    {
        var solution = SudokuGenerator.PatternSolution(9, 3, new Random(1));
        var cell = 40;
        var givens = (int[])solution.Clone();
        givens[cell] = 0;
        var game = new SudokuGame(new GeneratedPuzzle
        {
            Board = BoardFactory.Classic(9),
            Difficulty = Difficulty.Easy,
            Givens = givens,
            Solution = solution
        });

        Assert.True(game.Hint());
        Assert.Equal("naked-single", game.LastHint!.Technique);
        Assert.Equal(cell, game.LastHint.Cell);
        Assert.Equal(solution[cell], game.Current[cell]);
    }

    [Fact]
    public void Hint_IsClearedByUndo()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Easy, seed: 5);
        var game = new SudokuGame(puzzle);

        Assert.True(game.Hint());
        Assert.NotNull(game.LastHint);
        game.Undo();
        Assert.Null(game.LastHint);
    }
}
