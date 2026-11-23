using System.Numerics;
using Sudoku.Core.Models;

namespace Sudoku.Core.Services;

public static class HintFinder
{
    public static SudokuHint? Find(BoardDefinition board, int[] current, int[] solution, SudokuSolver solver)
    {
        var empties = Enumerable.Range(0, board.CellCount)
            .Where(i => current[i] == 0)
            .ToList();
        if (empties.Count == 0)
            return null;

        var candidates = new int[board.CellCount];
        foreach (var cell in empties)
            candidates[cell] = solver.CandidatesMask(current, board, cell);

        var wrongEntries = Enumerable.Range(0, board.CellCount)
            .Any(i => current[i] != 0 && current[i] != solution[i]);
        var valid = solver.IsValid(current, board, allowEmpty: true);

        foreach (var cell in empties)
        {
            var mask = candidates[cell];
            if (mask == 0 || (mask & (mask - 1)) != 0)
                continue;

            var value = BitOperations.TrailingZeroCount((uint)mask) + 1;
            if (value != solution[cell])
                continue;

            return new SudokuHint(
                cell,
                value,
                "naked-single",
                NakedSingleExplanation(board, cell, value));
        }

        foreach (var group in board.Groups)
        {
            var used = 0;
            foreach (var cell in group)
            {
                if (current[cell] != 0)
                    used |= 1 << (current[cell] - 1);
            }

            for (var value = 1; value <= board.Size; value++)
            {
                var bit = 1 << (value - 1);
                if ((used & bit) != 0)
                    continue;

                var found = -1;
                var count = 0;
                foreach (var cell in group)
                {
                    if (current[cell] != 0 || (candidates[cell] & bit) == 0)
                        continue;
                    found = cell;
                    if (++count > 1)
                        break;
                }

                if (count != 1 || solution[found] != value)
                    continue;

                return new SudokuHint(
                    found,
                    value,
                    "hidden-single",
                    HiddenSingleExplanation(board, group, found, value));
            }
        }

        var bestCell = -1;
        var bestCount = int.MaxValue;
        foreach (var cell in empties)
        {
            var count = BitOperations.PopCount((uint)candidates[cell]);
            if (count < bestCount)
            {
                bestCount = count;
                bestCell = cell;
            }
        }

        if (bestCell < 0)
            return null;

        var bestValue = solution[bestCell];
        var corrupted = wrongEntries || !valid || bestCount == 0;
        var explanation = corrupted
            ? $"Клетка ({CellName(board, bestCell)}): на поле есть ошибочные цифры, поэтому обычная логика ненадёжна. Открываю верную цифру {bestValue} из решения."
            : $"Клетка ({CellName(board, bestCell)}): простая одиночка не находится, поэтому открываю клетку с наименьшим числом вариантов ({bestCount}). Верная цифра — {bestValue}.";

        return new SudokuHint(bestCell, bestValue, "solution", explanation);
    }

    private static string NakedSingleExplanation(BoardDefinition board, int cell, int value)
    {
        var cage = CageFor(board, cell);
        var cageNote = cage is null
            ? string.Empty
            : $" Контур с суммой {cage.Sum} тоже оставляет только этот вариант.";
        var scope = board.Kind is PuzzleKind.Star or PuzzleKind.Hoshi
            ? "в связанных с ней линиях"
            : "в её строке, столбце или регионе";
        return $"Клетка ({CellName(board, cell)}): единственный кандидат — {value}. Остальные цифры уже стоят {scope}.{cageNote}";
    }

    private static string HiddenSingleExplanation(BoardDefinition board, int[] group, int cell, int value)
    {
        var unit = UnitName(board, group);
        return $"Клетка ({CellName(board, cell)}): в {unit} цифра {value} может стоять только здесь. В остальных клетках эта цифра уже занята или запрещена соседями, поэтому ставим {value}.";
    }

    private static Cage? CageFor(BoardDefinition board, int cell)
    {
        foreach (var cage in board.Cages)
        {
            if (cage.Cells.Contains(cell))
                return cage;
        }

        return null;
    }

    private static string UnitName(BoardDefinition board, int[] group)
    {
        if (group.Length == 0)
            return "линии";

        var classic = board.Kind is PuzzleKind.Classic9 or PuzzleKind.Classic16
            or PuzzleKind.Diagonal or PuzzleKind.Jigsaw or PuzzleKind.Killer;

        if (classic)
        {
            var row = group[0] / board.Width;
            if (group.Length == board.Size && group.All(c => c / board.Width == row))
                return $"строке {row + 1}";

            var col = group[0] % board.Width;
            if (group.Length == board.Size && group.All(c => c % board.Width == col))
                return $"столбце {col + 1}";

            if (board.DiagonalCells.Length > 0 && group.All(c => board.DiagonalCells[c]))
                return "диагонали";

            foreach (var cage in board.Cages)
            {
                if (cage.Cells.Length == group.Length && cage.Cells.All(group.Contains))
                    return $"контуре с суммой {cage.Sum}";
            }

            var region = board.RegionOfCell[group[0]];
            if (group.All(c => board.RegionOfCell[c] == region))
            {
                return board.Kind == PuzzleKind.Jigsaw
                    ? $"регионе {region + 1}"
                    : $"блоке {region + 1}";
            }
        }
        else if (board.Kind == PuzzleKind.Hoshi)
        {
            var region = board.RegionOfCell[group[0]];
            if (group.Length == board.Size && group.All(c => board.RegionOfCell[c] == region))
                return $"секторе {region + 1}";
        }

        return "линии";
    }

    private static string CellName(BoardDefinition board, int cell)
    {
        if (board.Kind == PuzzleKind.Star)
        {
            var (ring, sector) = AstraLayout.Coords(cell);
            return $"кольцо {ring + 1}, сектор {sector + 1}";
        }

        if (board.Kind == PuzzleKind.Hoshi)
            return $"клетка {cell + 1}, сектор {HoshiLayout.Sector(cell) + 1}";

        var (row, col) = board.Coords(cell);
        return $"строка {row + 1}, столбец {col + 1}";
    }
}
