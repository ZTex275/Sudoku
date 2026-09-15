using Sudoku.Core.Models;

namespace Sudoku.Core;

public static class PuzzleCatalog
{
    public static IReadOnlyList<PuzzleInfo> All { get; } =
    [
        new(PuzzleKind.Classic9, "Классика 9×9", "Ряды, столбцы и квадраты 3×3", "9×9"),
        new(PuzzleKind.Classic16, "Большое 16×16", "Гексадоку: 1–9 и A–G, квадраты 4×4", "16×16"),
        new(PuzzleKind.Diagonal, "Диагональное", "Судоку-X: обе длинные диагонали без повторов", "X"),
        new(PuzzleKind.Jigsaw, "Фигурное", "Регионы неправильной формы вместо квадратов", "Jigsaw"),
        new(PuzzleKind.Star, "Звезда", "Звёздные регионы и дополнительная группа-звезда", "★")
    ];

    public static IReadOnlyList<(Difficulty Value, string Title)> Difficulties { get; } =
    [
        (Difficulty.Easy, "Лёгкий"),
        (Difficulty.Medium, "Средний"),
        (Difficulty.Hard, "Сложный"),
        (Difficulty.Expert, "Эксперт")
    ];
}
