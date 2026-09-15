using Sudoku.Core;
using Sudoku.Core.Models;
using Sudoku.Core.Services;

namespace Sudoku.Tests;

public class GeneratorTests
{
    private readonly SudokuGenerator _generator = new();
    private readonly SudokuSolver _solver = new();

    [Fact]
    public void Pattern_9x9_IsValidClassicSudoku()
    {
        var board = BoardFactory.Classic(9);
        var grid = SudokuGenerator.PatternSolution(9, 3, new Random(1));
        Assert.True(_solver.IsValid(grid, board, allowEmpty: false));
    }

    [Fact]
    public void Pattern_16x16_IsValid()
    {
        var board = BoardFactory.Classic(16);
        var grid = SudokuGenerator.PatternSolution(16, 4, new Random(2));
        Assert.True(_solver.IsValid(grid, board, allowEmpty: false));
        Assert.Equal(16, grid.Max());
    }

    [Theory]
    [InlineData(PuzzleKind.Classic9)]
    [InlineData(PuzzleKind.Diagonal)]
    [InlineData(PuzzleKind.Jigsaw)]
    [InlineData(PuzzleKind.Star)]
    public void Generate_Easy9_HasUniqueSolution(PuzzleKind kind)
    {
        var puzzle = _generator.Generate(kind, Difficulty.Easy, seed: 42);
        Assert.Equal(kind, puzzle.Board.Kind);
        Assert.True(_solver.IsValid(puzzle.Solution, puzzle.Board, allowEmpty: false));
        Assert.True(_solver.IsValid(puzzle.Givens, puzzle.Board, allowEmpty: true));
        Assert.True(puzzle.ClueCount < puzzle.Board.CellCount);
        Assert.Equal(1, _solver.CountSolutions(puzzle.Givens, puzzle.Board, limit: 2));
    }

    [Fact]
    public void Generate_Classic16_Easy_IsValid()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic16, Difficulty.Easy, seed: 7);
        Assert.Equal(16, puzzle.Board.Size);
        Assert.True(_solver.IsValid(puzzle.Solution, puzzle.Board, allowEmpty: false));
        Assert.True(puzzle.ClueCount < 256);
    }

    [Fact]
    public void Game_HintFillsFromSolution()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Easy, seed: 3);
        var game = new SudokuGame(puzzle);
        Assert.True(game.Hint());
        var filled = Enumerable.Range(0, game.Board.CellCount)
            .Where(i => game.Givens[i] == 0 && game.Current[i] != 0)
            .ToList();
        Assert.NotEmpty(filled);
        Assert.All(filled, i => Assert.Equal(game.Solution[i], game.Current[i]));
    }
}
