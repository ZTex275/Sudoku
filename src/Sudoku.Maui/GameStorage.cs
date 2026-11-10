using Microsoft.JSInterop;
using Microsoft.Maui.Storage;
using Sudoku.SharedUI;

namespace Sudoku.Maui;

public sealed class MauiGameStorage : IGameStorage
{
    private const string FileName = "sudoku.savedGame.json";
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IJSRuntime _js;

    public MauiGameStorage(IJSRuntime js) => _js = js;

    private static string FilePath => Path.Combine(FileSystem.Current.AppDataDirectory, FileName);

    public async Task SaveAsync(string json)
    {
        await Gate.WaitAsync();
        try
        {
            var path = FilePath;
            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, json);
            File.Move(temp, path, true);
        }
        catch
        {
        }
        finally
        {
            Gate.Release();
        }

        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", GamePersistence.StorageKey, json);
        }
        catch
        {
        }
    }

    public async Task<string?> LoadAsync()
    {
        await Gate.WaitAsync();
        try
        {
            var path = FilePath;
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path);
                if (!string.IsNullOrWhiteSpace(json))
                    return json;
            }
        }
        catch
        {
        }
        finally
        {
            Gate.Release();
        }

        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", GamePersistence.StorageKey);
        }
        catch
        {
            return null;
        }
    }
}
