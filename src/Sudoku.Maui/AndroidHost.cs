namespace Sudoku.Maui;

/// <summary>
/// Задел под Android. Веб уже хостит те же компоненты SharedUI.
/// В MAUI-проекте:
/// 1) builder.Services.AddSudokuGame();
/// 2) MainPage с BlazorWebView, HostPage = wwwroot/index.html
/// 3) RootComponents.Add(typeof(Sudoku.SharedUI.GameView), "#app");
/// ApplicationId: com.sudoku.app, минимум Android 24.
/// </summary>
public static class AndroidHost
{
    public const string ApplicationId = "com.sudoku.app";
    public const string ApplicationTitle = "Судоку";
    public const string RootSelector = "#app";
    public static Type RootComponent => typeof(SharedUI.GameView);
}
