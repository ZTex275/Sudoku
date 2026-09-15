using Microsoft.Extensions.DependencyInjection;
using Sudoku.Core.Services;

namespace Sudoku.SharedUI;

public static class SudokuUi
{
    public static IServiceCollection AddSudokuGame(this IServiceCollection services)
    {
        services.AddSingleton<SudokuGenerator>();
        return services;
    }
}
