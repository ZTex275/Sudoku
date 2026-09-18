using System.Text.Json;
using Microsoft.JSInterop;
using Sudoku.Core.Models;

namespace Sudoku.SharedUI;

public sealed class GamePersistence
{
    public const string StorageKey = "sudoku.savedGame";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IJSRuntime _js;

    public GamePersistence(IJSRuntime js) => _js = js;

    public async Task SaveAsync(SavedGame save)
    {
        try
        {
            var json = JsonSerializer.Serialize(save, Json);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // localStorage может быть недоступен — партия просто не запомнится.
        }
    }

    public async Task<SavedGame?> LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize<SavedGame>(json, Json);
        }
        catch
        {
            return null;
        }
    }
}
