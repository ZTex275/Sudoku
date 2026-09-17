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

    [Fact]
    public void KillerCages_CoverBoardAndMatchSolutionSums()
    {
        var solution = SudokuGenerator.PatternSolution(9, 3, new Random(5));
        var cages = CageBuilder.Partition(solution, 9, new Random(5), 4, preferSingles: true);
        Assert.True(CageBuilder.CoversBoard(cages, 81));
        var board = BoardFactory.Killer(cages);
        Assert.Equal(PuzzleKind.Killer, board.Kind);
        Assert.True(_solver.IsValid(solution, board, allowEmpty: false));
        Assert.All(cages, cage =>
        {
            Assert.Equal(cage.Sum, cage.Cells.Sum(i => solution[i]));
            Assert.Equal(cage.Cells.Length, cage.Cells.Select(i => solution[i]).Distinct().Count());
        });
    }

    [Theory]
    [InlineData(Difficulty.Medium)]
    [InlineData(Difficulty.Hard)]
    public void Generate_Killer_WithoutGivens_IsUnique(Difficulty difficulty)
    {
        var puzzle = _generator.Generate(PuzzleKind.Killer, difficulty, seed: 21);
        Assert.Equal(0, puzzle.ClueCount);
        Assert.True(CageBuilder.CoversBoard(puzzle.Board.Cages, 81));
        Assert.Equal(1, _solver.CountSolutions(puzzle.Givens, puzzle.Board, limit: 2));
    }

    [Fact]
    public void HoshiLayout_HasSixTrianglesAndGappedEightCellLines()
    {
        var board = BoardFactory.Hoshi();
        Assert.Equal(PuzzleKind.Hoshi, board.Kind);
        Assert.Equal(9, board.Size);
        Assert.Equal(HoshiLayout.Cells, board.CellCount);
        Assert.Equal(6, Enumerable.Range(0, 6).Count(s => board.RegionOfCell.Count(id => id == s) == 9));

        var eights = board.Groups.Where(g => g.Length == 8).ToList();
        Assert.Equal(6, eights.Count);
        Assert.Contains(board.Groups, HoshiLayout.HasCenterGap);

        var nines = board.Groups.Count(g => g.Length == 9);
        Assert.True(nines >= 12);
    }

    [Theory]
    [InlineData(PuzzleKind.Classic9)]
    [InlineData(PuzzleKind.Diagonal)]
    [InlineData(PuzzleKind.Jigsaw)]
    [InlineData(PuzzleKind.Star)]
    [InlineData(PuzzleKind.Hoshi)]
    [InlineData(PuzzleKind.Killer)]
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
        if (kind == PuzzleKind.Hoshi)
        {
            Assert.Equal(9, puzzle.Board.Size);
            Assert.Equal(HoshiLayout.Cells, puzzle.Board.CellCount);
            Assert.Contains(puzzle.Board.Groups, g => g.Length == 8);
        }
        if (kind == PuzzleKind.Killer)
        {
            Assert.NotEmpty(puzzle.Board.Cages);
            Assert.True(CageBuilder.CoversBoard(puzzle.Board.Cages, 81));
            Assert.Equal(0, puzzle.ClueCount);
            Assert.All(puzzle.Board.Cages, cage =>
            {
                Assert.Equal(cage.Sum, cage.Cells.Sum(i => puzzle.Solution[i]));
                Assert.Equal(cage.Cells.Length, cage.Cells.Select(i => puzzle.Solution[i]).Distinct().Count());
            });
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
