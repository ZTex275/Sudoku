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

    [Fact]
    public void JigsawRegions_AreIrregularNotBoxes()
    {
        var map = RegionBuilder.Jigsaw(new Random(11));
        Assert.Equal(81, map.Length);
        Assert.False(RegionBuilder.IsStandardBoxes(map, 9, 3));
        Assert.All(Enumerable.Range(0, 9), r => Assert.Equal(9, map.Count(id => id == r)));
    }

    [Fact]
    public void AstraLayout_Pattern_HasUniqueDigitsOnAdjacentStrips()
    {
        var board = BoardFactory.Astra();
        Assert.Equal(7, board.Size);
        Assert.Equal(AstraLayout.Cells, board.CellCount);
        Assert.Equal(AstraLayout.Sectors * 2, board.Groups.Count);
        Assert.All(board.Groups, g => Assert.Equal(7, g.Length));

        for (var seed = 0; seed < 12; seed++)
        {
            var grid = AstraLayout.PatternSolution(new Random(seed));
            Assert.True(_solver.IsValid(grid, board, allowEmpty: false));
            for (var s = 0; s < AstraLayout.Sectors; s++)
            {
                var cw = Enumerable.Range(0, 7).Select(r => grid[AstraLayout.Index(r, s + r / 2)]);
                var ccw = Enumerable.Range(0, 7).Select(r => grid[AstraLayout.Index(r, s - (r + 1) / 2)]);
                Assert.Equal(7, cw.Distinct().Count());
                Assert.Equal(7, ccw.Distinct().Count());
            }

            for (var r = 0; r < AstraLayout.Rings; r++)
            {
                for (var s = 0; s < AstraLayout.Sectors; s++)
                {
                    var value = grid[AstraLayout.Index(r, s)];
                    foreach (var (nr, ns) in AstraLayout.Neighbors(r, s))
                        Assert.NotEqual(value, grid[AstraLayout.Index(nr, ns)]);
                }
            }
        }
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
        if (kind == PuzzleKind.Jigsaw)
            Assert.False(RegionBuilder.IsStandardBoxes(puzzle.Board.RegionOfCell, 9, 3));
        if (kind == PuzzleKind.Star)
        {
            Assert.Equal(7, puzzle.Board.Size);
            Assert.Equal(AstraLayout.Cells, puzzle.Board.CellCount);
        }
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

    [Fact]
    public void Game_NotesStayWhenPeerDigitIsEntered()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Easy, seed: 3);
        var game = new SudokuGame(puzzle);
        var empty = Enumerable.Range(0, game.Board.CellCount).Where(i => game.Givens[i] == 0).ToList();
        var a = empty[0];
        var b = empty.First(i => i != a && game.Board.ArePeers(a, i));
        game.Selected = a;
        game.PencilMode = true;
        game.Place(5);
        Assert.NotEqual(0UL, game.Notes[a] & (1UL << 5));
        game.Selected = b;
        game.PencilMode = false;
        game.Place(5);
        Assert.NotEqual(0UL, game.Notes[a] & (1UL << 5));
    }

    [Fact]
    public void Game_UndoRestoresLastMove()
    {
        var puzzle = _generator.Generate(PuzzleKind.Classic9, Difficulty.Easy, seed: 3);
        var game = new SudokuGame(puzzle);
        var cell = Enumerable.Range(0, game.Board.CellCount).First(i => game.Givens[i] == 0);
        game.Selected = cell;
        game.Place(4);
        Assert.Equal(4, game.Current[cell]);
        Assert.True(game.CanUndo);
        game.Undo();
        Assert.Equal(0, game.Current[cell]);
        Assert.False(game.CanUndo);
    }
}
